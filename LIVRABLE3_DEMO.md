# EasySave 3.0 - Demo and Validation Guide

This page is a practical script for demonstrating V3 features during defense.

## 1) Preconditions

- Build solution: `dotnet build EasySave.slnx`
- Start GUI: `dotnet run --project EasySave.GUI`
- (Optional) Start central log service:
  - `cd LogCentralizer`
  - `docker build -t easysave-log-centralizer .`
  - `docker run -d --name easysave-log-centralizer -p 8080:8080 -v easysave_logs:/data easysave-log-centralizer`

## 2) Settings to prepare

In the GUI `Settings` page:

- `Priority extensions`: `.zip,.bak`
- `Large file threshold (KB)`: `512`
- `Business software`: `calc`
- `Log destination mode`: `Local + Central`
- `Central endpoint`: `http://localhost:8080/api/logs`
- `Central client id`: machine/user label (example `POSTE-01`)

Save settings.

## 3) Data setup for test jobs

Create at least 3 jobs with overlapping workloads:

- Job A: includes large files (`> 512KB`)
- Job B: includes many small files + `.zip` priority
- Job C: includes mixed files and at least one `.bak`

Expected: jobs run in parallel, but priority/gate rules still apply.

## 4) Validation checklist (V3)

### 4.1 Parallel backups

- Launch `Run All`.
- Verify multiple jobs switch to running state nearly at the same time.

### 4.2 Priority file rule

- While any `.zip` or `.bak` remains pending in any job, non-priority files should wait.
- Observe progress/state transitions and resulting transfer order in logs.

### 4.3 Large file parallel restriction

- Confirm no two files above threshold transfer simultaneously.
- Smaller files may continue in other jobs (subject to priority rule).

### 4.4 Pause / Play / Stop interactions

- Per-job controls:
  - `Pause`: effective after current file.
  - `Resume`: continues from pending queue.
  - `Stop`: immediate cancellation request.
- Global controls:
  - `Pause All`, `Resume All`, `Stop All`.

### 4.5 Business software auto-pause

- Start `calc.exe` while backups are running.
- Expect all jobs to move to `Paused`.
- Close calculator -> jobs resume automatically.

### 4.6 CryptoSoft mono-instance

- Ensure encrypted extensions are configured (example `.txt,.docx`).
- Run multiple jobs requiring encryption.
- Verify encryption operations are serialized (no concurrent CryptoSoft runs).

### 4.7 Centralized logs

- Check local daily logs in configured local directory.
- Check central daily file in Docker volume (`/data/YYYY-MM-DD.ndjson`).
- Verify one server-side daily file regardless of number of clients.

## 5) Files to inspect during defense

- Runtime state: `%APPDATA%\\EasySave\\state.json`
- Local logs: configured log directory (`.json`, `.xml`, or `.ndjson`)
- Central logs: `LogCentralizer` data volume file `YYYY-MM-DD.ndjson`

## 6) Quick V4 roadmap (benefit / effort)

1. **Deduplication by content hash**
   - Client benefit: saves storage and transfer time.
   - Effort: medium/high (index, hash cache, conflict handling).
2. **Incremental snapshots and restore wizard**
   - Client benefit: granular restore, stronger backup product value.
   - Effort: high.
3. **Adaptive throttling by bandwidth/CPU**
   - Client benefit: less impact on production machines.
   - Effort: medium.
4. **Resumable interrupted transfers**
   - Client benefit: reliability on unstable networks.
   - Effort: medium/high.
5. **Observability dashboard (queue, errors, throughput)**
   - Client benefit: easier support and SLA monitoring.
   - Effort: medium.
