using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Core.Models;
using EasySave.Core.Services;
using EasyLog;
using Xunit;

namespace EasySave.Tests
{
    /// <summary>
    /// Unit tests for BackupService — file copy engine, business software detection.
    /// </summary>
    public class BackupServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _sourceDir;
        private readonly string _targetDir;

        public BackupServiceTests()
        {
            _tempDir   = Path.Combine(Path.GetTempPath(), "EasySave_BS_" + Guid.NewGuid());
            _sourceDir = Path.Combine(_tempDir, "Source");
            _targetDir = Path.Combine(_tempDir, "Target");
            Directory.CreateDirectory(_sourceDir);
            Directory.CreateDirectory(_targetDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private BackupService BuildService(BusinessSoftwareService? businessSoftware = null)
        {
            var stateService = new StateService(_tempDir);
            var allStates = new System.Collections.Generic.List<BackupState>
                                   { new BackupState { JobName = "TestJob" } };

            string logDir = Path.Combine(_tempDir, "Logs");
            var    logger           = new JsonLogger(logDir);
            businessSoftware ??= new BusinessSoftwareService();

            return new BackupService(stateService, allStates, logger, businessSoftware);
        }

        private BackupJob MakeJob(BackupType type = BackupType.Full)
            => new BackupJob
            {
                Name            = "TestJob",
                SourceDirectory = _sourceDir,
                TargetDirectory = _targetDir,
                Type            = type
            };

        private AppSettings MakeSettings(string businessSoftware = "")
            => new AppSettings
            {
                LogFormat            = LogFormat.Json,
                BusinessSoftwareName = businessSoftware,
                EncryptedExtensions  = new System.Collections.Generic.List<string>()
            };

        // ── Full backup ───────────────────────────────────────────────────────

        [Fact]
        public void Execute_FullBackup_CopiesAllFiles()
        {
            // Arrange — create source files
            File.WriteAllText(Path.Combine(_sourceDir, "a.txt"), "hello");
            File.WriteAllText(Path.Combine(_sourceDir, "b.txt"), "world");

            var svc = BuildService();
            var job = MakeJob(BackupType.Full);

            // Act
            bool ok = svc.Execute(job, 0, MakeSettings());

            // Assert
            Assert.True(ok);
            Assert.True(File.Exists(Path.Combine(_targetDir, "a.txt")));
            Assert.True(File.Exists(Path.Combine(_targetDir, "b.txt")));
        }

        [Fact]
        public void Execute_FullBackup_EmptySource_Succeeds()
        {
            // Empty source directory → no files to copy, should succeed
            var svc = BuildService();
            bool ok = svc.Execute(MakeJob(), 0, MakeSettings());
            Assert.True(ok);
        }

        [Fact]
        public void Execute_MissingSourceDirectory_ThrowsDirectoryNotFoundException()
        {
            var job = new BackupJob
            {
                Name            = "Bad",
                SourceDirectory = @"C:\___nonexistent_path___\source",
                TargetDirectory = _targetDir,
                Type            = BackupType.Full
            };
            var svc = BuildService();
            Assert.Throws<DirectoryNotFoundException>(() => svc.Execute(job, 0, MakeSettings()));
        }

        // ── Differential backup ───────────────────────────────────────────────

        [Fact]
        public void Execute_DifferentialBackup_OnlyCopiesChangedFiles()
        {
            // Arrange — create a file in source and also put an identical copy in target
            string sourceFile = Path.Combine(_sourceDir, "same.txt");
            string targetFile = Path.Combine(_targetDir, "same.txt");
            File.WriteAllText(sourceFile, "identical content");
            File.WriteAllText(targetFile, "identical content");

            // Make the target timestamp exactly match the source
            File.SetLastWriteTime(targetFile, File.GetLastWriteTime(sourceFile));

            // Add a new file that target doesn't have
            File.WriteAllText(Path.Combine(_sourceDir, "new.txt"), "new content");

            var svc = BuildService();
            bool ok = svc.Execute(MakeJob(BackupType.Differential), 0, MakeSettings());

            Assert.True(ok);
            // "new.txt" must have been copied
            Assert.True(File.Exists(Path.Combine(_targetDir, "new.txt")));
        }

        // ── Business software blocking ────────────────────────────────────────

        [Fact]
        public void Execute_BusinessSoftwareRunning_PausesUntilCancelled()
        {
            File.WriteAllText(Path.Combine(_sourceDir, "file.txt"), "data");
            var settings = MakeSettings(businessSoftware: "System");
            var svc = BuildService();

            using var cts = new CancellationTokenSource(250);
            Assert.Throws<OperationCanceledException>(() => svc.Execute(MakeJob(), 0, settings, cts.Token));
        }

        [Fact]
        public void Execute_NoBusinessSoftware_DoesNotBlock()
        {
            File.WriteAllText(Path.Combine(_sourceDir, "file.txt"), "data");
            var settings = MakeSettings(businessSoftware: ""); // empty = disabled
            var svc = BuildService();
            bool ok = svc.Execute(MakeJob(), 0, settings);
            Assert.True(ok);
        }

        [Fact]
        public async Task Execute_BusinessSoftwareStops_ResumesAutomatically()
        {
            File.WriteAllText(Path.Combine(_sourceDir, "file.txt"), "data");
            var businessSoftware = new ToggleBusinessSoftwareService { Running = true };
            var svc = BuildService(businessSoftware);
            var settings = MakeSettings(businessSoftware: "DemoBusinessApp");
            var observedStates = new List<BackupState>();
            using var pausedSeen = new ManualResetEventSlim(false);

            Task<bool> run = Task.Run(() => svc.Execute(
                MakeJob(),
                0,
                settings,
                stateChanged: (_, state) =>
                {
                    lock (observedStates)
                    {
                        observedStates.Add(state);
                    }

                    if (state.State == BackupStateType.Paused)
                        pausedSeen.Set();
                }));

            Assert.True(pausedSeen.Wait(TimeSpan.FromSeconds(2)));
            businessSoftware.Running = false;

            bool ok = await run.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(ok);
            Assert.True(File.Exists(Path.Combine(_targetDir, "file.txt")));

            lock (observedStates)
            {
                Assert.Contains(observedStates, s =>
                    s.State == BackupStateType.Paused
                    && s.PauseReason.Contains("Business software", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(observedStates, s =>
                    s.State == BackupStateType.Active
                    && string.IsNullOrWhiteSpace(s.PauseReason));
                Assert.Contains(observedStates, s => s.State == BackupStateType.End);
            }
        }

        private sealed class ToggleBusinessSoftwareService : BusinessSoftwareService
        {
            public volatile bool Running;

            public override bool IsRunning(string processName) => Running;
        }

    }
}
