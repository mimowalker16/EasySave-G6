using System;
using System.Collections.Generic;
using System.IO;
using EasyLog;
using Xunit;

namespace EasySave.Tests
{
    /// <summary>
    /// Unit tests for JsonLogger and XmlLogger — verifies files are created
    /// and contain the expected entries.
    /// </summary>
    public class LoggerTests : IDisposable
    {
        private readonly string _tempDir;

        public LoggerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "EasySave_Log_" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
        }

        // ── JsonLogger ───────────────────────────────────────────────────────

        [Fact]
        public void JsonLogger_LogTransfer_CreatesLogFile()
        {
            var logger = new JsonLogger(_tempDir);
            logger.LogTransfer("Job1", @"C:\src\file.txt", @"C:\dst\file.txt", 1024, 15, 0);

            string[] files = Directory.GetFiles(_tempDir, "*.json");
            Assert.Single(files);
        }

        [Fact]
        public void JsonLogger_LogTransfer_FileContainsEntry()
        {
            var logger = new JsonLogger(_tempDir);
            logger.LogTransfer("MyJob", @"C:\src\test.txt", @"C:\dst\test.txt", 512, 8, 0);

            string json = File.ReadAllText(Directory.GetFiles(_tempDir, "*.json")[0]);
            Assert.Contains("MyJob", json);
            Assert.Contains("test.txt", json);
        }

        [Fact]
        public void JsonLogger_MultipleEntries_AppendedToSameFile()
        {
            var logger = new JsonLogger(_tempDir);
            logger.LogTransfer("Job", @"C:\a.txt", @"C:\b\a.txt", 100, 5, 0);
            logger.LogTransfer("Job", @"C:\b.txt", @"C:\b\b.txt", 200, 6, 0);

            string[] files = Directory.GetFiles(_tempDir, "*.json");
            Assert.Single(files); // Still only one daily file

            string json = File.ReadAllText(files[0]);
            // JSON array must contain 2 entries
            int count = CountOccurrences(json, "\"BackupJobName\"");
            Assert.Equal(2, count);
        }

        // ── XmlLogger ────────────────────────────────────────────────────────

        [Fact]
        public void XmlLogger_LogTransfer_CreatesLogFile()
        {
            var logger = new XmlLogger(_tempDir);
            logger.LogTransfer("Job1", @"C:\src\file.xml", @"C:\dst\file.xml", 2048, 20, 0);

            string[] files = Directory.GetFiles(_tempDir, "*.xml");
            Assert.Single(files);
        }

        [Fact]
        public void XmlLogger_LogTransfer_FileContainsEntry()
        {
            var logger = new XmlLogger(_tempDir);
            logger.LogTransfer("XmlJob", @"C:\src\data.xml", @"C:\dst\data.xml", 800, 10, 5);

            string xml = File.ReadAllText(Directory.GetFiles(_tempDir, "*.xml")[0]);
            Assert.Contains("XmlJob", xml);
            Assert.Contains("data.xml", xml);
        }

        [Fact]
        public void XmlLogger_EncryptionTimeMs_PresentInOutput()
        {
            var logger = new XmlLogger(_tempDir);
            logger.LogTransfer("EncJob", @"C:\src\enc.xml", @"C:\dst\enc.xml", 300, 7, 42);

            string xml = File.ReadAllText(Directory.GetFiles(_tempDir, "*.xml")[0]);
            Assert.Contains("42", xml);
        }

        // ── LoggerFactory ────────────────────────────────────────────────────

        [Fact]
        public void LoggerFactory_Json_ReturnsJsonLogger()
        {
            ILogger logger = LoggerFactory.Create(LogFormat.Json);
            Assert.IsType<JsonLogger>(logger);
        }

        [Fact]
        public void LoggerFactory_Xml_ReturnsXmlLogger()
        {
            ILogger logger = LoggerFactory.Create(LogFormat.Xml);
            Assert.IsType<XmlLogger>(logger);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0;
            int idx   = 0;
            while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += needle.Length;
            }
            return count;
        }
    }
}
