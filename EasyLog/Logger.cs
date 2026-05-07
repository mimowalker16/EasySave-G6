using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace EasyLog
{
    public enum LogFormat { Json, Xml }

    public enum JsonLogLayout
    {
        PrettyArray,
        Ndjson
    }

    public enum LogDestinationMode
    {
        LocalOnly,
        CentralOnly,
        LocalAndCentral
    }

    public sealed class LoggerOptions
    {
        public string? LogDirectory { get; init; }
        public LogDestinationMode DestinationMode { get; init; } = LogDestinationMode.LocalOnly;
        public string? CentralLogEndpoint { get; init; }
        public string? CentralClientId { get; init; }
    }

    public interface ILogger
    {
        void LogTransfer(
            string backupJobName,
            string sourceFilePath,
            string targetFilePath,
            long fileSize,
            long transferTimeMs,
            long encryptionTimeMs = 0);
    }

    [XmlRoot("LogEntry")]
    public class LogEntry
    {
        /// <summary>ISO 8601 timestamp when the transfer was logged.</summary>
        [JsonPropertyName("Timestamp")]
        [XmlElement("Timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        /// <summary>Configured backup job name.</summary>
        [JsonPropertyName("BackupJobName")]
        [XmlElement("BackupJobName")]
        public string BackupJobName { get; set; } = string.Empty;

        /// <summary>Full source path (UNC preferred).</summary>
        [JsonPropertyName("SourceFilePath")]
        [XmlElement("SourceFilePath")]
        public string SourceFilePath { get; set; } = string.Empty;

        /// <summary>Full destination path (UNC preferred).</summary>
        [JsonPropertyName("TargetFilePath")]
        [XmlElement("TargetFilePath")]
        public string TargetFilePath { get; set; } = string.Empty;

        /// <summary>Transferred file length in bytes.</summary>
        [JsonPropertyName("FileSize")]
        [XmlElement("FileSize")]
        public long FileSize { get; set; }

        /// <summary>Copy duration in milliseconds; negative on failure.</summary>
        [JsonPropertyName("TransferTimeMs")]
        [XmlElement("TransferTimeMs")]
        public long TransferTimeMs { get; set; }

        [JsonPropertyName("EncryptionTimeMs")]
        [XmlElement("EncryptionTimeMs")]
        public long EncryptionTimeMs { get; set; }
    }

    public static class LogDirectoryResolver
    {
        public static string Resolve(string? customOrEmpty)
        {
            if (!string.IsNullOrWhiteSpace(customOrEmpty))
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(customOrEmpty.Trim()));

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasySave",
                "Logs");
        }
    }

    public static class LogPathFormatter
    {
        public static string ToUncFormat(string path)
        {
            if (string.IsNullOrEmpty(path) || path.StartsWith(@"\\"))
                return path;

            if (path.Length >= 2 && path[1] == ':')
            {
                string machine = Environment.MachineName;
                char drive = char.ToUpperInvariant(path[0]);
                string rest = path.Substring(2).Replace("/", @"\");
                return $@"\\{machine}\{drive}${rest}";
            }

            return path;
        }
    }

    internal static class LogFileSync
    {
        private static readonly ConcurrentDictionary<string, object> Locks = new();

        public static object For(string logFile)
            => Locks.GetOrAdd(Path.GetFullPath(logFile), _ => new object());
    }

    internal static class CentralLogSender
    {
        private static readonly HttpClient Http = new();

        public static void TrySend(LoggerOptions options, LogFormat format, LogEntry entry)
        {
            if (options.DestinationMode == LogDestinationMode.LocalOnly)
                return;
            if (string.IsNullOrWhiteSpace(options.CentralLogEndpoint))
                return;

            try
            {
                var payload = new
                {
                    clientId = string.IsNullOrWhiteSpace(options.CentralClientId)
                        ? Environment.MachineName
                        : options.CentralClientId.Trim(),
                    format = format.ToString(),
                    timestamp = entry.Timestamp,
                    entry
                };

                _ = Http.PostAsJsonAsync(options.CentralLogEndpoint, payload);
            }
            catch
            {
                // Central logging is best-effort; backup execution must continue.
            }
        }
    }

    public class JsonLogger : ILogger
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private readonly string _logDirectory;
        private readonly LoggerOptions _options;

        public JsonLogger() : this(LogDirectoryResolver.Resolve(null), new LoggerOptions())
        { }

        public JsonLogger(string logDirectory) : this(logDirectory, new LoggerOptions())
        { }

        public JsonLogger(string logDirectory, LoggerOptions options)
        {
            _logDirectory = logDirectory;
            _options = options;
        }

        public void LogTransfer(
            string backupJobName,
            string sourceFilePath,
            string targetFilePath,
            long fileSize,
            long transferTimeMs,
            long encryptionTimeMs = 0)
        {
            LogEntry entry = BuildEntry(
                backupJobName,
                sourceFilePath,
                targetFilePath,
                fileSize,
                transferTimeMs,
                encryptionTimeMs);

            if (_options.DestinationMode != LogDestinationMode.CentralOnly)
                WriteLocal(entry);

            CentralLogSender.TrySend(_options, LogFormat.Json, entry);
        }

        private void WriteLocal(LogEntry entry)
        {
            Directory.CreateDirectory(_logDirectory);
            string logFile = Path.Combine(_logDirectory, $"{DateTime.Now:yyyy-MM-dd}.json");

            lock (LogFileSync.For(logFile))
            {
                var entries = new List<LogEntry>();
                if (File.Exists(logFile))
                {
                    try
                    {
                        entries = JsonSerializer.Deserialize<List<LogEntry>>(
                            File.ReadAllText(logFile),
                            JsonOptions) ?? new List<LogEntry>();
                    }
                    catch
                    {
                        entries = new List<LogEntry>();
                    }
                }

                entries.Add(entry);
                File.WriteAllText(logFile, JsonSerializer.Serialize(entries, JsonOptions), Encoding.UTF8);
            }
        }

        private static LogEntry BuildEntry(
            string backupJobName,
            string sourceFilePath,
            string targetFilePath,
            long fileSize,
            long transferTimeMs,
            long encryptionTimeMs) => new()
        {
            Timestamp = DateTime.Now.ToString("o"),
            BackupJobName = backupJobName,
            SourceFilePath = LogPathFormatter.ToUncFormat(sourceFilePath),
            TargetFilePath = LogPathFormatter.ToUncFormat(targetFilePath),
            FileSize = fileSize,
            TransferTimeMs = transferTimeMs,
            EncryptionTimeMs = encryptionTimeMs
        };
    }

    public class XmlLogger : ILogger
    {
        private readonly string _logDirectory;
        private readonly LoggerOptions _options;

        [XmlRoot("LogEntries")]
        public class LogEntryList
        {
            /// <summary>Sequential log rows.</summary>
            [XmlElement("LogEntry")]
            public List<LogEntry> Entries { get; set; } = new();
        }

        public XmlLogger() : this(LogDirectoryResolver.Resolve(null), new LoggerOptions())
        { }

        public XmlLogger(string logDirectory) : this(logDirectory, new LoggerOptions())
        { }

        public XmlLogger(string logDirectory, LoggerOptions options)
        {
            _logDirectory = logDirectory;
            _options = options;
        }

        public void LogTransfer(
            string backupJobName,
            string sourceFilePath,
            string targetFilePath,
            long fileSize,
            long transferTimeMs,
            long encryptionTimeMs = 0)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now.ToString("o"),
                BackupJobName = backupJobName,
                SourceFilePath = LogPathFormatter.ToUncFormat(sourceFilePath),
                TargetFilePath = LogPathFormatter.ToUncFormat(targetFilePath),
                FileSize = fileSize,
                TransferTimeMs = transferTimeMs,
                EncryptionTimeMs = encryptionTimeMs
            };

            if (_options.DestinationMode != LogDestinationMode.CentralOnly)
                WriteLocal(entry);

            CentralLogSender.TrySend(_options, LogFormat.Xml, entry);
        }

        private void WriteLocal(LogEntry entry)
        {
            Directory.CreateDirectory(_logDirectory);
            string logFile = Path.Combine(_logDirectory, $"{DateTime.Now:yyyy-MM-dd}.xml");
            var serializer = new XmlSerializer(typeof(LogEntryList));

            lock (LogFileSync.For(logFile))
            {
                var list = new LogEntryList();
                if (File.Exists(logFile))
                {
                    try
                    {
                        using var reader = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                        list = (LogEntryList?)serializer.Deserialize(reader) ?? new LogEntryList();
                    }
                    catch
                    {
                        list = new LogEntryList();
                    }
                }

                list.Entries.Add(entry);

                using var writer = new FileStream(logFile, FileMode.Create, FileAccess.Write, FileShare.Read);
                serializer.Serialize(writer, list);
            }
        }
    }

    public static class LoggerFactory
    {
        public static ILogger Create(LogFormat format)
            => Create(format, null);

        public static ILogger Create(LogFormat format, LoggerOptions? options)
        {
            LoggerOptions opts = options ?? new LoggerOptions();
            string directory = LogDirectoryResolver.Resolve(opts.LogDirectory);
            return format == LogFormat.Xml
                ? new XmlLogger(directory, opts)
                : new JsonLogger(directory, opts);
        }
    }
}
