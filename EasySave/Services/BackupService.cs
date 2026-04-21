using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using EasySave.Models;
using EasyLog;

namespace EasySave.Services
{
    /// <summary>
    /// Core backup engine. Handles Full and Differential backup strategies.
    /// Updates state in real-time and logs every file transfer to EasyLog.
    /// </summary>
    public class BackupService
    {
        private readonly StateService _stateService;
        private readonly List<BackupState> _allStates;

        public BackupService(StateService stateService, List<BackupState> allStates)
        {
            _stateService = stateService;
            _allStates = allStates;
        }

        /// <summary>
        /// Executes the given backup job (Full or Differential).
        /// </summary>
        /// <param name="job">The backup job to execute.</param>
        /// <param name="stateIndex">Index of this job's state in the allStates list.</param>
        /// <returns>True if completed without errors, false if any error occurred.</returns>
        public bool Execute(BackupJob job, int stateIndex)
        {
            if (!Directory.Exists(job.SourceDirectory))
                throw new DirectoryNotFoundException($"Source directory not found: {job.SourceDirectory}");

            // Collect all files to process
            var filesToCopy = CollectFiles(job);

            // Initialize state
            var state = _allStates[stateIndex];
            state.State = BackupStateType.Active;
            state.TotalFiles = filesToCopy.Count;
            state.TotalSize = ComputeTotalSize(filesToCopy);
            state.RemainingFiles = filesToCopy.Count;
            state.RemainingSize = state.TotalSize;
            state.Progress = 0;
            state.LastActionTimestamp = DateTime.Now.ToString("o");
            _stateService.UpdateState(_allStates);

            bool hasError = false;

            foreach (var (sourceFile, targetFile) in filesToCopy)
            {
                state.CurrentSourceFile = sourceFile;
                state.CurrentTargetFile = targetFile;
                state.LastActionTimestamp = DateTime.Now.ToString("o");
                _stateService.UpdateState(_allStates);

                long fileSize = new FileInfo(sourceFile).Length;
                long transferTimeMs;

                var sw = Stopwatch.StartNew();
                try
                {
                    // Ensure target subdirectory exists
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
                    transferTimeMs = -sw.ElapsedMilliseconds; // Negative = error
                    hasError = true;
                }

                // Log this transfer
                Logger.LogTransfer(job.Name, sourceFile, targetFile, fileSize, transferTimeMs);

                // Update remaining counts
                state.RemainingFiles--;
                state.RemainingSize -= fileSize;
                state.Progress = state.TotalFiles > 0
                    ? Math.Round((1.0 - (double)state.RemainingFiles / state.TotalFiles) * 100, 1)
                    : 100;
            }

            // Mark job as done
            state.State = BackupStateType.End;
            state.Progress = 100;
            state.RemainingFiles = 0;
            state.RemainingSize = 0;
            state.CurrentSourceFile = string.Empty;
            state.CurrentTargetFile = string.Empty;
            state.LastActionTimestamp = DateTime.Now.ToString("o");
            _stateService.UpdateState(_allStates);

            return !hasError;
        }

        // ──────────────────────────────────────────────────────────────────
        // Private helpers
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the list of (sourcePath, targetPath) pairs to copy
        /// based on the job type (Full or Differential).
        /// </summary>
        private List<(string source, string target)> CollectFiles(BackupJob job)
        {
            var result = new List<(string, string)>();

            var allFiles = Directory.EnumerateFiles(job.SourceDirectory, "*", SearchOption.AllDirectories);

            foreach (string sourceFile in allFiles)
            {
                // Compute relative path to preserve directory structure
                string relativePath = Path.GetRelativePath(job.SourceDirectory, sourceFile);
                string targetFile = Path.Combine(job.TargetDirectory, relativePath);

                if (job.Type == BackupType.Full)
                {
                    result.Add((sourceFile, targetFile));
                }
                else // Differential
                {
                    bool shouldCopy = !File.Exists(targetFile)
                        || File.GetLastWriteTime(sourceFile) > File.GetLastWriteTime(targetFile);

                    if (shouldCopy)
                        result.Add((sourceFile, targetFile));
                }
            }

            return result;
        }

        private static long ComputeTotalSize(List<(string source, string target)> files)
        {
            long total = 0;
            foreach (var (source, _) in files)
                total += new FileInfo(source).Length;
            return total;
        }
    }
}
