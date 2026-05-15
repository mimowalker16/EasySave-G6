using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using EasySave.Core.Models;
using EasySave.Core.Strategies;
using EasyLog;

namespace EasySave.Core.Services
{
    public class BackupService
    {
        private const int CopyBufferBytes = 262144;
        private const int CoordinationSleepMs = 120;
        private const string CryptoSoftKey = "EasySave-CryptoSoft-Key";

        private static readonly SemaphoreSlim LargeFileTransferSemaphore = new(1, 1);
        private static readonly SemaphoreSlim CryptoSoftSemaphore = new(1, 1);
        private static int _priorityFilesRemaining;

        private readonly StateService _stateService;
        private readonly List<BackupState> _allStates;
        private readonly ILogger _logger;
        private readonly BusinessSoftwareService _businessSoftware;

        public BackupService(
            StateService stateService,
            List<BackupState> allStates,
            ILogger logger,
            BusinessSoftwareService businessSoftware)
        {
            _stateService = stateService;
            _allStates = allStates;
            _logger = logger;
            _businessSoftware = businessSoftware;
        }

        public bool Execute(
            BackupJob job,
            int stateIndex,
            AppSettings settings,
            CancellationToken cancellationToken = default,
            Func<bool>? userPauseRequested = null,
            Action<int, BackupState>? stateChanged = null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(job.SourceDirectory))
                throw new DirectoryNotFoundException($"Source directory not found: {job.SourceDirectory}");

            IBackupStrategy strategy = job.Type == BackupType.Full
                ? new CompleteBackup()
                : new DifferentialBackup();

            List<(string Source, string Target)> filesToCopy = strategy.CollectFiles(job);
            HashSet<string> priorityExtensions = BuildExtensionSet(settings.PriorityExtensions);
            HashSet<string> encryptedExtensions = BuildExtensionSet(settings.EncryptedExtensions);

            if (priorityExtensions.Count > 0)
            {
                filesToCopy = filesToCopy
                    .OrderByDescending(f => IsPriorityFile(f.Source, priorityExtensions))
                    .ToList();
            }

            long largeThresholdBytes = settings.LargeFileThresholdKb > 0
                ? settings.LargeFileThresholdKb * 1024L
                : -1;

            int localPriorityRemaining = filesToCopy.Count(f => IsPriorityFile(f.Source, priorityExtensions));
            if (localPriorityRemaining > 0)
                Interlocked.Add(ref _priorityFilesRemaining, localPriorityRemaining);

            BackupState state = _allStates[stateIndex];
            bool hasError = false;

            try
            {
                state.State = BackupStateType.Active;
                state.TotalFiles = filesToCopy.Count;
                state.TotalSize = ComputeTotalSize(filesToCopy);
                state.RemainingFiles = filesToCopy.Count;
                state.RemainingSize = state.TotalSize;
                state.Progress = filesToCopy.Count == 0 ? 100 : 0;
                state.CurrentSourceFile = string.Empty;
                state.CurrentTargetFile = string.Empty;
                state.PauseReason = string.Empty;
                state.LastActionTimestamp = DateTime.Now.ToString("o");
                PersistState(stateIndex, stateChanged);

                foreach ((string sourceFile, string targetFile) in filesToCopy)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    bool isPriority = IsPriorityFile(sourceFile, priorityExtensions);
                    WaitForBusinessSoftwarePause(stateIndex, state, settings, cancellationToken, stateChanged);
                    WaitForPriorityGateIfNeeded(isPriority, stateIndex, state, cancellationToken, stateChanged);
                    WaitForUserPause(stateIndex, state, cancellationToken, userPauseRequested, stateChanged);

                    long fileSize = SafeFileLength(sourceFile);
                    state.State = BackupStateType.Active;
                    state.PauseReason = string.Empty;
                    state.CurrentSourceFile = sourceFile;
                    state.CurrentTargetFile = targetFile;
                    state.LastActionTimestamp = DateTime.Now.ToString("o");
                    PersistState(stateIndex, stateChanged);

                    long transferTimeMs;
                    long encryptionTimeMs = 0;
                    var sw = Stopwatch.StartNew();

                    try
                    {
                        string? targetDir = Path.GetDirectoryName(targetFile);
                        if (!string.IsNullOrEmpty(targetDir))
                            Directory.CreateDirectory(targetDir);

                        bool useLargeSemaphore = largeThresholdBytes > 0 && fileSize > largeThresholdBytes;
                        if (useLargeSemaphore)
                            LargeFileTransferSemaphore.Wait(cancellationToken);

                        try
                        {
                            CopyFileWithCancellation(sourceFile, targetFile, cancellationToken);
                        }
                        finally
                        {
                            if (useLargeSemaphore)
                                LargeFileTransferSemaphore.Release();
                        }

                        sw.Stop();
                        transferTimeMs = sw.ElapsedMilliseconds;
                    }
                    catch (OperationCanceledException)
                    {
                        sw.Stop();
                        TryDeletePartialTarget(targetFile);
                        throw;
                    }
                    catch
                    {
                        sw.Stop();
                        transferTimeMs = -Math.Max(1, sw.ElapsedMilliseconds);
                        hasError = true;
                        _logger.LogTransfer(job.Name, sourceFile, targetFile, fileSize, transferTimeMs, 0);
                        MarkFileProcessed(stateIndex, state, fileSize, stateChanged);
                        MarkPriorityProcessed(isPriority, ref localPriorityRemaining);
                        continue;
                    }

                    string extension = Path.GetExtension(sourceFile).ToLowerInvariant();
                    if (encryptedExtensions.Count > 0 && encryptedExtensions.Contains(extension))
                    {
                        CryptoSoftSemaphore.Wait(cancellationToken);
                        try
                        {
                            encryptionTimeMs = RunCryptoSoft(targetFile, cancellationToken);
                        }
                        finally
                        {
                            CryptoSoftSemaphore.Release();
                        }

                        if (encryptionTimeMs < 0)
                            hasError = true;
                    }

                    _logger.LogTransfer(job.Name, sourceFile, targetFile, fileSize, transferTimeMs, encryptionTimeMs);
                    MarkFileProcessed(stateIndex, state, fileSize, stateChanged);
                    MarkPriorityProcessed(isPriority, ref localPriorityRemaining);
                }

                state.State = BackupStateType.End;
                state.Progress = 100;
                state.RemainingFiles = 0;
                state.RemainingSize = 0;
                state.CurrentSourceFile = string.Empty;
                state.CurrentTargetFile = string.Empty;
                state.PauseReason = string.Empty;
                state.LastActionTimestamp = DateTime.Now.ToString("o");
                PersistState(stateIndex, stateChanged);

                return !hasError;
            }
            catch (OperationCanceledException)
            {
                state.State = BackupStateType.Canceled;
                state.CurrentSourceFile = string.Empty;
                state.CurrentTargetFile = string.Empty;
                state.PauseReason = string.Empty;
                state.LastActionTimestamp = DateTime.Now.ToString("o");
                PersistState(stateIndex, stateChanged);
                throw;
            }
            finally
            {
                if (localPriorityRemaining > 0)
                    Interlocked.Add(ref _priorityFilesRemaining, -localPriorityRemaining);
            }
        }

        private void WaitForBusinessSoftwarePause(
            int stateIndex,
            BackupState state,
            AppSettings settings,
            CancellationToken cancellationToken,
            Action<int, BackupState>? stateChanged)
        {
            bool paused = false;
            while (_businessSoftware.IsRunning(settings.BusinessSoftwareName))
            {
                cancellationToken.ThrowIfCancellationRequested();
                paused = true;
                state.State = BackupStateType.Paused;
                state.PauseReason = $"Business software '{settings.BusinessSoftwareName}' is running.";
                state.LastActionTimestamp = DateTime.Now.ToString("o");
                PersistState(stateIndex, stateChanged);
                Thread.Sleep(CoordinationSleepMs);
            }

            if (paused)
            {
                state.State = BackupStateType.Active;
                state.PauseReason = string.Empty;
                state.LastActionTimestamp = DateTime.Now.ToString("o");
                PersistState(stateIndex, stateChanged);
            }
        }

        private void WaitForPriorityGateIfNeeded(
            bool isPriority,
            int stateIndex,
            BackupState state,
            CancellationToken cancellationToken,
            Action<int, BackupState>? stateChanged)
        {
            if (isPriority)
                return;

            while (Volatile.Read(ref _priorityFilesRemaining) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                state.State = BackupStateType.Paused;
                state.PauseReason = "Waiting for priority files in another job.";
                state.LastActionTimestamp = DateTime.Now.ToString("o");
                PersistState(stateIndex, stateChanged);
                Thread.Sleep(CoordinationSleepMs);
            }
        }

        private void WaitForUserPause(
            int stateIndex,
            BackupState state,
            CancellationToken cancellationToken,
            Func<bool>? userPauseRequested,
            Action<int, BackupState>? stateChanged)
        {
            if (userPauseRequested == null)
                return;

            while (userPauseRequested())
            {
                cancellationToken.ThrowIfCancellationRequested();
                state.State = BackupStateType.Paused;
                state.PauseReason = "Paused by user.";
                state.LastActionTimestamp = DateTime.Now.ToString("o");
                PersistState(stateIndex, stateChanged);
                Thread.Sleep(CoordinationSleepMs);
            }
        }

        private void MarkFileProcessed(
            int stateIndex,
            BackupState state,
            long fileSize,
            Action<int, BackupState>? stateChanged)
        {
            state.RemainingFiles = Math.Max(0, state.RemainingFiles - 1);
            state.RemainingSize = Math.Max(0, state.RemainingSize - fileSize);
            state.Progress = state.TotalFiles > 0
                ? Math.Round((1.0 - (double)state.RemainingFiles / state.TotalFiles) * 100, 1)
                : 100;
            state.LastActionTimestamp = DateTime.Now.ToString("o");
            PersistState(stateIndex, stateChanged);
        }

        private void PersistState(int stateIndex, Action<int, BackupState>? stateChanged)
        {
            BackupState snapshot;
            lock (_allStates)
            {
                snapshot = CloneState(_allStates[stateIndex]);
                _stateService.UpdateState(_allStates);
            }

            stateChanged?.Invoke(stateIndex, snapshot);
        }

        private static BackupState CloneState(BackupState state) => new()
        {
            JobName = state.JobName,
            LastActionTimestamp = state.LastActionTimestamp,
            State = state.State,
            TotalFiles = state.TotalFiles,
            TotalSize = state.TotalSize,
            RemainingFiles = state.RemainingFiles,
            RemainingSize = state.RemainingSize,
            Progress = state.Progress,
            CurrentSourceFile = state.CurrentSourceFile,
            CurrentTargetFile = state.CurrentTargetFile,
            PauseReason = state.PauseReason
        };

        private static HashSet<string> BuildExtensionSet(IEnumerable<string> extensions)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in extensions)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                string ext = raw.Trim().ToLowerInvariant();
                result.Add(ext.StartsWith('.') ? ext : "." + ext);
            }

            return result;
        }

        private static bool IsPriorityFile(string path, HashSet<string> priorityExtensions)
            => priorityExtensions.Count > 0
               && priorityExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

        private static void MarkPriorityProcessed(bool isPriority, ref int localPriorityRemaining)
        {
            if (!isPriority || localPriorityRemaining <= 0)
                return;

            localPriorityRemaining--;
            Interlocked.Decrement(ref _priorityFilesRemaining);
        }

        private static void CopyFileWithCancellation(string sourcePath, string destPath, CancellationToken cancellationToken)
        {
            using var source = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                CopyBufferBytes,
                FileOptions.SequentialScan);

            using var destination = new FileStream(
                destPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                CopyBufferBytes,
                FileOptions.WriteThrough);

            var buffer = new byte[CopyBufferBytes];
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                destination.Write(buffer, 0, read);
            }
        }

        private static long RunCryptoSoft(string filePath, CancellationToken cancellationToken)
        {
            string cryptoSoftPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CryptoSoft.exe");
            if (!File.Exists(cryptoSoftPath))
                return -1;

            var sw = Stopwatch.StartNew();
            Process? process = null;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = cryptoSoftPath,
                    Arguments = $"{QuoteArgument(filePath)} {QuoteArgument(CryptoSoftKey)}",
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    CreateNoWindow = true
                };

                process = Process.Start(psi);
                if (process == null)
                    return -99;

                while (!process.WaitForExit(100))
                    cancellationToken.ThrowIfCancellationRequested();

                sw.Stop();
                return process.ExitCode == 0 ? sw.ElapsedMilliseconds : -Math.Abs(process.ExitCode);
            }
            catch (OperationCanceledException)
            {
                if (process?.HasExited == false)
                {
                    try { process.Kill(entireProcessTree: true); }
                    catch { }
                }

                throw;
            }
            catch
            {
                sw.Stop();
                return -Math.Max(1, sw.ElapsedMilliseconds);
            }
            finally
            {
                process?.Dispose();
            }
        }

        private static void TryDeletePartialTarget(string targetFile)
        {
            try
            {
                if (File.Exists(targetFile))
                    File.Delete(targetFile);
            }
            catch
            {
            }
        }

        private static string QuoteArgument(string argument)
            => "\"" + argument.Replace("\"", "\\\"") + "\"";

        private static long SafeFileLength(string path)
        {
            try { return new FileInfo(path).Length; }
            catch { return 0; }
        }

        private static void TryKillCrypto(Process? proc)
        {
            if (proc?.HasExited == false)
            {
                try { proc.Kill(entireProcessTree: true); }
                catch { /* ignore */ }
            }
        }

        private static long ComputeTotalSize(List<(string Source, string Target)> files)
        {
            long total = 0;
            foreach ((string source, _) in files)
                total += SafeFileLength(source);
            return total;
        }
    }
}
