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
    /// Core backup engine. Executes a backup job using the Strategy pattern:
    /// the concrete file-collection logic is delegated to an <see cref="IBackupStrategy"/>.
    /// Updates job state in real-time and logs every file transfer via EasyLog.
    /// </summary>
    public class BackupService
    {
        private readonly StateService _stateService;
        private readonly List<BackupState> _allStates;

        public BackupService(StateService stateService, List<BackupState> allStates)
        {
            _stateService = stateService;
            _allStates    = allStates;
        }

        /// <summary>
        /// Executes the given backup job using the appropriate strategy
        /// (Full → <see cref="CompleteBackup"/>, Differential → <see cref="DifferentialBackup"/>).
        /// </summary>
        /// <param name="job">The backup job to execute.</param>
        /// <param name="stateIndex">Zero-based index of this job's state in the allStates list.</param>
        /// <returns>True if completed without errors; false if any file transfer failed.</returns>
        public bool Execute(BackupJob job, int stateIndex)
        {
            if (!Directory.Exists(job.SourceDirectory))
                throw new DirectoryNotFoundException(
                    $"Source directory not found: {job.SourceDirectory}");

            // Select the strategy based on the job type
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
                state.CurrentSourceFile   = sourceFile;
                state.CurrentTargetFile   = targetFile;
                state.LastActionTimestamp = DateTime.Now.ToString("o");
                _stateService.UpdateState(_allStates);

                long fileSize       = new FileInfo(sourceFile).Length;
                long transferTimeMs;

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
                    transferTimeMs = -sw.ElapsedMilliseconds; // Negative indicates an error
                    hasError = true;
                }

                Logger.LogTransfer(job.Name, sourceFile, targetFile, fileSize, transferTimeMs);

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

        // ──────────────────────────────────────────────────────────────────
        // Private helpers
        // ──────────────────────────────────────────────────────────────────

        private static long ComputeTotalSize(List<(string Source, string Target)> files)
        {
            long total = 0;
            foreach ((string source, _) in files)
                total += new FileInfo(source).Length;
            return total;
        }
    }
}
