using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using EasySave.Core.Models;

namespace EasySave.Core.Services
{
    /// <summary>
    /// Manages the real-time state file for all backup jobs.
    /// State is written to %APPDATA%\EasySave\state.json after every file operation.
    /// </summary>
    public class StateService
    {
        private readonly string _stateFile;
        private readonly object _lock = new();

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true
        };

        public StateService()
        {
            string stateDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasySave");

            Directory.CreateDirectory(stateDirectory);
            _stateFile = Path.Combine(stateDirectory, "state.json");
        }

        /// <summary>
        /// Rewrites the complete state file with the current list of job states.
        /// Thread-safe: protected by an internal lock.
        /// </summary>
        /// <param name="allStates">The complete list of current job states.</param>
        public void UpdateState(List<BackupState> allStates)
        {
            lock (_lock)
            {
                string json = JsonSerializer.Serialize(allStates, _jsonOptions);
                File.WriteAllText(_stateFile, json);
            }
        }

        /// <summary>
        /// Loads the current state from file.
        /// Returns an empty list if the file does not exist or cannot be parsed.
        /// </summary>
        public List<BackupState> LoadState()
        {
            if (!File.Exists(_stateFile))
                return new List<BackupState>();

            try
            {
                string json = File.ReadAllText(_stateFile);
                return JsonSerializer.Deserialize<List<BackupState>>(json, _jsonOptions)
                       ?? new List<BackupState>();
            }
            catch
            {
                return new List<BackupState>();
            }
        }
    }
}
