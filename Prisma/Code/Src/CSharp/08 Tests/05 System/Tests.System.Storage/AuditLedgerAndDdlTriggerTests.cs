using ExxerCube.Prisma.Infrastructure.Database.EntityFramework;
using ExxerCube.Prisma.Testing.Infrastructure.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.System.Storage;

/// <summary>
/// Integration tests for migration <c>20260621100000_AuditLedgerAndDdlTrigger</c>.
///
/// G-S2 tests: verify that AuditRecords is an APPEND-ONLY LEDGER table after migration.
///   - INSERT succeeds (append is always permitted on a ledger table).
///   - UPDATE is blocked by the SQL engine (ledger row immutability, error 37359).
///   - DELETE is blocked by the SQL engine (ledger row immutability, error 37359).
///   - sys.tables.ledger_type = 2 (APPEND_ONLY_LEDGER).
///
/// G-S4 tests: verify that the DDL trigger blocks DROP TABLE on protected tables and
///             allows DROP TABLE on non-protected tables.
///
/// All tests run against an isolated Testcontainers SQL Server 2022 database
/// (mcr.microsoft.com/mssql/server:2022-latest). SQL Server 2022 supports ledger tables
/// fully. No changes are applied to the live DESKTOP-FB2ES22\SQL2025 instance.
/// </summary>
public sealed class AuditLedgerAndDdlTriggerTests : IDisposable
{
    private readonly SqlServerContainerFixture _fixture;
    private string _connectionString = string.Empty;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// Creates an isolated database for this test class and applies migrations up to and
    /// including <c>AuditLedgerAndDdlTrigger</c> via <see cref="DbContext.Database.MigrateAsync"/>.
    /// </summary>
    public AuditLedgerAndDdlTriggerTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureAvailable();

        _connectionString = _fixture
            .CreateIsolatedDatabaseAsync(nameof(AuditLedgerAndDdlTriggerTests), Ct)
            .GetAwaiter()
            .GetResult();

        // Apply ALL migrations including the new ledger + DDL trigger migration.
        var options = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseSqlServer(_connectionString)
            .Options;
        using var ctx = new PrismaDbContext(options);
        ctx.Database.MigrateAsync(Ct).GetAwaiter().GetResult();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // G-S2 — Ledger table verification
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// After applying the migration, <c>sys.tables</c> must report <c>ledger_type = 2</c>
    /// (APPEND_ONLY_LEDGER) for the AuditRecords table.
    /// </summary>
    [Fact]
    public async Task AuditRecords_AfterMigration_IsAppendOnlyLedgerTable()
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(Ct);

        await using var cmd = conn.CreateCommand();
        // ledger_type_desc is more stable across SQL Server 2022 patch levels than ledger_type
        // (the integer enum has varied between RTM and later CUs).
        // APPEND_ONLY_LEDGER = the table is an append-only ledger (no UPDATE/DELETE permitted).
        cmd.CommandText = @"
            SELECT ledger_type_desc
            FROM sys.tables
            WHERE name = N'AuditRecords'
              AND schema_id = SCHEMA_ID(N'dbo');";

        var result = await cmd.ExecuteScalarAsync(Ct);

        result.ShouldNotBeNull("AuditRecords table must exist after migration");
        var ledgerTypeDesc = result!.ToString()!;
        // SQL Server 2022 (mcr.microsoft.com/mssql/server:2022-latest) reports
        // ledger_type_desc = 'APPEND_ONLY_LEDGER_TABLE' for append-only ledger tables.
        // Earlier docs listed 'APPEND_ONLY_LEDGER'; both mean the same thing.
        ledgerTypeDesc.ShouldBe("APPEND_ONLY_LEDGER_TABLE",
            $"ledger_type_desc must be APPEND_ONLY_LEDGER_TABLE after the G-S2 migration (actual: {ledgerTypeDesc})");
    }

    /// <summary>
    /// INSERT into the ledger table must succeed — append is the only permitted operation.
    /// </summary>
    [Fact]
    public async Task AuditRecords_Insert_SucceedsOnLedgerTable()
    {
        var auditId = Guid.NewGuid().ToString("N");

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(Ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO [dbo].[AuditRecords]
                ([AuditId],[ActionType],[CorrelationId],[Stage],[Success],[Timestamp])
            VALUES
                (@AuditId, 1, @CorrelationId, 1, 1, GETUTCDATE());";
        cmd.Parameters.AddWithValue("@AuditId", auditId);
        cmd.Parameters.AddWithValue("@CorrelationId", Guid.NewGuid().ToString("N"));

        // Should not throw.
        var rows = await cmd.ExecuteNonQueryAsync(Ct);
        rows.ShouldBe(1, "INSERT must succeed on an append-only ledger table");
    }

    /// <summary>
    /// UPDATE on a ledger append-only table must be rejected by the SQL engine with error 37359.
    /// </summary>
    [Fact]
    public async Task AuditRecords_Update_IsBlockedByLedger()
    {
        // First INSERT a row so we have something to update.
        var auditId = Guid.NewGuid().ToString("N");

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(Ct);

        await using (var insertCmd = conn.CreateCommand())
        {
            insertCmd.CommandText = @"
                INSERT INTO [dbo].[AuditRecords]
                    ([AuditId],[ActionType],[CorrelationId],[Stage],[Success],[Timestamp])
                VALUES
                    (@AuditId, 1, @CorrelationId, 1, 1, GETUTCDATE());";
            insertCmd.Parameters.AddWithValue("@AuditId", auditId);
            insertCmd.Parameters.AddWithValue("@CorrelationId", Guid.NewGuid().ToString("N"));
            await insertCmd.ExecuteNonQueryAsync(Ct);
        }

        // Attempt UPDATE — must fail.
        await using var updateCmd = conn.CreateCommand();
        updateCmd.CommandText = @"
            UPDATE [dbo].[AuditRecords]
            SET    [UserId] = N'tampered'
            WHERE  [AuditId] = @AuditId;";
        updateCmd.Parameters.AddWithValue("@AuditId", auditId);

        var exception = await Should.ThrowAsync<SqlException>(
            async () => await updateCmd.ExecuteNonQueryAsync(Ct));

        // SQL Server raises error 37359 when UPDATE is attempted on an append-only ledger table.
        exception.Number.ShouldBe(37359,
            $"Expected SQL error 37359 (ledger table modification denied) but got {exception.Number}: {exception.Message}");
    }

    /// <summary>
    /// DELETE on a ledger append-only table must be rejected by the SQL engine with error 37359.
    /// </summary>
    [Fact]
    public async Task AuditRecords_Delete_IsBlockedByLedger()
    {
        var auditId = Guid.NewGuid().ToString("N");

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(Ct);

        await using (var insertCmd = conn.CreateCommand())
        {
            insertCmd.CommandText = @"
                INSERT INTO [dbo].[AuditRecords]
                    ([AuditId],[ActionType],[CorrelationId],[Stage],[Success],[Timestamp])
                VALUES
                    (@AuditId, 1, @CorrelationId, 1, 1, GETUTCDATE());";
            insertCmd.Parameters.AddWithValue("@AuditId", auditId);
            insertCmd.Parameters.AddWithValue("@CorrelationId", Guid.NewGuid().ToString("N"));
            await insertCmd.ExecuteNonQueryAsync(Ct);
        }

        await using var deleteCmd = conn.CreateCommand();
        deleteCmd.CommandText = @"
            DELETE FROM [dbo].[AuditRecords]
            WHERE [AuditId] = @AuditId;";
        deleteCmd.Parameters.AddWithValue("@AuditId", auditId);

        var exception = await Should.ThrowAsync<SqlException>(
            async () => await deleteCmd.ExecuteNonQueryAsync(Ct));

        // Same error 37359 is raised for DELETE on an append-only ledger table.
        exception.Number.ShouldBe(37359,
            $"Expected SQL error 37359 (ledger table modification denied) but got {exception.Number}: {exception.Message}");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // G-S4 — DDL trigger verification
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the DDL trigger <c>TR_ProtectCriticalSchema</c> exists at the database
    /// level after migration.
    /// </summary>
    [Fact]
    public async Task DdlTrigger_AfterMigration_ExistsOnDatabase()
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(Ct);

        await using var cmd = conn.CreateCommand();
        // parent_class = 0 means DATABASE-level trigger (not table-level).
        cmd.CommandText = @"
            SELECT COUNT(1)
            FROM sys.triggers
            WHERE name = N'TR_ProtectCriticalSchema'
              AND parent_class = 0;";

        var count = (int)(await cmd.ExecuteScalarAsync(Ct))!;
        count.ShouldBe(1, "TR_ProtectCriticalSchema DATABASE trigger must exist after migration");
    }

    /// <summary>
    /// Attempts to DROP a protected table in a database whose name does NOT match the
    /// 'PrismaTest_%' bypass. The trigger must block it.
    ///
    /// Because CreateIsolatedDatabaseAsync always prefixes with 'PrismaTest_', we provision
    /// a separate database with a non-matching name, apply migrations, and then confirm DROP
    /// TABLE AuditRecords is rejected.
    /// </summary>
    [Fact]
    public async Task DdlTrigger_DropProtectedTable_IsBlockedInProductionNamedDb()
    {
        // Create a database whose name does NOT match 'PrismaTest_%' to avoid the bypass.
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var prodLikeDbName = $"PrismaGateTest{suffix}";

        // Use a master-level connection to create/drop the DB.
        var masterConnectionString = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master"
        }.ConnectionString;

        await using var masterConn = new SqlConnection(masterConnectionString);
        await masterConn.OpenAsync(Ct);

        await using (var createDbCmd = masterConn.CreateCommand())
        {
            createDbCmd.CommandText = $@"
                IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'{prodLikeDbName}')
                    CREATE DATABASE [{prodLikeDbName}];";
            await createDbCmd.ExecuteNonQueryAsync(Ct);
        }

        try
        {
            var prodLikeConnStr = new SqlConnectionStringBuilder(_connectionString)
            {
                InitialCatalog = prodLikeDbName
            }.ConnectionString;

            // Apply all migrations so the trigger is installed and tables exist.
            var dbOptions = new DbContextOptionsBuilder<PrismaDbContext>()
                .UseSqlServer(prodLikeConnStr)
                .Options;
            await using (var ctx = new PrismaDbContext(dbOptions))
            {
                await ctx.Database.MigrateAsync(Ct);
            }

            // Attempt to DROP a protected table — trigger must block it.
            await using var prodConn = new SqlConnection(prodLikeConnStr);
            await prodConn.OpenAsync(Ct);

            await using var dropCmd = prodConn.CreateCommand();
            dropCmd.CommandText = "DROP TABLE [dbo].[AuditRecords];";

            var exception = await Should.ThrowAsync<SqlException>(
                async () => await dropCmd.ExecuteNonQueryAsync(Ct));

            // ShouldContain on string + customMessage collides with the Shouldly IEnumerable<char>
            // overload in 4.x; use a boolean check + ShouldBeTrue instead.
            exception.Message.Contains("TR_ProtectCriticalSchema").ShouldBeTrue(
                $"DDL trigger must raise the protection error. Actual: {exception.Message}");
        }
        finally
        {
            // Clean up the prod-like DB.
            try
            {
                await using var dropDbCmd = masterConn.CreateCommand();
                dropDbCmd.CommandText = $@"
                    IF EXISTS (SELECT name FROM sys.databases WHERE name = N'{prodLikeDbName}')
                    BEGIN
                        ALTER DATABASE [{prodLikeDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                        DROP DATABASE [{prodLikeDbName}];
                    END;";
                await dropDbCmd.ExecuteNonQueryAsync(CancellationToken.None);
            }
            catch { /* non-fatal cleanup */ }
        }
    }

    /// <summary>
    /// DROP TABLE on a non-protected table must succeed.
    /// Creates a throwaway table that is not in the protected set and drops it.
    /// Runs in the PrismaTest_ prefixed DB so the trigger's test-bypass fires —
    /// verifying the bypass allows non-protected-table drops cleanly.
    /// </summary>
    [Fact]
    public async Task DdlTrigger_DropNonProtectedTable_Succeeds()
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(Ct);

        // Create a throwaway table that is not in the protected set.
        await using (var createCmd = conn.CreateCommand())
        {
            createCmd.CommandText = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'ThrowawayUnprotected')
                    CREATE TABLE [dbo].[ThrowawayUnprotected] ([Id] int PRIMARY KEY);";
            await createCmd.ExecuteNonQueryAsync(Ct);
        }

        // Drop it — must not throw (bypass fires for PrismaTest_ prefixed DBs,
        // and even without bypass the trigger only blocks the protected set).
        await using var dropCmd = conn.CreateCommand();
        dropCmd.CommandText = "DROP TABLE [dbo].[ThrowawayUnprotected];";

        await dropCmd.ExecuteNonQueryAsync(Ct);
        // No exception = pass.
    }

    /// <inheritdoc/>
    public void Dispose() { }
}
