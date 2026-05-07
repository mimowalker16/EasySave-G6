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

        /// <summary>The job is waiting for a resume condition or user action.</summary>
        Paused,

        /// <summary>The job was stopped before completion.</summary>
        Canceled,

        /// <summary>The job has completed its last execution successfully.</summary>
        End
    }
}
