using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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

        // ── Settings fields ────────────────────────────────────────────────
        private int    _logFormatIndex;   // 0=JSON, 1=XML
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

        public MainViewModel(BackupViewModel core)
        {
            _core = core;

            // Load settings into form
            LogFormatIndex           = core.Settings.LogFormat == LogFormat.Xml ? 1 : 0;
            BusinessSoftware         = core.Settings.BusinessSoftwareName;
            EncryptedExtensionsText  = string.Join(",", core.Settings.EncryptedExtensions);

            SaveJobCommand     = new RelayCommand(_ => SaveJob());
            DeleteJobCommand   = new RelayCommand(_ => DeleteSelected(), _ => SelectedJob != null);
            ExecuteJobCommand  = new RelayCommand(_ => ExecuteSelected(), _ => SelectedJob != null);
            ExecuteAllCommand  = new RelayCommand(_ => ExecuteAll());
            SelectJobCommand   = new RelayCommand(o => SelectJob(o as JobRow));
            NewJobCommand      = new RelayCommand(_ => StartNewJob());
            SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
            NavigateCommand    = new RelayCommand(o => CurrentPage = o?.ToString() ?? "Jobs");

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

        private void ExecuteSelected()
        {
            if (SelectedJob == null) return;
            var row = SelectedJob;
            row.Status   = "Running…";
            row.Progress = 0;

            var (ok, err) = _core.ExecuteJob(row.Index);

            row.Status   = ok ? "Done ✔" : $"Error: {err}";
            row.Progress = 100;
            StatusMessage = ok
                ? $"✔ Job '{row.Name}' completed."
                : $"✖ Job '{row.Name}': {err}";
        }

        private void ExecuteAll()
        {
            foreach (var row in JobRows)
            {
                row.Status   = "Running…";
                row.Progress = 0;
            }

            var (allOk, errors) = _core.ExecuteAllJobs();

            foreach (var row in JobRows)
            {
                row.Status   = "Done ✔";
                row.Progress = 100;
            }

            StatusMessage = allOk
                ? "✔ All jobs completed."
                : $"✖ Completed with errors: {string.Join(", ", errors)}";
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
                EncryptedExtensions  = extensions,
                BusinessSoftwareName = BusinessSoftware.Trim()
            };
            _core.UpdateSettings(settings);
            StatusMessage = "✔ Settings saved.";
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
