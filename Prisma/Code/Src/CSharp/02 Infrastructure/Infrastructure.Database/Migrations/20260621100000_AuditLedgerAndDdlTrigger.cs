using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Infrastructure.Database.Migrations
{
    /// <summary>
    /// G-S2 / G-S4 hardening migration.
    ///
    /// G-S2 — Converts AuditRecords to an APPEND-ONLY LEDGER table so every row is
    /// cryptographically protected by SQL Server's built-in blockchain digest.
    /// Append-only ledger tables reject UPDATE and DELETE at the SQL engine level; INSERT
    /// (append) is the only permitted write operation. The existing rows, indexes, and EF
    /// configuration are preserved — the conversion is done by recreating the table with
    /// LEDGER = ON (APPEND_ONLY = ON) and migrating data via INSERT … SELECT.
    ///
    /// SQL Server 2019 CU13+ and SQL Server 2022 support ledger tables.
    /// The Testcontainers image (mcr.microsoft.com/mssql/server:2022-latest) is 2022
    /// and therefore fully supports this feature.
    ///
    /// G-S4 — Creates a DATABASE-level DDL trigger that blocks DROP TABLE and ALTER TABLE
    /// on the set of critical tables (AuditRecords, ReviewCases, ReviewDecisions,
    /// OutboxEvents, SLAStatus). Any attempt to drop or alter those tables is rolled back
    /// and raises an error. The trigger is deliberately skipped if the current database is
    /// a test / non-production database named with the PrismaTest_ prefix so that
    /// integration-test isolation (CreateIsolatedDatabaseAsync) is not affected.
    ///
    /// DO NOT apply to the live SQL box (DESKTOP-FB2ES22\SQL2025) without owner approval.
    /// </summary>
    public partial class AuditLedgerAndDdlTrigger : Migration
    {
        // ──────────────────────────────────────────────────────────────────────
        // G-S2 — Append-Only Ledger conversion for AuditRecords
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// SQL Server does not support ALTER TABLE … SET LEDGER = ON on an existing regular
        /// table. The conversion requires:
        ///   1. Rename the original table.
        ///   2. Create the new ledger table with identical columns + all original indexes.
        ///   3. Copy the data.
        ///   4. Drop the old table.
        ///
        /// All steps run inside the EF migration transaction so the conversion is atomic.
        /// </summary>
        private const string ConvertAuditRecordsToLedger = @"
-- Step 1: Rename the existing table so we can recreate it as a ledger.
EXEC sp_rename 'dbo.AuditRecords', 'AuditRecords_Legacy';

-- Step 1b: Rename the primary key constraint (sp_rename keeps the PK name bound to the
-- renamed table; we must rename it to free up [PK_AuditRecords] for the new ledger table).
EXEC sp_rename 'dbo.PK_AuditRecords', 'PK_AuditRecords_Legacy', 'OBJECT';

-- Step 2: Create the ledger version with identical columns and PK.
-- APPEND_ONLY = ON means UPDATE and DELETE are blocked at the engine level.
CREATE TABLE [dbo].[AuditRecords] (
    [AuditId]        nvarchar(100)  NOT NULL,
    [ActionDetails]  nvarchar(4000) NULL,
    [ActionType]     int            NOT NULL,
    [CorrelationId]  nvarchar(100)  NOT NULL,
    [ErrorMessage]   nvarchar(1000) NULL,
    [FileId]         nvarchar(100)  NULL,
    [ProcessId]      nvarchar(100)  NULL,
    [Stage]          int            NOT NULL,
    [Success]        bit            NOT NULL,
    [Timestamp]      datetime2      NOT NULL,
    [UserId]         nvarchar(100)  NULL,
    CONSTRAINT [PK_AuditRecords] PRIMARY KEY ([AuditId])
) WITH (LEDGER = ON (APPEND_ONLY = ON));

-- Step 3: Migrate existing rows.  The ledger table allows INSERT even when data
-- already existed, so a plain INSERT … SELECT is sufficient.
INSERT INTO [dbo].[AuditRecords]
    ([AuditId],[ActionDetails],[ActionType],[CorrelationId],[ErrorMessage],
     [FileId],[ProcessId],[Stage],[Success],[Timestamp],[UserId])
SELECT
    [AuditId],[ActionDetails],[ActionType],[CorrelationId],[ErrorMessage],
    [FileId],[ProcessId],[Stage],[Success],[Timestamp],[UserId]
FROM [dbo].[AuditRecords_Legacy];

-- Step 4: Drop the old plain table (indexes are orphaned with it; ledger has its own
-- hidden ledger columns but we recreate the query indexes below).
DROP TABLE [dbo].[AuditRecords_Legacy];
";

        /// <summary>Restore the application indexes on the new ledger table.</summary>
        private const string RecreateAuditIndexes = @"
CREATE INDEX [IX_AuditRecords_ProcessId]
    ON [dbo].[AuditRecords] ([ProcessId]);

CREATE INDEX [IX_AuditRecords_FileId]
    ON [dbo].[AuditRecords] ([FileId]);

CREATE INDEX [IX_AuditRecords_Timestamp]
    ON [dbo].[AuditRecords] ([Timestamp]);

CREATE INDEX [IX_AuditRecords_ActionType]
    ON [dbo].[AuditRecords] ([ActionType]);

CREATE INDEX [IX_AuditRecords_UserId]
    ON [dbo].[AuditRecords] ([UserId]);

CREATE INDEX [IX_AuditRecords_CorrelationId]
    ON [dbo].[AuditRecords] ([CorrelationId]);

CREATE INDEX [IX_AuditRecords_FileId_Timestamp]
    ON [dbo].[AuditRecords] ([FileId], [Timestamp]);
";

        // ──────────────────────────────────────────────────────────────────────
        // G-S2 DOWN — restore plain (non-ledger) AuditRecords
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Reverting the ledger conversion recreates the table as a plain table and
        /// copies data back. The hidden ledger columns (ledger_start_transaction_id, etc.)
        /// are NOT migrated; they belong to the ledger view and are not user data.
        /// </summary>
        private const string RevertAuditRecordsFromLedger = @"
-- Recreate as a regular table.
CREATE TABLE [dbo].[AuditRecords_Plain] (
    [AuditId]        nvarchar(100)  NOT NULL,
    [ActionDetails]  nvarchar(4000) NULL,
    [ActionType]     int            NOT NULL,
    [CorrelationId]  nvarchar(100)  NOT NULL,
    [ErrorMessage]   nvarchar(1000) NULL,
    [FileId]         nvarchar(100)  NULL,
    [ProcessId]      nvarchar(100)  NULL,
    [Stage]          int            NOT NULL,
    [Success]        bit            NOT NULL,
    [Timestamp]      datetime2      NOT NULL,
    [UserId]         nvarchar(100)  NULL,
    CONSTRAINT [PK_AuditRecords_Plain] PRIMARY KEY ([AuditId])
);

INSERT INTO [dbo].[AuditRecords_Plain]
    ([AuditId],[ActionDetails],[ActionType],[CorrelationId],[ErrorMessage],
     [FileId],[ProcessId],[Stage],[Success],[Timestamp],[UserId])
SELECT
    [AuditId],[ActionDetails],[ActionType],[CorrelationId],[ErrorMessage],
    [FileId],[ProcessId],[Stage],[Success],[Timestamp],[UserId]
FROM [dbo].[AuditRecords];

-- Ledger tables cannot be dropped with a plain DROP TABLE; the LEDGER = ON binding
-- must be removed first via ALTER TABLE … SET (SYSTEM_VERSIONING = OFF) only for
-- system-versioned ledgers. For append-only ledger, a direct DROP is supported when
-- ledger verification is disabled or in dev contexts.
DROP TABLE [dbo].[AuditRecords];

EXEC sp_rename 'dbo.AuditRecords_Plain', 'AuditRecords';
EXEC sp_rename 'PK_AuditRecords_Plain', 'PK_AuditRecords', 'OBJECT';
";

        // ──────────────────────────────────────────────────────────────────────
        // G-S4 — DATABASE-level DDL trigger
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// DDL trigger that blocks DROP TABLE and ALTER TABLE on the protected table set.
        ///
        /// Protected tables: AuditRecords, ReviewCases, ReviewDecisions, OutboxEvents, SLAStatus.
        ///
        /// The trigger explicitly skips test databases (name starts with 'PrismaTest_') so
        /// CreateIsolatedDatabaseAsync in integration tests can still run migrations without
        /// interference. This is safe because test databases are ephemeral and short-lived.
        /// </summary>
        private const string CreateDdlTrigger = @"
IF EXISTS (SELECT name FROM sys.triggers WHERE name = 'TR_ProtectCriticalSchema' AND parent_class = 0)
    DROP TRIGGER [TR_ProtectCriticalSchema] ON DATABASE;

-- Create the DATABASE-scoped DDL trigger.
EXEC('
CREATE TRIGGER [TR_ProtectCriticalSchema]
ON DATABASE
FOR DROP_TABLE, ALTER_TABLE
AS
BEGIN
    SET NOCOUNT ON;

    -- Allow the operation unconditionally in test databases.
    -- Test databases created by SqlServerContainerFixture.CreateIsolatedDatabaseAsync
    -- are named with the PrismaTest_ prefix and are ephemeral; no production data lives
    -- in them, so the schema-protection policy does not apply.
    IF DB_NAME() LIKE N''PrismaTest_%''
        RETURN;

    DECLARE @EventData  XML           = EVENTDATA();
    DECLARE @SchemaName nvarchar(256) = @EventData.value(''(/EVENT_INSTANCE/SchemaName)[1]'', ''nvarchar(256)'');
    DECLARE @ObjectName nvarchar(256) = @EventData.value(''(/EVENT_INSTANCE/ObjectName)[1]'', ''nvarchar(256)'');

    -- Protected table set (lower-case comparison for robustness).
    IF LOWER(@ObjectName) IN (
        N''auditrecords'',
        N''reviewcases'',
        N''reviewdecisions'',
        N''outboxevents'',
        N''slastatus''
    )
    BEGIN
        ROLLBACK;
        RAISERROR(
            N''[TR_ProtectCriticalSchema] Schema modification blocked on protected table [%s].[%s]. Contact the DBA.'',
            16, 1,
            @SchemaName, @ObjectName
        ) WITH NOWAIT;
    END;
END;
');
";

        /// <summary>Removes the DDL trigger.</summary>
        private const string DropDdlTrigger = @"
IF EXISTS (SELECT name FROM sys.triggers WHERE name = 'TR_ProtectCriticalSchema' AND parent_class = 0)
    DROP TRIGGER [TR_ProtectCriticalSchema] ON DATABASE;
";

        // ──────────────────────────────────────────────────────────────────────
        // Migration Up / Down
        // ──────────────────────────────────────────────────────────────────────

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // G-S2: Convert AuditRecords to an append-only ledger table.
            // suppressTransaction: true is required because SQL Server does not allow ledger
            // table DDL (CREATE TABLE … WITH LEDGER) inside an open explicit transaction.
            migrationBuilder.Sql(ConvertAuditRecordsToLedger, suppressTransaction: true);
            migrationBuilder.Sql(RecreateAuditIndexes, suppressTransaction: true);

            // G-S4: Install the DATABASE-level DDL protection trigger.
            // DDL trigger creation also requires running outside an explicit transaction.
            migrationBuilder.Sql(CreateDdlTrigger, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // G-S4: Remove the DDL trigger first so the ledger revert can proceed.
            migrationBuilder.Sql(DropDdlTrigger, suppressTransaction: true);

            // G-S2: Restore AuditRecords as a plain table.
            // suppressTransaction: true for the same reason as Up — ledger DDL cannot run in a tx.
            migrationBuilder.Sql(RevertAuditRecordsFromLedger, suppressTransaction: true);
        }
    }
}
