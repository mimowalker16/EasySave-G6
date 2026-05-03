using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace EasyLog
{
    // ──────────────────────────────────────────────────────────────────────────
    // Log format selection
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Output format for daily log files.</summary>
    public enum LogFormat { Json, Xml }

    // ──────────────────────────────────────────────────────────────────────────
    // Contract
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Contract for daily log writers used by EasySave.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs a single file-transfer event.
        /// </summary>
        /// <param name="backupJobName">Name of the backup job.</param>
        /// <param name="sourceFilePath">Source file path (UNC format preferred).</param>
        /// <param name="targetFilePath">Target file path (UNC format preferred).</param>
        /// <param name="fileSize">File size in bytes.</param>
        /// <param name="transferTimeMs">Transfer duration in ms. Negative = error.</param>
        /// <param name="encryptionTimeMs">
        ///   Encryption duration in ms.
        ///   0  = no encryption applied.
        ///  >0  = encryption succeeded (duration in ms).
        ///  &lt;0  = encryption error code.
        /// </param>
        void LogTransfer(
            string backupJobName,
            string sourceFilePath,
            string targetFilePath,
            long   fileSize,
            long   transferTimeMs,
            long   encryptionTimeMs = 0);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Shared log entry
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Represents a single log entry written to the daily log file.</summary>
    [XmlRoot("LogEntry")]
    public class LogEntry
    {
        [JsonPropertyName("Timestamp")]
        [XmlElement("Timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("BackupJobName")]
        [XmlElement("BackupJobName")]
        public string BackupJobName { get; set; } = string.Empty;

        [JsonPropertyName("SourceFilePath")]
        [XmlElement("SourceFilePath")]
        public string SourceFilePath { get; set; } = string.Empty;

        [JsonPropertyName("TargetFilePath")]
        [XmlElement("TargetFilePath")]
        public string TargetFilePath { get; set; } = string.Empty;

        [JsonPropertyName("FileSize")]
        [XmlElement("FileSize")]
        public long FileSize { get; set; }

        [JsonPropertyName("TransferTimeMs")]
        [XmlElement("TransferTimeMs")]
        public long TransferTimeMs { get; set; }

        /// <summary>
        /// Time spent encrypting the file in milliseconds.
        /// 0 = not encrypted, &gt;0 = success, &lt;0 = error code.
        /// </summary>
        [JsonPropertyName("EncryptionTimeMs")]
        [XmlElement("EncryptionTimeMs")]
        public long EncryptionTimeMs { get; set; }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // JSON implementation
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Thread-safe daily JSON log writer.
    /// Writes to %APPDATA%\EasySave\Logs\YYYY-MM-DD.json
    /// </summary>
    public class JsonLogger : ILogger
    {
        private static readonly object Lock = new();

        private readonly string _logDirectory;

        /// <summary>Production constructor — uses %APPDATA%\EasySave\Logs\.</summary>
        public JsonLogger() : this(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasySave", "Logs"))
        { }

        /// <summary>Test constructor — uses the provided directory.</summary>
        public JsonLogger(string logDirectory)
        {
            _logDirectory = logDirectory;
        }

        /// <inheritdoc/>
        public void LogTransfer(
            string backupJobName,
            string sourceFilePath,
            string targetFilePath,
            long   fileSize,
            long   transferTimeMs,
            long   encryptionTimeMs = 0)
        {
            var entry = BuildEntry(backupJobName, sourceFilePath, targetFilePath,
                                   fileSize, transferTimeMs, encryptionTimeMs);
            lock (Lock)
            {
                Directory.CreateDirectory(_logDirectory);
                string logFile = Path.Combine(_logDirectory, $"{DateTime.Now:yyyy-MM-dd}.json");

                List<LogEntry> entries = new();
                if (File.Exists(logFile))
                {
                    try
                    {
                        string existing = File.ReadAllText(logFile);
                        entries = JsonSerializer.Deserialize<List<LogEntry>>(existing)
                                  ?? new List<LogEntry>();
                    }
                    catch { entries = new List<LogEntry>(); }
                }

                entries.Add(entry);
                File.WriteAllText(logFile,
                    JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
            }
        }

        private static LogEntry BuildEntry(
            string backupJobName, string sourceFilePath, string targetFilePath,
            long fileSize, long transferTimeMs, long encryptionTimeMs) => new()
        {
            Timestamp        = DateTime.Now.ToString("o"),
            BackupJobName    = backupJobName,
            SourceFilePath   = ToUncFormat(sourceFilePath),
            TargetFilePath   = ToUncFormat(targetFilePath),
            FileSize         = fileSize,
            TransferTimeMs   = transferTimeMs,
            EncryptionTimeMs = encryptionTimeMs
        };

        private static string ToUncFormat(string path)
        {
            if (string.IsNullOrEmpty(path) || path.StartsWith(@"\\")) return path;
            if (path.Length >= 2 && path[1] == ':')
            {
                string machine = Environment.MachineName;
                char   drive   = path[0];
                string rest    = path.Substring(2);
                return $@"\\{machine}\{drive}${rest}";
            }
            return path;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // XML implementation
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Thread-safe daily XML log writer.
    /// Writes to %APPDATA%\EasySave\Logs\YYYY-MM-DD.xml
    /// </summary>
    public class XmlLogger : ILogger
    {
        private static readonly object Lock = new();

        private readonly string _logDirectory;

        // Wrapper needed for XmlSerializer to produce a root <LogEntries> element
        [XmlRoot("LogEntries")]
        public class LogEntryList
        {
            [XmlElement("LogEntry")]
            public List<LogEntry> Entries { get; set; } = new();
        }

        /// <summary>Production constructor — uses %APPDATA%\EasySave\Logs\.</summary>
        public XmlLogger() : this(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasySave", "Logs"))
        { }

        /// <summary>Test constructor — uses the provided directory.</summary>
        public XmlLogger(string logDirectory)
        {
            _logDirectory = logDirectory;
        }

        /// <inheritdoc/>
        public void LogTransfer(
            string backupJobName,
            string sourceFilePath,
            string targetFilePath,
            long   fileSize,
            long   transferTimeMs,
            long   encryptionTimeMs = 0)
        {
            var entry = new LogEntry
            {
                Timestamp        = DateTime.Now.ToString("o"),
                BackupJobName    = backupJobName,
                SourceFilePath   = ToUncFormat(sourceFilePath),
                TargetFilePath   = ToUncFormat(targetFilePath),
                FileSize         = fileSize,
                TransferTimeMs   = transferTimeMs,
                EncryptionTimeMs = encryptionTimeMs
            };

            var serializer = new XmlSerializer(typeof(LogEntryList));

            lock (Lock)
            {
                Directory.CreateDirectory(_logDirectory);
                string logFile = Path.Combine(_logDirectory, $"{DateTime.Now:yyyy-MM-dd}.xml");

                var list = new LogEntryList();
                if (File.Exists(logFile))
                {
                    try
                    {
                        using var reader = new FileStream(logFile, FileMode.Open);
                        list = (LogEntryList?)serializer.Deserialize(reader) ?? new LogEntryList();
                    }
                    catch { list = new LogEntryList(); }
                }

                list.Entries.Add(entry);

                using var writer = new FileStream(logFile, FileMode.Create);
                serializer.Serialize(writer, list);
            }
        }

        private static string ToUncFormat(string path)
        {
            if (string.IsNullOrEmpty(path) || path.StartsWith(@"\\")) return path;
            if (path.Length >= 2 && path[1] == ':')
            {
                string machine = Environment.MachineName;
                char   drive   = path[0];
                string rest    = path.Substring(2);
                return $@"\\{machine}\{drive}${rest}";
            }
            return path;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Factory
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Creates the appropriate ILogger based on the requested format.</summary>
    public static class LoggerFactory
    {
        public static ILogger Create(LogFormat format) => format switch
        {
            LogFormat.Xml => new XmlLogger(),
            _             => new JsonLogger()
        };
    }
}
