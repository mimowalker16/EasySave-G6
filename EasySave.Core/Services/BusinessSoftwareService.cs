using System.Diagnostics;
using System.Linq;

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
        public bool IsRunning(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return false;

            return Process.GetProcessesByName(processName).Length > 0;
        }
    }
}
