using System;
using System.IO;
using EasySave.Core.Models;
using EasySave.Core.Services;
using Xunit;

namespace EasySave.Tests
{
    public class PreflightTests : IDisposable
    {
        private readonly string _tempDir;

        public PreflightTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "EasySave_pf_" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); }
            catch { /* ignore */ }
        }

        [Fact]
        public void Preflight_ValidDirectories_Succeeds()
        {
            string src = Path.Combine(_tempDir, "Src");
            string tgt = Path.Combine(_tempDir, "Tgt");
            Directory.CreateDirectory(src);

            var job = new BackupJob
            {
                Name            = "J",
                SourceDirectory = src,
                TargetDirectory = tgt,
                Type            = BackupType.Full
            };

            BackupPreflight.Result r = BackupPreflight.Validate(job);
            Assert.True(r.Ok);
        }

        [Fact]
        public void Preflight_MissingSource_Fails()
        {
            var job = new BackupJob
            {
                Name            = "J",
                SourceDirectory = Path.Combine(_tempDir, "NopeSrc"),
                TargetDirectory = Path.Combine(_tempDir, "Tgt"),
                Type            = BackupType.Full
            };

            BackupPreflight.Result r = BackupPreflight.Validate(job);
            Assert.False(r.Ok);
            Assert.Equal("preflight_source_missing", r.ErrorKey);
        }

        [Fact]
        public void Preflight_EmptyTarget_Fails()
        {
            string src = Path.Combine(_tempDir, "Src2");
            Directory.CreateDirectory(src);

            var job = new BackupJob
            {
                Name            = "J",
                SourceDirectory = src,
                TargetDirectory = "   ",
                Type            = BackupType.Full
            };

            BackupPreflight.Result r = BackupPreflight.Validate(job);
            Assert.False(r.Ok);
            Assert.Equal("preflight_target_empty", r.ErrorKey);
        }
    }
}
