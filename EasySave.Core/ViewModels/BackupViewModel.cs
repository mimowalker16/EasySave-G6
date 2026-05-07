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
<<<<<<< Updated upstream
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
        private readonly ConcurrentDictionary<int, JobRunControl> _runControls = new();

=======
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

>>>>>>> Stashed changes
        private sealed class JobRunControl
        {
            public CancellationTokenSource Cts { get; } = new();
            public volatile bool PauseRequested;
        }

        public List<BackupJob> Jobs { get; private set; }
<<<<<<< Updated upstream

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
=======
        public AppSettings Settings { get; private set; }

        public event Action<int, BackupState>? StateChanged;
>>>>>>> Stashed changes

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
<<<<<<< Updated upstream
                LogDirectory   = Settings.LogDirectory,
                JsonLogLayout  = Settings.JsonLogLayout,
=======
                LogDirectory = Settings.LogDirectory,
>>>>>>> Stashed changes
                DestinationMode = Settings.LogDestinationMode,
                CentralLogEndpoint = Settings.CentralLogEndpoint,
                CentralClientId = Settings.CentralClientId
            });
        }

<<<<<<< Updated upstream
        /// <summary>Updates and persists global settings and rebuilds the log writer.</summary>
=======
>>>>>>> Stashed changes
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

<<<<<<< Updated upstream
            Jobs[idx].Name             = name.Trim();
            Jobs[idx].SourceDirectory  = source.Trim();
            Jobs[idx].TargetDirectory  = target.Trim();
            Jobs[idx].Type             = type;
            _states[idx].JobName       = name.Trim();
=======
            Jobs[idx].Name = name.Trim();
            Jobs[idx].SourceDirectory = source.Trim();
            Jobs[idx].TargetDirectory = target.Trim();
            Jobs[idx].Type = type;
            _states[idx].JobName = name.Trim();
>>>>>>> Stashed changes

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

<<<<<<< Updated upstream
        /// <summary>Runs one job (exclusive gate, pre-flight checks).</summary>
=======
>>>>>>> Stashed changes
        public (bool Success, string Error) ExecuteJob(
            int oneBasedIndex,
            CancellationToken cancellationToken = default)
        {
            int idx = oneBasedIndex - 1;
            if (idx < 0 || idx >= Jobs.Count)
                return (false, "invalid_index");

<<<<<<< Updated upstream
            BackupPreflight.Result pf = BackupPreflight.Validate(Jobs[idx]);
            if (!pf.Ok)
                return (false, pf.ErrorKey);

            if (!ExecutionGate.Wait(0))
                return (false, "execution_busy");

            var control = _runControls.GetOrAdd(idx, _ => new JobRunControl());
            try
            {
                BackupService svc = CreateBackupService();
                try
                {
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken, control.Cts.Token);
                    bool ok = svc.Execute(Jobs[idx], idx, Settings, linked.Token, () => control.PauseRequested);
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
                _runControls.TryRemove(idx, out _);
                ExecutionGate.Release();
            }
        }

        /// <summary>Runs all jobs in parallel under a single acquisition of the execution gate.</summary>
        public (bool AllSuccess, List<string> Errors) ExecuteAllJobs(
            CancellationToken cancellationToken = default)
        {
            if (Jobs.Count == 0)
                return (true, new List<string>());
=======
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
>>>>>>> Stashed changes

            foreach (int oneIdx in oneBasedIndices)
            {
<<<<<<< Updated upstream
                BackupPreflight.Result pf = BackupPreflight.Validate(Jobs[i]);
                if (!pf.Ok)
                    return (false, new List<string> { $"{Jobs[i].Name}: {pf.ErrorKey}" });
=======
                cancellationToken.ThrowIfCancellationRequested();
                var (ok, error) = ExecuteJob(oneIdx, cancellationToken);
                if (!ok)
                {
                    allOk = false;
                    string label = oneIdx >= 1 && oneIdx <= Jobs.Count ? Jobs[oneIdx - 1].Name : $"Index {oneIdx}";
                    errors.Add($"{label}: {error}");
                }
>>>>>>> Stashed changes
            }

            if (!ExecutionGate.Wait(0))
                return (false, new List<string> { "execution_busy" });

            int allOkFlag = 1;
            var  errors = new ConcurrentBag<string>();
            try
            {
                var tasks = new List<Task>();
                for (int i = 0; i < Jobs.Count; i++)
                {
                    int idx = i;
                    tasks.Add(Task.Run(() =>
                    {
                        BackupService svc = CreateBackupService();
                        var control = _runControls.GetOrAdd(idx, _ => new JobRunControl());
                        try
                        {
                            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                                cancellationToken, control.Cts.Token);
                            bool ok = svc.Execute(Jobs[idx], idx, Settings, linked.Token, () => control.PauseRequested);
                            if (!ok)
                            {
                                Interlocked.Exchange(ref allOkFlag, 0);
                                errors.Add($"{Jobs[idx].Name}: completed with errors");
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            Interlocked.Exchange(ref allOkFlag, 0);
                            errors.Add($"{Jobs[idx].Name}: cancelled");
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Exchange(ref allOkFlag, 0);
                            errors.Add($"{Jobs[idx].Name}: {ex.Message}");
                        }
                        finally
                        {
                            _runControls.TryRemove(idx, out _);
                        }
                    }, cancellationToken));
                }

                Task.WaitAll(tasks.ToArray());
            }
            finally
            {
                ExecutionGate.Release();
            }

            return (allOkFlag == 1, errors.ToList());
        }

<<<<<<< Updated upstream
        /// <summary>Runs selected jobs in parallel (CLI / range) with one gate acquisition.</summary>
        public (bool AllSuccess, List<string> Errors) ExecuteJobs(
            IEnumerable<int> oneBasedIndices,
            CancellationToken cancellationToken = default)
        {
            int allOkFlag = 1;
            var  errors = new ConcurrentBag<string>();
            var  indices = new List<int>();
=======
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
>>>>>>> Stashed changes

            foreach (int oneIdx in oneBasedIndices)
            {
                int idx = oneIdx - 1;
                if (idx < 0 || idx >= Jobs.Count)
<<<<<<< Updated upstream
                {
                    errors.Add($"Index {oneIdx} out of range");
                    Interlocked.Exchange(ref allOkFlag, 0);
                    continue;
                }

                indices.Add(idx);
            }

            if (indices.Count == 0)
                return (allOkFlag == 1, errors.ToList());

            foreach (int idx in indices)
            {
                BackupPreflight.Result pf = BackupPreflight.Validate(Jobs[idx]);
                if (!pf.Ok)
                {
                    Interlocked.Exchange(ref allOkFlag, 0);
                    errors.Add($"{Jobs[idx].Name}: {pf.ErrorKey}");
                    return (allOkFlag == 1, errors.ToList());
                }
            }

            if (!ExecutionGate.Wait(0))
                return (false, new List<string> { "execution_busy" });

            try
            {
                var tasks = new List<Task>();
                foreach (int idx in indices)
                {
                    int localIdx = idx;
                    tasks.Add(Task.Run(() =>
                    {
                        BackupService svc = CreateBackupService();
                        var control = _runControls.GetOrAdd(localIdx, _ => new JobRunControl());
                        try
                        {
                            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                                cancellationToken, control.Cts.Token);
                            bool ok = svc.Execute(
                                Jobs[localIdx], localIdx, Settings, linked.Token, () => control.PauseRequested);
                            if (!ok)
                            {
                                Interlocked.Exchange(ref allOkFlag, 0);
                                errors.Add($"{Jobs[localIdx].Name}: completed with errors");
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            Interlocked.Exchange(ref allOkFlag, 0);
                            errors.Add($"{Jobs[localIdx].Name}: cancelled");
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Exchange(ref allOkFlag, 0);
                            errors.Add($"{Jobs[localIdx].Name}: {ex.Message}");
                        }
                        finally
                        {
                            _runControls.TryRemove(localIdx, out _);
                        }
                    }, cancellationToken));
                }

                Task.WaitAll(tasks.ToArray());
            }
            finally
            {
                ExecutionGate.Release();
            }

            return (allOkFlag == 1, errors.ToList());
        }
=======
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
>>>>>>> Stashed changes

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

        /// <summary>Requests pause for one running job (1-based index).</summary>
        public bool PauseJob(int oneBasedIndex)
        {
            int idx = oneBasedIndex - 1;
            if (_runControls.TryGetValue(idx, out JobRunControl? ctl))
            {
                ctl.PauseRequested = true;
                return true;
            }
            return false;
        }

        /// <summary>Requests resume for one paused/running job (1-based index).</summary>
        public bool ResumeJob(int oneBasedIndex)
        {
            int idx = oneBasedIndex - 1;
            if (_runControls.TryGetValue(idx, out JobRunControl? ctl))
            {
                ctl.PauseRequested = false;
                return true;
            }
            return false;
        }

        /// <summary>Requests immediate stop/cancel for one running job (1-based index).</summary>
        public bool StopJob(int oneBasedIndex)
        {
            int idx = oneBasedIndex - 1;
            if (_runControls.TryGetValue(idx, out JobRunControl? ctl))
            {
                ctl.Cts.Cancel();
                return true;
            }
            return false;
        }

        public void PauseAllJobs()
        {
            foreach (JobRunControl c in _runControls.Values)
                c.PauseRequested = true;
        }

        public void ResumeAllJobs()
        {
            foreach (JobRunControl c in _runControls.Values)
                c.PauseRequested = false;
        }

        public void StopAllJobs()
        {
            foreach (JobRunControl c in _runControls.Values)
                c.Cts.Cancel();
        }

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
