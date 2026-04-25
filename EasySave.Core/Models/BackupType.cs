namespace EasySave.Core.Models
{
    /// <summary>
    /// Defines the type of backup operation.
    /// </summary>
    public enum BackupType
    {
        /// <summary>Copies all files from the source directory to the target directory.</summary>
        Full,

        /// <summary>Copies only files that differ (by LastWriteTime) from the target directory.</summary>
        Differential
    }
}
