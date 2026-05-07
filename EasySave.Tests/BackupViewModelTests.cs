using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Core.Models;
using EasySave.Core.Services;
using EasySave.Core.ViewModels;
using EasyLog;
using Xunit;

namespace EasySave.Tests
{
    /// <summary>
    /// Unit tests for BackupViewModel — job creation, editing, deletion, limits.
    /// Uses temporary AppData folders to avoid polluting real config.
    /// </summary>
    public class BackupViewModelTests : IDisposable
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        private readonly string _tempDir;
        private readonly BackupViewModel _vm;

        public BackupViewModelTests()
        {
            // Isolate every test in its own temp directory
            _tempDir = Path.Combine(Path.GetTempPath(), "EasySave_Tests_" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);

            var configService    = new ConfigService(_tempDir);
            var stateService     = new StateService(_tempDir);
            var settingsService  = new SettingsService(_tempDir);
            var businessSoftware = new BusinessSoftwareService();

            _vm = new BackupViewModel(
                configService,
                stateService,
                settingsService,
                businessSoftware,
                maxJobs: 5);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
        }

        // ── CreateJob ────────────────────────────────────────────────────────

        [Fact]
        public void CreateJob_ValidArgs_ReturnsSuccess()
        {
            var (ok, err) = _vm.CreateJob("TestJob", @"C:\Source", @"C:\Target", BackupType.Full);
            Assert.True(ok);
            Assert.Empty(err);
            Assert.Single(_vm.Jobs);
        }

        [Fact]
        public void CreateJob_EmptyName_ReturnsError()
        {
            var (ok, err) = _vm.CreateJob("  ", @"C:\Source", @"C:\Target", BackupType.Full);
            Assert.False(ok);
            Assert.Equal("name_empty", err);
        }

        [Fact]
        public void CreateJob_DuplicateName_ReturnsError()
        {
            _vm.CreateJob("Dup", @"C:\A", @"C:\B", BackupType.Full);
            var (ok, err) = _vm.CreateJob("dup", @"C:\C", @"C:\D", BackupType.Full); // case-insensitive
            Assert.False(ok);
            Assert.Equal("name_exists", err);
        }

        [Fact]
        public void CreateJob_ExceedsMaxJobs_ReturnsError()
        {
            for (int i = 1; i <= 5; i++)
                _vm.CreateJob($"Job{i}", $@"C:\S{i}", $@"C:\T{i}", BackupType.Full);

            var (ok, err) = _vm.CreateJob("Job6", @"C:\S6", @"C:\T6", BackupType.Full);
            Assert.False(ok);
            Assert.Equal("max_jobs", err);
        }

        [Fact]
        public void CreateJob_Differential_StoresCorrectType()
        {
            _vm.CreateJob("DiffJob", @"C:\Source", @"C:\Target", BackupType.Differential);
            Assert.Equal(BackupType.Differential, _vm.Jobs[0].Type);
        }

        // ── EditJob ──────────────────────────────────────────────────────────

        [Fact]
        public void EditJob_ValidArgs_UpdatesJob()
        {
            _vm.CreateJob("Old", @"C:\A", @"C:\B", BackupType.Full);
            var (ok, err) = _vm.EditJob(1, "New", @"C:\C", @"C:\D", BackupType.Differential);
            Assert.True(ok);
            Assert.Empty(err);
            Assert.Equal("New", _vm.Jobs[0].Name);
            Assert.Equal(BackupType.Differential, _vm.Jobs[0].Type);
        }

        [Fact]
        public void EditJob_InvalidIndex_ReturnsError()
        {
            var (ok, err) = _vm.EditJob(99, "X", @"C:\X", @"C:\Y", BackupType.Full);
            Assert.False(ok);
            Assert.Equal("invalid_index", err);
        }

        [Fact]
        public void EditJob_DuplicateName_ReturnsError()
        {
            _vm.CreateJob("Alpha", @"C:\A", @"C:\B", BackupType.Full);
            _vm.CreateJob("Beta",  @"C:\C", @"C:\D", BackupType.Full);
            var (ok, err) = _vm.EditJob(2, "Alpha", @"C:\C", @"C:\D", BackupType.Full);
            Assert.False(ok);
            Assert.Equal("name_exists", err);
        }

        // ── DeleteJob ────────────────────────────────────────────────────────

        [Fact]
        public void DeleteJob_ValidIndex_RemovesJob()
        {
            _vm.CreateJob("ToDelete", @"C:\A", @"C:\B", BackupType.Full);
            Assert.Single(_vm.Jobs);
            var (ok, _) = _vm.DeleteJob(1);
            Assert.True(ok);
            Assert.Empty(_vm.Jobs);
        }

        [Fact]
        public void DeleteJob_InvalidIndex_ReturnsError()
        {
            var (ok, err) = _vm.DeleteJob(0);
            Assert.False(ok);
            Assert.Equal("invalid_index", err);
        }

        // ── Unlimited jobs (v2.0) ────────────────────────────────────────────

        [Fact]
        public void CreateJob_UnlimitedMode_AllowsMoreThan5()
        {
            var configService    = new ConfigService(_tempDir);
            var stateService     = new StateService(_tempDir);
            var settingsService  = new SettingsService(_tempDir);
            var businessSoftware = new BusinessSoftwareService();

            var unlimitedVm = new BackupViewModel(
                configService, stateService, settingsService, businessSoftware, maxJobs: 0);

            for (int i = 1; i <= 10; i++)
            {
                var (ok, _) = unlimitedVm.CreateJob($"Job{i}", $@"C:\S{i}", $@"C:\T{i}", BackupType.Full);
                Assert.True(ok, $"Failed at job #{i}");
            }
            Assert.Equal(10, unlimitedVm.Jobs.Count);
        }

        [Fact]
        public void ExecuteAllJobsParallel_CopiesConfiguredJobs()
        {
            string source1 = Path.Combine(_tempDir, "Source1");
            string source2 = Path.Combine(_tempDir, "Source2");
            string target1 = Path.Combine(_tempDir, "Target1");
            string target2 = Path.Combine(_tempDir, "Target2");
            Directory.CreateDirectory(source1);
            Directory.CreateDirectory(source2);
            File.WriteAllText(Path.Combine(source1, "a.txt"), "a");
            File.WriteAllText(Path.Combine(source2, "b.txt"), "b");

            _vm.CreateJob("JobA", source1, target1, BackupType.Full);
            _vm.CreateJob("JobB", source2, target2, BackupType.Full);

            var (ok, errors) = _vm.ExecuteAllJobsParallel();

            Assert.True(ok, string.Join(", ", errors));
            Assert.True(File.Exists(Path.Combine(target1, "a.txt")));
            Assert.True(File.Exists(Path.Combine(target2, "b.txt")));
        }
    }
}
