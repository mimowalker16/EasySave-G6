namespace EasySave.Core.Models
{
    /// <summary>
    /// Represents the current execution state of a backup job.
    /// </summary>
    public enum BackupStateType
    {
        /// <summary>The job has never been run or has been reset.</summary>
        Inactive,

        /// <summary>The job is currently running.</summary>
        Active,

        /// <summary>The job has completed its last execution successfully.</summary>
        End
    }
}
