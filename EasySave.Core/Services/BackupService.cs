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
    /// <summary>
    /// Core backup engine. Executes a backup job using the Strategy pattern,
    /// cooperative cancellation, throttled state snapshots, and chunked file copy.
    /// </summary>
    public class BackupService
    {
        private const int CopyBufferBytes            = 262144;
        private const int StateFlushEveryNFiles      = 8;
        private const int CoordinationSleepMs        = 120;

        // V3 global coordination primitives shared by all jobs in the current process.
        private static readonly SemaphoreSlim LargeFileTransferSemaphore = new(1, 1);
        private static readonly SemaphoreSlim CryptoSoftSemaphore        = new(1, 1);
        private static int _priorityFilesRemaining;

        private readonly StateService               _stateService;
        private readonly List<BackupState>          _allStates;
        private readonly ILogger                    _logger;
        private readonly BusinessSoftwareService    _businessSoftware;

        private readonly TimeSpan _stateMinInterval = TimeSpan.FromMilliseconds(150);
        private          DateTimeOffset _nextStateFlushUtc = DateTimeOffset.MinValue;

        public BackupService(
            StateService           stateService,
            List<BackupState>      allStates,
            ILogger                logger,
            BusinessSoftwareService businessSoftware)
        {
            _stateService     = stateService;
            _allStates        = allStates;
            _logger           = logger;
            _businessSoftware = businessSoftware;
        }

        /// <summary>Runs one backup job. Throws <see cref="OperationCanceledException"/> when <paramref name="cancellationToken"/> is triggered.</summary>
        public bool Execute(
            BackupJob        job,
            int              stateIndex,
            AppSettings      settings,
            CancellationToken cancellationToken = default,
            Func<bool>?      userPauseRequested = null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(job.SourceDirectory))
                throw new DirectoryNotFoundException(
                    $"Source directory not found: {job.SourceDirectory}");

            IBackupStrategy strategy = job.Type == BackupType.Full
                ? new CompleteBackup()
                : new DifferentialBackup();

            List<(string Source, string Target)> filesToCopy = strategy.CollectFiles(job);
            var priorityExt = BuildNormalizedExtensionSet(settings.PriorityExtensions);
            if (priorityExt.Count > 0)
            {
                // Prevent self-deadlock: process priority files first within each job.
                filesToCopy = filesToCopy
                    .OrderByDescending(f => IsPriorityFile(f.Source, priorityExt))
                    .ToList();
            }

            long largeThresholdBytes = settings.LargeFileThresholdKb > 0
                ? settings.LargeFileThresholdKb * 1024L
                : -1;

            int localPriorityRemaining = filesToCopy.Count(f => IsPriorityFile(f.Source, priorityExt));
            if (localPriorityRemaining > 0)
                Interlocked.Add(ref _priorityFilesRemaining, localPriorityRemaining);

            BackupState state = _allStates[stateIndex];
            bool        hasError = false;

            try
            {
                state.State               = BackupStateType.Active;
                state.TotalFiles          = filesToCopy.Count;
                state.TotalSize           = ComputeTotalSize(filesToCopy);
                state.RemainingFiles      = filesToCopy.Count;
                state.RemainingSize       = state.TotalSize;
                state.Progress            = 0;
                state.LastActionTimestamp = DateTime.Now.ToString("o");
                PersistStateIfNeeded(forceImmediate: true);

                for (int fi = 0; fi < filesToCopy.Count; fi++)
                {
                    (string sourceFile, string targetFile) = filesToCopy[fi];
                    cancellationToken.ThrowIfCancellationRequested();

                    bool isPriority = IsPriorityFile(sourceFile, priorityExt);
                    WaitForBusinessSoftwarePause(state, settings, cancellationToken);
                    WaitForPriorityGateIfNeeded(isPriority, state, cancellationToken);
                    WaitForUserPause(state, cancellationToken, userPauseRequested);

                    state.CurrentSourceFile   = sourceFile;
                    state.CurrentTargetFile   = targetFile;
                    state.State               = BackupStateType.Active;
                    state.LastActionTimestamp = DateTime.Now.ToString("o");
                    PersistStateIfNeeded(ShouldForceAfterFile(fi, filesToCopy.Count));

                    long fileSize = SafeFileLength(sourceFile);

                    long transferTimeMs;
                    long encryptionTimeMs = 0;

                    var sw = Stopwatch.StartNew();
                    try
                    {
                        string? targetDir = Path.GetDirectoryName(targetFile);
                        if (targetDir != null)
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
                    catch (Exception)
                    {
                        sw.Stop();
                        transferTimeMs = -sw.ElapsedMilliseconds;
                        hasError = true;
                        _logger.LogTransfer(job.Name, sourceFile, targetFile,
                            fileSize, transferTimeMs, 0);
                        state.RemainingFiles--;
                        state.RemainingSize -= fileSize;
                        UpdateProgress(state);
                        MarkPriorityFileAsProcessed(isPriority, ref localPriorityRemaining);
                        continue;
                    }

                    string extension = Path.GetExtension(sourceFile).ToLowerInvariant();
                    if (settings.EncryptedExtensions.Count > 0
                        && settings.EncryptedExtensions.Contains(extension))
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

                    _logger.LogTransfer(job.Name, sourceFile, targetFile,
                        fileSize, transferTimeMs, encryptionTimeMs);

                    state.RemainingFiles--;
                    state.RemainingSize -= fileSize;
                    UpdateProgress(state);
                    MarkPriorityFileAsProcessed(isPriority, ref localPriorityRemaining);

                    PersistStateIfNeeded(ShouldForceAfterFile(fi, filesToCopy.Count));
                }

                FinalizeSuccessState(state);
                PersistStateIfNeeded(forceImmediate: true);
                return !hasError;
            }
            catch (OperationCanceledException)
            {
                state.State               = BackupStateType.Canceled;
                state.CurrentSourceFile   = string.Empty;
                state.CurrentTargetFile   = string.Empty;
                state.LastActionTimestamp = DateTime.Now.ToString("o");
                PersistStateIfNeeded(forceImmediate: true);
                throw;
            }
            finally
            {
                // Ensure global priority counter cannot leak on cancellation/errors.
                if (localPriorityRemaining > 0)
                    Interlocked.Add(ref _priorityFilesRemaining, -localPriorityRemaining);
            }
        }

        private static HashSet<string> BuildNormalizedExtensionSet(IEnumerable<string> exts)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string ext in exts)
            {
                if (string.IsNullOrWhiteSpace(ext)) continue;
                string normalized = ext.Trim().ToLowerInvariant();
                if (!normalized.StartsWith('.'))
                    normalized = "." + normalized;
                set.Add(normalized);
            }
            return set;
        }

        private static bool IsPriorityFile(string path, HashSet<string> priorityExt)
            => priorityExt.Count > 0 && priorityExt.Contains(Path.GetExtension(path).ToLowerInvariant());

        private static void MarkPriorityFileAsProcessed(bool isPriority, ref int localPriorityRemaining)
        {
            if (isPriority)
            {
                localPriorityRemaining = Math.Max(0, localPriorityRemaining - 1);
                Interlocked.Decrement(ref _priorityFilesRemaining);
            }
        }

        private void WaitForPriorityGateIfNeeded(bool isPriority, BackupState state, CancellationToken ct)
        {
            if (isPriority)
                return;

            while (Volatile.Read(ref _priorityFilesRemaining) > 0)
            {
                ct.ThrowIfCancellationRequested();
                state.State               = BackupStateType.Paused;
                state.LastActionTimestamp = DateTime.Now.ToString("o");
                PersistStateIfNeeded(forceImmediate: false);
                Thread.Sleep(CoordinationSleepMs);
            }
        }

        private void WaitForBusinessSoftwarePause(
            BackupState state, AppSettings settings, CancellationToken ct)
        {
            while (_businessSoftware.IsRunning(settings.BusinessSoftwareName))
            {
                ct.ThrowIfCancellationRequested();
                state.State               = BackupStateType.Paused;
                state.LastActionTimestamp = DateTime.Now.ToString("o");
                PersistStateIfNeeded(forceImmediate: false);
                Thread.Sleep(CoordinationSleepMs);
            }
        }

        private void WaitForUserPause(
            BackupState state, CancellationToken ct, Func<bool>? userPauseRequested)
        {
            if (userPauseRequested == null)
                return;

            while (userPauseRequested())
            {
                ct.ThrowIfCancellationRequested();
                state.State               = BackupStateType.Paused;
                state.LastActionTimestamp = DateTime.Now.ToString("o");
                PersistStateIfNeeded(forceImmediate: false);
                Thread.Sleep(CoordinationSleepMs);
            }
        }

        private void FinalizeSuccessState(BackupState state)
        {
            state.State = BackupStateType.End;
            if (state.RemainingFiles <= 0 || state.TotalFiles == 0)
            {
                state.Progress            = 100;
                state.RemainingFiles      = 0;
                state.RemainingSize       = 0;
                state.CurrentSourceFile   = string.Empty;
                state.CurrentTargetFile   = string.Empty;
            }
            state.LastActionTimestamp = DateTime.Now.ToString("o");
        }

        private static void UpdateProgress(BackupState state)
        {
            state.Progress = state.TotalFiles > 0
                ? Math.Round((1.0 - (double)state.RemainingFiles / state.TotalFiles) * 100, 1)
                : 100;
        }

        private bool ShouldForceAfterFile(int zeroBasedIndex, int total)
        {
            if (total <= 0) return true;
            int oneBased = zeroBasedIndex + 1;
            return oneBased % StateFlushEveryNFiles == 0 || oneBased == total;
        }

        /// <summary>Persists state immediately if forced, otherwise at most once per <see cref="_stateMinInterval"/>.</summary>
        private void PersistStateIfNeeded(bool forceImmediate)
        {
            var now = DateTimeOffset.UtcNow;
            if (!forceImmediate && now < _nextStateFlushUtc)
                return;

            _stateService.UpdateState(_allStates);
            _nextStateFlushUtc = now.Add(_stateMinInterval);
        }

        private static void TryDeletePartialTarget(string targetFile)
        {
            try
            {
                if (File.Exists(targetFile))
                    File.Delete(targetFile);
            }
            catch { /* ignore */ }
        }

        private static long SafeFileLength(string path)
        {
            try { return new FileInfo(path).Length; }
            catch { return 0; }
        }

        private static void CopyFileWithCancellation(string sourcePath, string destPath, CancellationToken ct)
        {
            const int bufSize = CopyBufferBytes;
            using var src = new FileStream(
                sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufSize,
                FileOptions.SequentialScan);
            using var dst = new FileStream(
                destPath, FileMode.Create, FileAccess.Write, FileShare.None, bufSize,
                FileOptions.WriteThrough);

            var buffer = new byte[bufSize];
            int        read;
            while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                dst.Write(buffer, 0, read);
            }
        }

        private static long RunCryptoSoft(string filePath, CancellationToken ct)
        {
            string cryptoSoftPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "CryptoSoft.exe");

            if (!File.Exists(cryptoSoftPath))
                return -1;

            var sw = Stopwatch.StartNew();
            Process? proc = null;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = cryptoSoftPath,
                    Arguments              = $"\"{filePath}\"",
                    UseShellExecute        = false,
                    RedirectStandardOutput = false,
                    CreateNoWindow         = true
                };

                proc = Process.Start(psi);
                if (proc == null)
                {
                    sw.Stop();
                    return -98;
                }

                while (!proc.WaitForExit(200))
                    ct.ThrowIfCancellationRequested();

                sw.Stop();
                int exitCode = proc.ExitCode;
                if (exitCode == 0)
                    return sw.ElapsedMilliseconds;

                if (exitCode < 0)
                    return exitCode;
                return -Math.Abs(exitCode);
            }
            catch (OperationCanceledException)
            {
                TryKillCrypto(proc);
                sw.Stop();
                throw;
            }
            catch
            {
                sw.Stop();
                return -sw.ElapsedMilliseconds;
            }
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
