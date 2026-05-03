using System;
using System.Collections.Generic;
using System.IO;
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
            Assert.Empty(settings.EncryptedExtensions);
            Assert.Empty(settings.BusinessSoftwareName);
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
        public void Load_CorruptFile_ReturnsDefaults()
        {
            string settingsFile = Path.Combine(_tempDir, "settings.json");
            File.WriteAllText(settingsFile, "{ invalid json [[[");

            var settings = _svc.Load();
            // Should gracefully fall back to defaults
            Assert.Equal(LogFormat.Json, settings.LogFormat);
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
