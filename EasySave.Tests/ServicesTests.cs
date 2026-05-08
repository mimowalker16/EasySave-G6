using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using EasySave.Core.Models;
using EasySave.Core.Services;
using EasyLog;
using Xunit;

namespace EasySave.Tests
{
    /// <summary>
    /// Unit tests for SettingsService — save and load round-trip.
    /// </summary>
    public class SettingsServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly SettingsService _svc;

        public SettingsServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "EasySave_Cfg_" + Guid.NewGuid());
            _svc     = new SettingsService(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
        }

        [Fact]
        public void Load_WhenNoFile_ReturnsDefaults()
        {
            var settings = _svc.Load();
            Assert.Equal(LogFormat.Json, settings.LogFormat);
            Assert.Equal(LogDestinationMode.LocalOnly, settings.LogDestinationMode);
            Assert.Empty(settings.LogDirectory);
            Assert.Empty(settings.PriorityExtensions);
            Assert.Equal(0, settings.LargeFileThresholdKb);
            Assert.Empty(settings.EncryptedExtensions);
            Assert.Empty(settings.BusinessSoftwareName);
            Assert.Equal("en", settings.UiLanguage);
        }

        [Fact]
        public void Save_ThenLoad_RoundTripsLogFormat()
        {
            var original = new AppSettings { LogFormat = LogFormat.Xml };
            _svc.Save(original);

            var loaded = _svc.Load();
            Assert.Equal(LogFormat.Xml, loaded.LogFormat);
        }

        [Fact]
        public void Save_ThenLoad_RoundTripsBusinessSoftwareName()
        {
            var original = new AppSettings { BusinessSoftwareName = "myERP" };
            _svc.Save(original);

            var loaded = _svc.Load();
            Assert.Equal("myERP", loaded.BusinessSoftwareName);
        }

        [Fact]
        public void Save_ThenLoad_RoundTripsEncryptedExtensions()
        {
            var original = new AppSettings
            {
                EncryptedExtensions = new List<string> { ".txt", ".docx", ".pdf" }
            };
            _svc.Save(original);

            var loaded = _svc.Load();
            Assert.Equal(3, loaded.EncryptedExtensions.Count);
            Assert.Contains(".txt",  loaded.EncryptedExtensions);
            Assert.Contains(".docx", loaded.EncryptedExtensions);
            Assert.Contains(".pdf",  loaded.EncryptedExtensions);
        }

        [Fact]
        public void Save_ThenLoad_RoundTripsV3Settings()
        {
            var original = new AppSettings
            {
                LogDirectory = @"%APPDATA%\EasySave\Logs",
                LogDestinationMode = LogDestinationMode.LocalAndCentral,
                CentralLogEndpoint = "http://localhost:8080/api/logs",
                CentralClientId = "POSTE-01",
                PriorityExtensions = new List<string> { ".zip", ".bak" },
                LargeFileThresholdKb = 512,
                UiLanguage = "fr"
            };

            _svc.Save(original);

            var loaded = _svc.Load();
            Assert.Equal(original.LogDirectory, loaded.LogDirectory);
            Assert.Equal(LogDestinationMode.LocalAndCentral, loaded.LogDestinationMode);
            Assert.Equal(original.CentralLogEndpoint, loaded.CentralLogEndpoint);
            Assert.Equal(original.CentralClientId, loaded.CentralClientId);
            Assert.Contains(".zip", loaded.PriorityExtensions);
            Assert.Equal(512, loaded.LargeFileThresholdKb);
            Assert.Equal("fr", loaded.UiLanguage);
        }

        [Fact]
        public void Load_CorruptFile_ReturnsDefaults()
        {
            string settingsFile = Path.Combine(_tempDir, "settings.json");
            File.WriteAllText(settingsFile, "{ invalid json [[[");

            var settings = _svc.Load();
            // Should gracefully fall back to defaults
            Assert.Equal(LogFormat.Json, settings.LogFormat);
        }

        [Fact]
        public void Save_ThenLoad_RoundTripsLogDirectoryAndNdjsonLayout()
        {
            var original = new AppSettings
            {
                LogDirectory  = @"D:\CompanyLogs\EasySave",
                JsonLogLayout = JsonLogLayout.Ndjson,
                LogFormat     = LogFormat.Json
            };
            _svc.Save(original);

            var loaded = _svc.Load();
            Assert.Equal(@"D:\CompanyLogs\EasySave", loaded.LogDirectory);
            Assert.Equal(JsonLogLayout.Ndjson, loaded.JsonLogLayout);
        }

        [Fact]
        public async Task ConcurrentSaveAndLoad_SettingsFileRemainsReadable()
        {
            var writers = new List<Task>();
            for (int i = 0; i < 20; i++)
            {
                int copy = i;
                writers.Add(Task.Run(() =>
                    _svc.Save(new AppSettings
                    {
                        BusinessSoftwareName = $"ERP-{copy}",
                        LargeFileThresholdKb = copy,
                        PriorityExtensions = new List<string> { ".prio" }
                    })));
            }

            await Task.WhenAll(writers);

            AppSettings loaded = _svc.Load();
            Assert.StartsWith("ERP-", loaded.BusinessSoftwareName);
            Assert.Contains(".prio", loaded.PriorityExtensions);
        }
    }

    /// <summary>
    /// Unit tests for backup-job configuration persistence.
    /// </summary>
    public class ConfigServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly ConfigService _svc;

        public ConfigServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "EasySave_Jobs_" + Guid.NewGuid());
            _svc = new ConfigService(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        [Fact]
        public async Task ConcurrentSaveAndLoad_JobsFileRemainsReadable()
        {
            var writers = new List<Task>();
            for (int i = 0; i < 20; i++)
            {
                int copy = i;
                writers.Add(Task.Run(() =>
                    _svc.SaveJobs(new List<BackupJob>
                    {
                        new()
                        {
                            Name = $"Job-{copy}",
                            SourceDirectory = $@"C:\Source{copy}",
                            TargetDirectory = $@"C:\Target{copy}",
                            Type = copy % 2 == 0 ? BackupType.Full : BackupType.Differential
                        }
                    })));
            }

            await Task.WhenAll(writers);

            List<BackupJob> loaded = _svc.LoadJobs();
            Assert.Single(loaded);
            Assert.StartsWith("Job-", loaded[0].Name);
        }
    }

    /// <summary>
    /// Unit tests for BusinessSoftwareService.
    /// </summary>
    public class BusinessSoftwareServiceTests
    {
        private readonly BusinessSoftwareService _svc = new();

        [Fact]
        public void IsRunning_EmptyName_ReturnsFalse()
        {
            Assert.False(_svc.IsRunning(""));
        }

        [Fact]
        public void IsRunning_WhitespaceName_ReturnsFalse()
        {
            Assert.False(_svc.IsRunning("   "));
        }

        [Fact]
        public void IsRunning_KnownRunningProcess_ReturnsTrue()
        {
            // "System" is guaranteed to be running on any Windows machine
            Assert.True(_svc.IsRunning("System"));
        }

        [Fact]
        public void IsRunning_NonexistentProcess_ReturnsFalse()
        {
            Assert.False(_svc.IsRunning("___totally_fake_process_xyz_123___"));
        }
    }
}
