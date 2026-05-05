using System.Collections.Generic;
using System.IO;
using EasySave.Core.Models;

namespace EasySave.Core.Strategies
{
    /// <summary>
    /// Full backup strategy: copies every file from the source directory
    /// to the target directory, regardless of whether it already exists.
    /// </summary>
    public class CompleteBackup : IBackupStrategy
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
                result.Add((sourceFile, targetFile));
            }

            return result;
        }
    }
}
