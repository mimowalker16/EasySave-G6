# EasySave Livrable 3 Requirements Checklist

This checklist maps each Livrable 3 requirement to the current implementation status.

## Functional Requirements

- [x] L'IHM du logiciel est soignee.
  - Evidence: WPF dashboard with job cards, right-side editor, settings sections, validation, folder browsing, and bilingual labels.

- [x] Le logiciel permet d'enregistrer un nombre illimite de travaux.
  - Evidence: GUI starts `BackupViewModel` with `maxJobs: 0`; automated test covers more than 5 jobs.

- [x] Le logiciel permet a l'utilisateur de parametrer la liste des types de fichiers devant etre cryptes.
  - Evidence: `EncryptedExtensions` setting is persisted and exposed in the GUI settings page.

- [x] Le logiciel permet a l'utilisateur de parametrer le logiciel metier.
  - Evidence: `BusinessSoftwareName` setting is persisted and exposed in the GUI settings page.

- [x] Le logiciel permet a l'utilisateur de parametrer la taille des fichiers volumineux.
  - Evidence: `LargeFileThresholdKb` setting is persisted, validated, and used by the backup engine.

- [x] Le logiciel permet a l'utilisateur de parametrer les fichiers prioritaires.
  - Evidence: `PriorityExtensions` setting is persisted and used by the V3 priority gate.

- [x] La gestion de CryptoSoft mono-instance est fonctionnelle.
  - Evidence: CryptoSoft uses a named mutex; EasySave serializes CryptoSoft calls with a process-wide semaphore.

- [x] La gestion des fichiers cryptes est fonctionnelle.
  - Evidence: backup service invokes CryptoSoft only for configured encrypted extensions and logs the encryption duration or error code.

- [x] Le fichier log journalier repond au nouveau cahier des charges avec ajout du temps de cryptage.
  - Evidence: `EncryptionTimeMs` is present in JSON/XML log entries and tested.

- [x] Le fichier log journalier est disponible en format XML et JSON.
  - Evidence: EasyLog provides `JsonLogger`, `XmlLogger`, and `LoggerFactory`.

- [x] Le logiciel utilise la DLL EasyLog.
  - Evidence: core and tests reference the EasyLog project/library for all transfer logs.

- [x] Le logiciel est multi-langues.
  - Evidence: console localization remains in JSON; WPF uses English/French resource dictionaries with runtime switching.

- [x] La gestion des fichiers prioritaires est fonctionnelle.
  - Evidence: non-priority transfers wait while priority extensions are pending in active jobs.

- [x] Les fichiers non prioritaires sont bloques tant qu'il reste des fichiers prioritaires a transferer.
  - Evidence: V3 backup coordination checks the global priority counter before each non-priority transfer.

- [x] La gestion des fichiers volumineux est fonctionnelle.
  - Evidence: only one transfer above `LargeFileThresholdKb` can acquire the large-file semaphore at a time.

- [x] L'utilisateur peut agir sur chaque travail avec Pause, Stop, Play.
  - Evidence: WPF exposes per-job Run, Pause, Resume, and Stop controls wired to core V3 commands.

- [x] La gestion de l'acces concurrentiel des fichiers historique et configuration est fonctionnelle.
  - Evidence: logs and state use synchronized writes; job/settings configuration now uses per-file locks and atomic replace.

- [x] Les travaux se mettent en pause si detection du logiciel metier.
  - Evidence: backup engine pauses while the configured process is running and resumes automatically once it stops.

- [x] La centralisation des logs est fonctionnelle.
  - Evidence: EasyLog can send local/central/both log entries; Docker `LogCentralizer` exposes `POST /api/logs` and writes one daily NDJSON file with `clientId`.

## Demo Test Data

Use `TestData/Livrable3` for manual validation:

- `SourceA` and `SourceB`: source directories for two parallel jobs.
- `TargetA` and `TargetB`: empty target directories.
- Suggested settings:
  - Priority extensions: `.prio`
  - Encrypted extensions: `.secret`
  - Large file threshold: `1`
  - Business software: `Calculator` or `calc`
  - Log format: test once with JSON, once with XML
  - Central endpoint: `http://localhost:8080/api/logs`
