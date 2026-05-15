using System;
using System.Diagnostics;

namespace EasySave.Core.Services
{
    /// <summary>
    /// Detects whether a named business-critical process is currently running.
    /// If detected during a backup, the backup should be blocked or interrupted.
    /// </summary>
    public class BusinessSoftwareService
    {
        /// <summary>
        /// Returns true if a process matching <paramref name="processName"/> is running.
        /// Comparison is case-insensitive. Pass an empty string to disable detection.
        /// </summary>
        /// <param name="processName">
        /// The process name to look for (without .exe extension), e.g. "calc" or "MYERP".
        /// </param>
        public virtual bool IsRunning(string processName)
        {
            string normalizedName = NormalizeProcessName(processName);
            if (string.IsNullOrWhiteSpace(normalizedName))
                return false;

            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    string actualName = NormalizeProcessName(process.ProcessName);
                    bool exactMatch = actualName.Equals(normalizedName, StringComparison.OrdinalIgnoreCase);
                    bool fuzzyMatch = normalizedName.Length >= 4
                                      && actualName.Contains(normalizedName, StringComparison.OrdinalIgnoreCase);

                    if (!exactMatch && !fuzzyMatch)
                        continue;

                    if (HasVisibleWindow(process))
                        return true;

                    if (exactMatch && ShouldCountHeadlessProcess(actualName))
                        return true;
                }
                catch
                {
                    // Some protected processes reject metadata access. Ignore them instead of blocking backups forever.
                }
                finally
                {
                    process.Dispose();
                }
            }

            return false;
        }

        private static string NormalizeProcessName(string processName)
        {
            string normalized = processName.Trim().Trim('"');
            return normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? normalized[..^4]
                : normalized;
        }

        private static bool HasVisibleWindow(Process process)
        {
            try
            {
                return process.MainWindowHandle != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }

        private static bool ShouldCountHeadlessProcess(string processName)
        {
            // Windows Calculator can keep a hidden packaged process alive after the user closes the window.
            // For the demo business-software scenario, a hidden Calculator process should not hold backups paused.
            return !processName.Contains("calculator", StringComparison.OrdinalIgnoreCase);
        }
    }
}
