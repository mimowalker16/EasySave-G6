using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EasyLog;
using EasySave.Core.Models;
using EasySave.Core.Services;
using EasySave.Core.ViewModels;
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

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
        private string _currentPage = "Jobs";
        private string _formNameError = string.Empty;
        private string _formSourceError = string.Empty;
        private string _formTargetError = string.Empty;
        private string _settingsError = string.Empty;

        private int _languageIndex;
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
        public ICommand BrowseSourceCommand { get; }
        public ICommand BrowseTargetCommand { get; }
        public ICommand BrowseLogDirectoryCommand { get; }

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

            RefreshJobRows();
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

                row.Status = ok ? "Done" : (error == "cancelled" ? "Canceled" : $"Error: {error}");
                StatusMessage = ok ? $"{row.Name} completed." : $"{row.Name}: {error}";
                if (ok) row.Progress = 100;
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
            if (LargeFileThresholdKb < 0)
            {
                SettingsError = "Large file threshold must be zero or greater.";
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
                PriorityExtensions = ParseExtensions(PriorityExtensionsText),
                LargeFileThresholdKb = LargeFileThresholdKb,
                EncryptedExtensions = ParseExtensions(EncryptedExtensionsText),
                BusinessSoftwareName = BusinessSoftware.Trim()
            };

            _core.UpdateSettings(settings);
            SettingsError = string.Empty;
            StatusMessage = "Settings saved.";
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
            OnPropertyChanged(nameof(HasJobs));
            OnPropertyChanged(nameof(HasNoJobs));
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
