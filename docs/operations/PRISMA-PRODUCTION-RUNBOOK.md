# Prisma OCR Pipeline — Production Operational Runbook

> **Review Status**
> This document was authored against source-code evidence (code paths cited throughout).
> It MUST be reviewed by a person with production-environment access before being marked
> "verified for use." Reviewer: **TODO**. Date verified: **TODO**.

---

## Table of Contents

1. [Quick Reference](#1-quick-reference)
2. [Worker Startup and Restart Sequence](#2-worker-startup-and-restart-sequence)
3. [Database Migration Execution](#3-database-migration-execution)
4. [SIARA Credential Rotation](#4-siara-credential-rotation)
5. [Pipeline Failure Triage](#5-pipeline-failure-triage)
6. [Database Backup and Recovery](#6-database-backup-and-recovery)
7. [On-Call Escalation Path](#7-on-call-escalation-path)

---

## 1. Quick Reference

### Service inventory

| Service | Binary | Clearance role | Default port |
|---|---|---|---|
| Orion Worker (Downloader) | `ExxerCube.Prisma.Orion.Worker` | `Download` | 5200 (configure via `ASPNETCORE_URLS`) |
| Athena Worker (Extractor) | `ExxerCube.Prisma.Athena.Worker` | `Extract` | 5201 |
| Reconciliator Worker | `ExxerCube.Prisma.Reconciliator.Worker` | `Reconcile` | 5202 |
| Web.UI (Blazor) | `ExxerCube.Prisma.Web.UI` | — (Identity cookie auth) | 5000 / 5001 HTTPS |

### Health endpoints (all four services)

| Path | Semantics |
|---|---|
| `GET /health/live` | Always HTTP 200 while the process is running. Liveness probe. |
| `GET /health/ready` | HTTP 200 when the service is ready to handle work; HTTP 503 otherwise. Readiness probe. |
| `GET /health` | Aggregate; HTTP 503 when any registered check is unhealthy. |

Worker readiness probes:
- **Orion** — `SiaraWatchLoop` implements `IReadinessProbe`
- **Athena** — `ExtractionPipelineService` implements `IReadinessProbe`
- **Reconciliator** — `ReconciliationPipelineService` implements `IReadinessProbe`
- **Web.UI** — `PrismaDbHealthCheck` (tests `ApplicationDbContext.CanConnectAsync`)

Sources:
- `Prisma/Code/Src/CSharp/04 Services/Orion/Prisma.Orion.Worker/Program.cs` lines 244-264
- `Prisma/Code/Src/CSharp/04 Services/Athena/Prisma.Athena.Worker/Program.cs` lines 258-278
- `Prisma/Code/Src/CSharp/04 Services/Reconciliator/Prisma.Reconciliator.Worker/Program.cs` lines 140-160
- `Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Web.UI/Program.cs` lines 123-133

### Mandatory environment variables (all workers)

| Variable | Purpose | Default / placeholder |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string (PrismaDbContext) | `DEV-PLACEHOLDER-*` — workers skip DB if placeholder |
| `ProcessIdentity__JwtSecret` | Shared secret for inter-process JWT clearance tokens | `DEV-ONLY-PLACEHOLDER-REPLACE-VIA-ENV-OR-KEYVAULT-IN-PRODUCTION` |
| `ProcessIdentity__JwtIssuer` | JWT issuer claim | `prisma-pipeline` |
| `ProcessIdentity__JwtAudience` | JWT audience claim | `prisma-pipeline` |
| `SEQ_URL` | Seq structured-log ingest URL | `http://localhost:5341` (workers default this if unset) |
| `TESSDATA_PREFIX` | Tesseract language-data directory | Auto-discovered (see §5.2) |

Web.UI additional:

| Variable | Purpose |
|---|---|
| `ConnectionStrings__ApplicationConnection` | PrismaDbContext for application data (SEPARATE from DefaultConnection/Identity) |

---

## 2. Worker Startup and Restart Sequence

### 2.1 Dependency graph

The three-actor pipeline follows a strict producer-consumer chain over SignalR hubs:

```
Orion Worker  ──/hubs/ingestion──►  Athena Worker  ──/hubs/reconciliation──►  Reconciliator Worker
     │                                     │
     └── emits DocumentDownloadedEvent     └── emits ExtractionCompletedEvent
```

Web.UI connects to Orion's hub independently; it is not on the critical pipeline path.

**Start order:** Orion must be up and serving `/hubs/ingestion` before Athena starts, and
Athena must be up and serving `/hubs/reconciliation` before Reconciliator starts.
**Stopping:** reverse order (Reconciliator → Athena → Orion). Web.UI is independent.

### 2.2 First-time startup checklist

Before starting any process, verify:

- [ ] SQL Server reachable at the connection-string endpoint.
- [ ] `PrismaDbContext` migrations applied (see §3).
- [ ] `ApplicationDbContext` migrations applied (Web.UI only — see §3).
- [ ] `TESSDATA_PREFIX` set or Tesseract installed in a standard location (see §5.2).
- [ ] Shared document-storage volume mounted at the same path on Athena and Reconciliator hosts
      (configured in `Storage:BasePath`; Athena and Reconciliator both reference
      `SharedStoragePathResolver` keyed to `StorageOptions:BasePath`).
- [ ] `ProcessIdentity__JwtSecret` identical on all three workers (token minted by one,
      validated by the next).
- [ ] `SiaraUrl` set in Web.UI and/or Orion `NavigationTargets` config.
- [ ] `SEQ_URL` set to point at the Seq instance.

### 2.3 Starting each service

#### Orion Worker (start first)

```powershell
# Set required environment variables in the shell, or use a service manager / Docker env block.
$env:ConnectionStrings__DefaultConnection = "Server=<host>;Database=PrismaDb;..."
$env:ProcessIdentity__JwtSecret          = "<shared-secret>"
$env:SEQ_URL                             = "http://<seq-host>:5341"
$env:ASPNETCORE_URLS                     = "http://+:5200"

dotnet run --project "Prisma/Code/Src/CSharp/04 Services/Orion/Prisma.Orion.Worker/ExxerCube.Prisma.Orion.Worker.csproj" --no-build
```

Confirm readiness:
```powershell
curl http://localhost:5200/health/ready
# Expected: HTTP 200  { "status": "Healthy", ... }
```

If Orion is NOT ready (503), check that `SiaraWatchLoop` has connected. Inspect Seq logs for
`"Orion"` source and the `SiaraWatchLoop` startup message.

#### Athena Worker (start second — after Orion is ready)

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=<host>;Database=PrismaDb;..."
$env:ProcessIdentity__JwtSecret          = "<shared-secret>"   # must match Orion
$env:SEQ_URL                             = "http://<seq-host>:5341"
$env:TESSDATA_PREFIX                     = "/usr/share/tesseract-ocr/4.00/tessdata"  # or omit if Tesseract is installed
$env:ASPNETCORE_URLS                     = "http://+:5201"
# Orion hub URL — Athena is a SignalR client of Orion:
$env:IngestionClient__HubUrl             = "http://<orion-host>:5200/hubs/ingestion"

dotnet run --project "Prisma/Code/Src/CSharp/04 Services/Athena/Prisma.Athena.Worker/ExxerCube.Prisma.Athena.Worker.csproj" --no-build
```

Confirm readiness:
```powershell
curl http://localhost:5201/health/ready
# Expected: HTTP 200  { "status": "Healthy", ... }
```

If Athena returns 503, the `SiaraIngestionHubClient` has likely not connected to Orion yet.
Confirm Orion is running and the hub URL is correct.

#### Reconciliator Worker (start third — after Athena is ready)

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=<host>;Database=PrismaDb;..."
$env:ProcessIdentity__JwtSecret          = "<shared-secret>"   # must match Orion/Athena
$env:SEQ_URL                             = "http://<seq-host>:5341"
$env:ASPNETCORE_URLS                     = "http://+:5202"
# Athena hub URL — Reconciliator is a SignalR client of Athena:
$env:ReconciliationClient__HubUrl        = "http://<athena-host>:5201/hubs/reconciliation"
$env:Storage__BasePath                   = "/mnt/prisma-docs"  # same volume as Athena

dotnet run --project "Prisma/Code/Src/CSharp/04 Services/Reconciliator/Prisma.Reconciliator.Worker/ExxerCube.Prisma.Reconciliator.Worker.csproj" --no-build
```

Confirm readiness:
```powershell
curl http://localhost:5202/health/ready
# Expected: HTTP 200  { "status": "Healthy", ... }
```

#### Web.UI (independent — start any time after DB is ready)

```powershell
$env:ConnectionStrings__DefaultConnection    = "Server=<host>;Database=PrismaId;..."   # Identity DB
$env:ConnectionStrings__ApplicationConnection = "Server=<host>;Database=PrismaDb;..."  # Prisma app DB
$env:SEQ_URL                                 = "http://<seq-host>:5341"
$env:ASPNETCORE_URLS                         = "http://+:5000;https://+:5001"

dotnet run --project "Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Web.UI/ExxerCube.Prisma.Web.UI.csproj" --no-build
```

Confirm:
```powershell
curl http://localhost:5000/health
# Expected: HTTP 200  {"status":"Healthy"}
```

### 2.4 Graceful restart

For a routine restart (config change, patch, etc.) where no data loss is acceptable:

1. Stop Reconciliator — it will finish any in-flight reconciliation cycle.
2. Stop Athena — the hub disconnects; Orion continues buffering download events in memory.
3. Stop Orion.
4. Apply the change (config, binary, etc.).
5. Start in forward order: Orion → Athena → Reconciliator.
6. Confirm all three `/health/ready` endpoints return 200.

> **Note:** Events in flight when a hub disconnects are NOT durably persisted between processes
> in the current implementation (in-memory `InMemoryClearanceReplayGuard`). A restart may
> silently drop events that were emitted but not yet received. Verify the document queue
> after restart if data loss is a concern.

---

## 3. Database Migration Execution

### 3.1 Context ownership

The codebase has three EF Core DbContexts, each owned by a different host:

| DbContext | Owner host | Connection string key | Migration trigger |
|---|---|---|---|
| `PrismaDbContext` | Orion, Athena, Reconciliator workers | `DefaultConnection` | `--migrate-only` flag on any one worker |
| `ApplicationDbContext` | Web.UI only | `DefaultConnection` (Web.UI) | EF `UseMigrationsEndPoint` in Dev; manual `dotnet ef` in Prod |
| `TemplateDbContext` (export templates) | Web.UI only (via `AddAdaptiveExportServices`) | `ApplicationConnection` | Same as ApplicationDbContext |

**`PrismaDbMigrationRunner` only migrates `PrismaDbContext`.** The runner explicitly
documents that `ApplicationDbContext` and `TemplateDbContext` are excluded and are the
Web.UI's responsibility.

Source:
- `Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.Database/Startup/PrismaDbMigrationRunner.cs` lines 17-23, 46-51

### 3.2 Applying PrismaDbContext migrations (workers)

Run any one worker with `--migrate-only`. The runner applies pending migrations and exits
with code 0 on success or 1 on failure. Orion is the conventional choice because it is
started first, but any of the three workers works.

```powershell
# Recommended: run Orion as the migration init-container / pre-flight step
$env:ConnectionStrings__DefaultConnection = "Server=<host>;Database=PrismaDb;User Id=<user>;Password=<pw>;"

dotnet "Prisma/Code/Src/CSharp/04 Services/Orion/Prisma.Orion.Worker/bin/Release/net10.0/ExxerCube.Prisma.Orion.Worker.dll" --migrate-only
# OR from source:
dotnet run --project "Prisma/Code/Src/CSharp/04 Services/Orion/Prisma.Orion.Worker/ExxerCube.Prisma.Orion.Worker.csproj" -- --migrate-only

# Check exit code:
# $LASTEXITCODE -eq 0  → success
# $LASTEXITCODE -eq 1  → migration failed; check Seq / console for the exception
```

The `--migrate-only` block in all three workers (Orion lines 277-284, Athena lines 291-298,
Reconciliator lines 166-173) follows identical logic: build the host, call
`PrismaDbMigrationRunner.RunMigrationsAsync`, set `Environment.ExitCode`, then `return`
without starting the run loop.

K8s init-container usage (documented in source comments):
```yaml
# Example K8s init-container spec (adapt to your deployment)
initContainers:
  - name: prisma-migrate
    image: <prisma-orion-image>
    command: ["./ExxerCube.Prisma.Orion.Worker", "--migrate-only"]
    env:
      - name: ConnectionStrings__DefaultConnection
        valueFrom:
          secretKeyRef:
            name: prisma-db-secret
            key: connection-string
```

### 3.3 Applying ApplicationDbContext and TemplateDbContext migrations (Web.UI)

These contexts are NOT migrated by the workers. Apply manually using the EF CLI targeting
the Web.UI project:

```powershell
# ApplicationDbContext (ASP.NET Identity tables)
dotnet ef database update `
  --project "Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Web.UI/ExxerCube.Prisma.Web.UI.csproj" `
  --context ApplicationDbContext `
  -- --ConnectionStrings:DefaultConnection "Server=<host>;Database=PrismaId;..."

# TemplateDbContext (adaptive export templates)
dotnet ef database update `
  --project "Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Web.UI/ExxerCube.Prisma.Web.UI.csproj" `
  --context TemplateDbContext `
  -- --ConnectionStrings:ApplicationConnection "Server=<host>;Database=PrismaDb;..."
```

> **Note:** In Development, Web.UI calls `app.UseMigrationsEndPoint()` (Program.cs line 104)
> which auto-applies migrations on the first request. In Production this is NOT active;
> apply migrations explicitly as above before starting the UI.

### 3.4 Migration order for a fresh deployment

```
1. Apply PrismaDbContext     →  dotnet ... Orion.Worker --migrate-only   (exit 0)
2. Apply ApplicationDbContext →  dotnet ef ... --context ApplicationDbContext
3. Apply TemplateDbContext    →  dotnet ef ... --context TemplateDbContext
4. Start Orion → Athena → Reconciliator → Web.UI  (§2.3)
```

There is currently no foreign-key dependency between `ApplicationDbContext`/`TemplateDbContext`
and `PrismaDbContext`; steps 2 and 3 may be run in any order relative to each other, but
step 1 should run before the workers start (it is the workers' schema).

---

## 4. SIARA Credential Rotation

### 4.1 What "credentials" means in this system

Prisma does not authenticate to SIARA using a username/password login in the traditional
sense. The current integration uses browser-automation (Playwright) to scrape SIARA's
web portal; the `SiaraDocumentDownloader` navigates the portal using the credential
material configured in `SiaraOptions`. There is no SIARA REST API.

> ADR-010 (`docs/architecture/adr/`) documents the security-mandated credential design.
> Vault integration is a **planned future step** (PRISMA-E3, not yet implemented).

### 4.2 Where credentials live today (manual procedure)

SIARA login credentials are supplied via application configuration and must never be
committed to source control. The configuration section is `Siara` in each worker's
`appsettings.json`.

**Current appsettings structure (Orion Worker — representative):**
```json
{
  "Siara": {
    "Actor": {
      "ActorId": "orion-downloader-dev",
      "DisplayName": "Orion Downloader"
    }
  }
}
```

The SIARA portal URL is configured in Web.UI `appsettings.json` as `SiaraUrl` (line 15,
currently empty — must be set per environment). Navigation targets and portal credentials
are bound from the `NavigationTargets` section.

**To rotate credentials:**

1. Identify the current credential source:
   - If using user-secrets (local dev): `dotnet user-secrets set "Siara:Credentials:Username" "<new>" --project ...`
   - If using environment variables (staging/prod): update `Siara__Credentials__Username` and
     `Siara__Credentials__Password` in the process environment or secrets store.
   - If using a config file override: update the appropriate `appsettings.<Environment>.json`
     (do NOT commit credentials to source).

2. Restart **Orion Worker** (it owns the SIARA browser session).

3. Confirm Orion reaches the `/health/ready` → HTTP 200 state, which signals
   `SiaraWatchLoop` connected successfully with the new credentials.

4. Watch Seq logs (`source = "Orion"`) for SIARA authentication success/failure messages.

### 4.3 Future: vault integration (placeholder)

> **This section is a placeholder.** Vault integration (HashiCorp Vault or Azure Key Vault)
> is planned under PRISMA-E3 (not yet implemented). When implemented:
> - Credentials will be fetched at startup via the vault provider and injected as
>   `IOptions<SiaraOptions>`.
> - Rotation will be: rotate in vault → issue SIGHUP / rolling restart of Orion Worker.
> - The ADR governing this design is ADR-010.
>
> Until that work lands, the manual procedure in §4.2 is authoritative.

### 4.4 Inter-process JWT secret rotation

All three workers share a `ProcessIdentity:JwtSecret`. This secret signs clearance tokens
that flow Orion → Athena → Reconciliator. To rotate:

1. Update `ProcessIdentity__JwtSecret` in all three workers' environments simultaneously
   (or in a coordinated rolling restart).
2. Restart Orion, then Athena, then Reconciliator.
3. During the restart window, in-flight clearance tokens signed with the old key will be
   rejected. This is expected; events in flight will be silently dropped (see §2.4 note).

---

## 5. Pipeline Failure Triage

### 5.1 Athena Worker unreachable

**Symptom:** Reconciliator logs repeated hub-reconnect attempts; documents pile up in Orion
but are never extracted.

**Triage steps:**

```powershell
# 1. Check liveness — is the process running at all?
curl http://<athena-host>:5201/health/live
# If connection refused: process is down → restart Athena (§2.3)

# 2. Check readiness — process is up but not ready:
curl http://<athena-host>:5201/health/ready
# HTTP 503: ExtractionPipelineService.IsReady is false
# Check the "data" field in the JSON body for the specific reason.

# 3. Is the Orion hub visible from Athena?
curl http://<orion-host>:5200/health/live
# If unreachable: fix Orion first (§5.1 applies recursively to the upstream)

# 4. Check Seq for Athena startup errors:
# Filter: @l = "Error" AND source = "Athena"
```

**Common causes:**
- Athena started before Orion was ready: the `SiaraIngestionHubClient` will keep retrying;
  wait or restart Athena after Orion is healthy.
- Wrong `IngestionClient__HubUrl`: correct the env var and restart.
- Tesseract failure blocking startup: see §5.2.

### 5.2 Tesseract OCR failure

**Symptom:** Athena health stays at 503; Seq logs show `InvalidOperationException: Could not locate tessdata directory`; or OCR results are empty / error.

**Implementation detail:** `TesseractOcrExecutor` is registered as a Singleton (Athena
`Program.cs` line 94). On the first extraction call it resolves `tessdata` using a
double-checked lock on `_tessdataLock` (a static `SemaphoreSlim(1,1)`). Tessdata discovery
probes these paths in order
(`Infrastructure.Extraction/Teseract/TesseractOcrExecutor.cs` lines 288-311):

```
1.  TESSDATA_PREFIX environment variable
2.  %ProgramFiles%\Tesseract-OCR\tessdata          (Windows)
3.  %ProgramFiles(x86)%\Tesseract-OCR\tessdata     (Windows x86)
4.  /usr/share/tesseract-ocr/4.00/tessdata         (Linux Tesseract 4)
5.  /usr/share/tesseract-ocr/tessdata              (Linux generic)
6.  /usr/local/share/tessdata                      (Linux local)
7.  /opt/homebrew/share/tessdata                   (macOS Homebrew)
8.  {AppContext.BaseDirectory}/tessdata            (alongside the binary)
9.  {CWD}/tessdata
10. {AppContext.BaseDirectory}/x64/tessdata
11. {AppContext.BaseDirectory}/x86/tessdata
```

If none exist, the executor throws:
`"Could not locate tessdata directory. Please install Tesseract OCR or set TESSDATA_PREFIX environment variable."`

**Remediation:**
```powershell
# Option A: Install Tesseract and it will be auto-discovered
# Option B: Set TESSDATA_PREFIX explicitly (most reliable in containers):
$env:TESSDATA_PREFIX = "/usr/share/tesseract-ocr/4.00/tessdata"
# Restart Athena after setting the variable.

# Verify tessdata contains the Spanish language pack:
ls $env:TESSDATA_PREFIX | Where-Object Name -like "spa*"
# Expected: spa.traineddata (required for Spanish legal documents)
```

**Supported language codes** (from TesseractOcrExecutor lines 254-262):
`spa`/`spanish`/`es` → `"spa"`, `eng`/`english`/`en` → `"eng"`.

### 5.3 Tesseract second-init deadlock

**Symptom:** Athena hangs on the first OCR call; no error logged; the process is alive
(`/health/live` returns 200) but `/health/ready` stays 503 indefinitely.

**Cause:** Tesseract's native library cannot be initialized twice in the same process.
`DocxFieldExtractor` explicitly documents this:
> "The injected instance MUST be the already-registered `TesseractOcrExecutor` —
> do NOT new-up a second engine (Tesseract same-process second-init DEADLOCK)."
>
> Source: `Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.Extraction/Teseract/DocxFieldExtractor.cs` lines 25-26

**If this is triggered (e.g., after a code change that new-ups a second `TesseractEngine`):**

1. Kill and restart Athena — the deadlock is unrecoverable without a process restart.
2. Identify and revert the code path that created a second `TesseractEngine` instance.
   The DI-registered `IOcrExecutor` singleton is the ONLY permitted `TesseractOcrExecutor`
   instance per process; `DocxFieldExtractor` receives it by injection, not by `new`.

### 5.4 SIARA portal returning HTTP 503

**Symptom:** Orion logs show SIARA navigation failures; `SiaraWatchLoop` reports errors;
no new documents are downloaded.

**Triage steps:**

1. Determine if SIARA portal is in maintenance mode (check with the portal administrator
   or access the SIARA portal URL from a browser).
2. Check Orion readiness — during a SIARA outage Orion will report 503 from `/health/ready`
   because `SiaraWatchLoop` is not connected.
3. No action needed on the Prisma side during a transient SIARA outage: `SiaraWatchLoop`
   retries automatically. Documents already downloaded continue to flow through Athena and
   Reconciliator.
4. If the outage persists, monitor Seq for Orion recovery messages once SIARA is restored.
5. If SIARA credentials expired during the outage, rotate them (§4.2) before Orion's
   next reconnect attempt.

### 5.5 Reconciliator restart / recovery

The Reconciliator is the terminal stage. It receives `ExtractionCompletedEvent` from Athena
via SignalR and runs Classification → Export.

**Graceful restart (no data loss risk):**
```powershell
# Stop (SIGTERM / service manager stop)
# Reconciliator drains its current in-flight event before stopping.
# Restart:
dotnet "...ExxerCube.Prisma.Reconciliator.Worker.dll"
# Confirm readiness:
curl http://<reconciliator-host>:5202/health/ready
```

**Caution:** If Reconciliator is stopped while Athena is actively broadcasting events,
those events are not durably queued — they are in-memory only
(`InMemoryClearanceReplayGuard`). Events missed during a Reconciliator restart may need
to be reprocessed manually (re-trigger extraction on the affected documents via Web.UI).

**If Reconciliator fails to connect to Athena's hub after restart:**
- Confirm `ReconciliationClient__HubUrl` points to `http://<athena-host>:5201/hubs/reconciliation`.
- Confirm Athena is up and `/health/ready` returns 200.
- Restart Reconciliator; `ReconciliationHubClient` will retry the connection.

---

## 6. Database Backup and Recovery

> **Note:** The backup/recovery procedures below are SQL Server conventional procedures.
> This section documents what databases exist in Prisma and the recommended backup cadence.
> The actual backup jobs must be configured in SQL Server Agent or a DBA-managed solution
> for the target environment.

### 6.1 Databases

| Database | Owner | Contents |
|---|---|---|
| `PrismaDb` (or as configured in `DefaultConnection`) | Workers + Web.UI (app data) | Documents, audit trail, event log, field extractions, expediente records |
| `PrismaId` (or as configured in Web.UI `DefaultConnection`) | Web.UI only | ASP.NET Core Identity: users, roles, claims |

> The connection-string names are logical; the actual SQL Server database names are
> whatever the DBA sets in the connection strings. `PrismaDb` and `PrismaId` are
> the recommended names.

### 6.2 Recommended backup schedule

| Database | RPO target | Suggested schedule |
|---|---|---|
| `PrismaDb` | As low as 1 hour (audit trail + document state) | Full daily + transaction-log backup every 15 min |
| `PrismaId` | 24 hours (user accounts change rarely) | Full daily |

### 6.3 Manual backup (SQL Server)

```sql
-- Full backup of PrismaDb:
BACKUP DATABASE [PrismaDb]
  TO DISK = N'E:\Backups\PrismaDb_FULL_20260619.bak'
  WITH COMPRESSION, STATS = 10;

-- Transaction-log backup (PrismaDb must be in FULL recovery model):
BACKUP LOG [PrismaDb]
  TO DISK = N'E:\Backups\PrismaDb_LOG_20260619_1400.bak'
  WITH COMPRESSION, STATS = 10;
```

### 6.4 Restore procedure

```sql
-- 1. Take the target databases offline (stop all Prisma processes first):
ALTER DATABASE [PrismaDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;

-- 2. Restore the full backup:
RESTORE DATABASE [PrismaDb]
  FROM DISK = N'E:\Backups\PrismaDb_FULL_20260619.bak'
  WITH NORECOVERY, REPLACE, STATS = 10;

-- 3. Restore transaction log backups in chronological order:
RESTORE LOG [PrismaDb]
  FROM DISK = N'E:\Backups\PrismaDb_LOG_20260619_1400.bak'
  WITH RECOVERY, STATS = 10;
  -- Repeat for each LOG backup in sequence, using NORECOVERY for all but the last.

-- 4. Bring database back online:
ALTER DATABASE [PrismaDb] SET MULTI_USER;
```

After restore, run the `--migrate-only` step (§3.2) to confirm the schema is at the
expected migration level — the runner is idempotent and will log "no pending migrations"
if the restore was clean.

### 6.5 Post-restore validation

```powershell
# 1. Apply any pending migrations (idempotent):
dotnet ".../ExxerCube.Prisma.Orion.Worker.dll" --migrate-only

# 2. Start services in order (§2.3) and confirm all health endpoints:
curl http://localhost:5200/health/ready   # Orion
curl http://localhost:5201/health/ready   # Athena
curl http://localhost:5202/health/ready   # Reconciliator
curl http://localhost:5000/health         # Web.UI

# 3. Log into Web.UI and verify document records are present.
```

---

## 7. On-Call Escalation Path

> **This section is a placeholder.** Automated alerting and paging are planned under
> PRISMA-E3-S6 (not yet implemented). The wiring for alert rules, PagerDuty/OpsGenie
> integration, and escalation policies does not exist in the codebase today.
>
> When PRISMA-E3-S6 lands, this section should be replaced with:
> - The alert rule definitions (Seq / Prometheus / Azure Monitor source).
> - The on-call rotation configuration file path or link to the paging tool.
> - The escalation tier chart (L1 → L2 → engineering lead → engineering owner).
> - Severity definitions tied to health-endpoint status codes and business-impact thresholds.

### 7.1 Interim manual escalation procedure

Until automated alerting is wired, the recommended on-call procedure is:

1. **L1 — Liveness check:** Query `/health/live` on each service. If a process is down,
   restart it (§2.3) and notify the on-call engineer.

2. **L2 — Readiness check:** If the process is live but `/health/ready` returns 503,
   inspect the `data` field in the response body for the specific reason. Follow the
   relevant triage section (§5).

3. **L2 — Seq log review:** Filter Seq for `@l = "Error"` in the time window of the
   incident. Key sources: `"Orion"`, `"Athena"`, `"Reconciliator"`, `"Web.UI"`.

4. **L2 — Startup warnings:** On restart, each worker logs a `Warning` if the DB
   connection string is blank or placeholder. This blocks audit persistence but not the
   pipeline. Correct the env var and restart.

5. **L3 — SQL Server:** If `/health` on Web.UI returns `PrismaDbHealthCheck` unhealthy,
   verify SQL Server connectivity and the `ApplicationConnection` string.

6. **L3 — Engineering escalation:** If triage steps 1-5 do not resolve the incident within
   the SLA window, escalate to the engineering lead. Provide:
   - The output of each `/health/ready` endpoint.
   - Seq error log excerpt (last 50 lines of errors).
   - The specific triage step reached.

### 7.2 Alert rule placeholders (TODO — PRISMA-E3-S6)

| Alert | Source | Threshold | Severity | Paging config |
|---|---|---|---|---|
| Orion liveness lost | TODO | `/health/live` → connection refused for 60s | P1 | TODO |
| Athena liveness lost | TODO | `/health/live` → connection refused for 60s | P1 | TODO |
| Reconciliator liveness lost | TODO | `/health/live` → connection refused for 60s | P1 | TODO |
| Any service readiness 503 > 5min | TODO | `/health/ready` → HTTP 503 sustained | P2 | TODO |
| SQL Server unreachable | TODO | Web.UI `/health` → 503 | P1 | TODO |
| Migration failure | TODO | `--migrate-only` exit code 1 in CI/CD | P1 | TODO |

---

*End of runbook. Remember: this document requires production-access review before use.*
*Reviewer: **TODO** | Date verified: **TODO***
