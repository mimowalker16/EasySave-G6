using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using EasySave.Core.Models;
using EasySave.Core.Strategies;
using EasyLog;

namespace EasySave.Core.Services
{
    /// <summary>
    /// Core backup engine. Executes a backup job using the Strategy pattern.
    /// Delegates file-collection logic to an <see cref="IBackupStrategy"/>,
    /// updates real-time state, logs every transfer via <see cref="ILogger"/>,
    /// optionally encrypts files via CryptoSoft,
    /// and blocks execution if a business software is detected.
    /// </summary>
    public class BackupService
    {
        private readonly StateService           _stateService;
        private readonly List<BackupState>      _allStates;
        private readonly ILogger                _logger;
        private readonly BusinessSoftwareService _businessSoftware;

        /// <summary>
        /// Creates a new BackupService.
        /// </summary>
        /// <param name="stateService">Service used to persist real-time job state.</param>
        /// <param name="allStates">Shared in-memory list of all job states.</param>
        /// <param name="logger">Logger used for file-transfer events (JSON or XML).</param>
        /// <param name="businessSoftware">Service used to detect running business software.</param>
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

        /// <summary>
        /// Executes the given backup job using the appropriate strategy.
        /// Throws <see cref="InvalidOperationException"/> if a business software is detected.
        /// </summary>
        /// <param name="job">The backup job to execute.</param>
        /// <param name="stateIndex">Zero-based index of this job's state in the allStates list.</param>
        /// <param name="settings">Global application settings (extensions, business software name).</param>
        /// <returns>True if completed without errors; false if any file transfer failed.</returns>
        public bool Execute(BackupJob job, int stateIndex, AppSettings settings)
        {
            if (!Directory.Exists(job.SourceDirectory))
                throw new DirectoryNotFoundException(
                    $"Source directory not found: {job.SourceDirectory}");

            if (_businessSoftware.IsRunning(settings.BusinessSoftwareName))
                throw new InvalidOperationException(
                    $"Business software '{settings.BusinessSoftwareName}' is running. Backup blocked.");

            IBackupStrategy strategy = job.Type == BackupType.Full
                ? new CompleteBackup()
                : new DifferentialBackup();

            List<(string Source, string Target)> filesToCopy = strategy.CollectFiles(job);

            // Initialise state
            BackupState state = _allStates[stateIndex];
            state.State               = BackupStateType.Active;
            state.TotalFiles          = filesToCopy.Count;
            state.TotalSize           = ComputeTotalSize(filesToCopy);
            state.RemainingFiles      = filesToCopy.Count;
            state.RemainingSize       = state.TotalSize;
            state.Progress            = 0;
            state.LastActionTimestamp = DateTime.Now.ToString("o");
            _stateService.UpdateState(_allStates);

            bool hasError = false;

            foreach ((string sourceFile, string targetFile) in filesToCopy)
            {
                // Block mid-backup if business software is detected
                if (_businessSoftware.IsRunning(settings.BusinessSoftwareName))
                {
                    _logger.LogTransfer(job.Name, sourceFile, targetFile,
                        new FileInfo(sourceFile).Length, -1, 0);
                    hasError = true;
                    break;
                }

                state.CurrentSourceFile   = sourceFile;
                state.CurrentTargetFile   = targetFile;
                state.LastActionTimestamp = DateTime.Now.ToString("o");
                _stateService.UpdateState(_allStates);

                long fileSize       = new FileInfo(sourceFile).Length;
                long transferTimeMs;
                long encryptionTimeMs = 0;

                var sw = Stopwatch.StartNew();
                try
                {
                    string? targetDir = Path.GetDirectoryName(targetFile);
                    if (targetDir != null)
                        Directory.CreateDirectory(targetDir);

                    File.Copy(sourceFile, targetFile, overwrite: true);
                    sw.Stop();
                    transferTimeMs = sw.ElapsedMilliseconds;
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
                    continue;
                }

                // Encrypt the file if its extension is in the list
                string extension = Path.GetExtension(sourceFile).ToLowerInvariant();
                if (settings.EncryptedExtensions.Count > 0
                    && settings.EncryptedExtensions.Contains(extension))
                {
                    encryptionTimeMs = RunCryptoSoft(targetFile);
                    if (encryptionTimeMs < 0)
                        hasError = true;
                }

                _logger.LogTransfer(job.Name, sourceFile, targetFile,
                    fileSize, transferTimeMs, encryptionTimeMs);

                state.RemainingFiles--;
                state.RemainingSize -= fileSize;
                state.Progress = state.TotalFiles > 0
                    ? Math.Round((1.0 - (double)state.RemainingFiles / state.TotalFiles) * 100, 1)
                    : 100;
            }

            // Mark job as completed
            state.State               = BackupStateType.End;
            state.Progress            = 100;
            state.RemainingFiles      = 0;
            state.RemainingSize       = 0;
            state.CurrentSourceFile   = string.Empty;
            state.CurrentTargetFile   = string.Empty;
            state.LastActionTimestamp = DateTime.Now.ToString("o");
            _stateService.UpdateState(_allStates);

            return !hasError;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Private helpers
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Runs the external CryptoSoft process on the given file.
        /// Returns the encryption time in ms on success, or a negative error code on failure.
        /// </summary>
        private static long RunCryptoSoft(string filePath)
        {
            // CryptoSoft.exe is expected to be alongside the executable.
            string cryptoSoftPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "CryptoSoft.exe");

            if (!File.Exists(cryptoSoftPath))
                return -1; // CryptoSoft not installed

            var sw = Stopwatch.StartNew();
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

                using Process? proc = Process.Start(psi);
                proc?.WaitForExit();
                sw.Stop();

                int exitCode = proc?.ExitCode ?? -99;
                return exitCode == 0 ? sw.ElapsedMilliseconds : exitCode;
            }
            catch
            {
                sw.Stop();
                return -sw.ElapsedMilliseconds;
            }
        }

        private static long ComputeTotalSize(List<(string Source, string Target)> files)
        {
            long total = 0;
            foreach ((string source, _) in files)
                total += new FileInfo(source).Length;
            return total;
        }
    }
}
