using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using EasyLog;

namespace EasySave.Core.Services
{
    /// <summary>
    /// Persists global application settings:
    /// log format (JSON/XML), encrypted file extensions, and business software name.
    /// Settings are saved to %APPDATA%\EasySave\Config\settings.json.
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

        /// <summary>
        /// File extensions that should be encrypted via CryptoSoft (e.g. ".txt", ".docx").
        /// Empty list = no encryption.
        /// </summary>
        [JsonPropertyName("EncryptedExtensions")]
        public List<string> EncryptedExtensions { get; set; } = new();

        /// <summary>
        /// Process name of the business software that blocks backup execution.
        /// Empty = no blocking.
        /// </summary>
        [JsonPropertyName("BusinessSoftwareName")]
        public string BusinessSoftwareName { get; set; } = string.Empty;
    }
}
