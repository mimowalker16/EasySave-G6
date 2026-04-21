using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace EasyLog
{
    /// <summary>
    /// Thread-safe daily JSON log writer for EasySave file transfer events.
    /// Log files are written to %APPDATA%\EasySave\Logs\YYYY-MM-DD.json
    /// </summary>
    public static class Logger
    {
        private static readonly object _lock = new();

        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EasySave", "Logs");

        /// <summary>
        /// Logs a single file transfer event to the daily JSON log file.
        /// </summary>
        /// <param name="backupJobName">Name of the backup job.</param>
        /// <param name="sourceFilePath">Source file path (UNC format preferred).</param>
        /// <param name="targetFilePath">Target file path (UNC format preferred).</param>
        /// <param name="fileSize">File size in bytes.</param>
        /// <param name="transferTimeMs">Transfer duration in milliseconds. Negative value indicates an error.</param>
        public static void LogTransfer(
            string backupJobName,
            string sourceFilePath,
            string targetFilePath,
            long fileSize,
            long transferTimeMs)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now.ToString("o"),
                BackupJobName = backupJobName,
                SourceFilePath = ToUncFormat(sourceFilePath),
                TargetFilePath = ToUncFormat(targetFilePath),
                FileSize = fileSize,
                TransferTimeMs = transferTimeMs
            };

            lock (_lock)
            {
                Directory.CreateDirectory(LogDirectory);

                string logFile = Path.Combine(LogDirectory, $"{DateTime.Now:yyyy-MM-dd}.json");

                // Read existing entries
                List<LogEntry> entries = new();
                if (File.Exists(logFile))
                {
                    try
                    {
                        string existing = File.ReadAllText(logFile);
                        entries = JsonSerializer.Deserialize<List<LogEntry>>(existing)
                                  ?? new List<LogEntry>();
                    }
                    catch
                    {
                        entries = new List<LogEntry>();
                    }
                }

                entries.Add(entry);

                string json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(logFile, json);
            }
        }

        /// <summary>
        /// Converts a local path to UNC format (\\MachineName\...).
        /// If already UNC or no conversion possible, returns as-is.
        /// </summary>
        private static string ToUncFormat(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (path.StartsWith(@"\\")) return path;

            // Convert C:\foo\bar → \\MachineName\C$\foo\bar
            if (path.Length >= 2 && path[1] == ':')
            {
                string machine = Environment.MachineName;
                char drive = path[0];
                string rest = path.Substring(2);
                return $@"\\{machine}\{drive}${rest}";
            }

            return path;
        }
    }

    /// <summary>
    /// Represents a single log entry written to the daily log file.
    /// </summary>
    internal class LogEntry
    {
        [JsonPropertyName("Timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("BackupJobName")]
        public string BackupJobName { get; set; } = string.Empty;

        [JsonPropertyName("SourceFilePath")]
        public string SourceFilePath { get; set; } = string.Empty;

        [JsonPropertyName("TargetFilePath")]
        public string TargetFilePath { get; set; } = string.Empty;

        [JsonPropertyName("FileSize")]
        public long FileSize { get; set; }

        [JsonPropertyName("TransferTimeMs")]
        public long TransferTimeMs { get; set; }
    }
}
