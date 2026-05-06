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
    // ──────────────────────────────────────────────────────────────────────────
    // RelayCommand — minimal ICommand implementation
    // ──────────────────────────────────────────────────────────────────────────

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute    = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object?   parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged
        {
            add    => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // JobRow — display model for the DataGrid
    // ──────────────────────────────────────────────────────────────────────────

    public class JobRow : INotifyPropertyChanged
    {
        private string _status = "Idle";
        private double _progress;

        public int    Index  { get; set; }
        public string Name   { get; set; } = string.Empty;
        public string Type   { get; set; } = string.Empty;
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

    // ──────────────────────────────────────────────────────────────────────────
    // MainViewModel
    // ──────────────────────────────────────────────────────────────────────────

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly BackupViewModel _core;

        // ── Observable collections ─────────────────────────────────────────
        public ObservableCollection<JobRow> JobRows { get; } = new();

        // ── Form fields for Create / Edit ──────────────────────────────────
        private string _formName   = string.Empty;
        private string _formSource = string.Empty;
        private string _formTarget = string.Empty;
        private int    _formTypeIndex; // 0=Full, 1=Differential
        private JobRow? _selectedJob;
        private string _statusMessage = string.Empty;
        private bool   _isEditing;
        private string _currentPage = "Jobs";
        private bool   _isBusy;
        private CancellationTokenSource? _runCts;

        // ── Settings fields ────────────────────────────────────────────────
        private int    _logFormatIndex;   // 0=JSON, 1=XML
        private string _logDirectoryText = string.Empty;
        private int    _jsonLayoutIndex;  // 0=Pretty array, 1=NDJSON (.ndjson)
        private int    _logDestinationModeIndex; // 0 local,1 central,2 both
        private string _centralLogEndpoint = string.Empty;
        private string _centralClientId = string.Empty;
        private string _priorityExtensionsText = string.Empty;
        private long   _largeFileThresholdKb;
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
            set { _selectedJob = value; OnPropertyChanged(); }
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

        // Settings
        public int LogFormatIndex
        {
            get => _logFormatIndex;
            set { _logFormatIndex = value; OnPropertyChanged(); }
        }

        /// <summary>Absolute or expandable path (%ProgramData%\...) where daily logs are stored; empty uses default AppData folder.</summary>
        public string LogDirectoryText
        {
            get => _logDirectoryText;
            set { _logDirectoryText = value; OnPropertyChanged(); }
        }

        /// <summary>0 = pretty JSON array; 1 = fast NDJSON line-delimited (*.ndjson).</summary>
        public int JsonLayoutIndex
        {
            get => _jsonLayoutIndex;
            set { _jsonLayoutIndex = value; OnPropertyChanged(); }
        }

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

        /// <summary>
        /// Comma-separated list of extensions to encrypt (e.g. ".txt,.docx").
        /// Bound to the Settings page TextBox. Parsed on save.
        /// </summary>
        public string EncryptedExtensionsText
        {
            get => _encryptedExtensionsText;
            set { _encryptedExtensionsText = value; OnPropertyChanged(); }
        }

        // ── Commands ───────────────────────────────────────────────────────
        public ICommand SaveJobCommand    { get; }
        public ICommand DeleteJobCommand  { get; }
        public ICommand ExecuteJobCommand { get; }
        public ICommand ExecuteAllCommand { get; }
        public ICommand SelectJobCommand  { get; }
        public ICommand NewJobCommand     { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand NavigateCommand   { get; }
        public ICommand CancelRunCommand { get; }
        public ICommand PauseJobCommand { get; }
        public ICommand ResumeJobCommand { get; }
        public ICommand StopJobCommand { get; }
        public ICommand PauseAllCommand { get; }
        public ICommand ResumeAllCommand { get; }
        public ICommand StopAllCommand { get; }

        public MainViewModel(BackupViewModel core)
        {
            _core = core;

            // Load settings into form
            LogFormatIndex           = core.Settings.LogFormat == LogFormat.Xml ? 1 : 0;
            LogDirectoryText         = core.Settings.LogDirectory ?? string.Empty;
            JsonLayoutIndex          = core.Settings.JsonLogLayout == JsonLogLayout.Ndjson ? 1 : 0;
            LogDestinationModeIndex  = core.Settings.LogDestinationMode switch
            {
                LogDestinationMode.CentralOnly => 1,
                LogDestinationMode.LocalAndCentral => 2,
                _ => 0
            };
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

            RefreshJobRows();
        }

        // ── Job actions ────────────────────────────────────────────────────

        private void SaveJob()
        {
            var type = FormTypeIndex == 1 ? BackupType.Differential : BackupType.Full;

            if (IsEditing && SelectedJob != null)
            {
                var (ok, err) = _core.EditJob(SelectedJob.Index, FormName, FormSource, FormTarget, type);
                StatusMessage = ok ? "✔ Job updated." : $"✖ {err}";
            }
            else
            {
                var (ok, err) = _core.CreateJob(FormName, FormSource, FormTarget, type);
                StatusMessage = ok ? "✔ Job created." : $"✖ {err}";
            }

            RefreshJobRows();
            StartNewJob();
        }

        private void DeleteSelected()
        {
            if (SelectedJob == null) return;
            var (ok, err) = _core.DeleteJob(SelectedJob.Index);
            StatusMessage = ok ? "✔ Job deleted." : $"✖ {err}";
            RefreshJobRows();
            StartNewJob();
        }

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
            {
                row.Status   = "Running…";
                row.Progress = 0;
            }

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
        }

        private void SelectJob(JobRow? row)
        {
            if (row == null) return;
            SelectedJob    = row;
            IsEditing      = true;
            FormName       = row.Name;
            FormSource     = row.Source;
            FormTarget     = row.Target;
            FormTypeIndex  = row.Type == "Differential" ? 1 : 0;
        }

        private void StartNewJob()
        {
            IsEditing      = false;
            SelectedJob    = null;
            FormName       = string.Empty;
            FormSource     = string.Empty;
            FormTarget     = string.Empty;
            FormTypeIndex  = 0;
        }

        // ── Settings ────────────────────────────────────────────────────────

        private void SaveSettings()
        {
            // Parse the comma-separated extensions string → List<string>
            var extensions = new System.Collections.Generic.List<string>();
            foreach (string raw in EncryptedExtensionsText.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string ext = raw.Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(ext))
                    extensions.Add(ext.StartsWith('.') ? ext : "." + ext);
            }

            var settings = new AppSettings
            {
                LogFormat            = LogFormatIndex == 1 ? LogFormat.Xml : LogFormat.Json,
                LogDirectory         = LogDirectoryText.Trim(),
                JsonLogLayout        = JsonLayoutIndex == 1 ? JsonLogLayout.Ndjson : JsonLogLayout.PrettyArray,
                LogDestinationMode   = LogDestinationModeIndex switch
                {
                    1 => LogDestinationMode.CentralOnly,
                    2 => LogDestinationMode.LocalAndCentral,
                    _ => LogDestinationMode.LocalOnly
                },
                CentralLogEndpoint   = CentralLogEndpoint.Trim(),
                CentralClientId      = CentralClientId.Trim(),
                PriorityExtensions   = ParseExtensions(PriorityExtensionsText),
                LargeFileThresholdKb = LargeFileThresholdKb < 0 ? 0 : LargeFileThresholdKb,
                EncryptedExtensions  = extensions,
                BusinessSoftwareName = BusinessSoftware.Trim()
            };
            _core.UpdateSettings(settings);
            StatusMessage = "✔ Settings saved.";
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

        // ── Helpers ────────────────────────────────────────────────────────

        private void RefreshJobRows()
        {
            JobRows.Clear();
            for (int i = 0; i < _core.Jobs.Count; i++)
            {
                var j = _core.Jobs[i];
                JobRows.Add(new JobRow
                {
                    Index  = i + 1,
                    Name   = j.Name,
                    Type   = j.Type.ToString(),
                    Source = j.SourceDirectory,
                    Target = j.TargetDirectory,
                    Status = "Idle",
                });
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
