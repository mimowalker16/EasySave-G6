using System.Collections.Generic;
using System.IO;
using EasySave.Core.Models;

namespace EasySave.Core.Strategies
{
    /// <summary>
    /// Differential backup strategy: copies only files that are newer in the source
    /// than in the target (compared by LastWriteTime), or that do not yet exist
    /// in the target directory.
    /// </summary>
    public class DifferentialBackup : IBackupStrategy
    {
        /// <inheritdoc />
        public List<(string Source, string Target)> CollectFiles(BackupJob job)
        {
            var result = new List<(string Source, string Target)>();

            foreach (string sourceFile in Directory.EnumerateFiles(
                         job.SourceDirectory, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(job.SourceDirectory, sourceFile);
                string targetFile   = Path.Combine(job.TargetDirectory, relativePath);

                // Single FileInfo stat instead of two separate OS calls (Exists + GetLastWriteTime).
                var targetInfo = new FileInfo(targetFile);
                bool shouldCopy = !targetInfo.Exists
                               || File.GetLastWriteTime(sourceFile) > targetInfo.LastWriteTime;

                if (shouldCopy)
                    result.Add((sourceFile, targetFile));
            }

            return result;
        }
    }
}

