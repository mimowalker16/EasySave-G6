using System;
using System.IO;
using EasySave.Core.Models;

namespace EasySave.Core.Services
{
    /// <summary>
    /// Validates source/target paths before starting a backup to fail fast with clear errors.
    /// </summary>
    public static class BackupPreflight
    {
        /// <param name="ErrorKey">Empty when <see cref="Ok"/> is true; otherwise a stable key for UI translation.</param>
        public readonly record struct Result(bool Ok, string ErrorKey)
        {
            public static Result Success => new(true, string.Empty);
        }

        /// <summary>Checks that the job's source exists and the target root is writable.</summary>
        public static Result Validate(BackupJob job)
        {
            if (string.IsNullOrWhiteSpace(job.SourceDirectory))
                return new Result(false, "preflight_source_empty");

            string source = job.SourceDirectory.Trim();
            if (!Directory.Exists(source))
                return new Result(false, "preflight_source_missing");

            if (string.IsNullOrWhiteSpace(job.TargetDirectory))
                return new Result(false, "preflight_target_empty");

            string targetRoot = job.TargetDirectory.Trim();

            try
            {
                Directory.CreateDirectory(targetRoot);
                string probe = Path.Combine(targetRoot, $".easysave_write_probe_{Guid.NewGuid():N}.tmp");
                File.WriteAllBytes(probe, new byte[] { 0 });
                File.Delete(probe);
            }
            catch (UnauthorizedAccessException)
            {
                return new Result(false, "preflight_target_denied");
            }
            catch (IOException)
            {
                return new Result(false, "preflight_target_io");
            }
            catch (Exception)
            {
                return new Result(false, "preflight_target_io");
            }

            return Result.Success;
        }
    }
}
