# Veriqan Worker — Operational Runbook

**Service:** `ExxerCube.Prisma.Veriqan.Worker`
**Code path:** `Prisma/Code/Src/CSharp/04 Services/Veriqan.Worker/`
**Story:** VERIQAN-E1-S9 (Wave-0); §§8–10 + auth/triage updates from Stories 6.1, 6.2, 6.5, 6.7, 6.8
**Last updated:** 2026-06-30

---

## Table of Contents

1. [Quick-reference config and endpoints](#1-quick-reference-config-and-endpoints)
2. [Monthly batch start](#2-monthly-batch-start)
3. [Exception-queue triage and retry](#3-exception-queue-triage-and-retry)
4. [Reference-bundle update procedure](#4-reference-bundle-update-procedure)
5. [Mid-batch resume](#5-mid-batch-resume)
6. [On-call escalation path](#6-on-call-escalation-path)
7. [SMTP configuration verification](#7-smtp-configuration-verification)
8. [Database migrations](#8-database-migrations)
9. [Durable result and reprocess-audit stores](#9-durable-result-and-reprocess-audit-stores)
10. [Audit immutability and required DB permissions](#10-audit-immutability-and-required-db-permissions)

---

## 1. Quick-reference config and endpoints

### HTTP endpoints

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/verify` | Single-statement verification |
| `POST` | `/batch` | Multi-statement batch verification |
| `GET` | `/health/live` | Liveness probe — always 200 if the process is up |
| `GET` | `/health/ready` | Readiness probe — 503 if any readiness check fails |
| `GET` | `/health` | Combined (liveness + readiness); 503 when not ready |

> **Auth note:** `POST /verify` and `POST /batch` are **JWT-bearer authenticated**
> (`Program.cs` adds `JwtBearer` authentication and decorates both endpoints with
> `.RequireAuthorization()`). Auth is **secure-by-default**: it is ON unless
> `Veriqan:Auth:Enabled` is explicitly set to `false`. The health endpoints
> (`/health`, `/health/live`, `/health/ready`) are always `AllowAnonymous`.
> Tokens are validated against the `Veriqan:Auth:Jwt` section (`Issuer`, `Audience`,
> `SigningKey` — HMAC-SHA256; issuer/audience validation is skipped when the
> corresponding key is omitted). A missing/placeholder `SigningKey` fails closed
> (every token rejected with 401) and is logged loudly at startup. A `FallbackPolicy`
> requires an authenticated user, so any future undecorated endpoint is not exposed
> anonymously by omission.
>
> The escape hatch `Veriqan:Auth:Enabled = false` installs a permissive allow-all
> policy for local/demo runs and emits a SECURITY WARNING — **never use it in an
> environment that handles real legal documents.**

### Required configuration keys

The following keys are audited at startup by `VeriqanConfigurationValidator`. Any absent or
empty key emits a structured **WARNING** log entry (the worker does not crash, but will not
function correctly):

| Config key | Purpose | Absence behaviour |
|---|---|---|
| `ConnectionStrings:VeriqanDb` | SQL Server connection string for durable verdict persistence | Falls back to IN-MEMORY — **worker is LIVE but NOT READY (503)** |
| `Veriqan:CsvReferenceData:RootDirectory` | Host path to institution CSV bundles | Reference lookups fail; readiness check fails (503) |
| `Veriqan:Smtp:Host` | SMTP relay for RED-verdict alert emails | Alert dispatch fails silently |
| `Veriqan:LegalBaseline:EncryptionKey` | AES-256 key (base64, 32 bytes) for encrypted SQL tolerance store | Required when SQL persistence is enabled |

Additional tunable keys (defaults shown):

| Config key | Default | Purpose |
|---|---|---|
| `Veriqan:BatchProcessor:MaxConcurrency` | `Environment.ProcessorCount` | Global worker-pool size for batch jobs |
| `Veriqan:PdfExtraction:MaxSizeBytes` | `52428800` (50 MB) | PDF size limit before rejection |
| `Veriqan:PdfExtraction:ParseTimeoutSeconds` | `30` | Wall-clock deadline per PDF parse |
| `Veriqan:Seq:ServerUrl` | `""` (disabled) | Serilog Seq sink; empty = console only |
| `Veriqan:OtlpEndpoint` | `http://localhost:4317` | OpenTelemetry OTLP collector endpoint |
| `Veriqan:Alerts:Recipients` | `[]` | Alert email recipients for RED verdicts |
| `Veriqan:Alerts:MaxRetryAttempts` | `3` | SMTP retry count |

Template for all production keys: `Prisma/Code/Src/CSharp/04 Services/Veriqan.Worker/appsettings.Production.json.template`

---

## 2. Monthly batch start

### Overview

A batch processes a list of statement PDF files in parallel. The `POST /batch` endpoint
accepts a JSON body containing an ordered list of statement submissions and optional
execution options.

### Request shape

```
POST /batch
Content-Type: application/json
```

```json
{
  "items": [
    {
      "pdf": "<base64-encoded PDF bytes>",
      "fileName": "statement-ACC-0001-sep-2025.pdf",
      "contextKey": {
        "institution": "Demo Bank (Iqubica)",
        "periodLabel": "Sep-Oct 2025",
        "accountRef": "ACC-0001",
        "productId": "TC-NL"
      }
    },
    {
      "pdf": "<base64-encoded PDF bytes>",
      "fileName": "statement-ACC-0002-sep-2025.pdf",
      "contextKey": {
        "institution": "Demo Bank (Iqubica)",
        "periodLabel": "Sep-Oct 2025",
        "accountRef": "ACC-0002"
      }
    }
  ],
  "options": {
    "maxDegreeOfParallelism": 4,
    "resume": false
  }
}
```

**Field notes:**

- `pdf` — raw PDF bytes, base64-encoded.
- `fileName` — informational; appears in all log lines for this item.
- `contextKey.institution` — must match the institution directory name in the reference bundle
  (spaces are preserved; the adapter normalises to filesystem-safe form internally, e.g.
  `"Demo Bank (Iqubica)"` → directory `Demo_Bank_(Iqubica)/`).
- `contextKey.periodLabel`, `accountRef`, `productId` — all optional; used to narrow
  reference-data lookups.
- `options.maxDegreeOfParallelism` — per-call parallelism (default 4). Overrides the global
  `Veriqan:BatchProcessor:MaxConcurrency` for this request only.
- `options.resume` — set `true` to skip statements already persisted in the result store
  (resume an interrupted batch; see Section 5).
- `options` itself may be omitted; defaults apply.

### Minimal curl example

```bash
# Encode one PDF and submit a single-item batch
PDF_B64=$(base64 -w0 /path/to/statement.pdf)

curl -s -X POST http://localhost:5000/batch \
  -H "Content-Type: application/json" \
  -d "{
    \"items\": [{
      \"pdf\": \"${PDF_B64}\",
      \"fileName\": \"statement.pdf\",
      \"contextKey\": { \"institution\": \"Demo Bank (Iqubica)\", \"periodLabel\": \"Sep-Oct 2025\" }
    }],
    \"options\": { \"maxDegreeOfParallelism\": 4, \"resume\": false }
  }"
```

### Success response (HTTP 202 Accepted)

```json
{
  "totalSubmitted": 50,
  "completedCount": 48,
  "failedCount": 2
}
```

The 202 response is returned only after the entire batch finishes (the endpoint is
synchronous with respect to the batch). For large batches this means the HTTP connection
stays open until all items are processed.

### What to watch during a batch

1. **Logs** (console or Seq if configured):
   - `Batch starting: {Total} items, MaxConcurrency={MaxConcurrency}, Resume={Resume}` — confirms the batch is running.
   - `Pipeline failure queued for {FileName}: {Error}` — each failed item is logged at WARNING.
   - `Batch complete: Total=... Completed=... Green=... Red=... Blocked=... Failed=... ThroughputPerSecond=... P95LatencyMs=...` — final summary.

2. **OpenTelemetry metrics** (meter name `ExxerCube.Prisma.Veriqan`):
   - `veriqan.statement.processed` (counter, tagged `verdict=green|red|blocked`) — throughput.
   - `veriqan.statement.duration` (histogram, ms) — p95 latency; NFR-1 target is ≤ 10 000 ms.
   - `veriqan.statement.exceptions` (counter) — items that failed to complete the pipeline.

3. **Health probe** — a 503 from `/health/ready` during a batch indicates a configuration
   problem (SQL connectivity lost, bundle unmounted). See Section 6.

---

## 3. Exception-queue triage and retry

### Current state (Wave-0)

The exception queue is **IN-MEMORY** — it exists only for the lifetime of a single `POST /batch`
call and is returned as part of the batch report (`ExceptionQueue` field, not currently
surfaced in the HTTP response body). The response body returns only the counts
(`failedCount`).

> **[TODO after E3-S3]** A durable `BatchExceptionLog` table will be persisted to SQL so
> failed items survive process restarts and can be queried/retried without re-submitting the
> whole batch.

### How to find failed statements now (Wave-0)

**Step 1 — Read the batch response.** The `failedCount` field in the 202 response tells you
how many items failed. A non-zero value means at least one item is on the exception queue.

**Step 2 — Query the logs.** Every failed item produces a WARNING log entry:

```
Pipeline failure queued for {FileName}: {Error}
```

or, for unexpected exceptions:

```
Unexpected exception processing {FileName}
```

If using Seq, search for:
```
@MessageTemplate like '%failure queued%' or @MessageTemplate like '%Unexpected exception%'
```

If using console logs, grep the output:
```bash
journalctl -u veriqan-worker --since "today" | grep -E "failure queued|Unexpected exception"
```

**Step 3 — Identify the failure reason.** The error message in the log carries a typed
reason prefix:

| Error prefix | Meaning | Resolution |
|---|---|---|
| `FileSizeLimitExceeded:` | PDF exceeds `Veriqan:PdfExtraction:MaxSizeBytes` (50 MB default) | Compress or split the PDF; or raise `MaxSizeBytes` if justified |
| `Timeout:` | PDF parse exceeded `Veriqan:PdfExtraction:ParseTimeoutSeconds` (30 s default) | PDF may be malformed or pathologically large; investigate the file; raise `ParseTimeoutSeconds` if justified |
| `PasswordProtected` | PDF is encrypted/password-protected and no password is configured for the institution (Story 6.7 — surfaced on both full and header extraction paths) | Supply the institution password via the configured password provider (`PasswordProtected — add institution password to config`), or route the document for manual handling |
| `InsufficientExtractionCoverage` (BlockReason) | Fewer fields extracted than the configured minimum floor | PDF is blank, corrupt, or too layout-drifted; investigate the source document |
| `UnknownProduct` (BlockReason) | Product token from PDF did not match any entry in the reference bundle | Update `products.csv` in the bundle with the correct alias, or fix the source document |
| `InvalidBundle` (BlockReason) | Reference bundle failed to load | Check `Veriqan:CsvReferenceData:RootDirectory` and bundle CSVs; see Section 4 |
| `BLOCKED-from-binder` (in log) | Verdict signal is `Blocked` (not a pipeline failure — item completed) | This is a completed item counted in `blockedCount`, not `failedCount` |

Note: `Blocked` verdicts (BlockReason codes above) are **completed** items — they appear in
`completedCount` and `blockedCount`, not `failedCount`. Only items where the pipeline
returned `Result.Failure` or threw an unhandled exception are placed on the exception queue
and counted in `failedCount`.

> **Password-protected PDFs (Story 6.7):** an encrypted/password-protected input statement
> now surfaces a clear `PasswordProtected — add institution password to config` failure on
> **both** the full and header extraction paths, instead of a generic parse error. Operator
> action: supply the institution password via the configured password provider, or route the
> document for manual handling.

### How to retry failed statements (Wave-0)

1. Identify the failing files from the log (see Step 2 above).
2. Resolve the root cause (compress oversized PDFs, remove encryption, fix the bundle).
3. Re-submit only the failing files via `POST /batch` with the corrected PDFs.

> **[TODO after E3-S3]** Once the durable `BatchExceptionLog` is available, a dedicated
> retry endpoint or query will allow re-queueing failed items without re-submitting the
> entire batch.

### Single-statement reprocess (available now)

If a statement previously completed but produced an incorrect verdict (e.g. due to a bundle
error now corrected), use `POST /verify` to re-run the pipeline for that statement
individually. The `IReprocessService` (`ReprocessService`) is registered in DI for future
consumers but is **not** wired into `VerificationPipeline.ProcessAsync` and has no HTTP
endpoint yet — there is no transparent in-pipeline reprocess today `[TODO after E3-S3]`. The
current reprocess path is re-submission: `POST /batch` with `options.resume = false` re-runs
all items unconditionally (bulk reprocess), or `POST /verify` re-runs a single statement.

---

## 4. Reference-bundle update procedure

### What a bundle is

A reference bundle is a directory of CSV files under the configured
`Veriqan:CsvReferenceData:RootDirectory`. Each institution has one sub-directory; the
directory name is derived from the institution name by replacing spaces with underscores and
stripping filesystem-invalid characters.

Example:
```
/opt/veriqan/reference-data/csv/
  Demo_Bank_(Iqubica)/
    bundle-metadata.csv          (REQUIRED)
    products.csv
    interest-rates.csv
    tolerance-config.csv
    validation-constants.csv
    mandatory-legends.csv
    sequential-images.csv
    promotions.csv
    client-accounts.csv
    client-accounts-entries.csv
    prior-statements.csv
    prior-statements-installments.csv
    expected-transactions.csv
```

### Authoring guide

Full CSV schema, column descriptions, example rows, and the sign-off process are documented
in:

```
Prisma/Data/Veriqan/reference-bundles/BUNDLE-AUTHORING-GUIDE.md
```

The canonical deployable location for bundles is:

```
Prisma/Data/Veriqan/reference-bundles/
```

### Update procedure

1. **Author the updated CSV(s)** following the schema in `BUNDLE-AUTHORING-GUIDE.md`.
   For any regulatory CSV (`interest-rates.csv`, `tolerance-config.csv`,
   `mandatory-legends.csv`, `validation-constants.csv`), obtain the required sign-offs
   from the bank's compliance team and legal counsel before proceeding.

2. **Validate the bundle** by running the CSV adapter against a sample statement in a
   non-production environment. Confirm zero schema errors appear in the worker log.

3. **Copy the updated bundle directory** to the host path configured in
   `Veriqan:CsvReferenceData:RootDirectory`. The adapter reads from disk on every request;
   no recompilation is needed.

4. **Restart or signal the worker.** Because there is no startup cache the new CSVs are
   effective immediately on the next request. For a running worker, a graceful restart
   is sufficient (no in-flight requests are affected — each statement pipeline resolves its
   bundle at execution time).

5. **Verify via health probe:**
   ```bash
   curl -s http://localhost:5000/health/ready | jq .
   ```
   Confirm `csvReferenceDataRoot: true` in the response `data` object. A `false` means
   `RootDirectory` is misconfigured or empty.

6. **Submit a test statement** via `POST /verify` to confirm the new bundle is resolved
   correctly (check logs for `InvalidBundle` or `UnknownProduct` errors).

> **BLOCKED on bank relationship:** No production bundle for any real institution can be
> created until the bank engagement (issue #17) is formalised. The `Demo_Bank_(Iqubica)`
> bundle is synthetic test data only. See the authoring guide.

---

## 5. Mid-batch resume

### What resume does

When `options.resume = true` in the `POST /batch` request, the batch processor checks each
statement's SHA-256 content hash against the `IVerificationResultStore` before dispatching
it to the pipeline. Items whose hash is already recorded as completed are **skipped** and
counted as `alreadyCompletedCount` in the batch report. Only items not yet completed are
processed.

This allows a batch to be re-submitted after an interruption (process restart, OOM kill,
etc.) and pick up from where it stopped, without re-running already-verified statements.

### Resume prerequisites

Resume requires durable persistence (`ConnectionStrings:VeriqanDb` configured). With the
in-memory fallback, `alreadyCompletedCount` will always be zero because the store does not
survive the restart.

> **[TODO after E3-S3]** A durable `BatchExceptionLog` will also be needed to resume
> exception-queue items. Currently only successfully-completed items are stored.

### How to resume an interrupted batch

1. Re-submit the **same full batch** via `POST /batch` with `options.resume = true` and
   the same `items` list (including the already-completed items — they will be skipped).

   ```bash
   curl -s -X POST http://localhost:5000/batch \
     -H "Content-Type: application/json" \
     -d '{ "items": [...same list as original...], "options": { "resume": true } }'
   ```

2. The response will show:
   - `completedCount` — newly-processed items in this run.
   - `alreadyCompletedCount` is not in the HTTP response body but is logged:
     `Batch complete: ... AlreadyCompleted={AlreadyCompleted} ...`

3. If `alreadyCompletedCount == totalSubmitted` the batch was already fully complete — no
   pipeline work was done (idempotent).

### Limitations (Wave-0)

- Resume skips items that completed **successfully** (Green, Red, or Blocked verdicts).
  Items that failed (placed on the exception queue) are **not** skipped — they will be
  retried on resume.
- There is no partial-progress API; the only way to check which items were previously
  completed is to query the SQL result store directly.

  > **[TODO after E3-S3]** A progress/status query endpoint will be added.

---

## 6. On-call escalation path

### Step 1 — Check liveness

```bash
curl -s http://localhost:5000/health/live
# Expected: HTTP 200, { "status": "Healthy", "description": "Veriqan worker process is running", ... }
```

A non-200 or connection-refused response means the process is down. Restart the worker.

### Step 2 — Check readiness

```bash
curl -s http://localhost:5000/health/ready
# Expected (healthy): HTTP 200, { "status": "Healthy", ... }
# Not ready:          HTTP 503, { "status": "Unhealthy", "data": { "sqlConnectivity": false, ... } }
```

The response `data` object contains three boolean fields:

| Field | What it checks | Failure meaning |
|---|---|---|
| `sqlConnectivity` | `VeriqanDbContext.Database.CanConnectAsync()` | SQL Server is unreachable, or `ConnectionStrings:VeriqanDb` is absent (in-memory fallback) |
| `csvReferenceDataRoot` | `Veriqan:CsvReferenceData:RootDirectory` exists and is non-empty | Bundle directory is missing, unmounted, or path is wrong |
| `toleranceCacheWarm` | `SqlLegalToleranceProvider.IsWarm` | Startup migration/seed sequence has not completed yet |

> **Critical readiness ruling:** The worker is always **LIVE** (process is up) but is **NOT READY
> (503)** when `ConnectionStrings:VeriqanDb` is absent or empty. The in-memory fallback loses
> all verdicts on restart and is therefore ineligible for production traffic. A 503 from
> `/health/ready` with `sqlConnectivity: false` means you must configure a real SQL
> connection string. Do not route production traffic to a worker returning 503 on
> `/health/ready`.

### Step 3 — Check startup warnings

On any anomaly, inspect the startup log for `VeriqanConfigurationValidator` warnings:

```
VeriqanDb connection string absent — persistence will be IN-MEMORY. ...
Critical Veriqan configuration key Veriqan:CsvReferenceData:RootDirectory is absent or empty. ...
```

These warnings appear at `[WRN]` level within the first few seconds of startup.

### Step 4 — Check SQL Server

If `sqlConnectivity: false`:

1. Confirm `ConnectionStrings:VeriqanDb` is set (environment variable
   `CONNECTIONSTRINGS__VERIQANDB` or `appsettings.Production.json`).
2. Test connectivity from the worker host:
   ```bash
   # Using sqlcmd (if available)
   sqlcmd -S <server> -U <user> -P <password> -Q "SELECT 1"
   ```
3. Confirm the `VeriqanDb` database exists and the service account has the required
   permissions (`db_datareader`, `db_datawriter`, `db_ddladmin` for migrations).

### Step 5 — Check the bundle mount

If `csvReferenceDataRoot: false`:

1. Confirm the path in `Veriqan:CsvReferenceData:RootDirectory` is correct and accessible.
2. Check that the institution sub-directory exists and contains at least one file.
3. If running in Docker, verify the volume mount:
   ```bash
   docker inspect veriqan-worker | jq '.[].Mounts'
   ```

### Step 6 — Check Seq / log aggregation

If `Veriqan:Seq:ServerUrl` is configured and Seq is unavailable, the worker continues
with console-only logging (the Seq sink failure does not crash the process). Confirm Seq
connectivity and check for buffered events.

### Escalation contacts

> [TODO — populate with actual team contacts for your deployment]

---

## 7. SMTP configuration verification

### What SMTP is used for

The pipeline's notify stage dispatches an alert email to `Veriqan:Alerts:Recipients` when
a statement produces a **RED verdict**. The worker uses a direct SMTP relay (not an SDK
email service); no credentials are required for an unauthenticated internal relay.

### SMTP config keys

| Key | Default | Notes |
|---|---|---|
| `Veriqan:Smtp:Host` | `localhost` | SMTP relay hostname or IP. Required for alerts. |
| `Veriqan:Smtp:Port` | `25` (dev) / `587` (prod template) | Typically 25 for unauthenticated relay, 587 for TLS |
| `Veriqan:Smtp:EnableSsl` | `false` (dev) / `true` (prod template) | TLS/STARTTLS |
| `Veriqan:Smtp:From` | `noreply@veriqan.local` | Sender address |
| `Veriqan:Smtp:UserName` | `null` | SMTP auth user; `null` = unauthenticated relay |
| `Veriqan:Smtp:Password` | `null` | SMTP auth password; `null` = unauthenticated relay |
| `Veriqan:Alerts:Recipients` | `[]` | JSON array of recipient email addresses. At least one required. |
| `Veriqan:Alerts:MaxRetryAttempts` | `3` | SMTP retry count on failure |
| `Veriqan:Alerts:BaseRetryDelayMs` | `500` | Base delay between retries (ms) |

If `Veriqan:Smtp:Host` is absent or empty, `VeriqanConfigurationValidator` emits a
WARNING at startup but does not block the worker. Alert emails will silently fail.

### Verification procedure

1. **Check the startup warning.** If `Veriqan:Smtp:Host` is absent, the validator logs:
   ```
   Critical Veriqan configuration key Veriqan:Smtp:Host is absent or empty. Purpose: SMTP host — RED-verdict alert emails cannot be dispatched without it.
   ```
   Fix by setting the key.

2. **Confirm the recipients list is non-empty.**
   ```bash
   # If using environment variable overrides, check:
   echo $VERIQAN__ALERTS__RECIPIENTS
   # Expected: a JSON array, e.g. '["compliance@yourdomain.com"]'
   ```

3. **Test SMTP relay connectivity** from the worker host:
   ```bash
   # Telnet test (port 25 or 587)
   telnet <smtp-host> <port>
   # Expect: "220 <hostname> ESMTP"
   ```

4. **Trigger a test alert** by submitting a statement that will produce a RED verdict
   (a statement with known compliance failures). Watch the worker logs for:
   ```
   # Alert sent successfully
   [INF] Alert dispatched for job {JobId} to {RecipientCount} recipient(s).
   # Alert failed (will retry MaxRetryAttempts times)
   [WRN] Alert dispatch failed for job {JobId}: {Error}
   ```

5. **Confirm delivery** in the recipient inbox or mail server queue.

> **Note:** In the current Wave-0 implementation there is no dry-run or test-email endpoint.
> The only way to trigger an alert is to process a statement that produces a RED verdict.
> A dedicated `/notify/test` endpoint is not yet implemented.

---

## 8. Database migrations

### Operational model (Story 6.8)

EF Core migrations + the legal-baseline seed can be applied in one of two ways:

1. **At host boot (default)** — for dev / standalone deployments. The host migrates and seeds
   on every startup.
2. **As a separate CI / pre-deploy step (12-factor)** — migrations run *before* the live
   server process starts, and the host does NOT run DDL at boot.

The mode is selected by a single config flag:

| Config key | Type | Default | Behaviour |
|---|---|---|---|
| `Veriqan:RunMigrationsAtStartup` | bool | `true` | `true` (dev/standalone): host applies EF Core migrations + seeds the legal baseline at startup. `false` (CI/12-factor): host does NOT run DDL — you must apply migrations separately first (see below). |

### Migrate-only command

To apply migrations + seed without starting the web host:

```bash
dotnet ExxerCube.Prisma.Veriqan.Worker.dll --migrate
```

- Applies EF Core migrations, seeds the legal baseline, warms the cache, then **exits** — it
  does NOT start the web host.
- Exit code `0` = success; non-zero = failure (`2` when `ConnectionStrings:VeriqanDb` is
  absent; `1` on cancellation or unexpected error). Wire this into CI as a gated pre-deploy
  step and abort the deploy on a non-zero exit.
- Reads `ConnectionStrings:VeriqanDb` (and `Veriqan:LegalBaseline:EncryptionKey` to seed the
  encrypted tolerance store) from the same config sources as the host.

### IMPORTANT — cache warming is NOT gated by the flag

`Veriqan:RunMigrationsAtStartup` gates **only the DDL migrate+seed**. The host **always**
warms the `SqlLegalToleranceProvider` in-process at startup (so the first verification
request does not fail). This means:

> Even with `Veriqan:RunMigrationsAtStartup = false`, the host still requires the database to
> be **already migrated and seeded** (by the `--migrate` step) before it boots. If the schema
> or legal baseline is missing, the host **fails loud at startup** — it does not silently fall
> back to in-code defaults. Run `--migrate` to completion (exit 0) before starting the host.

### CI artifact path — `ef migrations bundle` (O4, VERIQAN-E3-S5 remainder)

The `--migrate` CLI above requires deploying the full Worker binary. When a DBA needs to apply
schema changes **without** a Worker deployment — offline apply, a change-control window, or an
environment where nobody wants to stand up the whole service just to run DDL — use the portable
EF Core migrations bundle instead. It is a single self-contained native executable with the
entire migration lineage baked in; it does **not** seed the legal baseline or warm any cache (it
is DDL-only), so it is a schema-apply tool, not a substitute for `--migrate`.

**Where it comes from:** the `veriqan-migrations` job in
`Prisma/Code/Src/CSharp/.github/workflows/quality-gates.yml` builds it on every CI run and
uploads it as the `veriqan-efbundle` artifact. `dotnet-ef` is pinned in
`.config/dotnet-tools.json` (version `10.0.8`) to match the
`Microsoft.EntityFrameworkCore.*` `PackageVersion` line in `Directory.Packages.props` — bump
both together.

**Building it locally** (from the repo root, with the local tool manifest restored):

```bash
dotnet tool restore
dotnet ef migrations bundle \
  --project "Prisma/Code/Src/CSharp/02 Infrastructure/Veriqan.Infrastructure.Persistence/ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.csproj" \
  --self-contained \
  -r linux-x64 \
  --output veriqan-efbundle \
  --force
```

No `--startup-project` is needed: `VeriqanDbContextFactory`
(`EntityFramework/Design/VeriqanDbContextFactory.cs`) is an
`IDesignTimeDbContextFactory<VeriqanDbContext>`, so EF's tooling resolves the design-time
context straight from the Persistence project. `--self-contained -r linux-x64` matches the
container/CI runner target so the bundle runs with no .NET runtime installed on the target box.
The output binary (`veriqan-efbundle`) is git-ignored — never commit it; treat it as a build
artifact, rebuild or re-download it per deploy.

**Applying it:**

```bash
chmod +x veriqan-efbundle
./veriqan-efbundle --connection "Server=<host>,<port>;Database=<db>;User Id=<user>;Password=<password>;TrustServerCertificate=True;Encrypt=True"
```

Exit code `0` = success (including the no-op case where the database is already up to date);
non-zero = failure. The bundle is idempotent — safe to re-run against a database that already
has some or all migrations applied; it only applies what is missing, in lineage order.

**From-zero CI verification:** the same `veriqan-migrations` job also proves the bundle applies
the **full** migration lineage cleanly to a brand-new, empty database — the same guarantee a
first-time production rollout needs. It starts an ephemeral `mssql/server:2022-latest`
container, polls readiness with `sqlcmd -Q "SELECT 1"` in a bounded retry loop, creates an empty
database, runs the freshly built bundle against it, and asserts `veriqan.__EFMigrationsHistory`
contains at least one row. As of the migration lineage ending at
`20260728155959_AddBatchExceptionLog`, this applies all 10 migrations in order and exits `0`.
The container is torn down at the end of the job (`if: always()`), so a failed run never leaks a
container on the runner.

> **Caveat:** `quality-gates.yml` lives at `Prisma/Code/Src/CSharp/.github/workflows/`, not at
> the repo root, so GitHub Actions does not currently execute it automatically. The repo still
> treats it as the canonical CI definition; every command in the `veriqan-migrations` job has
> been verified by running it locally end-to-end (build the bundle, stand up a container, apply
> from zero, assert, tear down) against a real SQL Server 2022 container.

---

## 9. Durable result and reprocess-audit stores

### What is now persisted (Story 6.1)

Two SQL tables (schema `veriqan`) persist verification idempotency and the reprocess audit
trail so they **survive a process restart**:

| Table | Role |
|---|---|
| `veriqan.VerificationOutcomeSnapshots` | Idempotency / result cache, keyed by the document **content-hash** (SHA-256). Backs mid-batch resume (Section 5) and single-statement idempotency. |
| `veriqan.ReprocessAuditLog` | Append-only log of reprocess events (the reprocess audit trail). |

These **replace the former in-memory stores**. They are active **only when
`ConnectionStrings:VeriqanDb` is configured**. With no connection string the worker falls back
to in-memory, non-durable stores (and is **NOT READY / 503** — see Section 6); resume and the
reprocess audit trail do not survive a restart in that mode.

---

## 10. Audit immutability and required DB permissions

### Append-only audit protection (Story 6.2)

The append-only audit tables `veriqan.Dispositions` and `veriqan.ReprocessAuditLog` are
protected at two levels:

- **Database engine:** `AFTER UPDATE, DELETE` triggers that `THROW` (error 51000) and roll the
  transaction back — UPDATE and DELETE are prohibited.
- **Application:** an EF Core `SaveChanges` interceptor (`ImmutableEntityInterceptor`) rejects
  mutations early.

`veriqan.VerificationOutcomeSnapshots` is **intentionally mutable** (reprocess legitimately
UPDATEs it) and is **NOT** trigger-protected.

### Known limitation

The triggers do **not** fire on `TRUNCATE TABLE`, and a principal with `ALTER TABLE`,
`CONTROL`, or `db_owner` membership can `DISABLE TRIGGER` and bypass them entirely. The
engine-level tamper-evidence therefore holds **only** under a least-privilege deployment.

### Deployment checklist item (hard requirement)

> The application's runtime DB principal MUST be granted least privilege on the `veriqan`
> schema — `INSERT` and `SELECT` (and `UPDATE` only where legitimately needed, e.g.
> `veriqan.VerificationOutcomeSnapshots`) — and MUST NOT be a member of `db_owner` and MUST
> NOT be granted `ALTER TABLE` or `CONTROL` on the audit tables `veriqan.Dispositions` and
> `veriqan.ReprocessAuditLog`. Without this, the append-only audit guarantee is void
> (`TRUNCATE`/`DISABLE TRIGGER` bypass). Operational `TRUNCATE`/restore on the audit tables
> (DR only) must run under a privileged DBA role, never under the application principal.

Note: this least-privilege requirement is stricter than the `db_ddladmin` grant mentioned for
migrations in Section 6 Step 4 — grant `db_ddladmin` (or the broader rights needed for DDL)
to the **migration/`--migrate` principal**, and run the live host under the restricted
application principal described above.

---

*Runbook accuracy note: all routes, config keys, health-check field names, error prefixes,
and batch report fields in this document were verified against the source code as of
VERIQAN-E1 Wave-0 (2026-06-19). The exception-queue persistence (`BatchExceptionLog`) and
durable retry endpoint are explicitly deferred to E3-S3.*
