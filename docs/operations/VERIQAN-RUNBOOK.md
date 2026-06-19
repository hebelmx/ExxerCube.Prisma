# Veriqan Worker — Operational Runbook

**Service:** `ExxerCube.Prisma.Veriqan.Worker`
**Code path:** `Prisma/Code/Src/CSharp/04 Services/Veriqan.Worker/`
**Story:** VERIQAN-E1-S9 (Wave-0)
**Last updated:** 2026-06-19

---

## Table of Contents

1. [Quick-reference config and endpoints](#1-quick-reference-config-and-endpoints)
2. [Monthly batch start](#2-monthly-batch-start)
3. [Exception-queue triage and retry](#3-exception-queue-triage-and-retry)
4. [Reference-bundle update procedure](#4-reference-bundle-update-procedure)
5. [Mid-batch resume](#5-mid-batch-resume)
6. [On-call escalation path](#6-on-call-escalation-path)
7. [SMTP configuration verification](#7-smtp-configuration-verification)

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

> **Auth note:** `POST /verify` and `POST /batch` have no authentication as of Wave-0.
> Production gating (E4) is deferred. Do not expose these endpoints to untrusted networks
> before E4 is implemented.

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
| `PasswordProtected` | PDF is encrypted and no password is configured for the institution | Decrypt the PDF before submission, or configure an institution password (not yet exposed via config in Wave-0) |
| `InsufficientExtractionCoverage` (BlockReason) | Fewer fields extracted than the configured minimum floor | PDF is blank, corrupt, or too layout-drifted; investigate the source document |
| `UnknownProduct` (BlockReason) | Product token from PDF did not match any entry in the reference bundle | Update `products.csv` in the bundle with the correct alias, or fix the source document |
| `InvalidBundle` (BlockReason) | Reference bundle failed to load | Check `Veriqan:CsvReferenceData:RootDirectory` and bundle CSVs; see Section 4 |
| `BLOCKED-from-binder` (in log) | Verdict signal is `Blocked` (not a pipeline failure — item completed) | This is a completed item counted in `blockedCount`, not `failedCount` |

Note: `Blocked` verdicts (BlockReason codes above) are **completed** items — they appear in
`completedCount` and `blockedCount`, not `failedCount`. Only items where the pipeline
returned `Result.Failure` or threw an unhandled exception are placed on the exception queue
and counted in `failedCount`.

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

*Runbook accuracy note: all routes, config keys, health-check field names, error prefixes,
and batch report fields in this document were verified against the source code as of
VERIQAN-E1 Wave-0 (2026-06-19). The exception-queue persistence (`BatchExceptionLog`) and
durable retry endpoint are explicitly deferred to E3-S3.*
