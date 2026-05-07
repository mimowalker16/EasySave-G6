using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EasySave.Core.Models;
using EasySave.Core.Services;
using EasySave.Core.ViewModels;
using EasyLog;

namespace EasySave.GUI.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    public class JobRow : INotifyPropertyChanged
    {
        private string _status = "Idle";
        private double _progress;

        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly BackupViewModel _core;
        private CancellationTokenSource? _runCts;
        private bool _isBusy;

        public ObservableCollection<JobRow> JobRows { get; } = new();

        private string _formName = string.Empty;
        private string _formSource = string.Empty;
        private string _formTarget = string.Empty;
        private int _formTypeIndex;
        private JobRow? _selectedJob;
        private string _statusMessage = string.Empty;
        private bool _isEditing;
        private string _currentPage = "Jobs";
        private bool   _isBusy;
        private CancellationTokenSource? _runCts;

<<<<<<< Updated upstream
        // ── Settings fields ────────────────────────────────────────────────
        private int    _logFormatIndex;   // 0=JSON, 1=XML
        private string _logDirectoryText = string.Empty;
        private int    _jsonLayoutIndex;  // 0=Pretty array, 1=NDJSON (.ndjson)
        private int    _logDestinationModeIndex; // 0 local,1 central,2 both
        private string _centralLogEndpoint = string.Empty;
        private string _centralClientId = string.Empty;
        private string _priorityExtensionsText = string.Empty;
        private long   _largeFileThresholdKb;
=======
        private int _logFormatIndex;
        private string _logDirectoryText = string.Empty;
        private int _logDestinationModeIndex;
        private string _centralLogEndpoint = string.Empty;
        private string _centralClientId = string.Empty;
        private string _priorityExtensionsText = string.Empty;
        private long _largeFileThresholdKb;
>>>>>>> Stashed changes
        private string _businessSoftware = string.Empty;
        private string _encryptedExtensionsText = string.Empty;

        public string FormName
        {
            get => _formName;
            set { _formName = value; OnPropertyChanged(); }
        }

        public string FormSource
        {
            get => _formSource;
            set { _formSource = value; OnPropertyChanged(); }
        }

        public string FormTarget
        {
            get => _formTarget;
            set { _formTarget = value; OnPropertyChanged(); }
        }

        public int FormTypeIndex
        {
            get => _formTypeIndex;
            set { _formTypeIndex = value; OnPropertyChanged(); }
        }

        public JobRow? SelectedJob
        {
            get => _selectedJob;
            set { _selectedJob = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public bool IsEditing
        {
            get => _isEditing;
            set { _isEditing = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormTitle)); }
        }

        public string FormTitle => IsEditing ? "Edit Job" : "New Job";

        /// <summary>True while backup work runs on a background thread (UI stays responsive).</summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string CurrentPage
        {
            get => _currentPage;
            set { _currentPage = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public int LogFormatIndex
        {
            get => _logFormatIndex;
            set { _logFormatIndex = value; OnPropertyChanged(); }
        }

<<<<<<< Updated upstream
        /// <summary>Absolute or expandable path (%ProgramData%\...) where daily logs are stored; empty uses default AppData folder.</summary>
=======
>>>>>>> Stashed changes
        public string LogDirectoryText
        {
            get => _logDirectoryText;
            set { _logDirectoryText = value; OnPropertyChanged(); }
        }

<<<<<<< Updated upstream
        /// <summary>0 = pretty JSON array; 1 = fast NDJSON line-delimited (*.ndjson).</summary>
        public int JsonLayoutIndex
        {
            get => _jsonLayoutIndex;
            set { _jsonLayoutIndex = value; OnPropertyChanged(); }
        }

=======
>>>>>>> Stashed changes
        public int LogDestinationModeIndex
        {
            get => _logDestinationModeIndex;
            set { _logDestinationModeIndex = value; OnPropertyChanged(); }
        }

        public string CentralLogEndpoint
        {
            get => _centralLogEndpoint;
            set { _centralLogEndpoint = value; OnPropertyChanged(); }
        }

        public string CentralClientId
        {
            get => _centralClientId;
            set { _centralClientId = value; OnPropertyChanged(); }
        }

        public string PriorityExtensionsText
        {
            get => _priorityExtensionsText;
            set { _priorityExtensionsText = value; OnPropertyChanged(); }
        }

        public long LargeFileThresholdKb
        {
            get => _largeFileThresholdKb;
            set { _largeFileThresholdKb = value; OnPropertyChanged(); }
        }

        public string BusinessSoftware
        {
            get => _businessSoftware;
            set { _businessSoftware = value; OnPropertyChanged(); }
        }

        public string EncryptedExtensionsText
        {
            get => _encryptedExtensionsText;
            set { _encryptedExtensionsText = value; OnPropertyChanged(); }
        }

        public ICommand SaveJobCommand { get; }
        public ICommand DeleteJobCommand { get; }
        public ICommand ExecuteJobCommand { get; }
        public ICommand ExecuteAllCommand { get; }
        public ICommand SelectJobCommand { get; }
        public ICommand NewJobCommand { get; }
        public ICommand SaveSettingsCommand { get; }
<<<<<<< Updated upstream
        public ICommand NavigateCommand   { get; }
        public ICommand CancelRunCommand { get; }
=======
        public ICommand NavigateCommand { get; }
>>>>>>> Stashed changes
        public ICommand PauseJobCommand { get; }
        public ICommand ResumeJobCommand { get; }
        public ICommand StopJobCommand { get; }
        public ICommand PauseAllCommand { get; }
        public ICommand ResumeAllCommand { get; }
        public ICommand StopAllCommand { get; }

        public MainViewModel(BackupViewModel core)
        {
            _core = core;
            _core.StateChanged += OnCoreStateChanged;

<<<<<<< Updated upstream
            // Load settings into form
            LogFormatIndex           = core.Settings.LogFormat == LogFormat.Xml ? 1 : 0;
            LogDirectoryText         = core.Settings.LogDirectory ?? string.Empty;
            JsonLayoutIndex          = core.Settings.JsonLogLayout == JsonLogLayout.Ndjson ? 1 : 0;
            LogDestinationModeIndex  = core.Settings.LogDestinationMode switch
=======
            LogFormatIndex = core.Settings.LogFormat == LogFormat.Xml ? 1 : 0;
            LogDirectoryText = core.Settings.LogDirectory;
            LogDestinationModeIndex = core.Settings.LogDestinationMode switch
>>>>>>> Stashed changes
            {
                LogDestinationMode.CentralOnly => 1,
                LogDestinationMode.LocalAndCentral => 2,
                _ => 0
            };
<<<<<<< Updated upstream
            CentralLogEndpoint       = core.Settings.CentralLogEndpoint;
            CentralClientId          = core.Settings.CentralClientId;
            PriorityExtensionsText   = string.Join(",", core.Settings.PriorityExtensions);
            LargeFileThresholdKb     = core.Settings.LargeFileThresholdKb;
            BusinessSoftware         = core.Settings.BusinessSoftwareName;
            EncryptedExtensionsText  = string.Join(",", core.Settings.EncryptedExtensions);

            SaveJobCommand     = new RelayCommand(_ => SaveJob(), _ => !IsBusy);
            DeleteJobCommand   = new RelayCommand(_ => DeleteSelected(), _ => !IsBusy && SelectedJob != null);
            ExecuteJobCommand  = new RelayCommand(ExecuteOneKickoff,
                _ => !IsBusy && JobRows.Count > 0);
            ExecuteAllCommand  = new RelayCommand(_ => ExecuteAllKickoff(),
                _ => !IsBusy && JobRows.Count > 0);
            SelectJobCommand   = new RelayCommand(o => SelectJob(o as JobRow), _ => !IsBusy);
            NewJobCommand      = new RelayCommand(_ => StartNewJob(), _ => !IsBusy);
            SaveSettingsCommand = new RelayCommand(_ => SaveSettings(), _ => !IsBusy);
            NavigateCommand    = new RelayCommand(o => CurrentPage = o?.ToString() ?? "Jobs");
            CancelRunCommand   = new RelayCommand(_ => CancelRun(), _ => IsBusy);
            PauseJobCommand    = new RelayCommand(PauseJob);
            ResumeJobCommand   = new RelayCommand(ResumeJob);
            StopJobCommand     = new RelayCommand(StopJob);
            PauseAllCommand    = new RelayCommand(_ => PauseAllJobs());
            ResumeAllCommand   = new RelayCommand(_ => ResumeAllJobs());
            StopAllCommand     = new RelayCommand(_ => StopAllJobs());
=======
            CentralLogEndpoint = core.Settings.CentralLogEndpoint;
            CentralClientId = core.Settings.CentralClientId;
            PriorityExtensionsText = string.Join(",", core.Settings.PriorityExtensions);
            LargeFileThresholdKb = core.Settings.LargeFileThresholdKb;
            BusinessSoftware = core.Settings.BusinessSoftwareName;
            EncryptedExtensionsText = string.Join(",", core.Settings.EncryptedExtensions);

            SaveJobCommand = new RelayCommand(_ => SaveJob(), _ => !IsBusy);
            DeleteJobCommand = new RelayCommand(DeleteJob, _ => !IsBusy);
            ExecuteJobCommand = new RelayCommand(ExecuteJobKickoff, _ => !IsBusy);
            ExecuteAllCommand = new RelayCommand(_ => ExecuteAllKickoff(), _ => !IsBusy && JobRows.Count > 0);
            SelectJobCommand = new RelayCommand(o => SelectJob(o as JobRow), _ => !IsBusy);
            NewJobCommand = new RelayCommand(_ => StartNewJob(), _ => !IsBusy);
            SaveSettingsCommand = new RelayCommand(_ => SaveSettings(), _ => !IsBusy);
            NavigateCommand = new RelayCommand(o => CurrentPage = o?.ToString() ?? "Jobs");
            PauseJobCommand = new RelayCommand(o => PauseJob(o as JobRow));
            ResumeJobCommand = new RelayCommand(o => ResumeJob(o as JobRow));
            StopJobCommand = new RelayCommand(o => StopJob(o as JobRow));
            PauseAllCommand = new RelayCommand(_ => PauseAllJobs());
            ResumeAllCommand = new RelayCommand(_ => ResumeAllJobs());
            StopAllCommand = new RelayCommand(_ => StopAllJobs());
>>>>>>> Stashed changes

            RefreshJobRows();
        }

        private void SaveJob()
        {
            var type = FormTypeIndex == 1 ? BackupType.Differential : BackupType.Full;

            if (IsEditing && SelectedJob != null)
            {
                var (ok, err) = _core.EditJob(SelectedJob.Index, FormName, FormSource, FormTarget, type);
                StatusMessage = ok ? "Job updated." : err;
            }
            else
            {
                var (ok, err) = _core.CreateJob(FormName, FormSource, FormTarget, type);
                StatusMessage = ok ? "Job created." : err;
            }

            RefreshJobRows();
            StartNewJob();
        }

        private void DeleteJob(object? parameter)
        {
            var row = parameter as JobRow ?? SelectedJob;
            if (row == null) return;

            var (ok, err) = _core.DeleteJob(row.Index);
            StatusMessage = ok ? "Job deleted." : err;
            RefreshJobRows();
            StartNewJob();
        }

<<<<<<< Updated upstream
        /// <summary>Runs job from toolbar list button (<see cref="JobRow"/> CommandParameter).</summary>
        private void ExecuteOneKickoff(object? parameter)
        {
            var row = parameter as JobRow;
            _ = ExecuteOneAsync(row ?? SelectedJob);
        }

        private void ExecuteAllKickoff()
            => _ = ExecuteAllAsync();

        private void CancelRun()
        {
            _runCts?.Cancel();
            StatusMessage = "Cancelling — will stop after the current file operation…";
        }

        private void PauseJob(object? parameter)
        {
            if (parameter is not JobRow row) return;
            if (_core.PauseJob(row.Index))
            {
                row.Status = "Paused";
                StatusMessage = $"⏸ Job '{row.Name}' paused.";
            }
        }

        private void ResumeJob(object? parameter)
        {
            if (parameter is not JobRow row) return;
            if (_core.ResumeJob(row.Index))
            {
                row.Status = "Running…";
                StatusMessage = $"▶ Job '{row.Name}' resumed.";
            }
        }

        private void StopJob(object? parameter)
        {
            if (parameter is not JobRow row) return;
            if (_core.StopJob(row.Index))
            {
                row.Status = "Canceled";
                StatusMessage = $"◼ Job '{row.Name}' stop requested.";
            }
        }

        private void PauseAllJobs()
        {
            _core.PauseAllJobs();
            foreach (var row in JobRows)
                if (row.Status.StartsWith("Running", StringComparison.OrdinalIgnoreCase))
                    row.Status = "Paused";
            StatusMessage = "⏸ Pause requested for all running jobs.";
        }

        private void ResumeAllJobs()
        {
            _core.ResumeAllJobs();
            foreach (var row in JobRows)
                if (row.Status.StartsWith("Paused", StringComparison.OrdinalIgnoreCase))
                    row.Status = "Running…";
            StatusMessage = "▶ Resume requested for all paused jobs.";
        }

        private void StopAllJobs()
        {
            _core.StopAllJobs();
            foreach (var row in JobRows)
                if (row.Status.StartsWith("Running", StringComparison.OrdinalIgnoreCase) ||
                    row.Status.StartsWith("Paused", StringComparison.OrdinalIgnoreCase))
                    row.Status = "Canceled";
            StatusMessage = "◼ Stop requested for all jobs.";
        }

        /// <summary>Executes a single backup on a pool thread so the WPF window stays responsive.</summary>
        private async Task ExecuteOneAsync(JobRow? row)
        {
            if (row == null || IsBusy) return;

            _runCts?.Dispose();
            _runCts = new CancellationTokenSource();
            CancellationToken token = _runCts.Token;

            row.Status    = "Running…";
            row.Progress  = 0;
            StatusMessage = $"Working on \"{row.Name}\"…";

            IsBusy = true;
            try
            {
                (bool ok, string err) = await Task.Run(
                    () => _core.ExecuteJob(row.Index, token), token).ConfigureAwait(false);

                Application.Current!.Dispatcher.Invoke(() =>
                {
                    if (err == "cancelled")
                    {
                        row.Progress = 0;
                        row.Status   = "Canceled";
                        StatusMessage = $"◼ Job '{row.Name}' was cancelled.";
                    }
                    else
                    {
                        row.Progress = ok ? 100 : 0;
                        row.Status   = ok ? "Done ✔" : $"Error: {err}";
                        StatusMessage = ok
                            ? $"✔ Job '{row.Name}' completed."
                            : $"✖ Job '{row.Name}': {err}";
                    }
                });
            }
            finally
            {
                Application.Current!.Dispatcher.Invoke(() => { IsBusy = false; });
                _runCts?.Dispose();
                _runCts = null;
            }
        }

        /// <summary>Runs all jobs sequentially on a background thread.</summary>
        private async Task ExecuteAllAsync()
        {
            if (IsBusy || JobRows.Count == 0) return;

            _runCts?.Dispose();
            _runCts = new CancellationTokenSource();
            CancellationToken token = _runCts.Token;

            foreach (var row in JobRows)
=======
        private void ExecuteJobKickoff(object? parameter)
        {
            var row = parameter as JobRow ?? SelectedJob;
            if (row == null || IsBusy) return;
            _ = ExecuteJobAsync(row);
        }

        private void ExecuteAllKickoff()
        {
            if (IsBusy) return;
            _ = ExecuteAllAsync();
        }

        private async Task ExecuteJobAsync(JobRow row)
        {
            _runCts?.Dispose();
            _runCts = new CancellationTokenSource();
            IsBusy = true;

            row.Status = "Running";
            row.Progress = 0;
            StatusMessage = $"Running {row.Name}...";

            try
>>>>>>> Stashed changes
            {
                (bool ok, string error) = await Task.Run(
                    () => _core.ExecuteJob(row.Index, _runCts.Token));

                if (!ok)
                {
                    row.Status = error == "cancelled" ? "Canceled" : $"Error: {error}";
                    StatusMessage = $"{row.Name}: {error}";
                }
                else
                {
                    row.Progress = 100;
                    row.Status = "Done";
                    StatusMessage = $"{row.Name} completed.";
                }
            }
            finally
            {
                IsBusy = false;
                _runCts?.Dispose();
                _runCts = null;
            }
        }

        private async Task ExecuteAllAsync()
        {
            _runCts?.Dispose();
            _runCts = new CancellationTokenSource();
            IsBusy = true;

            foreach (JobRow row in JobRows)
            {
                row.Status = "Running";
                row.Progress = 0;
            }

<<<<<<< Updated upstream
            StatusMessage = "Running all jobs…";
            IsBusy        = true;

            try
            {
                (bool allOk, List<string> errors) = await Task.Run(
                    () => _core.ExecuteAllJobs(token), token).ConfigureAwait(false);

                Application.Current!.Dispatcher.Invoke(() =>
                {
                    bool anyCancel = errors.Exists(e => e.Contains("cancelled", StringComparison.OrdinalIgnoreCase));
                    foreach (var row in JobRows)
                    {
                        row.Progress = allOk ? 100 : (anyCancel ? 0 : 100);
                        row.Status   = allOk ? "Done ✔" : (anyCancel ? "Canceled / errors" : "Completed with errors");
                    }

                    StatusMessage = allOk
                        ? "✔ All jobs completed."
                        : $"✖ {string.Join(", ", errors)}";
                });
            }
            finally
            {
                Application.Current!.Dispatcher.Invoke(() => { IsBusy = false; });
                _runCts?.Dispose();
                _runCts = null;
            }
=======
            StatusMessage = "Running all jobs in parallel...";

            try
            {
                (bool ok, List<string> errors) = await Task.Run(
                    () => _core.ExecuteAllJobsParallel(_runCts.Token));

                StatusMessage = ok
                    ? "All jobs completed."
                    : $"Completed with errors: {string.Join(", ", errors)}";
            }
            finally
            {
                IsBusy = false;
                _runCts?.Dispose();
                _runCts = null;
            }
        }

        private void PauseJob(JobRow? row)
        {
            if (row == null) return;
            if (_core.PauseJob(row.Index))
            {
                row.Status = "Paused";
                StatusMessage = $"{row.Name} paused.";
            }
        }

        private void ResumeJob(JobRow? row)
        {
            if (row == null) return;
            if (_core.ResumeJob(row.Index))
            {
                row.Status = "Running";
                StatusMessage = $"{row.Name} resumed.";
            }
        }

        private void StopJob(JobRow? row)
        {
            if (row == null) return;
            if (_core.StopJob(row.Index))
            {
                row.Status = "Stopping";
                StatusMessage = $"{row.Name} stop requested.";
            }
        }

        private void PauseAllJobs()
        {
            _core.PauseAllJobs();
            foreach (JobRow row in JobRows)
                if (row.Status == "Running")
                    row.Status = "Paused";
            StatusMessage = "Pause requested for running jobs.";
        }

        private void ResumeAllJobs()
        {
            _core.ResumeAllJobs();
            foreach (JobRow row in JobRows)
                if (row.Status == "Paused")
                    row.Status = "Running";
            StatusMessage = "Resume requested for paused jobs.";
        }

        private void StopAllJobs()
        {
            _core.StopAllJobs();
            foreach (JobRow row in JobRows)
                if (row.Status == "Running" || row.Status == "Paused")
                    row.Status = "Stopping";
            StatusMessage = "Stop requested for running jobs.";
>>>>>>> Stashed changes
        }

        private void SelectJob(JobRow? row)
        {
            if (row == null) return;
            SelectedJob = row;
            IsEditing = true;
            FormName = row.Name;
            FormSource = row.Source;
            FormTarget = row.Target;
            FormTypeIndex = row.Type == "Differential" ? 1 : 0;
        }

        private void StartNewJob()
        {
            IsEditing = false;
            SelectedJob = null;
            FormName = string.Empty;
            FormSource = string.Empty;
            FormTarget = string.Empty;
            FormTypeIndex = 0;
        }

        private void SaveSettings()
        {
            var settings = new AppSettings
            {
<<<<<<< Updated upstream
                LogFormat            = LogFormatIndex == 1 ? LogFormat.Xml : LogFormat.Json,
                LogDirectory         = LogDirectoryText.Trim(),
                JsonLogLayout        = JsonLayoutIndex == 1 ? JsonLogLayout.Ndjson : JsonLogLayout.PrettyArray,
                LogDestinationMode   = LogDestinationModeIndex switch
=======
                LogFormat = LogFormatIndex == 1 ? LogFormat.Xml : LogFormat.Json,
                LogDirectory = LogDirectoryText.Trim(),
                LogDestinationMode = LogDestinationModeIndex switch
>>>>>>> Stashed changes
                {
                    1 => LogDestinationMode.CentralOnly,
                    2 => LogDestinationMode.LocalAndCentral,
                    _ => LogDestinationMode.LocalOnly
                },
<<<<<<< Updated upstream
                CentralLogEndpoint   = CentralLogEndpoint.Trim(),
                CentralClientId      = CentralClientId.Trim(),
                PriorityExtensions   = ParseExtensions(PriorityExtensionsText),
                LargeFileThresholdKb = LargeFileThresholdKb < 0 ? 0 : LargeFileThresholdKb,
                EncryptedExtensions  = extensions,
=======
                CentralLogEndpoint = CentralLogEndpoint.Trim(),
                CentralClientId = CentralClientId.Trim(),
                PriorityExtensions = ParseExtensions(PriorityExtensionsText),
                LargeFileThresholdKb = Math.Max(0, LargeFileThresholdKb),
                EncryptedExtensions = ParseExtensions(EncryptedExtensionsText),
>>>>>>> Stashed changes
                BusinessSoftwareName = BusinessSoftware.Trim()
            };

            _core.UpdateSettings(settings);
            StatusMessage = "Settings saved.";
        }

<<<<<<< Updated upstream
        private static List<string> ParseExtensions(string rawText)
        {
            var result = new List<string>();
            foreach (string raw in rawText.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string ext = raw.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(ext)) continue;
                result.Add(ext.StartsWith('.') ? ext : "." + ext);
            }
            return result;
        }

        // ── Helpers ────────────────────────────────────────────────────────
=======
        private void OnCoreStateChanged(int zeroBasedIndex, BackupState state)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (zeroBasedIndex < 0 || zeroBasedIndex >= JobRows.Count)
                    return;

                JobRow row = JobRows[zeroBasedIndex];
                row.Progress = state.Progress;
                row.Status = state.State switch
                {
                    BackupStateType.Active => "Running",
                    BackupStateType.Paused => "Paused",
                    BackupStateType.Canceled => "Canceled",
                    BackupStateType.End => "Done",
                    _ => "Idle"
                };
            });
        }
>>>>>>> Stashed changes

        private void RefreshJobRows()
        {
            JobRows.Clear();
            for (int i = 0; i < _core.Jobs.Count; i++)
            {
                BackupJob job = _core.Jobs[i];
                JobRows.Add(new JobRow
                {
                    Index = i + 1,
                    Name = job.Name,
                    Type = job.Type.ToString(),
                    Source = job.SourceDirectory,
                    Target = job.TargetDirectory,
                    Status = "Idle"
                });
            }
        }

        private static List<string> ParseExtensions(string rawText)
        {
            var result = new List<string>();
            foreach (string raw in rawText.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string ext = raw.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(ext)) continue;
                result.Add(ext.StartsWith('.') ? ext : "." + ext);
            }

            return result;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
