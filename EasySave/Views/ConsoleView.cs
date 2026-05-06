using System;
using System.Collections.Generic;
using System.IO;
using EasySave.Core.Localization;
using EasySave.Core.Models;
using EasySave.Core.Services;
using EasySave.Core.ViewModels;
using EasyLog;

namespace EasySave.Views
{
    /// <summary>
    /// Console-based user interface for EasySave v1.1.
    /// Handles all user interaction; delegates all business logic to <see cref="BackupViewModel"/>.
    /// No business logic lives here — only display and input.
    /// </summary>
    public class ConsoleView
    {
        private readonly BackupViewModel _vm;
        private readonly LanguageManager _lang;

        public ConsoleView(BackupViewModel vm, LanguageManager lang)
        {
            _vm   = vm;
            _lang = lang;
        }

        /// <summary>Starts the interactive console menu loop.</summary>
        public void Run()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            while (true)
            {
                PrintHeader();
                PrintMenu();

                string choice = Prompt(_lang.Get("prompt_choice")).Trim();
                TryClear();

                switch (choice)
                {
                    case "1": ShowJobs();       break;
                    case "2": CreateJob();      break;
                    case "3": EditJob();        break;
                    case "4": DeleteJob();      break;
                    case "5": ExecuteOne();     break;
                    case "6": ExecuteAll();     break;
                    case "7": ChangeLanguage(); break;
                    case "8": ShowSettings();   break;
                    case "9":
                        PrintBye();
                        return;
                    default:
                        PrintWarning(_lang.Get("label_invalid_choice"));
                        WaitEnter();
                        break;
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Menu actions
        // ──────────────────────────────────────────────────────────────────────

        private void ShowJobs()
        {
            PrintSectionTitle("[ LIST ]");
            if (_vm.Jobs.Count == 0)
            {
                PrintInfo(_lang.Get("label_no_jobs"));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(_lang.Get("label_job_list_header"));
                Console.WriteLine(_lang.Get("label_job_list_sep"));
                Console.ResetColor();

                for (int i = 0; i < _vm.Jobs.Count; i++)
                {
                    BackupJob j        = _vm.Jobs[i];
                    string    typeName = j.Type == BackupType.Full
                        ? _lang.Get("label_full")
                        : _lang.Get("label_differential");

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write($"  {i + 1} ");
                    Console.ResetColor();
                    Console.WriteLine($"| {j.Name,-20} | {typeName,-13} | {j.SourceDirectory,-32}| {j.TargetDirectory}");
                }
            }
            WaitEnter();
        }

        private void CreateJob()
        {
            PrintSectionTitle("[ CREATE JOB ]");

            string     name   = Prompt(_lang.Get("prompt_job_name"));
            string     source = Prompt(_lang.Get("prompt_source"));
            string     target = Prompt(_lang.Get("prompt_target"));
            BackupType type   = PromptBackupType();

            var (success, error) = _vm.CreateJob(name, source, target, type);
            if (success)
                PrintSuccess(_lang.Get("label_created"));
            else
                PrintError(TranslateError(error));

            WaitEnter();
        }

        private void EditJob()
        {
            PrintSectionTitle("[ EDIT JOB ]");

            if (_vm.Jobs.Count == 0) { PrintInfo(_lang.Get("label_no_jobs")); WaitEnter(); return; }

            int idx = PromptIndex();
            if (idx < 1) { WaitEnter(); return; }

            BackupJob existing = _vm.Jobs[idx - 1];
            Console.WriteLine($"  Editing: {existing.Name}");
            Console.WriteLine("  (Leave blank to keep current value)");
            Console.WriteLine();

            string     name   = PromptOrKeep(_lang.Get("prompt_job_name"), existing.Name);
            string     source = PromptOrKeep(_lang.Get("prompt_source"),   existing.SourceDirectory);
            string     target = PromptOrKeep(_lang.Get("prompt_target"),   existing.TargetDirectory);
            BackupType type   = PromptBackupTypeOrKeep(existing.Type);

            var (success, error) = _vm.EditJob(idx, name, source, target, type);
            if (success)
                PrintSuccess(_lang.Get("label_updated"));
            else
                PrintError(TranslateError(error));

            WaitEnter();
        }

        private void DeleteJob()
        {
            PrintSectionTitle("[ DELETE JOB ]");

            if (_vm.Jobs.Count == 0) { PrintInfo(_lang.Get("label_no_jobs")); WaitEnter(); return; }

            int idx = PromptIndex();
            if (idx < 1) { WaitEnter(); return; }

            var (success, error) = _vm.DeleteJob(idx);
            if (success)
                PrintSuccess(_lang.Get("label_deleted"));
            else
                PrintError(TranslateError(error));

            WaitEnter();
        }

        private void ExecuteOne()
        {
            PrintSectionTitle("[ EXECUTE JOB ]");

            if (_vm.Jobs.Count == 0) { PrintInfo(_lang.Get("label_no_jobs")); WaitEnter(); return; }

            ShowJobsInline();

            int idx = PromptIndex();
            if (idx < 1) { WaitEnter(); return; }

            string jobName = _vm.Jobs[idx - 1].Name;
            Console.WriteLine();
            PrintInfo(_lang.Get("label_executing", jobName));

            var (success, error) = _vm.ExecuteJob(idx);

            if (success)
                PrintSuccess(_lang.Get("label_done"));
            else if (error == "errors")
                PrintWarning(_lang.Get("label_done_errors"));
            else if (error == "cancelled")
                PrintWarning(_lang.Get("label_cancelled"));
            else
                PrintError(TranslateError(error));

            WaitEnter();
        }

        private void ExecuteAll()
        {
            PrintSectionTitle("[ EXECUTE ALL ]");

            if (_vm.Jobs.Count == 0) { PrintInfo(_lang.Get("label_no_jobs")); WaitEnter(); return; }

            Console.WriteLine();
            var (allOk, errors) = _vm.ExecuteAllJobs();

            if (allOk)
                PrintSuccess(_lang.Get("label_done"));
            else
            {
                bool anyCancel = errors.Exists(s => s.Contains("cancelled", StringComparison.OrdinalIgnoreCase));
                PrintWarning(anyCancel ? _lang.Get("label_cancelled") : _lang.Get("label_done_errors"));
                foreach (string e in errors)
                    PrintError($"  • {TranslateError(e)}");
            }

            WaitEnter();
        }

        private void ChangeLanguage()
        {
            PrintSectionTitle("[ LANGUAGE ]");
            Console.WriteLine(_lang.Get("prompt_language"));

            string choice = Prompt("> ").Trim();
            if (choice == "1")
            {
                _lang.SetLanguage("en");
                PrintSuccess(_lang.Get("label_language_changed"));
            }
            else if (choice == "2")
            {
                _lang.SetLanguage("fr");
                PrintSuccess(_lang.Get("label_language_changed"));
            }
            else
            {
                PrintWarning(_lang.Get("label_invalid_choice"));
            }

            WaitEnter();
        }

        private void ShowSettings()
        {
            PrintSectionTitle(_lang.Get("label_settings_title"));

            // Current log format
            string currentFormat = _vm.Settings.LogFormat == LogFormat.Json ? "JSON" : "XML";
            PrintInfo(_lang.Get("label_current_log_format", currentFormat));

            // Choose new format
            Console.WriteLine(_lang.Get("prompt_log_format"));
            string formatChoice = Prompt("> ").Trim();
            LogFormat newFormat = formatChoice == "2" ? LogFormat.Xml : LogFormat.Json;

            string logDir = PromptOrKeep(_lang.Get("prompt_log_directory"), _vm.Settings.LogDirectory);

            Console.WriteLine(_lang.Get("prompt_json_layout"));
            string layoutChoice = Prompt("> ").Trim();
            JsonLogLayout jsonLayout = layoutChoice == "2" ? JsonLogLayout.Ndjson : JsonLogLayout.PrettyArray;

            Console.WriteLine(_lang.Get("prompt_log_destination_mode"));
            string destinationChoice = Prompt("> ").Trim();
            LogDestinationMode destinationMode = destinationChoice switch
            {
                "2" => LogDestinationMode.CentralOnly,
                "3" => LogDestinationMode.LocalAndCentral,
                _ => LogDestinationMode.LocalOnly
            };

            string centralEndpoint = PromptOrKeep(
                _lang.Get("prompt_central_endpoint"),
                _vm.Settings.CentralLogEndpoint);

            string centralClientId = PromptOrKeep(
                _lang.Get("prompt_central_client_id"),
                _vm.Settings.CentralClientId);

            string priorityCsv = PromptOrKeep(
                _lang.Get("prompt_priority_extensions"),
                string.Join(",", _vm.Settings.PriorityExtensions));

            string thresholdRaw = PromptOrKeep(
                _lang.Get("prompt_large_threshold_kb"),
                _vm.Settings.LargeFileThresholdKb.ToString());
            long thresholdKb = 0;
            _ = long.TryParse(thresholdRaw, out thresholdKb);
            if (thresholdKb < 0) thresholdKb = 0;

            // Business software name
            string businessSoftware = PromptOrKeep(
                _lang.Get("label_business_software"),
                _vm.Settings.BusinessSoftwareName);

            // Apply settings
            var updated = new AppSettings
            {
                LogFormat            = newFormat,
                LogDirectory         = string.IsNullOrWhiteSpace(logDir) ? string.Empty : logDir.Trim(),
                JsonLogLayout        = jsonLayout,
                LogDestinationMode   = destinationMode,
                CentralLogEndpoint   = centralEndpoint.Trim(),
                CentralClientId      = centralClientId.Trim(),
                PriorityExtensions   = ParseExtensionsCsv(priorityCsv),
                LargeFileThresholdKb = thresholdKb,
                EncryptedExtensions  = _vm.Settings.EncryptedExtensions,
                BusinessSoftwareName = businessSoftware.Trim()
            };

            _vm.UpdateSettings(updated);
            PrintSuccess(_lang.Get("label_log_format_changed"));
            WaitEnter();
        }

        // ──────────────────────────────────────────────────────────────────────
        // UI Helpers
        // ──────────────────────────────────────────────────────────────────────

        private static void TryClear()
        {
            try { Console.Clear(); } catch (IOException) { /* Non-interactive terminal — skip */ }
        }

        private void PrintHeader()
        {
            TryClear();
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║         ███████╗ █████╗ ███████╗██╗   ██╗               ║");
            Console.WriteLine("║         ██╔════╝██╔══██╗██╔════╝╚██╗ ██╔╝               ║");
            Console.WriteLine("║         █████╗  ███████║███████╗ ╚████╔╝                ║");
            Console.WriteLine("║         ██╔══╝  ██╔══██║╚════██║  ╚██╔╝                 ║");
            Console.WriteLine("║         ███████╗██║  ██║███████║   ██║                  ║");
            Console.WriteLine("║         ╚══════╝╚═╝  ╚═╝╚══════╝   ╚═╝  v1.1           ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════╣");
            Console.ResetColor();
        }

        private void PrintMenu()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"║  {_lang.Get("menu_title"),-56}║");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("╠══════════════════════════════════════════════════════════╣");
            Console.ResetColor();

            string[] keys = { "menu_1", "menu_2", "menu_3", "menu_4",
                               "menu_5", "menu_6", "menu_7", "menu_8", "menu_9" };
            foreach (string key in keys)
                Console.WriteLine($"║  {_lang.Get(key),-56}║");

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
        }

        private void PrintSectionTitle(string title)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"\n  {title}");
            Console.WriteLine(new string('─', 60));
            Console.ResetColor();
        }

        private static void PrintSuccess(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  ✔ {msg}");
            Console.ResetColor();
        }

        private static void PrintInfo(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"\n  ℹ {msg}");
            Console.ResetColor();
        }

        private static void PrintWarning(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n  ⚠ {msg}");
            Console.ResetColor();
        }

        private static void PrintError(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n  ✖ {msg}");
            Console.ResetColor();
        }

        private static void PrintBye()
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("\n  Goodbye / Au revoir!\n");
            Console.ResetColor();
        }

        private static string Prompt(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"\n  {message}");
            Console.ResetColor();
            return Console.ReadLine() ?? string.Empty;
        }

        private static string PromptOrKeep(string message, string current)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"\n  {message}[{current}] ");
            Console.ResetColor();
            string input = Console.ReadLine() ?? string.Empty;
            return string.IsNullOrWhiteSpace(input) ? current : input.Trim();
        }

        private BackupType PromptBackupType()
        {
            string t = Prompt(_lang.Get("prompt_type")).Trim();
            return t == "2" ? BackupType.Differential : BackupType.Full;
        }

        private BackupType PromptBackupTypeOrKeep(BackupType current)
        {
            string currentLabel = current == BackupType.Full
                ? _lang.Get("label_full")
                : _lang.Get("label_differential");

            string t = PromptOrKeep(_lang.Get("prompt_type"), currentLabel);

            if (t == "2" || t.Equals("differential", StringComparison.OrdinalIgnoreCase))
                return BackupType.Differential;
            if (t == "1" || t.Equals("full", StringComparison.OrdinalIgnoreCase))
                return BackupType.Full;
            return current;
        }

        private static List<string> ParseExtensionsCsv(string csv)
        {
            var result = new List<string>();
            foreach (string raw in csv.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string ext = raw.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(ext)) continue;
                result.Add(ext.StartsWith('.') ? ext : "." + ext);
            }
            return result;
        }

        private int PromptIndex()
        {
            string raw = Prompt(_lang.Get("prompt_job_index")).Trim();
            if (int.TryParse(raw, out int idx) && idx >= 1 && idx <= _vm.Jobs.Count)
                return idx;

            PrintError(_lang.Get("label_invalid_index"));
            return -1;
        }

        private void ShowJobsInline()
        {
            for (int i = 0; i < _vm.Jobs.Count; i++)
            {
                BackupJob j        = _vm.Jobs[i];
                string    typeName = j.Type == BackupType.Full
                    ? _lang.Get("label_full")
                    : _lang.Get("label_differential");

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"  {i + 1}. ");
                Console.ResetColor();
                Console.WriteLine($"{j.Name} ({typeName})");
            }
        }

        private void WaitEnter()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"\n  {_lang.Get("label_press_enter")}");
            Console.ResetColor();
            Console.ReadLine();
        }

        private string TranslateError(string error)
        {
            if (error.Contains(':'))
            {
                int idx = error.IndexOf(':');
                string head = error[..idx].Trim();
                string tail = error[(idx + 1)..].Trim();
                return $"{head}: {TranslateError(tail)}";
            }

            return error switch
            {
                "max_jobs"                  => _lang.Get("label_max_jobs"),
                "name_empty"                => _lang.Get("label_name_empty"),
                "name_exists"               => _lang.Get("label_name_exists"),
                "invalid_index"             => _lang.Get("label_invalid_index"),
                "execution_busy"            => _lang.Get("label_execution_busy"),
                "cancelled"                 => _lang.Get("label_cancelled"),
                "preflight_source_empty"    => _lang.Get("label_preflight_source_empty"),
                "preflight_source_missing"  => _lang.Get("label_preflight_source_missing"),
                "preflight_target_empty"    => _lang.Get("label_preflight_target_empty"),
                "preflight_target_denied"   => _lang.Get("label_preflight_target_denied"),
                "preflight_target_io"       => _lang.Get("label_preflight_target_io"),
                _                           => error
            };
        }
    }
}
