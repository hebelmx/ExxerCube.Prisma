using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Infrastructure.Database.Migrations
{
    /// <summary>
    /// Widens <c>AuditRecords.ErrorMessage</c> from <c>nvarchar(1000)</c> to <c>nvarchar(4000)</c>.
    ///
    /// A long DbUpdateException message (SQL Server error text + a stack fragment) overflowed the
    /// 1000-character cap, which produced a secondary <c>DbUpdateException</c> on the audit INSERT
    /// itself ("String or binary data would be truncated"). Owner approved widening to 4000 chars
    /// (NOT nvarchar(max)) on 2026-06-24.
    ///
    /// <para><strong>Why this migration drops and recreates a DDL trigger.</strong>
    /// <c>AuditRecords</c> was converted to an APPEND-ONLY LEDGER table by migration
    /// <c>20260621100000_AuditLedgerAndDdlTrigger</c> (G-S2) and is additionally protected by the
    /// database-level DDL trigger <c>TR_ProtectCriticalSchema</c> (G-S4), which ROLLS BACK any
    /// <c>ALTER TABLE</c> against the protected table set (AuditRecords, ReviewCases, ReviewDecisions,
    /// OutboxEvents, SLAStatus) in any database NOT named <c>PrismaTest_%</c>. So a bare
    /// <c>ALTER COLUMN</c> would be rolled back by that guard. Per the owner's direction (2026-06-24)
    /// the migration therefore: (1) DISABLES the guard (drops <c>TR_ProtectCriticalSchema</c>),
    /// (2) widens the column, then (3) RE-ENABLES the guard (recreates the identical trigger) so the
    /// app continues to pass the schema-protection inspection.</para>
    ///
    /// <para><strong>The widen is ledger-safe.</strong> Per SQL Server ledger documentation, changing
    /// "the length of variable length columns" via <c>ALTER COLUMN</c> is an explicitly SUPPORTED
    /// alteration on ledger tables because it does not impact the underlying data or the row hashes
    /// captured in the ledger. No table rebuild is required, the cryptographic ledger digest/history
    /// is preserved unchanged, and all existing audit rows are untouched. (Changing a column's data
    /// type would not be supported — only the length increase is.) See
    /// https://learn.microsoft.com/sql/relational-databases/security/ledger/ledger-limits#altering-columns
    ///</para>
    ///
    /// <para>All statements run with <c>suppressTransaction: true</c>, mirroring
    /// <c>20260621100000_AuditLedgerAndDdlTrigger</c> — DDL trigger create/drop must not run inside an
    /// explicit migration transaction.</para>
    ///
    /// The <c>TR_ProtectCriticalSchema</c> body below is copied VERBATIM from
    /// <c>20260621100000_AuditLedgerAndDdlTrigger</c> so the re-enabled guard is byte-identical to the
    /// original. The trigger skips <c>PrismaTest_%</c> databases, so Testcontainers / EnsureCreated
    /// test isolation is unaffected (those DBs never get the trigger and use a plain, non-ledger table).
    ///
    /// DO NOT apply to live SQL (DESKTOP-FB2ES22\SQL2025) without owner approval.
    /// Chains after <c>20260621100000_AuditLedgerAndDdlTrigger</c>.
    /// </summary>
    public partial class WidenAuditErrorMessageColumn : Migration
    {
        // ── Step 1/3: disable the schema-protection guard ──────────────────────────
        private const string DropDdlTrigger = @"
IF EXISTS (SELECT name FROM sys.triggers WHERE name = 'TR_ProtectCriticalSchema' AND parent_class = 0)
    DROP TRIGGER [TR_ProtectCriticalSchema] ON DATABASE;
";

        // ── Step 2/3: widen the column (ledger-safe varlen length increase) ────────
        private const string WidenErrorMessage = @"
ALTER TABLE [dbo].[AuditRecords] ALTER COLUMN [ErrorMessage] nvarchar(4000) NULL;
";

        private const string NarrowErrorMessage = @"
ALTER TABLE [dbo].[AuditRecords] ALTER COLUMN [ErrorMessage] nvarchar(1000) NULL;
";

        // ── Step 3/3: re-enable the schema-protection guard ────────────────────────
        // Copied VERBATIM from 20260621100000_AuditLedgerAndDdlTrigger so the re-enabled guard is
        // byte-identical to the original.
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

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Disable the guard so the protected-table ALTER is not rolled back.
            migrationBuilder.Sql(DropDdlTrigger, suppressTransaction: true);

            // 2) Widen ErrorMessage to nvarchar(4000). Ledger-safe: a varlen length increase does not
            //    touch existing data or row hashes (see class remarks).
            migrationBuilder.Sql(WidenErrorMessage, suppressTransaction: true);

            // 3) Re-enable the guard (byte-identical to the original).
            migrationBuilder.Sql(CreateDdlTrigger, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1) Disable the guard.
            migrationBuilder.Sql(DropDdlTrigger, suppressTransaction: true);

            // 2) Restore ErrorMessage to nvarchar(1000). NOTE: narrowing is only safe if every stored
            //    value already fits 1000 chars; rows written after the Up() widen may not, in which case
            //    this Down() will fail at the engine level. Reverting is expected only in dev contexts.
            migrationBuilder.Sql(NarrowErrorMessage, suppressTransaction: true);

            // 3) Re-enable the guard.
            migrationBuilder.Sql(CreateDdlTrigger, suppressTransaction: true);
        }
    }
}
