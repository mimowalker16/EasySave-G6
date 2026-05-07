using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Core.Models;
using EasySave.Core.Services;
using EasyLog;

namespace EasySave.Core.ViewModels
{
    public class BackupViewModel
    {
        private readonly int _maxJobs;
        private readonly ConfigService _configService;
        private readonly StateService _stateService;
        private readonly SettingsService _settingsService;
        private readonly BusinessSoftwareService _businessSoftware;
        private readonly List<BackupState> _states;
        private readonly ConcurrentDictionary<int, JobRunControl> _runControls = new();

        private ILogger _logger = null!;

        private sealed class JobRunControl
        {
            public CancellationTokenSource Cts { get; } = new();
            public volatile bool PauseRequested;
        }

        public List<BackupJob> Jobs { get; private set; }
        public AppSettings Settings { get; private set; }

        public event Action<int, BackupState>? StateChanged;

        public BackupViewModel(
            ConfigService configService,
            StateService stateService,
            SettingsService settingsService,
            BusinessSoftwareService businessSoftware,
            int maxJobs = 5)
        {
            _configService = configService;
            _stateService = stateService;
            _settingsService = settingsService;
            _businessSoftware = businessSoftware;
            _maxJobs = maxJobs;

            Jobs = _configService.LoadJobs();
            Settings = _settingsService.Load();
            RebuildLogger();

            _states = _stateService.LoadState();
            EnsureStatesMatch();
        }

        private void RebuildLogger()
        {
            _logger = LoggerFactory.Create(Settings.LogFormat, new LoggerOptions
            {
                LogDirectory = Settings.LogDirectory,
                DestinationMode = Settings.LogDestinationMode,
                CentralLogEndpoint = Settings.CentralLogEndpoint,
                CentralClientId = Settings.CentralClientId
            });
        }

        public void UpdateSettings(AppSettings settings)
        {
            Settings = settings;
            _settingsService.Save(settings);
            RebuildLogger();
        }

        public (bool Success, string Error) CreateJob(
            string name,
            string source,
            string target,
            BackupType type)
        {
            if (_maxJobs > 0 && Jobs.Count >= _maxJobs)
                return (false, "max_jobs");

            if (string.IsNullOrWhiteSpace(name))
                return (false, "name_empty");

            if (Jobs.Exists(j => j.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return (false, "name_exists");

            var job = new BackupJob
            {
                Name = name.Trim(),
                SourceDirectory = source.Trim(),
                TargetDirectory = target.Trim(),
                Type = type
            };

            Jobs.Add(job);
            _states.Add(new BackupState { JobName = job.Name });
            Persist();
            return (true, string.Empty);
        }

        public (bool Success, string Error) EditJob(
            int oneBasedIndex,
            string name,
            string source,
            string target,
            BackupType type)
        {
            int idx = oneBasedIndex - 1;
            if (idx < 0 || idx >= Jobs.Count)
                return (false, "invalid_index");

            if (string.IsNullOrWhiteSpace(name))
                return (false, "name_empty");

            for (int i = 0; i < Jobs.Count; i++)
            {
                if (i != idx && Jobs[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return (false, "name_exists");
            }

            Jobs[idx].Name = name.Trim();
            Jobs[idx].SourceDirectory = source.Trim();
            Jobs[idx].TargetDirectory = target.Trim();
            Jobs[idx].Type = type;
            _states[idx].JobName = name.Trim();

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

        public (bool Success, string Error) ExecuteJob(
            int oneBasedIndex,
            CancellationToken cancellationToken = default)
        {
            int idx = oneBasedIndex - 1;
            if (idx < 0 || idx >= Jobs.Count)
                return (false, "invalid_index");

            return ExecuteJobByIndex(idx, cancellationToken);
        }

        public (bool AllSuccess, List<string> Errors) ExecuteAllJobs(
            CancellationToken cancellationToken = default)
        {
            return ExecuteJobs(Jobs.Select((_, i) => i + 1), cancellationToken);
        }

        public (bool AllSuccess, List<string> Errors) ExecuteJobs(
            IEnumerable<int> oneBasedIndices,
            CancellationToken cancellationToken = default)
        {
            bool allOk = true;
            var errors = new List<string>();

            foreach (int oneIdx in oneBasedIndices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (ok, error) = ExecuteJob(oneIdx, cancellationToken);
                if (!ok)
                {
                    allOk = false;
                    string label = oneIdx >= 1 && oneIdx <= Jobs.Count
                        ? Jobs[oneIdx - 1].Name
                        : $"Index {oneIdx}";
                    errors.Add($"{label}: {error}");
                }
            }

            return (allOk, errors);
        }

        public (bool AllSuccess, List<string> Errors) ExecuteAllJobsParallel(
            CancellationToken cancellationToken = default)
        {
            return ExecuteJobsParallel(Jobs.Select((_, i) => i + 1), cancellationToken);
        }

        public (bool AllSuccess, List<string> Errors) ExecuteJobsParallel(
            IEnumerable<int> oneBasedIndices,
            CancellationToken cancellationToken = default)
        {
            var errors = new ConcurrentBag<string>();
            var indices = new List<int>();

            foreach (int oneIdx in oneBasedIndices)
            {
                int idx = oneIdx - 1;
                if (idx < 0 || idx >= Jobs.Count)
                    errors.Add($"Index {oneIdx}: invalid_index");
                else
                    indices.Add(idx);
            }

            if (indices.Count == 0)
                return (!errors.Any(), errors.ToList());

            Task[] tasks = indices.Select(idx => Task.Run(() =>
            {
                var (ok, error) = ExecuteJobByIndex(idx, cancellationToken);
                if (!ok)
                    errors.Add($"{Jobs[idx].Name}: {error}");
            }, cancellationToken)).ToArray();

            try
            {
                Task.WaitAll(tasks);
            }
            catch (AggregateException ex)
            {
                foreach (Exception inner in ex.Flatten().InnerExceptions)
                {
                    if (inner is OperationCanceledException)
                        errors.Add("cancelled");
                    else
                        errors.Add(inner.Message);
                }
            }

            return (!errors.Any(), errors.ToList());
        }

        public bool PauseJob(int oneBasedIndex)
        {
            int idx = oneBasedIndex - 1;
            if (_runControls.TryGetValue(idx, out JobRunControl? control))
            {
                control.PauseRequested = true;
                return true;
            }

            return false;
        }

        public bool ResumeJob(int oneBasedIndex)
        {
            int idx = oneBasedIndex - 1;
            if (_runControls.TryGetValue(idx, out JobRunControl? control))
            {
                control.PauseRequested = false;
                return true;
            }

            return false;
        }

        public bool StopJob(int oneBasedIndex)
        {
            int idx = oneBasedIndex - 1;
            if (_runControls.TryGetValue(idx, out JobRunControl? control))
            {
                control.Cts.Cancel();
                return true;
            }

            return false;
        }

        public void PauseAllJobs()
        {
            foreach (JobRunControl control in _runControls.Values)
                control.PauseRequested = true;
        }

        public void ResumeAllJobs()
        {
            foreach (JobRunControl control in _runControls.Values)
                control.PauseRequested = false;
        }

        public void StopAllJobs()
        {
            foreach (JobRunControl control in _runControls.Values)
                control.Cts.Cancel();
        }

        private (bool Success, string Error) ExecuteJobByIndex(
            int idx,
            CancellationToken cancellationToken)
        {
            JobRunControl control = new();
            if (!_runControls.TryAdd(idx, control))
                return (false, "execution_busy");

            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    control.Cts.Token);

                BackupService service = CreateBackupService();
                bool ok = service.Execute(
                    Jobs[idx],
                    idx,
                    Settings,
                    linked.Token,
                    () => control.PauseRequested,
                    (stateIdx, state) => StateChanged?.Invoke(stateIdx, state));

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
            finally
            {
                _runControls.TryRemove(idx, out _);
            }
        }

        private BackupService CreateBackupService()
            => new(_stateService, _states, _logger, _businessSoftware);

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

            for (int i = 0; i < Jobs.Count; i++)
                _states[i].JobName = Jobs[i].Name;
        }
    }
}
