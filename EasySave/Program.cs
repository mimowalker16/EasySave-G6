using System;
using System.Collections.Generic;
using EasySave.Core.Localization;
using EasySave.Core.Services;
using EasySave.Core.ViewModels;
using EasySave.Views;

namespace EasySave
{
    /// <summary>
    /// EasySave v1.1 — Entry point.
    ///
    /// Usage:
    ///   EasySave.exe            → Interactive menu mode
    ///   EasySave.exe 1-3        → Execute jobs 1 to 3 (range)
    ///   EasySave.exe 1;3        → Execute jobs 1 and 3 (selection)
    /// </summary>
    internal class Program
    {
        static void Main(string[] args)
        {
            // Initialise core services (v1.1 keeps the 5-job limit)
            var configService    = new ConfigService();
            var stateService     = new StateService();
            var settingsService  = new SettingsService();
            var businessSoftware = new BusinessSoftwareService();
            var langManager      = new LanguageManager();

            var viewModel = new BackupViewModel(
                configService,
                stateService,
                settingsService,
                businessSoftware,
                maxJobs: 5);

            if (args.Length == 0)
            {
                // ── Interactive mode ──────────────────────────────────────
                var view = new ConsoleView(viewModel, langManager);
                view.Run();
            }
            else
            {
                // ── CLI mode ──────────────────────────────────────────────
                string    arg     = args[0].Trim();
                List<int> indices = ParseIndices(arg, viewModel.Jobs.Count);

                if (indices.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Invalid argument: '{arg}'");
                    Console.WriteLine("Usage: EasySave.exe [x-y | x;y;z]");
                    Console.ResetColor();
                    Environment.Exit(1);
                }

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"EasySave v1.1 — Executing {indices.Count} job(s)...\n");
                Console.ResetColor();

                var (allOk, errors) = viewModel.ExecuteJobs(indices);

                if (allOk)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("All jobs completed successfully.");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Completed with errors:");
                    Console.ResetColor();
                    foreach (string e in errors)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  • {e}");
                        Console.ResetColor();
                    }
                    Environment.Exit(2);
                }
            }
        }

        /// <summary>
        /// Parses a CLI argument into a list of 1-based job indices.
        /// Supports "1-3" (range) and "1;3" or "1;2;3" (list).
        /// </summary>
        private static List<int> ParseIndices(string arg, int totalJobs)
        {
            var result = new List<int>();

            if (arg.Contains('-'))
            {
                string[] parts = arg.Split('-');
                if (parts.Length == 2
                    && int.TryParse(parts[0], out int from)
                    && int.TryParse(parts[1], out int to)
                    && from >= 1 && to <= totalJobs && from <= to)
                {
                    for (int i = from; i <= to; i++)
                        result.Add(i);
                }
                return result;
            }

            if (arg.Contains(';'))
            {
                foreach (string part in arg.Split(';'))
                    if (int.TryParse(part.Trim(), out int idx) && idx >= 1 && idx <= totalJobs)
                        result.Add(idx);
                return result;
            }

            if (int.TryParse(arg, out int single) && single >= 1 && single <= totalJobs)
                result.Add(single);

            return result;
        }
    }
}
