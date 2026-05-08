# Livrable 3 Manual Test Data

Use these folders to demo EasySave V3 behavior.

Recommended jobs:

1. Job name: `L3-Job-A`
   Source: `TestData/Livrable3/SourceA`
   Target: `TestData/Livrable3/TargetA`
   Type: Full

2. Job name: `L3-Job-B`
   Source: `TestData/Livrable3/SourceB`
   Target: `TestData/Livrable3/TargetB`
   Type: Full

Recommended settings:

- Priority extensions: `.prio`
- Encrypted extensions: `.secret`
- Large file threshold: `1` KB
- Business software: `Calculator` or `calc`
- Log format: JSON, then XML for a second run
- Central logs: `http://localhost:8080/api/logs`

Expected observations:

- `.prio` files are transferred before non-priority files.
- `.secret` files are encrypted after transfer and log `EncryptionTimeMs`.
- `03-large.dat` is above 1 KB, so only one large transfer should run at a time.
- Running Calculator pauses active jobs until the process closes.
- Per-job Run/Pause/Resume/Stop buttons update job state.
