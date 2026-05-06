using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace EasyLog
{
    // ──────────────────────────────────────────────────────────────────────────
    // Log format selection
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Output format for daily log files.</summary>
    public enum LogFormat
    {
        /// <summary>JSON (see <see cref="JsonLogLayout"/>).</summary>
        Json,

        /// <summary>Indented XML (<c>LogEntries</c> root) in a daily file.</summary>
        Xml
    }

    /// <summary>How JSON daily files are stored when <see cref="LogFormat"/> is <see cref="LogFormat.Json"/>.</summary>
    public enum JsonLogLayout
    {
        /// <summary>A single pretty-printed JSON array per day (legacy, slower for huge runs).</summary>
        PrettyArray,

        /// <summary>One compact JSON object per line in a <c>.ndjson</c> file (append-only, fast).</summary>
        Ndjson
    }

    /// <summary>Where to write daily logs.</summary>
    public enum LogDestinationMode
    {
        /// <summary>Write only local files.</summary>
        LocalOnly,
        /// <summary>Send only to central server.</summary>
        CentralOnly,
        /// <summary>Write local and send central copy.</summary>
        LocalAndCentral
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Options + directory resolution
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Optional parameters when building a logger (<see cref="LoggerFactory"/>).</summary>
    public sealed class LoggerOptions
    {
        /// <summary>Custom log root. Empty or null = default %AppData%\EasySave\Logs. Environment variables expanded.</summary>
        public string? LogDirectory { get; init; }

        /// <summary>JSON storage layout (ignored when format is XML).</summary>
        public JsonLogLayout JsonLogLayout { get; init; } = JsonLogLayout.PrettyArray;

        /// <summary>Destination mode for logs.</summary>
        public LogDestinationMode DestinationMode { get; init; } = LogDestinationMode.LocalOnly;

        /// <summary>Optional central log server endpoint, e.g. http://localhost:8080/api/logs.</summary>
        public string? CentralLogEndpoint { get; init; }

        /// <summary>Logical client id to distinguish emitters in centralized mode.</summary>
        public string? CentralClientId { get; init; }
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
                        : options.CentralClientId,
                    format = format.ToString(),
                    timestamp = entry.Timestamp,
                    entry
                };
                // fire-and-forget by design: backup flow must not fail on central sink outages
                _ = Http.PostAsJsonAsync(options.CentralLogEndpoint, payload);
            }
            catch
            {
                // swallow centralization errors to preserve backup execution
            }
        }
    }

    /// <summary>Resolves the directory where daily log files are written.</summary>
    public static class LogDirectoryResolver
    {
        /// <summary>
        /// Returns an absolute path. When <paramref name="customOrEmpty"/> is blank, uses the default under AppData.
        /// </summary>
        public static string Resolve(string? customOrEmpty)
        {
            if (!string.IsNullOrWhiteSpace(customOrEmpty))
            {
                string expanded = Environment.ExpandEnvironmentVariables(customOrEmpty.Trim());
                return Path.GetFullPath(expanded);
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasySave", "Logs");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // UNC path normalization (shared)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Normalizes filesystem paths toward UNC-style notation for logs.</summary>
    public static class LogPathFormatter
    {
        /// <summary>
        /// Converts drive-letter paths (<c>X:\...</c>) to <c>\\Machine\X$\...</c> UNC form.
        /// Leaves existing UNC (<c>\\</c>) paths unchanged.
        /// </summary>
        public static string ToUncFormat(string path)
        {
            if (string.IsNullOrEmpty(path) || path.StartsWith(@"\\"))
                return path;

            if (path.Length >= 2 && path[1] == ':')
            {
                string machine = Environment.MachineName;
                char   drive   = char.ToUpperInvariant(path[0]);
                string rest    = path.Substring(2).Replace("/", @"\");
                return $@"\\{machine}\{drive}${rest}";
            }

            return path;
        }

        private static void WriteTextAtomic(string destinationPath, string content)
        {
            string? dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            string tempPath = Path.Combine(dir ?? @"\", $".{(Path.GetFileName(destinationPath))}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(tempPath, content);
                File.Move(tempPath, destinationPath, overwrite: true);
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

        private static void WriteBytesAtomic(string destinationPath, byte[] data)
        {
            string? dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            string tempPath = Path.Combine(dir ?? @"\", $".{(Path.GetFileName(destinationPath))}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllBytes(tempPath, data);
                File.Move(tempPath, destinationPath, overwrite: true);
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

        internal static void WriteAtomic(string destinationPath, string content)
            => WriteTextAtomic(destinationPath, content);

        internal static void WriteAtomic(string destinationPath, byte[] data)
            => WriteBytesAtomic(destinationPath, data);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Contract
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Contract for daily log writers used by EasySave.
    /// </summary>
    public interface ILogger
    {
        /// <summary>Appends one file transfer line to the daily log.</summary>
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

        /// <summary>
        /// Time spent encrypting the file in milliseconds.
        /// 0 = not encrypted, &gt;0 = success, &lt;0 = error code.
        /// </summary>
        [JsonPropertyName("EncryptionTimeMs")]
        [XmlElement("EncryptionTimeMs")]
        public long EncryptionTimeMs { get; set; }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Lock registry (one lock per absolute log path)
    // ──────────────────────────────────────────────────────────────────────────

    internal static class LogFileSync
    {
        private static readonly ConcurrentDictionary<string, object> Locks = new();

        public static object GetLock(string absoluteLogPath)
            => Locks.GetOrAdd(Path.GetFullPath(absoluteLogPath), _ => new object());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // JSON implementation
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Thread-safe daily JSON log writer (pretty array or NDJSON append).
    /// </summary>
    public class JsonLogger : ILogger
    {
        private readonly string            _logDirectory;
        private readonly JsonLogLayout     _layout;
        private readonly LoggerOptions     _options;

        private static readonly JsonSerializerOptions PrettyOpts = new()
        {
            WriteIndented = true
        };

        private static readonly JsonSerializerOptions NdjsonOpts = new()
        {
            WriteIndented = false
        };

        /// <summary>Production constructor — default log directory, pretty array JSON.</summary>
        public JsonLogger() : this(LogDirectoryResolver.Resolve(null), JsonLogLayout.PrettyArray)
        { }

        /// <summary>Test-friendly: directory only, legacy pretty array (.json).</summary>
        public JsonLogger(string logDirectory) : this(logDirectory, JsonLogLayout.PrettyArray)
        { }

        /// <summary>Explicit layout (NDJSON writes <c>*.ndjson</c>).</summary>
        public JsonLogger(string logDirectory, JsonLogLayout layout)
            : this(logDirectory, layout, new LoggerOptions())
        { }

        public JsonLogger(string logDirectory, JsonLogLayout layout, LoggerOptions options)
        {
            _logDirectory = logDirectory;
            _layout       = layout;
            _options      = options;
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

            if (_options.DestinationMode != LogDestinationMode.CentralOnly)
            {
                Directory.CreateDirectory(_logDirectory);
                string date   = $"{DateTime.Now:yyyy-MM-dd}";
                string logFile = _layout == JsonLogLayout.Ndjson
                    ? Path.Combine(_logDirectory, $"{date}.ndjson")
                    : Path.Combine(_logDirectory, $"{date}.json");

                lock (LogFileSync.GetLock(logFile))
                {
                    if (_layout == JsonLogLayout.Ndjson)
                        AppendNdjsonLine(logFile, entry);
                    else
                        RewritePrettyArray(logFile, entry);
                }
            }

            CentralLogSender.TrySend(_options, LogFormat.Json, entry);
        }

        private static void AppendNdjsonLine(string logFile, LogEntry entry)
        {
            string line = JsonSerializer.Serialize(entry, NdjsonOpts) + Environment.NewLine;
            using var fs = new FileStream(logFile, FileMode.Append, FileAccess.Write, FileShare.Read);
            byte[] bytes = Encoding.UTF8.GetBytes(line);
            fs.Write(bytes, 0, bytes.Length);
        }

        private static void RewritePrettyArray(string logFile, LogEntry entry)
        {
            List<LogEntry> entries = new();
            if (File.Exists(logFile))
            {
                try
                {
                    string existing = File.ReadAllText(logFile);
                    entries = JsonSerializer.Deserialize<List<LogEntry>>(existing, PrettyOpts)
                              ?? new List<LogEntry>();
                }
                catch { entries = new List<LogEntry>(); }
            }

            entries.Add(entry);
            LogPathFormatter.WriteAtomic(logFile,
                JsonSerializer.Serialize(entries, PrettyOpts));
        }

        private static LogEntry BuildEntry(
            string backupJobName, string sourceFilePath, string targetFilePath,
            long fileSize, long transferTimeMs, long encryptionTimeMs) => new()
        {
            Timestamp        = DateTime.Now.ToString("o"),
            BackupJobName    = backupJobName,
            SourceFilePath   = LogPathFormatter.ToUncFormat(sourceFilePath),
            TargetFilePath   = LogPathFormatter.ToUncFormat(targetFilePath),
            FileSize         = fileSize,
            TransferTimeMs   = transferTimeMs,
            EncryptionTimeMs = encryptionTimeMs
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // XML implementation
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Thread-safe daily XML log writer.
    /// </summary>
    public class XmlLogger : ILogger
    {
        private readonly string _logDirectory;
        private readonly LoggerOptions _options;

        /// <summary>Root envelope for persisted XML entries.</summary>
        [XmlRoot("LogEntries")]
        public class LogEntryList
        {
            /// <summary>Sequential log rows.</summary>
            [XmlElement("LogEntry")]
            public List<LogEntry> Entries { get; set; } = new();
        }

        /// <summary>Production constructor — default log directory.</summary>
        public XmlLogger() : this(LogDirectoryResolver.Resolve(null))
        { }

        /// <summary>Test constructor — uses the provided directory.</summary>
        public XmlLogger(string logDirectory)
            : this(logDirectory, new LoggerOptions())
        { }

        public XmlLogger(string logDirectory, LoggerOptions options)
        {
            _logDirectory = logDirectory;
            _options      = options;
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
                SourceFilePath   = LogPathFormatter.ToUncFormat(sourceFilePath),
                TargetFilePath   = LogPathFormatter.ToUncFormat(targetFilePath),
                FileSize         = fileSize,
                TransferTimeMs   = transferTimeMs,
                EncryptionTimeMs = encryptionTimeMs
            };

            if (_options.DestinationMode != LogDestinationMode.CentralOnly)
            {
                var serializer = new XmlSerializer(typeof(LogEntryList));

                Directory.CreateDirectory(_logDirectory);
                string logFile = Path.Combine(_logDirectory, $"{DateTime.Now:yyyy-MM-dd}.xml");

                lock (LogFileSync.GetLock(logFile))
                {
                    var list = new LogEntryList();
                    if (File.Exists(logFile))
                    {
                        try
                        {
                            using var reader = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                            list = (LogEntryList?)serializer.Deserialize(reader) ?? new LogEntryList();
                        }
                        catch { list = new LogEntryList(); }
                    }

                    list.Entries.Add(entry);

                    using var ms = new MemoryStream();
                    serializer.Serialize(ms, list);
                    LogPathFormatter.WriteAtomic(logFile, ms.ToArray());
                }
            }

            CentralLogSender.TrySend(_options, LogFormat.Xml, entry);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Factory
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Fabricates default <see cref="ILogger"/> instances for EasySave.</summary>
    public static class LoggerFactory
    {
        /// <summary>Creates a logger with default AppData directory and pretty JSON array.</summary>
        public static ILogger Create(LogFormat format)
            => Create(format, null);

        /// <summary>Creates a logger using optional directory and JSON layout.</summary>
        /// <param name="format">JSON or XML.</param>
        /// <param name="options">Null for all defaults.</param>
        public static ILogger Create(LogFormat format, LoggerOptions? options)
        {
            string dir = LogDirectoryResolver.Resolve(options?.LogDirectory);
            var opts = options ?? new LoggerOptions();
            return format switch
            {
                LogFormat.Xml => new XmlLogger(dir, opts),
                _             => new JsonLogger(dir, opts.JsonLogLayout, opts)
            };
        }
    }
}
