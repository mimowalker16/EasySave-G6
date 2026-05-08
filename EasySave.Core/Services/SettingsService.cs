using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using EasyLog;

namespace EasySave.Core.Services
{
    /// <summary>
    /// Persists global application settings:
    /// logging, encryption, scheduling, centralization, and business software detection.
    /// Settings are saved to %APPDATA%\EasySave\Config\settings.json.
    /// </summary>
    public class SettingsService
    {
        private static readonly ConcurrentDictionary<string, object> FileLocks = new();

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
            lock (GetFileLock())
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
        }

        /// <summary>Saves the given settings to disk.</summary>
        public void Save(AppSettings settings)
        {
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            lock (GetFileLock())
            {
                WriteAtomically(_settingsFile, json);
            }
        }

        private object GetFileLock()
            => FileLocks.GetOrAdd(Path.GetFullPath(_settingsFile), _ => new object());

        private static void WriteAtomically(string targetFile, string content)
        {
            string? dir = Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            string tempFile = Path.Combine(dir ?? ".", $".{Path.GetFileName(targetFile)}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(tempFile, content);
                File.Move(tempFile, targetFile, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); }
                    catch { }
                }
            }
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

        /// <summary>Reserved JSON layout setting kept for v1.1/v3 settings compatibility.</summary>
        [JsonPropertyName("JsonLogLayout")]
        public JsonLogLayout JsonLogLayout { get; set; } = JsonLogLayout.PrettyArray;

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
        /// File extensions that should be encrypted via CryptoSoft (e.g. ".txt", ".docx").
        /// </summary>
        [JsonPropertyName("EncryptedExtensions")]
        public List<string> EncryptedExtensions { get; set; } = new();

        /// <summary>Process name of the business software that blocks backup execution.</summary>
        [JsonPropertyName("BusinessSoftwareName")]
        public string BusinessSoftwareName { get; set; } = string.Empty;

        /// <summary>Preferred WPF language ("en" or "fr"). Console localization remains separate.</summary>
        [JsonPropertyName("UiLanguage")]
        public string UiLanguage { get; set; } = "en";
    }
}
