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

        private int _logFormatIndex;
        private string _logDirectoryText = string.Empty;
        private int _logDestinationModeIndex;
        private string _centralLogEndpoint = string.Empty;
        private string _centralClientId = string.Empty;
        private string _priorityExtensionsText = string.Empty;
        private long _largeFileThresholdKb;
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

        public string LogDirectoryText
        {
            get => _logDirectoryText;
            set { _logDirectoryText = value; OnPropertyChanged(); }
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
        public ICommand NavigateCommand { get; }
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

            LogFormatIndex = core.Settings.LogFormat == LogFormat.Xml ? 1 : 0;
            LogDirectoryText = core.Settings.LogDirectory;
            LogDestinationModeIndex = core.Settings.LogDestinationMode switch
            {
                LogDestinationMode.CentralOnly => 1,
                LogDestinationMode.LocalAndCentral => 2,
                _ => 0
            };
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
                LogFormat = LogFormatIndex == 1 ? LogFormat.Xml : LogFormat.Json,
                LogDirectory = LogDirectoryText.Trim(),
                LogDestinationMode = LogDestinationModeIndex switch
                {
                    1 => LogDestinationMode.CentralOnly,
                    2 => LogDestinationMode.LocalAndCentral,
                    _ => LogDestinationMode.LocalOnly
                },
                CentralLogEndpoint = CentralLogEndpoint.Trim(),
                CentralClientId = CentralClientId.Trim(),
                PriorityExtensions = ParseExtensions(PriorityExtensionsText),
                LargeFileThresholdKb = Math.Max(0, LargeFileThresholdKb),
                EncryptedExtensions = ParseExtensions(EncryptedExtensionsText),
                BusinessSoftwareName = BusinessSoftware.Trim()
            };

            _core.UpdateSettings(settings);
            StatusMessage = "Settings saved.";
        }

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
