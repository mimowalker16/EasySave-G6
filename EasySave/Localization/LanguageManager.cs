using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace EasySave.Localization
{
    /// <summary>
    /// Loads and serves localized UI strings from JSON language files.
    /// Default language is English. Switch dynamically via SetLanguage().
    /// </summary>
    public class LanguageManager
    {
        private Dictionary<string, string> _strings = new();

        private static readonly string LocalizationDirectory = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory,
            "Localization");

        /// <summary>Currently active language code ("en" or "fr").</summary>
        public string CurrentLanguage { get; private set; } = "en";

        public LanguageManager()
        {
            Load("en");
        }

        /// <summary>
        /// Switches to the specified language and reloads all strings.
        /// </summary>
        /// <param name="languageCode">Language code: "en" or "fr".</param>
        public void SetLanguage(string languageCode)
        {
            Load(languageCode);
            CurrentLanguage = languageCode;
        }

        /// <summary>
        /// Returns the localized string for the given key.
        /// Falls back to the key itself if not found.
        /// </summary>
        public string Get(string key)
        {
            return _strings.TryGetValue(key, out string? value) ? value : key;
        }

        /// <summary>
        /// Returns the localized string with format arguments applied.
        /// </summary>
        public string Get(string key, params object[] args)
        {
            string template = Get(key);
            try { return string.Format(template, args); }
            catch { return template; }
        }

        // ──────────────────────────────────────────────────────────────────

        private void Load(string languageCode)
        {
            string filePath = Path.Combine(LocalizationDirectory, $"{languageCode}.json");

            if (!File.Exists(filePath))
            {
                // Fallback to English if file not found
                filePath = Path.Combine(LocalizationDirectory, "en.json");
            }

            if (!File.Exists(filePath))
            {
                _strings = new Dictionary<string, string>();
                return;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                           ?? new Dictionary<string, string>();
            }
            catch
            {
                _strings = new Dictionary<string, string>();
            }
        }
    }
}
