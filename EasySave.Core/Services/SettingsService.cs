using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using EasyLog;

namespace EasySave.Core.Services
{
    /// <summary>
<<<<<<< Updated upstream
    /// Persists global application settings to %APPDATA%\EasySave\Config\settings.json.
=======
    /// Persists global application settings:
    /// logging, encryption, scheduling, centralization, and business software detection.
    /// Settings are saved to %APPDATA%\EasySave\Config\settings.json.
>>>>>>> Stashed changes
    /// </summary>
    public class SettingsService
    {
        private readonly string _settingsFile;

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        /// <summary>Production constructor — uses %APPDATA%\EasySave\Config\.</summary>
        public SettingsService() : this(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasySave", "Config"))
        { }

        /// <summary>Test constructor — uses the provided directory.</summary>
        public SettingsService(string configDirectory)
        {
            Directory.CreateDirectory(configDirectory);
            _settingsFile = Path.Combine(configDirectory, "settings.json");
        }

        /// <summary>Loads settings from disk. Returns defaults if not found.</summary>
        public AppSettings Load()
        {
            if (!File.Exists(_settingsFile))
                return new AppSettings();

            try
            {
                string json = File.ReadAllText(_settingsFile);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                       ?? new AppSettings();
            }
            catch { return new AppSettings(); }
        }

        /// <summary>Saves the given settings to disk.</summary>
        public void Save(AppSettings settings)
        {
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_settingsFile, json);
        }
    }

    /// <summary>Global application settings persisted across sessions.</summary>
    public class AppSettings
    {
        /// <summary>Log output format: JSON (default) or XML.</summary>
        [JsonPropertyName("LogFormat")]
        public LogFormat LogFormat { get; set; } = LogFormat.Json;

        /// <summary>Custom daily log directory. Empty uses %APPDATA%\EasySave\Logs.</summary>
        [JsonPropertyName("LogDirectory")]
        public string LogDirectory { get; set; } = string.Empty;

        /// <summary>Local/central logging destination mode.</summary>
        [JsonPropertyName("LogDestinationMode")]
        public LogDestinationMode LogDestinationMode { get; set; } = LogDestinationMode.LocalOnly;

        /// <summary>HTTP endpoint used by the Docker central log collector.</summary>
        [JsonPropertyName("CentralLogEndpoint")]
        public string CentralLogEndpoint { get; set; } = string.Empty;

        /// <summary>Client/user identifier stored with centralized entries.</summary>
        [JsonPropertyName("CentralClientId")]
        public string CentralClientId { get; set; } = string.Empty;

        /// <summary>File extensions that must be transferred before non-priority files.</summary>
        [JsonPropertyName("PriorityExtensions")]
        public List<string> PriorityExtensions { get; set; } = new();

        /// <summary>Only one file above this size can be copied at a time. 0 disables the rule.</summary>
        [JsonPropertyName("LargeFileThresholdKb")]
        public long LargeFileThresholdKb { get; set; } = 0;

        /// <summary>
        /// Custom root directory for daily logs. Empty = default under %AppData%\EasySave\Logs.
        /// Environment variables (e.g. %ProgramData%) are expanded when applied.
        /// </summary>
        [JsonPropertyName("LogDirectory")]
        public string LogDirectory { get; set; } = string.Empty;

        /// <summary>How JSON logs are stored when <see cref="LogFormat"/> is <see cref="LogFormat.Json"/>.</summary>
        [JsonPropertyName("JsonLogLayout")]
        public JsonLogLayout JsonLogLayout { get; set; } = JsonLogLayout.PrettyArray;

        /// <summary>Log destination mode: local, central, or both.</summary>
        [JsonPropertyName("LogDestinationMode")]
        public LogDestinationMode LogDestinationMode { get; set; } = LogDestinationMode.LocalOnly;

        /// <summary>Central log collector endpoint (HTTP), e.g. http://localhost:8080/api/logs.</summary>
        [JsonPropertyName("CentralLogEndpoint")]
        public string CentralLogEndpoint { get; set; } = string.Empty;

        /// <summary>Optional client identifier for centralized logs.</summary>
        [JsonPropertyName("CentralClientId")]
        public string CentralClientId { get; set; } = string.Empty;

        /// <summary>
        /// Extensions that are globally considered priority for V3 scheduling.
        /// Non-priority files must wait while at least one priority file remains pending.
        /// </summary>
        [JsonPropertyName("PriorityExtensions")]
        public List<string> PriorityExtensions { get; set; } = new();

        /// <summary>
        /// Threshold in kilobytes used to throttle parallel transfers of large files.
        /// If &lt;= 0, the large-file parallel restriction is disabled.
        /// </summary>
        [JsonPropertyName("LargeFileThresholdKb")]
        public long LargeFileThresholdKb { get; set; } = 0;

        /// <summary>
        /// File extensions that should be encrypted via CryptoSoft (e.g. ".txt", ".docx").
        /// </summary>
        [JsonPropertyName("EncryptedExtensions")]
        public List<string> EncryptedExtensions { get; set; } = new();

        /// <summary>Process name of the business software that blocks backup execution.</summary>
        [JsonPropertyName("BusinessSoftwareName")]
        public string BusinessSoftwareName { get; set; } = string.Empty;
    }
}
