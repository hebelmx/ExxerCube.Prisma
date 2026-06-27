# Prisma — Data Retention Policy

**Status:** Active · **Owner:** Compliance / Operations · **Applies to:** ExxerCube.Prisma production deployments
**Related:** PRISMA-E3-S1 (immutable audit ledger), ADR-012 (audit architecture), `Migrations/20260621100000_AuditLedgerAndDdlTrigger.cs`

## 1. Audit trail — 7-year immutable retention

The `AuditRecords` table is the regulated audit trail for all document-processing actions
(ingestion, OCR, fusion, classification, export, manual review, reconciliation). It records
who/what (ProcessId, UserId), when (Timestamp, UTC), the action (ActionType), the pipeline
stage (Stage), the correlation/file identity, and success/error outcome.

**Retention period: 7 years** from the row's `Timestamp`, aligned with Mexican financial-sector
record-keeping obligations (CNBV CUB / applicable banking record-retention requirements). Audit
rows MUST NOT be deleted or modified before the 7-year period elapses.

### 1.1 Immutability (how it is enforced)

`AuditRecords` is a **SQL Server append-only ledger table** (`LEDGER = ON (APPEND_ONLY = ON)`),
created by migration `20260621100000_AuditLedgerAndDdlTrigger.cs`:

- `INSERT` (append) is the only permitted write. `UPDATE` and `DELETE` are rejected by the SQL
  engine (error 37359) — verified by `AuditLedgerAndDdlTriggerTests` (insert ok; update blocked;
  delete blocked).
- A database-level DDL trigger (`TR_ProtectCriticalSchema`) blocks `DROP`/`ALTER` of the audit
  and review tables in production databases (test databases named `PrismaTest_%` are exempt for
  isolation).
- The ledger's built-in cryptographic digest provides tamper evidence; no application-level HMAC
  chaining is required while the ledger feature is in use.

Because the table is append-only, **retention is enforced by NOT deleting** — there is no purge
job for audit rows within the 7-year window, and deletion is technically impossible at the DB
level regardless.

### 1.2 Platform requirement

Ledger tables require **SQL Server 2019 CU13+ or SQL Server 2022/2025**. The migration applies
`LEDGER = ON` via raw SQL; on an unsupported engine the migration fails fast with a clear SQL
error (rather than silently creating a mutable table). Production SQL Server instances MUST be a
supported version before deploying this schema. (The Testcontainers test image is
`mcr.microsoft.com/mssql/server:2025-latest`.)

### 1.3 Archival after 7 years

After the 7-year window, audit rows may be archived to cold storage and removed under a
controlled, dual-authorized procedure. Because the live table is append-only, any approved
archival/purge is performed by a DBA via the ledger's supported migration path (e.g. exporting to
an archival ledger, then dropping the historical partition) — never by the application login.
This procedure is out of scope for the application and is a DBA/compliance-gated operation.

## 2. Other data classes (summary)

| Data | Store | Retention | Notes |
|------|-------|-----------|-------|
| Audit trail | `AuditRecords` (ledger) | 7 years (immutable) | This document, §1 |
| Processed documents / expedientes | application DB + file storage | per business policy | Operational; not covered by the immutable-audit guarantee |
| SIRO export artifacts | file storage | per business policy | Regenerable from source + audit |
| SIARA session/credential material | not persisted (passthrough) | n/a | See ADR-010 (no persisted identity) |

## 3. Review status

| Reviewer (prod-access) | Date | Notes |
|------------------------|------|-------|
| _TODO — requires a reviewer with production-environment access to confirm the retention period and archival procedure against current CNBV obligations._ | | |
