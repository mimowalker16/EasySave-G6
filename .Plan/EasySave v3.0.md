# EasySave V3 Remaining Must-Do Plan

## Summary
Bring the current codebase from mostly v1.1/v2 behavior to a defensible EasySave 3.0 delivery while preserving console v1.1 behavior: console stays limited to 5 jobs and sequential; WPF GUI becomes the V3 surface with unlimited jobs, parallel execution, per-job controls, priority rules, large-file throttling, business-software pause, CryptoSoft mono-instance handling, centralized logs, tests, and updated documentation.

## Key Changes
- Add V3 execution APIs in `BackupViewModel`: async/parallel `ExecuteAllJobs`, selected-job execution, `PauseJob`, `ResumeJob`, `StopJob`, and all-jobs equivalents using `CancellationTokenSource` per running job.
- Keep console v1.1 sequential by adding/keeping a separate sequential execution path for `EasySave` CLI/menu; GUI uses the new V3 parallel path.
- Extend `BackupStateType` with at least `Paused` and `Canceled`; update `state.json` continuously with progress, current file, paused/canceled status, and final state.
- Update `BackupService` to copy files in chunks so stop can cancel the current transfer immediately and delete partial targets.
- Implement V3 coordination:
  - Priority extensions in global settings; non-priority files wait while any priority file remains pending in any active job.
  - Large-file threshold in KB; only one file above threshold transfers at a time.
  - Business software detection pauses running jobs automatically and resumes when the process closes.
  - CryptoSoft calls are serialized with a process-wide semaphore; CryptoSoft itself gets a named mutex and clear exit code for mono-instance conflicts.
- Extend settings with `PriorityExtensions`, `LargeFileThresholdKb`, `LogDirectory`, `LogDestinationMode`, `CentralLogEndpoint`, `CentralClientId`, and keep `EncryptedExtensions` / `BusinessSoftwareName`.
- Extend EasyLog without breaking v1.1 calls:
  - Keep `ILogger.LogTransfer(..., encryptionTimeMs = 0)` compatible.
  - Add logger options/factory overload for local/custom directory, JSON/XML, and central/local/both destination.
  - Add central HTTP sender that includes `clientId`, format, timestamp, and the log entry.
- Restore/add tracked `LogCentralizer` source and Docker files: minimal ASP.NET service with `POST /api/logs`, `GET /`, and one daily central `.ndjson` file containing all clients differentiated by `clientId`.
- Update WPF:
  - Run jobs on background tasks so the UI stays responsive.
  - Add per-job Play/Pause/Stop and global Play/Pause/Stop controls.
  - Bind progress/status updates to rows.
  - Add settings inputs for priority extensions, large-file threshold, central logging mode/endpoint/client id, custom log directory, encrypted extensions, and business software.
- Update documentation:
  - README requirement matrix for v1.1/v2/v3 behavior.
  - V3 demo checklist: parallel jobs, priority files, large-file throttling, pause/play/stop, business software using Calculator, CryptoSoft mono-instance, central logs.
  - Add short V4 benefit/effort section for the direction note.

## Test Plan
- Unit tests:
  - v1.1 console/core sequential path still executes requested jobs in order.
  - unlimited jobs works for GUI/core V3 mode.
  - priority files are processed before non-priority files across parallel jobs.
  - large-file threshold prevents two large transfers at once.
  - pause/resume/stop changes state correctly; stop cancels current transfer and removes partial file.
  - business software detection pauses and resumes instead of failing V3 runs.
  - encrypted extensions invoke CryptoSoft once at a time and log encryption duration/error code.
  - JSON and XML logs still contain all required fields including `EncryptionTimeMs`.
  - central/local/both log modes route entries correctly.
- Integration/demo checks:
  - `dotnet build EasySave.slnx`
  - `dotnet test EasySave.Tests\EasySave.Tests.csproj`
  - launch WPF, create 3 jobs, run all, verify parallel progress.
  - run Docker centralizer and confirm one daily central log file receives entries from configured client id.
  - run console CLI `EasySave.exe 1-3` and `EasySave.exe 1;3`, confirming v1.1 sequential behavior remains.

## Assumptions
- Target is V3 defense readiness, not a full production hardening pass.
- Console remains v1.1: 5-job limit, JSON/XML setting, sequential execution.
- GUI is the V3 application surface: unlimited jobs, parallel execution, live controls.
- JSON remains pretty/indented for local logs unless NDJSON is used only by the centralizer.
- Central log delivery should not fail a backup if the Docker server is unavailable; local logging still records the transfer when enabled.
