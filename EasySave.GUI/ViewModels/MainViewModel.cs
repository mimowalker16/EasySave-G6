using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using EasyLog;
using EasySave.Core.Models;
using EasySave.Core.Services;
using EasySave.Core.ViewModels;
using EasySave.GUI.Services;
using Forms = System.Windows.Forms;

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
        private string _currentFileName = "-";
        private string _lastActionDisplay = "-";
        private int _remainingFiles;
        private string _remainingSizeDisplay = "-";
        private string _pauseReason = "-";

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
            set { _progress = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressText)); }
        }

        public string ProgressText => $"{Math.Round(Progress, 0)}%";

        public string CurrentFileName
        {
            get => _currentFileName;
            set { _currentFileName = string.IsNullOrWhiteSpace(value) ? "-" : value; OnPropertyChanged(); }
        }

        public string LastActionDisplay
        {
            get => _lastActionDisplay;
            set { _lastActionDisplay = string.IsNullOrWhiteSpace(value) ? "-" : value; OnPropertyChanged(); }
        }

        public int RemainingFiles
        {
            get => _remainingFiles;
            set { _remainingFiles = value; OnPropertyChanged(); }
        }

        public string RemainingSizeDisplay
        {
            get => _remainingSizeDisplay;
            set { _remainingSizeDisplay = string.IsNullOrWhiteSpace(value) ? "-" : value; OnPropertyChanged(); }
        }

        public string PauseReason
        {
            get => _pauseReason;
            set { _pauseReason = string.IsNullOrWhiteSpace(value) ? "-" : value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class HealthWarning
    {
        public string Title { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
    }

    public sealed class LogRow
    {
        public string Time { get; init; } = string.Empty;
        public string Job { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty;
        public string Target { get; init; } = string.Empty;
        public string Size { get; init; } = string.Empty;
        public string TransferTime { get; init; } = string.Empty;
        public string EncryptionTime { get; init; } = string.Empty;
        public string Result { get; init; } = string.Empty;
        public bool IsError { get; init; }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly BackupViewModel _core;
        private CancellationTokenSource? _runCts;
        private bool _isBusy;
        private bool _isFormDirty;

        public ObservableCollection<JobRow> JobRows { get; } = new();

        private string _formName = string.Empty;
        private string _formSource = string.Empty;
        private string _formTarget = string.Empty;
        private int _formTypeIndex;
        private JobRow? _selectedJob;
        private string _statusMessage = string.Empty;
        private bool _isEditing;
        private string _currentPage = "Dashboard";
        private string _formNameError = string.Empty;
        private string _formSourceError = string.Empty;
        private string _formTargetError = string.Empty;
        private string _settingsError = string.Empty;
        private string _lastSuccessfulBackup = "-";
        private string _logDateText = DateTime.Today.ToString("yyyy-MM-dd");
        private string _logJobFilter = string.Empty;
        private int _logStatusFilterIndex;
        private string _newPriorityExtension = string.Empty;
        private string _newEncryptedExtension = string.Empty;

        private int _languageIndex;
        private int _logFormatIndex;
        private string _logDirectoryText = string.Empty;
        private int _logDestinationModeIndex;
        private string _centralLogEndpoint = string.Empty;
        private string _centralClientId = string.Empty;
        private string _priorityExtensionsText = string.Empty;
        private long _largeFileThresholdKb;
        private string _largeFileThresholdText = "0";
        private string _businessSoftware = string.Empty;
        private string _encryptedExtensionsText = string.Empty;
        private int _themePaletteIndex;

        public ObservableCollection<HealthWarning> HealthWarnings { get; } = new();
        public ObservableCollection<LogRow> LogRows { get; } = new();
        public ObservableCollection<LogRow> RecentActivity { get; } = new();
        public ObservableCollection<string> PriorityExtensionChips { get; } = new();
        public ObservableCollection<string> EncryptedExtensionChips { get; } = new();
        public ObservableCollection<string> RunningProcessNames { get; } = new();

        public string FormName
        {
            get => _formName;
            set { _formName = value; OnPropertyChanged(); MarkFormDirtyAndValidate(); }
        }

        public string FormSource
        {
            get => _formSource;
            set { _formSource = value; OnPropertyChanged(); MarkFormDirtyAndValidate(); }
        }

        public string FormTarget
        {
            get => _formTarget;
            set { _formTarget = value; OnPropertyChanged(); MarkFormDirtyAndValidate(); }
        }

        public int FormTypeIndex
        {
            get => _formTypeIndex;
            set { _formTypeIndex = value; OnPropertyChanged(); ValidateJobForm(); }
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

        public string FormTitle => IsEditing ? T("Edit") : T("NewJob");

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

        public bool HasJobs => JobRows.Count > 0;
        public bool HasNoJobs => JobRows.Count == 0;
        public bool HasWarnings => HealthWarnings.Count > 0;
        public bool HasNoWarnings => HealthWarnings.Count == 0;
        public bool HasLogs => LogRows.Count > 0;
        public bool HasNoLogs => LogRows.Count == 0;
        public int TotalJobs => JobRows.Count;
        public int RunningJobs => JobRows.Count(j => j.Status == "Running" || j.Status == "Stopping");
        public int PausedJobs => JobRows.Count(j => j.Status == "Paused");
        public int FailedJobs => JobRows.Count(j => j.Status.StartsWith("Error", StringComparison.OrdinalIgnoreCase) || j.Status == "Canceled");
        public string LastSuccessfulBackup
        {
            get => _lastSuccessfulBackup;
            private set { _lastSuccessfulBackup = value; OnPropertyChanged(); }
        }
        public string PriorityIndicator => PriorityExtensionChips.Count > 0
            ? $"{PriorityExtensionChips.Count} priority extension(s) configured"
            : "No priority extensions configured";
        public string LargeFileIndicator => LargeFileThresholdKb > 0
            ? $"Large-file lock above {LargeFileThresholdKb} KB"
            : "Large-file throttling disabled";
        public string CryptoIndicator => EncryptedExtensionChips.Count > 0
            ? $"{EncryptedExtensionChips.Count} encrypted extension(s); CryptoSoft serialized"
            : "Encryption disabled";
        public string BusinessPauseIndicator => string.IsNullOrWhiteSpace(BusinessSoftware)
            ? "Business software pause disabled"
            : $"Pauses while '{BusinessSoftware}' is running";

        public string FormNameError
        {
            get => _formNameError;
            private set { _formNameError = value; OnPropertyChanged(); }
        }

        public string FormSourceError
        {
            get => _formSourceError;
            private set { _formSourceError = value; OnPropertyChanged(); }
        }

        public string FormTargetError
        {
            get => _formTargetError;
            private set { _formTargetError = value; OnPropertyChanged(); }
        }

        public string SettingsError
        {
            get => _settingsError;
            private set { _settingsError = value; OnPropertyChanged(); }
        }

        public bool CanSaveJob => string.IsNullOrEmpty(FormNameError)
                                  && string.IsNullOrEmpty(FormSourceError)
                                  && string.IsNullOrEmpty(FormTargetError)
                                  && !IsBusy;

        public int LanguageIndex
        {
            get => _languageIndex;
            set
            {
                if (_languageIndex == value) return;
                _languageIndex = value;
                OnPropertyChanged();
                ApplyLanguage(value == 1 ? "fr" : "en");
            }
        }

        public int ThemePaletteIndex
        {
            get => _themePaletteIndex;
            set
            {
                if (_themePaletteIndex == value) return;
                _themePaletteIndex = value;
                OnPropertyChanged();
                AppThemeService.ApplyPalette(AppThemeService.GetPaletteName(value));
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
            private set
            {
                _largeFileThresholdKb = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LargeFileIndicator));
            }
        }

        public string LargeFileThresholdText
        {
            get => _largeFileThresholdText;
            set
            {
                _largeFileThresholdText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LargeFileIndicator));
            }
        }

        public string BusinessSoftware
        {
            get => _businessSoftware;
            set { _businessSoftware = value; OnPropertyChanged(); OnPropertyChanged(nameof(BusinessPauseIndicator)); }
        }

        public string EncryptedExtensionsText
        {
            get => _encryptedExtensionsText;
            set { _encryptedExtensionsText = value; OnPropertyChanged(); }
        }

        public string LogDateText
        {
            get => _logDateText;
            set { _logDateText = value; OnPropertyChanged(); }
        }

        public string LogJobFilter
        {
            get => _logJobFilter;
            set { _logJobFilter = value; OnPropertyChanged(); }
        }

        public int LogStatusFilterIndex
        {
            get => _logStatusFilterIndex;
            set { _logStatusFilterIndex = value; OnPropertyChanged(); }
        }

        public string NewPriorityExtension
        {
            get => _newPriorityExtension;
            set { _newPriorityExtension = value; OnPropertyChanged(); }
        }

        public string NewEncryptedExtension
        {
            get => _newEncryptedExtension;
            set { _newEncryptedExtension = value; OnPropertyChanged(); }
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
        public ICommand BrowseSourceCommand { get; }
        public ICommand BrowseTargetCommand { get; }
        public ICommand BrowseLogDirectoryCommand { get; }
        public ICommand RefreshLogsCommand { get; }
        public ICommand OpenLogFileCommand { get; }
        public ICommand ExportLogFileCommand { get; }
        public ICommand TestCryptoSoftCommand { get; }
        public ICommand TestCentralLoggingCommand { get; }
        public ICommand RefreshProcessesCommand { get; }
        public ICommand AddPriorityExtensionCommand { get; }
        public ICommand RemovePriorityExtensionCommand { get; }
        public ICommand AddEncryptedExtensionCommand { get; }
        public ICommand RemoveEncryptedExtensionCommand { get; }
        public ICommand PreviewJobCommand { get; }

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
            SetLargeFileThreshold(core.Settings.LargeFileThresholdKb);
            BusinessSoftware = core.Settings.BusinessSoftwareName;
            EncryptedExtensionsText = string.Join(",", core.Settings.EncryptedExtensions);
            ResetChips(PriorityExtensionChips, core.Settings.PriorityExtensions);
            ResetChips(EncryptedExtensionChips, core.Settings.EncryptedExtensions);
            ThemePaletteIndex = AppThemeService.GetPaletteIndex(core.Settings.UiThemePalette);
            LanguageIndex = string.Equals(core.Settings.UiLanguage, "fr", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            SaveJobCommand = new RelayCommand(_ => SaveJob(), _ => CanSaveJob);
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
            BrowseSourceCommand = new RelayCommand(_ => BrowseFolder(path => FormSource = path, FormSource));
            BrowseTargetCommand = new RelayCommand(_ => BrowseFolder(path => FormTarget = path, FormTarget));
            BrowseLogDirectoryCommand = new RelayCommand(_ => BrowseFolder(path => LogDirectoryText = path, LogDirectoryText));
            RefreshLogsCommand = new RelayCommand(_ => RefreshLogs());
            OpenLogFileCommand = new RelayCommand(_ => OpenCurrentLogFile());
            ExportLogFileCommand = new RelayCommand(_ => ExportCurrentLogFile());
            TestCryptoSoftCommand = new RelayCommand(_ => TestCryptoSoft());
            TestCentralLoggingCommand = new RelayCommand(_ => _ = TestCentralLoggingAsync());
            RefreshProcessesCommand = new RelayCommand(_ => RefreshProcesses());
            AddPriorityExtensionCommand = new RelayCommand(_ => AddExtensionChip(PriorityExtensionChips, NewPriorityExtension, value => NewPriorityExtension = value));
            RemovePriorityExtensionCommand = new RelayCommand(o => RemoveExtensionChip(PriorityExtensionChips, o?.ToString()));
            AddEncryptedExtensionCommand = new RelayCommand(_ => AddExtensionChip(EncryptedExtensionChips, NewEncryptedExtension, value => NewEncryptedExtension = value));
            RemoveEncryptedExtensionCommand = new RelayCommand(o => RemoveExtensionChip(EncryptedExtensionChips, o?.ToString()));
            PreviewJobCommand = new RelayCommand(o => PreviewJob(o as JobRow), _ => !IsBusy);

            RefreshJobRows();
            RefreshDashboard();
            RefreshLogs();
            RefreshProcesses();
            StartNewJob();
            StatusMessage = "Ready.";
        }

        private void SaveJob()
        {
            _isFormDirty = true;
            ValidateJobForm();
            if (!CanSaveJob)
                return;

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

            MessageBoxResult confirm = System.Windows.MessageBox.Show(
                $"Delete backup job '{row.Name}'?",
                "Confirm delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
                return;

            var (ok, err) = _core.DeleteJob(row.Index);
            StatusMessage = ok ? "Job deleted." : err;
            RefreshJobRows();
            StartNewJob();
        }

        private void ExecuteJobKickoff(object? parameter)
        {
            var row = parameter as JobRow ?? SelectedJob;
            if (row == null || IsBusy) return;
            if (!ValidateRunJob(row))
                return;
            _ = ExecuteJobAsync(row);
        }

        private void ExecuteAllKickoff()
        {
            if (IsBusy) return;
            foreach (JobRow row in JobRows)
            {
                if (!ValidateRunJob(row))
                    return;
            }
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

                row.Status = ok ? "Done" : (error == "cancelled" ? "Canceled" : $"Error: {error}");
                StatusMessage = ok ? $"{row.Name} completed." : $"{row.Name}: {error}";
                if (ok) row.Progress = 100;
            }
            finally
            {
                IsBusy = false;
                RefreshDashboard();
                RefreshLogs();
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
                RefreshDashboard();
                RefreshLogs();
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
            foreach (JobRow row in JobRows.Where(r => r.Status == "Running"))
                row.Status = "Paused";
            StatusMessage = "Pause requested for running jobs.";
        }

        private void ResumeAllJobs()
        {
            _core.ResumeAllJobs();
            foreach (JobRow row in JobRows.Where(r => r.Status == "Paused"))
                row.Status = "Running";
            StatusMessage = "Resume requested for paused jobs.";
        }

        private void StopAllJobs()
        {
            _core.StopAllJobs();
            foreach (JobRow row in JobRows.Where(r => r.Status == "Running" || r.Status == "Paused"))
                row.Status = "Stopping";
            StatusMessage = "Stop requested for running jobs.";
        }

        private void SelectJob(JobRow? row)
        {
            if (row == null) return;
            SelectedJob = row;
            IsEditing = true;
            _isFormDirty = false;
            FormName = row.Name;
            FormSource = row.Source;
            FormTarget = row.Target;
            FormTypeIndex = row.Type == BackupType.Differential.ToString() ? 1 : 0;
            ClearValidation();
        }

        private void StartNewJob()
        {
            IsEditing = false;
            SelectedJob = null;
            _isFormDirty = false;
            _formName = string.Empty;
            _formSource = string.Empty;
            _formTarget = string.Empty;
            _formTypeIndex = 0;
            OnPropertyChanged(nameof(FormName));
            OnPropertyChanged(nameof(FormSource));
            OnPropertyChanged(nameof(FormTarget));
            OnPropertyChanged(nameof(FormTypeIndex));
            OnPropertyChanged(nameof(FormTitle));
            ClearValidation();
        }

        private void SaveSettings()
        {
            if (!TryReadLargeFileThreshold(out long thresholdKb))
            {
                return;
            }

            var settings = new AppSettings
            {
                LogFormat = LogFormatIndex == 1 ? LogFormat.Xml : LogFormat.Json,
                LogDirectory = LogDirectoryText.Trim(),
                JsonLogLayout = _core.Settings.JsonLogLayout,
                LogDestinationMode = LogDestinationModeIndex switch
                {
                    1 => LogDestinationMode.CentralOnly,
                    2 => LogDestinationMode.LocalAndCentral,
                    _ => LogDestinationMode.LocalOnly
                },
                CentralLogEndpoint = CentralLogEndpoint.Trim(),
                CentralClientId = CentralClientId.Trim(),
                PriorityExtensions = PriorityExtensionChips.ToList(),
                LargeFileThresholdKb = thresholdKb,
                EncryptedExtensions = EncryptedExtensionChips.ToList(),
                BusinessSoftwareName = BusinessSoftware.Trim(),
                UiLanguage = LanguageIndex == 1 ? "fr" : "en",
                UiThemePalette = AppThemeService.GetPaletteName(ThemePaletteIndex)
            };

            _core.UpdateSettings(settings);
            SetLargeFileThreshold(thresholdKb);
            PriorityExtensionsText = string.Join(",", PriorityExtensionChips);
            EncryptedExtensionsText = string.Join(",", EncryptedExtensionChips);
            SettingsError = string.Empty;
            StatusMessage = "Settings saved.";
            RefreshDashboard();
            RefreshLogs();
        }

        private void OnCoreStateChanged(int zeroBasedIndex, BackupState state)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
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
                row.CurrentFileName = Path.GetFileName(state.CurrentSourceFile);
                row.LastActionDisplay = FormatTimestamp(state.LastActionTimestamp);
                row.RemainingFiles = state.RemainingFiles;
                row.RemainingSizeDisplay = FormatBytes(state.RemainingSize);
                row.PauseReason = row.Status == "Paused"
                    ? BuildPauseReason(state)
                    : "-";
                if (state.State == BackupStateType.End)
                    LastSuccessfulBackup = FormatTimestamp(state.LastActionTimestamp);
                RefreshDashboard();
            });
        }

        private void RefreshJobRows()
        {
            JobRows.Clear();
            for (int i = 0; i < _core.Jobs.Count; i++)
            {
                BackupJob job = _core.Jobs[i];
                bool sourceExists = Directory.Exists(job.SourceDirectory);
                bool targetExists = Directory.Exists(job.TargetDirectory);
                JobRows.Add(new JobRow
                {
                    Index = i + 1,
                    Name = job.Name,
                    Type = job.Type.ToString(),
                    Source = job.SourceDirectory,
                    Target = job.TargetDirectory,
                    Status = "Idle",
                    PauseReason = sourceExists && targetExists
                        ? "-"
                        : "Path check needed"
                });
            }
            OnPropertyChanged(nameof(HasJobs));
            OnPropertyChanged(nameof(HasNoJobs));
            RefreshDashboard();
            CommandManager.InvalidateRequerySuggested();
        }

        private void MarkFormDirtyAndValidate()
        {
            _isFormDirty = true;
            ValidateJobForm();
        }

        private void ValidateJobForm()
        {
            if (!_isFormDirty)
                return;

            int selectedIndex = SelectedJob?.Index ?? 0;
            FormNameError = string.IsNullOrWhiteSpace(FormName)
                ? "Job name is required."
                : _core.Jobs.Select((job, i) => new { Job = job, Index = i + 1 })
                    .Any(item => item.Index != selectedIndex
                                 && item.Job.Name.Equals(FormName.Trim(), StringComparison.OrdinalIgnoreCase))
                    ? "A job with this name already exists."
                    : string.Empty;

            FormSourceError = string.IsNullOrWhiteSpace(FormSource)
                ? "Source folder is required."
                : string.Empty;

            FormTargetError = string.IsNullOrWhiteSpace(FormTarget)
                ? "Target folder is required."
                : string.Empty;

            OnPropertyChanged(nameof(CanSaveJob));
            CommandManager.InvalidateRequerySuggested();
        }

        private void ClearValidation()
        {
            FormNameError = string.Empty;
            FormSourceError = string.Empty;
            FormTargetError = string.Empty;
            OnPropertyChanged(nameof(CanSaveJob));
            CommandManager.InvalidateRequerySuggested();
        }

        private bool TryReadLargeFileThreshold(out long thresholdKb)
        {
            string raw = LargeFileThresholdText.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                thresholdKb = 0;
                SettingsError = "Large file threshold is required. Use 0 to disable it.";
                return false;
            }

            if (!long.TryParse(raw, out thresholdKb) || thresholdKb < 0)
            {
                SettingsError = "Large file threshold must be a whole number greater than or equal to zero.";
                return false;
            }

            return true;
        }

        private void SetLargeFileThreshold(long thresholdKb)
        {
            LargeFileThresholdKb = thresholdKb;
            _largeFileThresholdText = thresholdKb.ToString();
            OnPropertyChanged(nameof(LargeFileThresholdText));
        }

        private void RefreshDashboard()
        {
            HealthWarnings.Clear();
            foreach (BackupJob job in _core.Jobs)
            {
                if (!Directory.Exists(job.SourceDirectory))
                {
                    HealthWarnings.Add(new HealthWarning
                    {
                        Title = $"Missing source: {job.Name}",
                        Detail = job.SourceDirectory
                    });
                }

                if (!Directory.Exists(job.TargetDirectory))
                {
                    HealthWarnings.Add(new HealthWarning
                    {
                        Title = $"Target not ready: {job.Name}",
                        Detail = job.TargetDirectory
                    });
                }
            }

            string cryptoSoftPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CryptoSoft.exe");
            if (EncryptedExtensionChips.Count > 0 && !File.Exists(cryptoSoftPath))
            {
                HealthWarnings.Add(new HealthWarning
                {
                    Title = "CryptoSoft unavailable",
                    Detail = cryptoSoftPath
                });
            }

            if (LogDestinationModeIndex != 0 && string.IsNullOrWhiteSpace(CentralLogEndpoint))
            {
                HealthWarnings.Add(new HealthWarning
                {
                    Title = "Central logging endpoint missing",
                    Detail = "Central logging is selected but no endpoint is configured."
                });
            }

            if (!string.IsNullOrWhiteSpace(BusinessSoftware) && new BusinessSoftwareService().IsRunning(BusinessSoftware))
            {
                HealthWarnings.Add(new HealthWarning
                {
                    Title = "Business software detected",
                    Detail = $"Backups pause while '{BusinessSoftware}' is running."
                });
            }

            OnPropertyChanged(nameof(TotalJobs));
            OnPropertyChanged(nameof(RunningJobs));
            OnPropertyChanged(nameof(PausedJobs));
            OnPropertyChanged(nameof(FailedJobs));
            OnPropertyChanged(nameof(HasWarnings));
            OnPropertyChanged(nameof(HasNoWarnings));
            OnPropertyChanged(nameof(PriorityIndicator));
            OnPropertyChanged(nameof(LargeFileIndicator));
            OnPropertyChanged(nameof(CryptoIndicator));
            OnPropertyChanged(nameof(BusinessPauseIndicator));
        }

        private void RefreshLogs()
        {
            try
            {
                LogRows.Clear();
                RecentActivity.Clear();

                DateTime date = DateTime.TryParse(LogDateText, out DateTime parsed)
                    ? parsed.Date
                    : DateTime.Today;

                string logDir = LogDirectoryResolver.Resolve(LogDirectoryText);
                foreach (LogRow row in LoadLogRows(logDir, date)
                             .Where(PassesLogFilters)
                             .OrderByDescending(r => r.Time))
                {
                    LogRows.Add(row);
                    if (RecentActivity.Count < 6)
                        RecentActivity.Add(row);
                }

                LogDateText = date.ToString("yyyy-MM-dd");
                if (RecentActivity.FirstOrDefault(r => r.Result == "OK") is { } success)
                    LastSuccessfulBackup = success.Time;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                StatusMessage = $"Log refresh skipped: {ex.Message}";
            }
            finally
            {
                OnPropertyChanged(nameof(HasLogs));
                OnPropertyChanged(nameof(HasNoLogs));
            }
        }

        private IEnumerable<LogRow> LoadLogRows(string logDir, DateTime date)
        {
            string jsonFile = Path.Combine(logDir, $"{date:yyyy-MM-dd}.json");
            if (File.Exists(jsonFile))
            {
                foreach (LogRow row in LoadJsonLogRows(jsonFile))
                    yield return row;
            }

            string xmlFile = Path.Combine(logDir, $"{date:yyyy-MM-dd}.xml");
            if (File.Exists(xmlFile))
            {
                foreach (LogRow row in LoadXmlLogRows(xmlFile))
                    yield return row;
            }
        }

        private static IEnumerable<LogRow> LoadJsonLogRows(string file)
        {
            string json = ReadSharedText(file);
            if (string.IsNullOrWhiteSpace(json))
                yield break;

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException)
            {
                yield break;
            }

            using (document)
            {
            IEnumerable<JsonElement> entries = document.RootElement.ValueKind switch
            {
                JsonValueKind.Array => document.RootElement.EnumerateArray(),
                JsonValueKind.Object => new[] { document.RootElement },
                _ => Enumerable.Empty<JsonElement>()
            };

            foreach (JsonElement entry in entries)
                yield return BuildLogRow(
                    GetJsonString(entry, "Timestamp"),
                    GetJsonString(entry, "BackupJobName"),
                    GetJsonString(entry, "SourceFilePath"),
                    GetJsonString(entry, "TargetFilePath"),
                    GetJsonLong(entry, "FileSize"),
                    GetJsonLong(entry, "TransferTimeMs"),
                    GetJsonLong(entry, "EncryptionTimeMs"));
            }
        }

        private static IEnumerable<LogRow> LoadXmlLogRows(string file)
        {
            string xml = ReadSharedText(file);
            if (string.IsNullOrWhiteSpace(xml))
                yield break;

            XDocument document;
            try
            {
                document = XDocument.Parse(xml);
            }
            catch
            {
                yield break;
            }

            foreach (XElement entry in document.Descendants("LogEntry"))
            {
                yield return BuildLogRow(
                    (string?)entry.Element("Timestamp") ?? string.Empty,
                    (string?)entry.Element("BackupJobName") ?? string.Empty,
                    (string?)entry.Element("SourceFilePath") ?? string.Empty,
                    (string?)entry.Element("TargetFilePath") ?? string.Empty,
                    ParseLong((string?)entry.Element("FileSize")),
                    ParseLong((string?)entry.Element("TransferTimeMs")),
                    ParseLong((string?)entry.Element("EncryptionTimeMs")));
            }
        }

        private static string ReadSharedText(string file)
        {
            using var stream = new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private bool PassesLogFilters(LogRow row)
        {
            if (!string.IsNullOrWhiteSpace(LogJobFilter)
                && !row.Job.Contains(LogJobFilter.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;

            return LogStatusFilterIndex switch
            {
                1 => row.Result == "OK",
                2 => row.Result == "Error",
                _ => true
            };
        }

        private static LogRow BuildLogRow(
            string timestamp,
            string job,
            string source,
            string target,
            long size,
            long transferTime,
            long encryptionTime)
        {
            bool isError = transferTime < 0 || encryptionTime < 0;
            return new LogRow
            {
                Time = DateTime.TryParse(timestamp, out DateTime dt) ? dt.ToString("HH:mm:ss") : timestamp,
                Job = job,
                Source = source,
                Target = target,
                Size = FormatBytes(size),
                TransferTime = $"{transferTime} ms",
                EncryptionTime = $"{encryptionTime} ms",
                Result = isError ? "Error" : "OK",
                IsError = isError
            };
        }

        private void OpenCurrentLogFile()
        {
            string path = GetCurrentLogFilePath();
            if (!File.Exists(path))
            {
                StatusMessage = "No log file found for the selected date and format.";
                return;
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private void ExportCurrentLogFile()
        {
            string path = GetCurrentLogFilePath();
            if (!File.Exists(path))
            {
                StatusMessage = "No log file found for the selected date and format.";
                return;
            }

            using var dialog = new Forms.SaveFileDialog
            {
                FileName = Path.GetFileName(path),
                Filter = LogFormatIndex == 1 ? "XML log (*.xml)|*.xml|All files (*.*)|*.*" : "JSON log (*.json)|*.json|All files (*.*)|*.*",
                OverwritePrompt = true
            };

            if (dialog.ShowDialog() == Forms.DialogResult.OK)
            {
                try
                {
                    string sourcePath = Path.GetFullPath(path);
                    string destinationPath = Path.GetFullPath(dialog.FileName);
                    if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
                    {
                        StatusMessage = "Export skipped: source and destination are the same file.";
                        return;
                    }

                    CopyLogFile(sourcePath, destinationPath);
                    StatusMessage = $"Log exported to {dialog.FileName}.";
                }
                catch (IOException ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
                catch (UnauthorizedAccessException)
                {
                    StatusMessage = "Export failed: EasySave does not have permission to write to that location.";
                }
            }
        }

        private static void CopyLogFile(string sourcePath, string destinationPath)
        {
            string? destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);

            using var source = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            using var destination = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read);

            source.CopyTo(destination);
        }

        private string GetCurrentLogFilePath()
        {
            DateTime date = DateTime.TryParse(LogDateText, out DateTime parsed) ? parsed.Date : DateTime.Today;
            string logDir = LogDirectoryResolver.Resolve(LogDirectoryText);
            string extension = LogFormatIndex == 1 ? "xml" : "json";
            return Path.Combine(logDir, $"{date:yyyy-MM-dd}.{extension}");
        }

        private void TestCryptoSoft()
        {
            string cryptoSoftPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CryptoSoft.exe");
            StatusMessage = File.Exists(cryptoSoftPath)
                ? "CryptoSoft is available."
                : $"CryptoSoft not found: {cryptoSoftPath}";
            RefreshDashboard();
        }

        private async Task TestCentralLoggingAsync()
        {
            if (string.IsNullOrWhiteSpace(CentralLogEndpoint))
            {
                StatusMessage = "Central endpoint is empty.";
                return;
            }

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var payload = new
                {
                    clientId = string.IsNullOrWhiteSpace(CentralClientId) ? Environment.MachineName : CentralClientId.Trim(),
                    format = LogFormatIndex == 1 ? "Xml" : "Json",
                    timestamp = DateTime.Now.ToString("o"),
                    entry = new
                    {
                        Timestamp = DateTime.Now.ToString("o"),
                        BackupJobName = "Connectivity test",
                        SourceFilePath = "EasySave.GUI",
                        TargetFilePath = "LogCentralizer",
                        FileSize = 0,
                        TransferTimeMs = 0,
                        EncryptionTimeMs = 0
                    }
                };

                using HttpResponseMessage response = await client.PostAsJsonAsync(CentralLogEndpoint.Trim(), payload);
                StatusMessage = response.IsSuccessStatusCode
                    ? "Central logging endpoint answered successfully."
                    : $"Central logging test failed: {(int)response.StatusCode}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Central logging test failed: {ex.Message}";
            }

            RefreshDashboard();
        }

        private void RefreshProcesses()
        {
            RunningProcessNames.Clear();
            foreach (string processName in Process.GetProcesses()
                         .Select(p => p.ProcessName)
                         .Where(name => !string.IsNullOrWhiteSpace(name))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(name => name))
            {
                RunningProcessNames.Add(processName);
            }
        }

        private void PreviewJob(JobRow? row)
        {
            if (row == null) return;
            if (!Directory.Exists(row.Source))
            {
                StatusMessage = $"{row.Name}: source folder is missing.";
                return;
            }

            int fileCount = Directory.GetFiles(row.Source, "*", SearchOption.AllDirectories).Length;
            long totalSize = Directory.GetFiles(row.Source, "*", SearchOption.AllDirectories)
                .Sum(path => new FileInfo(path).Length);
            StatusMessage = $"{row.Name} preview: {fileCount} file(s), {FormatBytes(totalSize)} to inspect/copy.";
        }

        private bool ValidateRunJob(JobRow row)
        {
            if (!Directory.Exists(row.Source))
            {
                StatusMessage = $"{row.Name}: source folder does not exist.";
                return false;
            }

            if (!TryEnsureDirectory(row.Target))
            {
                StatusMessage = $"{row.Name}: target folder is not accessible.";
                return false;
            }

            string source = NormalizeDirectoryPath(row.Source);
            string target = NormalizeDirectoryPath(row.Target);
            if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = $"{row.Name}: source and target cannot be the same folder.";
                return false;
            }

            if (target.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = $"{row.Name}: target cannot be inside the source folder.";
                return false;
            }

            return true;
        }

        private static bool TryEnsureDirectory(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                return Directory.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeDirectoryPath(string path)
            => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        private void AddExtensionChip(ObservableCollection<string> chips, string rawValue, Action<string> clearInput)
        {
            string ext = NormalizeExtension(rawValue);
            if (string.IsNullOrWhiteSpace(ext))
                return;

            if (!chips.Contains(ext, StringComparer.OrdinalIgnoreCase))
                chips.Add(ext);

            clearInput(string.Empty);
            SyncExtensionTexts();
        }

        private void RemoveExtensionChip(ObservableCollection<string> chips, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            string? existing = chips.FirstOrDefault(c => c.Equals(value, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                chips.Remove(existing);

            SyncExtensionTexts();
        }

        private void SyncExtensionTexts()
        {
            PriorityExtensionsText = string.Join(",", PriorityExtensionChips);
            EncryptedExtensionsText = string.Join(",", EncryptedExtensionChips);
            OnPropertyChanged(nameof(PriorityIndicator));
            OnPropertyChanged(nameof(CryptoIndicator));
            RefreshDashboard();
        }

        private static void ResetChips(ObservableCollection<string> chips, IEnumerable<string> values)
        {
            chips.Clear();
            foreach (string value in values.Select(NormalizeExtension)
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                chips.Add(value);
            }
        }

        private static string NormalizeExtension(string raw)
        {
            string ext = raw.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(ext))
                return string.Empty;

            return ext.StartsWith('.') ? ext : "." + ext;
        }

        private static string BuildPauseReason(BackupState state)
        {
            if (!string.IsNullOrWhiteSpace(state.PauseReason))
                return state.PauseReason;

            if (string.IsNullOrWhiteSpace(state.CurrentSourceFile))
                return "Waiting for coordination rule or pause request.";

            return $"Paused before/after {Path.GetFileName(state.CurrentSourceFile)}";
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double value = Math.Max(0, bytes);
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.0} {units[unit]}";
        }

        private static string GetJsonString(JsonElement element, string propertyName)
            => element.TryGetProperty(propertyName, out JsonElement property)
                ? property.GetString() ?? string.Empty
                : string.Empty;

        private static long GetJsonLong(JsonElement element, string propertyName)
            => element.TryGetProperty(propertyName, out JsonElement property) && property.TryGetInt64(out long value)
                ? value
                : 0;

        private static long ParseLong(string? value)
            => long.TryParse(value, out long parsed) ? parsed : 0;

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

        private static string FormatTimestamp(string timestamp)
        {
            return DateTime.TryParse(timestamp, out DateTime value)
                ? value.ToString("HH:mm:ss")
                : "-";
        }

        private static void BrowseFolder(Action<string> assign, string currentPath)
        {
            using var dialog = new Forms.FolderBrowserDialog
            {
                ShowNewFolderButton = true,
                SelectedPath = Directory.Exists(currentPath) ? currentPath : string.Empty
            };

            if (dialog.ShowDialog() == Forms.DialogResult.OK)
                assign(dialog.SelectedPath);
        }

        private static void ApplyLanguage(string culture)
        {
            string source = culture == "fr"
                ? "Localization/Strings.fr.xaml"
                : "Localization/Strings.en.xaml";

            ResourceDictionary dictionary = new()
            {
                Source = new Uri(source, UriKind.Relative)
            };

            var merged = System.Windows.Application.Current.Resources.MergedDictionaries;
            ResourceDictionary? existing = merged.FirstOrDefault(d =>
                d.Source != null && d.Source.OriginalString.Contains("Localization/Strings.", StringComparison.OrdinalIgnoreCase));

            if (existing != null)
                merged.Remove(existing);

            merged.Insert(0, dictionary);
        }

        private static string T(string key)
            => System.Windows.Application.Current.TryFindResource(key)?.ToString() ?? key;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
