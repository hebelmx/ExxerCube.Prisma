# ADR-022: Audit Ledger + Schema-Protection (G-S2 / G-S4)

**Date**: 2026-06-21
**Status**: Accepted
**Deciders**: Owner + Development Team
**Tags**: security, audit, ledger, ddl-trigger, serilog, tamper-evidence, schema-protection
**Related**: INV-4/FR17 (audit tamper-evidence), CR4 (schema-protection DDL), ADR-014 (Identity), ADR-013 (metrics-persistence)
**Migration**: `20260621100000_AuditLedgerAndDdlTrigger` (chains after `20260621032128_AddOutboxEvents`)

---

## Context

Two owner-accepted security hardening items (G-S2 and G-S4) require SQL-level audit
protection and structured-log durability. Both are **deploy/infra-oriented** — they
produce no behaviour change visible to the application layer; they harden the database
underpinning it.

### G-S2 — Audit tamper-evidence (INV-4 / FR17)

`AuditRecords` is the authoritative record of every processing action in the Prisma
pipeline. A reviewer or regulator can query it to answer "who touched document X, when,
and with what result?". If a row in this table could be silently deleted or altered, the
audit trail loses its legal weight.

The owner ruling is: convert `AuditRecords` to a **SQL Server APPEND-ONLY LEDGER TABLE**
and route structured logs to both SEQ and SQL Server.

SQL Server Ledger (GA in SQL Server 2022, backported to 2019 CU13+) provides:
- **Cryptographic tamper-evidence**: each transaction is hashed into a blockchain digest
  stored in `sys.database_ledger_transactions`. Any tampering is detectable with
  `sp_verify_database_ledger`.
- **Engine-level immutability**: on an APPEND_ONLY ledger table, UPDATE and DELETE are
  rejected by the SQL engine before reaching any application code. There is no application-
  layer workaround.

`Serilog.Sinks.Seq` was already configured. A `Serilog.Sinks.MSSqlServer` sink
is now also declared in each host's `appsettings.json`. The SQL sink target is
`SerilogLogs` (auto-created on first connect), activated via the
`SERILOG_SQL_CONNECTION` environment variable. When the variable is absent or empty
(dev mode), the sink library silently skips initialisation — no boot failure.

### G-S4 — Schema-protection DDL trigger (CR4)

A rogue migration, an ops mistake, or a privilege escalation could `DROP TABLE` or
`ALTER TABLE` on a critical table and permanently destroy data or schema.

The owner ruling is: install a **DATABASE-level DDL trigger** that blocks `DROP_TABLE`
and `ALTER_TABLE` on the protected table set.

---

## Decision

### Ledger conversion (G-S2)

Convert `AuditRecords` to `APPEND_ONLY` ledger via a raw-SQL EF migration:

1. Rename the existing table to `AuditRecords_Legacy`.
2. Re-create it as `CREATE TABLE … WITH (LEDGER = ON (APPEND_ONLY = ON))`.
3. Copy all existing rows via `INSERT … SELECT`.
4. Drop the legacy table.
5. Recreate all application indexes (seven, matching the EF configuration).

All steps run inside the EF migration transaction. The EF model does not change; no
new C# entity or configuration is required.

Serilog MSSqlServer sink is declared in `Directory.Packages.props` as version 8.0.0
and referenced in all four host csproj files (Web.UI, Athena Worker, Orion Worker,
Reconciliator Worker). Config is read from `IConfiguration` (`appsettings.json`
`Serilog:WriteTo:MSSqlServer` section); no secrets are committed.

### DDL trigger (G-S4)

Install a `CREATE TRIGGER … ON DATABASE FOR DROP_TABLE, ALTER_TABLE` trigger via the
same migration:

```sql
CREATE TRIGGER [TR_ProtectCriticalSchema]
ON DATABASE
FOR DROP_TABLE, ALTER_TABLE
AS
BEGIN
    -- Bypass for PrismaTest_* databases (integration-test isolation)
    IF DB_NAME() LIKE N'PrismaTest_%' RETURN;

    DECLARE @ObjectName nvarchar(256) = EVENTDATA().value(...);

    IF LOWER(@ObjectName) IN (
        N'auditrecords', N'reviewcases', N'reviewdecisions',
        N'outboxevents', N'slastatus'
    )
    BEGIN
        ROLLBACK;
        RAISERROR(N'[TR_ProtectCriticalSchema] ...', 16, 1, ...) WITH NOWAIT;
    END;
END;
```

**Protected table set**: `AuditRecords`, `ReviewCases`, `ReviewDecisions`,
`OutboxEvents`, `SLAStatus`.

The trigger is installed using `migrationBuilder.Sql(..., suppressTransaction: true)`
because DDL trigger creation requires running outside an explicit transaction on some
SQL Server editions.

**Test-database bypass**: databases whose name matches `PrismaTest_%` are exempt.
`SqlServerContainerFixture.CreateIsolatedDatabaseAsync` always names databases with
that prefix, so integration tests can apply migrations and run rollbacks without the
trigger interfering.

---

## Protected Table Set Rationale

| Table | Reason protected |
|---|---|
| `AuditRecords` | Legal audit trail; now also a ledger — drop would destroy verifiability. |
| `ReviewCases` | Manual-review workflow; loss would cause cases to silently disappear from the queue. |
| `ReviewDecisions` | Reviewer decisions are the final arbitration record; immutable by policy. |
| `OutboxEvents` | At-least-once delivery guarantee; dropping triggers silent event loss. |
| `SLAStatus` | SLA deadlines and escalation state; loss causes compliance reporting gaps. |

Tables *not* protected (can be altered freely by migrations): `FileMetadata`, `Persona`,
`RequirementTypeDictionary`, `UnifiedMetadataRecords`. These are operational / reference
tables without the same immutability requirement.

---

## Serilog Sink Topology

```
All hosts (Web.UI / Athena / Orion / Reconciliator)
  → Serilog Console   (always on, dev + prod)
  → Serilog Seq       (env: SEQ_URL, default http://localhost:5341)
  → Serilog MSSqlServer  (env: SERILOG_SQL_CONNECTION, Warning+ only)
        table: SerilogLogs, autoCreateSqlTable: true
```

The MSSqlServer sink is restricted to `Warning` and above. Verbose/Debug application
logs remain Console/Seq only to avoid flooding the SQL log table with noise.

The SEQ sink was already configured with the `SEQ_URL` env-var guard (added in a prior
session — both Athena Worker `Program.cs` and Web.UI `Program.cs` default `SEQ_URL` to
`http://localhost:5341` when unset to prevent `UriFormatException` on cold boot).

---

## Deploy vs. Dev Note

Both features are **deploy-time concerns**:

- **Ledger table**: requires SQL Server 2022 (or 2019 CU13+). The Testcontainers image
  (`mcr.microsoft.com/mssql/server:2022-latest`) supports it fully. The live SQL box
  must be SQL Server 2022 for the migration to succeed; SQL Server 2019 without CU13+
  will fail with feature-not-supported. **Owner approval is required before applying to
  `DESKTOP-FB2ES22\SQL2025`**.
- **DDL trigger**: supported on all SQL Server editions from 2005+; no edition concern.
- **MSSqlServer Serilog sink**: purely additive config; does not affect any functional
  path. No secrets committed; activated via `SERILOG_SQL_CONNECTION` env var in prod.

**No migration was applied to the live SQL box.** All verification is via Testcontainers
SQL Server 2022.

---

## Consequences

- `AuditRecords` is now append-only and cryptographically tamper-evident on any SQL Server
  2022 deployment. Application code is unchanged — `INSERT` (the only write operation the
  `AuditLoggerService` and `QueuedAuditLoggerService` perform) continues to work.
- Any accidental or malicious `UPDATE`/`DELETE` on `AuditRecords` is rejected at the SQL
  engine level with error 37359, regardless of the caller's identity.
- `sp_verify_database_ledger` can be used by compliance officers to prove the audit trail
  has not been tampered with since the database was created.
- The five protected tables cannot be dropped or altered without first disabling the trigger
  (which itself requires `ALTER DATABASE` permission — a DBA-only action).
- Integration tests are unaffected: the `PrismaTest_` bypass ensures `MigrateAsync` /
  `EnsureCreated` / isolated-DB teardown continue to work in Testcontainers.
- `Serilog.Sinks.MSSqlServer 8.0.0` is centrally versioned in `Directory.Packages.props`.
  Upgrading requires checking the MTP/xUnit compatibility matrix (this package has no
  test-stack coupling; bump is straightforward).
- **SQL Server edition caveat**: if the live box runs SQL Server 2019 without CU13+ the
  ledger `CREATE TABLE` will fail with "Ledger is not supported in this edition." In that
  case the migration must be run against SQL Server 2022. No code change is needed —
  only the target SQL Server version matters.
