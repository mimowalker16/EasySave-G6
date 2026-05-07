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

<<<<<<< Updated upstream
        /// <summary>The job is temporarily paused (user pause or business software detected).</summary>
        Paused,

        /// <summary>The job has completed its last run (check logs for per-file errors).</summary>
        End,

        /// <summary>The user cancelled the run or the operation was interrupted cooperatively.</summary>
        Canceled
=======
        /// <summary>The job is waiting for a resume condition or user action.</summary>
        Paused,

        /// <summary>The job was stopped before completion.</summary>
        Canceled,

        /// <summary>The job has completed its last execution successfully.</summary>
        End
>>>>>>> Stashed changes
    }
}
