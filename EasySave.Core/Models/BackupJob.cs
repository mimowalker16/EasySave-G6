using System.Text.Json.Serialization;

namespace EasySave.Core.Models
{
    /// <summary>
    /// Represents a backup job configuration with source, target, and backup type.
    /// Serialized to JSON for persistence via ConfigService.
    /// </summary>
    public class BackupJob
    {
        /// <summary>Unique name identifying this backup job (max 50 chars).</summary>
        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Full path to the source directory to back up.</summary>
        [JsonPropertyName("SourceDirectory")]
        public string SourceDirectory { get; set; } = string.Empty;

        /// <summary>Full path to the target directory where files will be copied.</summary>
        [JsonPropertyName("TargetDirectory")]
        public string TargetDirectory { get; set; } = string.Empty;

        /// <summary>Type of backup: Full or Differential.</summary>
        [JsonPropertyName("Type")]
        public BackupType Type { get; set; } = BackupType.Full;
    }
}
