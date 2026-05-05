using System.Collections.Generic;
using EasySave.Core.Models;

namespace EasySave.Core.Strategies
{
    /// <summary>
    /// Defines the contract for a backup strategy.
    /// Each strategy is responsible for determining which files to copy
    /// from the source directory to the target directory.
    /// </summary>
    public interface IBackupStrategy
    {
        /// <summary>
        /// Collects the list of (sourcePath, targetPath) pairs to copy,
        /// based on the specific strategy logic (Full or Differential).
        /// </summary>
        /// <param name="job">The backup job configuration.</param>
        /// <returns>A list of file pairs to process.</returns>
        List<(string Source, string Target)> CollectFiles(BackupJob job);
    }
}
