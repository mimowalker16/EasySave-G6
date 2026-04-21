using System;
using System.Collections.Generic;
using EasySave.Models;
using EasySave.Services;

namespace EasySave.ViewModels
{
    /// <summary>
    /// Orchestrates all business logic for backup job management and execution.
    /// This class is completely independent of the console/view layer.
    /// </summary>
    public class BackupViewModel
    {
        private const int MaxJobs = 5;

        private readonly ConfigService _configService;
        private readonly StateService _stateService;
        private readonly List<BackupState> _states;

        /// <summary>The current list of configured backup jobs (max 5).</summary>
        public List<BackupJob> Jobs { get; private set; }

        public BackupViewModel(ConfigService configService, StateService stateService)
        {
            _configService = configService;
            _stateService = stateService;
            Jobs = _configService.LoadJobs();

            // Synchronize state list with currently loaded jobs
            _states = _stateService.LoadState();
            EnsureStatesMatch();
        }

        // ──────────────────────────────────────────────────────────────────
        // Job Management
        // ──────────────────────────────────────────────────────────────────

        /// <summary>Adds a new backup job. Returns false if max jobs reached or name conflict.</summary>
        public (bool success, string error) CreateJob(string name, string source, string target, BackupType type)
        {
            if (Jobs.Count >= MaxJobs)
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

        /// <summary>Edits an existing job by index (1-based). Returns false on invalid index.</summary>
        public (bool success, string error) EditJob(int oneBasedIndex, string name, string source, string target, BackupType type)
        {
            int idx = oneBasedIndex - 1;
            if (idx < 0 || idx >= Jobs.Count)
                return (false, "invalid_index");

            // Check name conflict (excluding self)
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

        /// <summary>Deletes a job by 1-based index. Returns false on invalid index.</summary>
        public (bool success, string error) DeleteJob(int oneBasedIndex)
        {
            int idx = oneBasedIndex - 1;
            if (idx < 0 || idx >= Jobs.Count)
                return (false, "invalid_index");

            Jobs.RemoveAt(idx);
            _states.RemoveAt(idx);
            Persist();
            return (true, string.Empty);
        }

        // ──────────────────────────────────────────────────────────────────
        // Execution
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Executes a single backup job by 1-based index.
        /// </summary>
        /// <returns>True if successful, false on error.</returns>
        public (bool success, string error) ExecuteJob(int oneBasedIndex)
        {
            int idx = oneBasedIndex - 1;
            if (idx < 0 || idx >= Jobs.Count)
                return (false, "invalid_index");

            var svc = new BackupService(_stateService, _states);
            try
            {
                bool ok = svc.Execute(Jobs[idx], idx);
                return (ok, ok ? string.Empty : "errors");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Executes all configured backup jobs sequentially.
        /// </summary>
        public (bool allSuccess, List<string> errors) ExecuteAllJobs()
        {
            bool allOk = true;
            var errors = new List<string>();

            var svc = new BackupService(_stateService, _states);

            for (int i = 0; i < Jobs.Count; i++)
            {
                try
                {
                    bool ok = svc.Execute(Jobs[i], i);
                    if (!ok)
                    {
                        allOk = false;
                        errors.Add($"{Jobs[i].Name}: completed with errors");
                    }
                }
                catch (Exception ex)
                {
                    allOk = false;
                    errors.Add($"{Jobs[i].Name}: {ex.Message}");
                }
            }

            return (allOk, errors);
        }

        /// <summary>
        /// Executes a set of jobs specified by 1-based indices.
        /// Supports CLI inputs like "1-3" or "1;3".
        /// </summary>
        public (bool allSuccess, List<string> errors) ExecuteJobs(IEnumerable<int> oneBasedIndices)
        {
            bool allOk = true;
            var errors = new List<string>();
            var svc = new BackupService(_stateService, _states);

            foreach (int oneIdx in oneBasedIndices)
            {
                int idx = oneIdx - 1;
                if (idx < 0 || idx >= Jobs.Count)
                {
                    errors.Add($"Index {oneIdx} out of range");
                    allOk = false;
                    continue;
                }

                try
                {
                    bool ok = svc.Execute(Jobs[idx], idx);
                    if (!ok)
                    {
                        allOk = false;
                        errors.Add($"{Jobs[idx].Name}: completed with errors");
                    }
                }
                catch (Exception ex)
                {
                    allOk = false;
                    errors.Add($"{Jobs[idx].Name}: {ex.Message}");
                }
            }

            return (allOk, errors);
        }

        // ──────────────────────────────────────────────────────────────────
        // Private helpers
        // ──────────────────────────────────────────────────────────────────

        private void Persist()
        {
            _configService.SaveJobs(Jobs);
            _stateService.UpdateState(_states);
        }

        /// <summary>
        /// Synchronizes the in-memory state list to match the current jobs list.
        /// </summary>
        private void EnsureStatesMatch()
        {
            while (_states.Count < Jobs.Count)
                _states.Add(new BackupState { JobName = Jobs[_states.Count].Name });

            while (_states.Count > Jobs.Count)
                _states.RemoveAt(_states.Count - 1);
        }
    }
}
