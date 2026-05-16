using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using EasySave.Core.Models;

namespace EasySave.Core.Services
{
    /// <summary>
    /// Manages the real-time state file for all backup jobs (atomic writes).
    /// </summary>
    public class StateService
    {
        private readonly string _stateFile;
        private readonly object _lock = new();

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            // state.json is machine-read only — compact JSON is ~2x faster to serialize.
            WriteIndented = true
        };

        /// <summary>Production constructor — uses %APPDATA%\EasySave\.</summary>
        public StateService() : this(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasySave"))
        { }

        /// <summary>Test constructor — uses the provided directory.</summary>
        public StateService(string stateDirectory)
        {
            Directory.CreateDirectory(stateDirectory);
            _stateFile = Path.Combine(stateDirectory, "state.json");
        }

        /// <summary>Rewrites the complete state file with the current list of job states.</summary>
        public void UpdateState(List<BackupState> allStates)
        {
            lock (_lock)
            {
                string json = JsonSerializer.Serialize(allStates, _jsonOptions);
                string? dir = Path.GetDirectoryName(_stateFile);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                string tempPath = Path.Combine(
                    dir ?? ".",
                    $".state.{Guid.NewGuid():N}.tmp");

                try
                {
                    File.WriteAllText(tempPath, json);
                    File.Move(tempPath, _stateFile, overwrite: true);
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); }
                        catch { /* best-effort */ }
                    }
                }
            }
        }

        /// <summary>Loads the current state from file.</summary>
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
