using System;
using System.Collections.Generic;
using System.Threading;
using EasySave.Core.Models;
using EasySave.Core.Services;
using EasyLog;

namespace EasySave.Core.ViewModels
{
    /// <summary>
    /// Orchestrates backup job management and execution (preflight, single-flight gate, cancellation).
    /// </summary>
    public class BackupViewModel
    {
        private static readonly SemaphoreSlim ExecutionGate = new(1, 1);

        private readonly int _maxJobs;

        private readonly ConfigService           _configService;
        private readonly StateService            _stateService;
        private readonly SettingsService         _settingsService;
        private readonly BusinessSoftwareService _businessSoftware;
        private readonly List<BackupState>       _states;
        private ILogger _logger = null!;

        public List<BackupJob> Jobs { get; private set; }

        public AppSettings Settings { get; private set; }

        public BackupViewModel(
            ConfigService           configService,
            StateService            stateService,
            SettingsService         settingsService,
            BusinessSoftwareService businessSoftware,
            int                     maxJobs = 5)
        {
            _configService    = configService;
            _stateService     = stateService;
            _settingsService  = settingsService;
            _businessSoftware = businessSoftware;
            _maxJobs          = maxJobs;

            Jobs     = _configService.LoadJobs();
            Settings = _settingsService.Load();
            RebuildLogger();

            _states = _stateService.LoadState();
            EnsureStatesMatch();
        }

        private void RebuildLogger()
        {
            _logger = LoggerFactory.Create(Settings.LogFormat, new LoggerOptions
            {
                LogDirectory   = Settings.LogDirectory,
                JsonLogLayout  = Settings.JsonLogLayout
            });
        }

        /// <summary>Updates and persists global settings and rebuilds the log writer.</summary>
        public void UpdateSettings(AppSettings settings)
        {
            Settings = settings;
            _settingsService.Save(settings);
            RebuildLogger();
        }

        public (bool Success, string Error) CreateJob(
            string name, string source, string target, BackupType type)
        {
            if (_maxJobs > 0 && Jobs.Count >= _maxJobs)
                return (false, "max_jobs");

            if (string.IsNullOrWhiteSpace(name))
                return (false, "name_empty");

            if (Jobs.Exists(j => j.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return (false, "name_exists");

            var job = new BackupJob
            {
                Name            = name.Trim(),
                SourceDirectory = source.Trim(),
                TargetDirectory = target.Trim(),
                Type            = type
            };

            Jobs.Add(job);
            _states.Add(new BackupState { JobName = job.Name });
            Persist();
            return (true, string.Empty);
        }

        public (bool Success, string Error) EditJob(
            int oneBasedIndex, string name, string source, string target, BackupType type)
        {
            int idx = oneBasedIndex - 1;
            if (idx < 0 || idx >= Jobs.Count)
                return (false, "invalid_index");

            for (int i = 0; i < Jobs.Count; i++)
            {
                if (i != idx && Jobs[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return (false, "name_exists");
            }

            Jobs[idx].Name             = name.Trim();
            Jobs[idx].SourceDirectory  = source.Trim();
            Jobs[idx].TargetDirectory  = target.Trim();
            Jobs[idx].Type             = type;
            _states[idx].JobName       = name.Trim();

            Persist();
            return (true, string.Empty);
        }

        public (bool Success, string Error) DeleteJob(int oneBasedIndex)
        {
            int idx = oneBasedIndex - 1;
            if (idx < 0 || idx >= Jobs.Count)
                return (false, "invalid_index");

            Jobs.RemoveAt(idx);
            _states.RemoveAt(idx);
            Persist();
            return (true, string.Empty);
        }

        /// <summary>Runs one job (exclusive gate, pre-flight checks).</summary>
        public (bool Success, string Error) ExecuteJob(
            int oneBasedIndex,
            CancellationToken cancellationToken = default)
        {
            int idx = oneBasedIndex - 1;
            if (idx < 0 || idx >= Jobs.Count)
                return (false, "invalid_index");

            BackupPreflight.Result pf = BackupPreflight.Validate(Jobs[idx]);
            if (!pf.Ok)
                return (false, pf.ErrorKey);

            if (!ExecutionGate.Wait(0))
                return (false, "execution_busy");

            try
            {
                BackupService svc = CreateBackupService();
                try
                {
                    bool ok = svc.Execute(Jobs[idx], idx, Settings, cancellationToken);
                    return (ok, ok ? string.Empty : "errors");
                }
                catch (OperationCanceledException)
                {
                    return (false, "cancelled");
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }
            }
            finally
            {
                ExecutionGate.Release();
            }
        }

        /// <summary>Runs all jobs sequentially under a single acquisition of the execution gate.</summary>
        public (bool AllSuccess, List<string> Errors) ExecuteAllJobs(
            CancellationToken cancellationToken = default)
        {
            if (Jobs.Count == 0)
                return (true, new List<string>());

            for (int i = 0; i < Jobs.Count; i++)
            {
                BackupPreflight.Result pf = BackupPreflight.Validate(Jobs[i]);
                if (!pf.Ok)
                    return (false, new List<string> { $"{Jobs[i].Name}: {pf.ErrorKey}" });
            }

            if (!ExecutionGate.Wait(0))
                return (false, new List<string> { "execution_busy" });

            bool      allOk = true;
            var       errors = new List<string>();
            try
            {
                BackupService svc = CreateBackupService();
                for (int i = 0; i < Jobs.Count; i++)
                {
                    try
                    {
                        bool ok = svc.Execute(Jobs[i], i, Settings, cancellationToken);
                        if (!ok)
                        {
                            allOk = false;
                            errors.Add($"{Jobs[i].Name}: completed with errors");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        allOk = false;
                        errors.Add($"{Jobs[i].Name}: cancelled");
                        break;
                    }
                    catch (Exception ex)
                    {
                        allOk = false;
                        errors.Add($"{Jobs[i].Name}: {ex.Message}");
                    }
                }
            }
            finally
            {
                ExecutionGate.Release();
            }

            return (allOk, errors);
        }

        /// <summary>Runs selected jobs (CLI / range) with one gate acquisition.</summary>
        public (bool AllSuccess, List<string> Errors) ExecuteJobs(
            IEnumerable<int> oneBasedIndices,
            CancellationToken cancellationToken = default)
        {
            bool allOk = true;
            var  errors = new List<string>();
            var  indices = new List<int>();

            foreach (int oneIdx in oneBasedIndices)
            {
                int idx = oneIdx - 1;
                if (idx < 0 || idx >= Jobs.Count)
                {
                    errors.Add($"Index {oneIdx} out of range");
                    allOk = false;
                    continue;
                }

                indices.Add(idx);
            }

            if (indices.Count == 0)
                return (allOk, errors);

            foreach (int idx in indices)
            {
                BackupPreflight.Result pf = BackupPreflight.Validate(Jobs[idx]);
                if (!pf.Ok)
                {
                    allOk = false;
                    errors.Add($"{Jobs[idx].Name}: {pf.ErrorKey}");
                    return (allOk, errors);
                }
            }

            if (!ExecutionGate.Wait(0))
                return (false, new List<string> { "execution_busy" });

            try
            {
                BackupService svc = CreateBackupService();
                foreach (int idx in indices)
                {
                    try
                    {
                        bool ok = svc.Execute(Jobs[idx], idx, Settings, cancellationToken);
                        if (!ok)
                        {
                            allOk = false;
                            errors.Add($"{Jobs[idx].Name}: completed with errors");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        allOk = false;
                        errors.Add($"{Jobs[idx].Name}: cancelled");
                        break;
                    }
                    catch (Exception ex)
                    {
                        allOk = false;
                        errors.Add($"{Jobs[idx].Name}: {ex.Message}");
                    }
                }
            }
            finally
            {
                ExecutionGate.Release();
            }

            return (allOk, errors);
        }

        private BackupService CreateBackupService() =>
            new BackupService(_stateService, _states, _logger, _businessSoftware);

        private void Persist()
        {
            _configService.SaveJobs(Jobs);
            _stateService.UpdateState(_states);
        }

        private void EnsureStatesMatch()
        {
            while (_states.Count < Jobs.Count)
                _states.Add(new BackupState { JobName = Jobs[_states.Count].Name });

            while (_states.Count > Jobs.Count)
                _states.RemoveAt(_states.Count - 1);
        }
    }
}
