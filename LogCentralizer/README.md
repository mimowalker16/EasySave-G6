# EasySave Log Centralizer (Docker)

Simple HTTP collector for centralized EasySave logs (V3 requirement).

## Endpoints

- `GET /` health/status
- `POST /api/logs` accepts JSON payload:

```json
{
  "clientId": "POSTE-01",
  "format": "Json",
  "timestamp": "2026-05-06T14:00:00.0000000Z",
  "entry": { "...": "..." }
}
```

Entries are appended to one daily server-side file:

- `/data/YYYY-MM-DD.ndjson`

This keeps one centralized log file per day regardless of the number of clients/machines.

## Run with Docker

```bash
docker build -t easysave-log-centralizer .
docker run -d --name easysave-log-centralizer -p 8080:8080 -v easysave_logs:/data easysave-log-centralizer
```

Then configure EasySave:

- Destination mode: `Central only` or `Local + Central`
- Endpoint: `http://localhost:8080/api/logs`
- Optional client id: machine/user label
