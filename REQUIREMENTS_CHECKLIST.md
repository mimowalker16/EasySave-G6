# EasySave Requirements Checklist

Use this file before demos, commits, or future changes. If a feature changes, update its status and evidence notes.

Legend:

- `[x]` implemented / currently expected to work
- `[ ]` missing or not yet validated
- `[~]` partial, fragile, or needs manual demo validation

## Version 1.0 - Console Baseline

| Status | Requirement | Evidence / Notes |
|--------|-------------|------------------|
| [x] | Console application using .NET | `EasySave` project, `Program.cs`, `ConsoleView.cs` |
| [x] | Create up to 5 backup jobs | Console creates `BackupViewModel` with `maxJobs: 5` |
| [x] | Job has name, source directory, target directory, backup type | `BackupJob` model |
| [x] | Full backup type | `CompleteBackup` strategy |
| [x] | Differential backup type | `DifferentialBackup` strategy |
| [x] | English and French users supported | `LanguageManager`, `Localization/en.json`, `Localization/fr.json` |
| [x] | Execute one backup job | Console menu option and `ExecuteJob` |
| [x] | Execute all backup jobs sequentially | Console calls sequential `ExecuteAllJobs` |
| [x] | CLI execution by range, example `EasySave.exe 1-3` | `Program.ParseIndices` and `ExecuteJobs` |
| [x] | CLI execution by selection, example `EasySave.exe 1;3` | Quote the argument in PowerShell: `"1;3"` |
| [x] | Sources/targets may be local, external, or network paths | Uses standard filesystem paths; UNC paths are preserved |
| [x] | Copy all files and subdirectories from source | Strategies enumerate recursively |
| [x] | Daily log file written during backup | `EasyLog` loggers |
| [x] | Log timestamp | `LogEntry.Timestamp` |
| [x] | Log backup job name | `LogEntry.BackupJobName` |
| [x] | Log full source path in UNC style | `LogPathFormatter.ToUncFormat` |
| [x] | Log full destination path in UNC style | `LogPathFormatter.ToUncFormat` |
| [x] | Log file size | `LogEntry.FileSize` |
| [x] | Log transfer time in ms, negative on error | `LogEntry.TransferTimeMs` |
| [x] | EasyLog is a separate DLL | `EasyLog` project |
| [x] | Real-time state file | `StateService`, `state.json` |
| [x] | State includes job name | `BackupState.JobName` |
| [x] | State includes last action timestamp | `BackupState.LastActionTimestamp` |
| [x] | State includes active/inactive/end state | `BackupStateType` |
| [x] | Active state includes total files | `BackupState.TotalFiles` |
| [x] | Active state includes total size | `BackupState.TotalSize` |
| [x] | Active state includes progress | `BackupState.Progress` |
| [x] | Active state includes remaining files | `BackupState.RemainingFiles` |
| [x] | Active state includes remaining size | `BackupState.RemainingSize` |
| [x] | Active state includes current source file | `BackupState.CurrentSourceFile` |
| [x] | Active state includes current destination file | `BackupState.CurrentTargetFile` |
| [x] | Logs/state/config not stored in `c:\temp` | Defaults use `%APPDATA%\EasySave` |
| [x] | JSON files are indented/readable | `JsonSerializerOptions { WriteIndented = true }` |

## Version 1.1 - Console Log Format Choice

| Status | Requirement | Evidence / Notes |
|--------|-------------|------------------|
| [x] | Console remains available | `EasySave` project |
| [x] | Console remains limited to 5 jobs | `maxJobs: 5` |
| [x] | Console can choose JSON or XML log format | Console settings menu |
| [x] | EasyLog remains compatible with v1.0 call shape | `ILogger.LogTransfer(..., encryptionTimeMs = 0)` default |
| [x] | XML log output available | `XmlLogger` |
| [x] | JSON log output still available | `JsonLogger` |

## Version 2.0 - WPF GUI and CryptoSoft

| Status | Requirement | Evidence / Notes |
|--------|-------------|------------------|
| [x] | Graphical application | `EasySave.GUI` WPF project |
| [x] | Console behavior preserved separately | `EasySave` still exists for v1.1 |
| [x] | MVVM architecture | `MainViewModel`, `BackupViewModel`, models/services |
| [x] | Unlimited backup jobs in GUI | GUI creates `BackupViewModel` with `maxJobs: 0` |
| [x] | User can define encrypted extensions | GUI settings `EncryptedExtensionsText` |
| [x] | Files with configured extensions use CryptoSoft | `BackupService.RunCryptoSoft` |
| [x] | Log includes encryption time | `LogEntry.EncryptionTimeMs` |
| [x] | Encryption time `0` means no encryption | Default parameter and default local value |
| [x] | Encryption time `>0` means success duration | `RunCryptoSoft` elapsed milliseconds |
| [x] | Encryption time `<0` means error code/failure | Negative return values |
| [x] | User can define business software process | GUI and console settings |
| [~] | Version 2.0 blocks launch if business software is running | V3 behavior pauses instead; explain this as V3 evolution during defense |
| [x] | Business software can be demonstrated with Calculator | Use process name `calc` |

## Version 3.0 - Parallel Backup and Live Control

| Status | Requirement | Evidence / Notes |
|--------|-------------|------------------|
| [x] | GUI is the V3 primary surface | `MainWindow.xaml`, title/settings updated |
| [x] | Backups run in parallel in GUI | `ExecuteAllJobsParallel`, WPF calls parallel path |
| [x] | Console stays sequential for v1.1 compatibility | Console calls `ExecuteAllJobs` / `ExecuteJobs` |
| [x] | Priority extensions configurable | `PriorityExtensions` setting |
| [x] | Non-priority files wait while priority files remain pending | `_priorityFilesRemaining` gate in `BackupService` |
| [x] | Large-file threshold configurable in KB | `LargeFileThresholdKb` setting |
| [x] | Two large files above threshold cannot transfer simultaneously | `LargeFileTransferSemaphore` |
| [x] | Small files may transfer while one large file transfers | Semaphore only wraps large-file copy |
| [x] | Pause one job | `PauseJob` command/API |
| [x] | Resume/play one job | `ResumeJob` command/API |
| [x] | Stop one job | `StopJob` command/API and cancellation token |
| [x] | Pause all jobs | `PauseAllJobs` |
| [x] | Resume/play all jobs | `ResumeAllJobs` |
| [x] | Stop all jobs | `StopAllJobs` |
| [x] | User can follow progress percentage | WPF row progress and `BackupState.Progress` |
| [x] | Business software pauses running jobs | `WaitForBusinessSoftwarePause` |
| [x] | Jobs resume automatically after business software closes | Business-software pause loop exits automatically |
| [x] | CryptoSoft is mono-instance | Named mutex in `CryptoSoft/Program.cs` |
| [x] | EasySave serializes CryptoSoft calls | `CryptoSoftSemaphore` |
| [x] | Centralized log service exists under Docker | `LogCentralizer` project and Dockerfile |
| [x] | Central log mode: local only | `LogDestinationMode.LocalOnly` |
| [x] | Central log mode: central only | `LogDestinationMode.CentralOnly` |
| [x] | Central log mode: local and central | `LogDestinationMode.LocalAndCentral` |
| [x] | Central service keeps one daily file for all clients | `LogCentralizer` writes `YYYY-MM-DD.ndjson` |
| [x] | Central logs distinguish clients/users | `CentralClientId` / payload `clientId` |

## Documentation and Defense Material

| Status | Requirement | Evidence / Notes |
|--------|-------------|------------------|
| [x] | README explains current architecture | `README.md` |
| [x] | V3 demo checklist exists | `LIVRABLE3_DEMO.md` |
| [x] | V4 evolution ideas prepared | `LIVRABLE3_DEMO.md` |
| [x] | V4 ideas include benefit/development effort | `LIVRABLE3_DEMO.md` |

## Verification Commands

Run these after functional changes:

```powershell
dotnet build EasySave.slnx --no-restore
dotnet test EasySave.Tests\EasySave.Tests.csproj --no-restore
```

Optional demo commands:

```powershell
dotnet run --project EasySave.GUI
dotnet run --project EasySave -- 1-3
dotnet run --project EasySave -- "1;3"
docker build -t easysave-log-centralizer -f LogCentralizer/Dockerfile .
docker run -d --name easysave-log-centralizer -p 8080:8080 -v easysave_logs:/data easysave-log-centralizer
```

## Future Change Review

Before merging future work, check:

- Does it preserve console v1.1 sequential behavior?
- Does it preserve WPF v3 parallel behavior?
- Does it keep `EasyLog` backward-compatible?
- Does it update `state.json` during long operations?
- Does it avoid writing runtime data to `c:\temp`?
- Does it keep logs readable and include required fields?
- Does it keep Docker centralization optional and best-effort?
