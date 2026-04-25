using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using EasySave.Core.Models;

namespace EasySave.Core.Services
{
    /// <summary>
    /// Handles persistence of backup job configurations.
    /// Jobs are saved to %APPDATA%\EasySave\Config\jobs.json in JSON format.
    /// </summary>
    public class ConfigService
    {
        private readonly string _configDirectory;
        private readonly string _configFile;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true
        };

        public ConfigService()
        {
            _configDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasySave", "Config");

            _configFile = Path.Combine(_configDirectory, "jobs.json");

            Directory.CreateDirectory(_configDirectory);
        }

        /// <summary>
        /// Loads all backup jobs from the configuration file.
        /// Returns an empty list if the file does not exist or cannot be read.
        /// </summary>
        public List<BackupJob> LoadJobs()
        {
            if (!File.Exists(_configFile))
                return new List<BackupJob>();

            try
            {
                string json = File.ReadAllText(_configFile);
                return JsonSerializer.Deserialize<List<BackupJob>>(json, _jsonOptions)
                       ?? new List<BackupJob>();
            }
            catch
            {
                return new List<BackupJob>();
            }
        }

        /// <summary>
        /// Saves the provided list of backup jobs to the configuration file.
        /// </summary>
        public void SaveJobs(List<BackupJob> jobs)
        {
            string json = JsonSerializer.Serialize(jobs, _jsonOptions);
            File.WriteAllText(_configFile, json);
        }
    }
}
