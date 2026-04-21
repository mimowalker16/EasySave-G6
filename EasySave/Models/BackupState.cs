using System.Text.Json.Serialization;

namespace EasySave.Models
{
    /// <summary>
    /// Represents the real-time execution state of a backup job.
    /// Written to state.json after every file operation.
    /// </summary>
    public class BackupState
    {
        /// <summary>Name of the backup job this state belongs to.</summary>
        [JsonPropertyName("JobName")]
        public string JobName { get; set; } = string.Empty;

        /// <summary>ISO 8601 timestamp of the last state change.</summary>
        [JsonPropertyName("LastActionTimestamp")]
        public string LastActionTimestamp { get; set; } = string.Empty;

        /// <summary>Current execution state of the job.</summary>
        [JsonPropertyName("State")]
        public BackupStateType State { get; set; } = BackupStateType.Inactive;

        /// <summary>Total number of files to process (only meaningful when Active).</summary>
        [JsonPropertyName("TotalFiles")]
        public int TotalFiles { get; set; }

        /// <summary>Total size of all files in bytes (only meaningful when Active).</summary>
        [JsonPropertyName("TotalSize")]
        public long TotalSize { get; set; }

        /// <summary>Number of files remaining to process.</summary>
        [JsonPropertyName("RemainingFiles")]
        public int RemainingFiles { get; set; }

        /// <summary>Total size of remaining files in bytes.</summary>
        [JsonPropertyName("RemainingSize")]
        public long RemainingSize { get; set; }

        /// <summary>Percentage progress (0–100).</summary>
        [JsonPropertyName("Progress")]
        public double Progress { get; set; }

        /// <summary>Full path of the file currently being copied.</summary>
        [JsonPropertyName("CurrentSourceFile")]
        public string CurrentSourceFile { get; set; } = string.Empty;

        /// <summary>Full target path of the file currently being copied.</summary>
        [JsonPropertyName("CurrentTargetFile")]
        public string CurrentTargetFile { get; set; } = string.Empty;
    }
}
