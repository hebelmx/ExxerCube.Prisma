// <copyright file="DbContextCoexistenceTests.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using ExxerCube.Prisma.Infrastructure.Database.EntityFramework;
using ExxerCube.Prisma.Testing.Infrastructure.Fixtures;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.IntegrationTests;

/// <summary>
/// Proves that <see cref="VeriqanDbContext"/> (veriqan schema + veriqan.__EFMigrationsHistory)
/// and <see cref="PrismaDbContext"/> (dbo schema) can coexist on the SAME SQL Server database
/// without any collision — no table conflicts, no migration-history-table conflicts, and no
/// mutual interference after both have applied their respective schema initialisation steps.
///
/// <para>
/// This is the carry-forward integration test from Story 1.3 (deferred from that story to
/// the dedicated Testcontainers DB-coexistence task, P2-A).
/// </para>
///
/// <para>
/// Test order:
/// 1. PrismaDbContext.Database.EnsureCreatedAsync() — creates dbo tables (EnsureCreated path, same
///    as all other Prisma storage tests; PrismaDbContext has no EF migrations).
/// 2. VeriqanDbContext.Database.MigrateAsync() — runs InitialVeriqanSchema migration, which calls
///    EnsureSchema("veriqan") and then creates veriqan.VerificationJobs, veriqan.Findings,
///    veriqan.JobVerdicts, and records the migration in veriqan.__EFMigrationsHistory.
/// Both operations target the SAME isolated database on the shared SQL Server container.
/// </para>
/// </summary>
public sealed class DbContextCoexistenceTests
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly ILogger<DbContextCoexistenceTests> _logger;

    /// <summary>
    /// Initialises the test class. <paramref name="fixture"/> is injected by the xUnit v3 assembly
    /// fixture mechanism declared in <c>AssemblyFixtures.cs</c>.
    /// </summary>
    public DbContextCoexistenceTests(
        SqlServerContainerFixture fixture,
        ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _logger = XUnitLogger.CreateLogger<DbContextCoexistenceTests>(output);
    }

    /// <summary>
    /// Proves that <see cref="VeriqanDbContext"/> and <see cref="PrismaDbContext"/> coexist on the
    /// same SQL Server database without schema or migrations-history collision.
    ///
    /// <para>Assertions:</para>
    /// <list type="bullet">
    ///   <item>All three Veriqan tables (VerificationJobs, Findings, JobVerdicts) exist in schema <c>veriqan</c>.</item>
    ///   <item>No Veriqan table appears in schema <c>dbo</c>.</item>
    ///   <item>A <c>__EFMigrationsHistory</c> row exists in schema <c>veriqan</c>.</item>
    ///   <item>If PrismaDbContext created a <c>dbo.__EFMigrationsHistory</c> table it is DISTINCT
    ///         from the veriqan one (i.e. two separate TABLE_SCHEMA rows).</item>
    ///   <item>At least one known PrismaDbContext table (FileMetadata) survived the Veriqan migration
    ///         and PrismaDbContext can still open without error.</item>
    ///   <item>A <see cref="VerificationJob"/> can be written and read back through
    ///         <see cref="VeriqanDbContext"/> on the shared database.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task Coexistence_BothContextsOnSameDatabase_NoCollision()
    {
        // ── Arrange ─────────────────────────────────────────────────────────────────────────────
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(
            "veriqan_coexist",
            ct);

        _logger.LogInformation(
            "Isolated database created. Connection = {Cs}",
            connectionString);

        // ── Act: Step 1 — PrismaDbContext.EnsureCreatedAsync ────────────────────────────────────
        // PrismaDbContext uses EnsureCreated (no EF migrations); it creates all dbo-schema tables.
        var prismaOptions = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using (var prismaCtx = new PrismaDbContext(prismaOptions))
        {
            await prismaCtx.Database.EnsureCreatedAsync(ct);
            _logger.LogInformation("PrismaDbContext.EnsureCreatedAsync completed.");
        }

        // ── Act: Step 2 — VeriqanDbContext.MigrateAsync ─────────────────────────────────────────
        // VeriqanDbContext has a real EF migration (InitialVeriqanSchema) that:
        //   a) calls EnsureSchema("veriqan")
        //   b) creates veriqan.VerificationJobs, veriqan.Findings, veriqan.JobVerdicts
        //   c) records the migration in veriqan.__EFMigrationsHistory
        var veriqanOptions = new DbContextOptionsBuilder<VeriqanDbContext>()
            .UseSqlServer(
                connectionString,
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "veriqan"))
            .Options;

        await using (var veriqanCtx = new VeriqanDbContext(veriqanOptions))
        {
            await veriqanCtx.Database.MigrateAsync(ct);
            _logger.LogInformation("VeriqanDbContext.MigrateAsync completed.");
        }

        // ── Assert via raw SQL ───────────────────────────────────────────────────────────────────
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        // (a) Veriqan tables exist and ALL live in schema 'veriqan' ─────────────────────────────
        var veriqanExpectedTables = new[] { "VerificationJobs", "Findings", "JobVerdicts" };

        foreach (var tableName in veriqanExpectedTables)
        {
            var schemaForTable = await QuerySingleStringAsync(
                connection,
                ct,
                @"SELECT TABLE_SCHEMA
                  FROM INFORMATION_SCHEMA.TABLES
                  WHERE TABLE_NAME = @tableName
                    AND TABLE_TYPE = 'BASE TABLE'",
                ("@tableName", tableName));

            schemaForTable.ShouldNotBeNull(
                $"Table '{tableName}' should exist after VeriqanDbContext.MigrateAsync but was not found in INFORMATION_SCHEMA.");
            schemaForTable.ShouldBe(
                "veriqan",
                $"Table '{tableName}' should live in schema 'veriqan' but found it in schema '{schemaForTable}'.");

            _logger.LogInformation(
                "Veriqan table verified: {Schema}.{Table}",
                schemaForTable,
                tableName);
        }

        // Negative: none of the Veriqan tables appear in 'dbo' ──────────────────────────────────
        foreach (var tableName in veriqanExpectedTables)
        {
            var dboExists = await QuerySingleStringAsync(
                connection,
                ct,
                @"SELECT TOP 1 TABLE_SCHEMA
                  FROM INFORMATION_SCHEMA.TABLES
                  WHERE TABLE_NAME  = @tableName
                    AND TABLE_SCHEMA = 'dbo'
                    AND TABLE_TYPE  = 'BASE TABLE'",
                ("@tableName", tableName));

            dboExists.ShouldBeNull(
                $"Veriqan table '{tableName}' must NOT exist in schema 'dbo', but it was found there.");
            _logger.LogInformation(
                "Confirmed '{Table}' is absent from dbo (as expected).",
                tableName);
        }

        // (b) veriqan.__EFMigrationsHistory exists and has at least one row ─────────────────────
        var historyTableSchema = await QuerySingleStringAsync(
            connection,
            ct,
            @"SELECT TABLE_SCHEMA
              FROM INFORMATION_SCHEMA.TABLES
              WHERE TABLE_NAME  = '__EFMigrationsHistory'
                AND TABLE_SCHEMA = 'veriqan'
                AND TABLE_TYPE  = 'BASE TABLE'");

        historyTableSchema.ShouldNotBeNull(
            "__EFMigrationsHistory should exist in schema 'veriqan' after VeriqanDbContext.MigrateAsync.");
        historyTableSchema.ShouldBe("veriqan");
        _logger.LogInformation(
            "__EFMigrationsHistory found in schema 'veriqan'.");

        // Verify the InitialVeriqanSchema migration row is present ───────────────────────────────
        var migrationId = await QuerySingleStringAsync(
            connection,
            ct,
            "SELECT TOP 1 MigrationId FROM [veriqan].[__EFMigrationsHistory] ORDER BY MigrationId");

        migrationId.ShouldNotBeNull(
            "veriqan.__EFMigrationsHistory should have at least one row after MigrateAsync.");
        migrationId.ShouldContain(
            "InitialVeriqanSchema",
            Case.Insensitive,
            $"The migration row should reference InitialVeriqanSchema but was '{migrationId}'.");
        _logger.LogInformation(
            "Migration row found in veriqan.__EFMigrationsHistory: {MigrationId}",
            migrationId);

        // (b) additional: EF migrations history tables are DISTINCT per schema ──────────────────
        // Count how many __EFMigrationsHistory tables exist across all schemas.
        // EnsureCreated does NOT create a migrations-history table (it skips the migrations
        // framework entirely), so we expect exactly one row here (the veriqan one).
        var historyTableCount = await QueryScalarIntAsync(
            connection,
            ct,
            @"SELECT COUNT(*)
              FROM INFORMATION_SCHEMA.TABLES
              WHERE TABLE_NAME = '__EFMigrationsHistory'
                AND TABLE_TYPE = 'BASE TABLE'");

        // Log the actual schemas found so the test report is informative.
        var historySchemas = await QueryColumnAsync(
            connection,
            ct,
            @"SELECT TABLE_SCHEMA
              FROM INFORMATION_SCHEMA.TABLES
              WHERE TABLE_NAME = '__EFMigrationsHistory'
                AND TABLE_TYPE = 'BASE TABLE'
              ORDER BY TABLE_SCHEMA");

        _logger.LogInformation(
            "__EFMigrationsHistory table(s) found across all schemas: Count={Count}, Schemas=[{Schemas}]",
            historyTableCount,
            string.Join(", ", historySchemas));

        // The veriqan history table must exist; there must be no dbo history table (EnsureCreated
        // does not create one, so only veriqan should be present).
        historySchemas.ShouldContain("veriqan",
            "The veriqan schema must have a __EFMigrationsHistory table.");
        historySchemas.ShouldNotContain("dbo",
            "PrismaDbContext uses EnsureCreated (not migrations), so dbo.__EFMigrationsHistory must NOT exist.");

        // (c) PrismaDbContext tables survived the Veriqan migration ─────────────────────────────
        var prismaTableSchema = await QuerySingleStringAsync(
            connection,
            ct,
            @"SELECT TABLE_SCHEMA
              FROM INFORMATION_SCHEMA.TABLES
              WHERE TABLE_NAME  = 'FileMetadata'
                AND TABLE_TYPE  = 'BASE TABLE'");

        prismaTableSchema.ShouldNotBeNull(
            "PrismaDbContext's FileMetadata table should still exist after VeriqanDbContext.MigrateAsync ran.");
        _logger.LogInformation(
            "PrismaDbContext table 'FileMetadata' still present in schema '{Schema}' after Veriqan migration.",
            prismaTableSchema);

        // PrismaDbContext can still open and query without error ─────────────────────────────────
        await using (var prismaVerify = new PrismaDbContext(prismaOptions))
        {
            var canConnect = await prismaVerify.Database.CanConnectAsync(ct);
            canConnect.ShouldBeTrue(
                "PrismaDbContext should still be able to connect after VeriqanDbContext migration.");
            _logger.LogInformation(
                "PrismaDbContext.CanConnectAsync returned true after Veriqan migration — context still functional.");
        }

        // (d) Optional: write + read a VerificationJob through VeriqanDbContext ─────────────────
        var jobId = Guid.NewGuid();
        var contentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"test-payload-{jobId}")));

        await using (var writeCtx = new VeriqanDbContext(veriqanOptions))
        {
            var job = new VerificationJob(
                id: jobId,
                contentHash: contentHash,
                receivedAtUtc: DateTimeOffset.UtcNow,
                status: VerificationJobStatus.Pending);

            writeCtx.VerificationJobs.Add(job);
            await writeCtx.SaveChangesAsync(ct);
            _logger.LogInformation(
                "VerificationJob written: Id={Id}, Hash={Hash}",
                jobId,
                contentHash);
        }

        await using (var readCtx = new VeriqanDbContext(veriqanOptions))
        {
            var loaded = await readCtx.VerificationJobs
                .SingleOrDefaultAsync(j => j.Id == jobId, ct);

            loaded.ShouldNotBeNull(
                $"VerificationJob {jobId} should be readable back from veriqan.VerificationJobs.");
            loaded!.ContentHash.ShouldBe(contentHash);
            loaded.Status.ShouldBe(VerificationJobStatus.Pending);
            _logger.LogInformation(
                "VerificationJob round-trip verified: Id={Id}, Status={Status}",
                loaded.Id,
                loaded.Status);
        }

        _logger.LogInformation(
            "All coexistence assertions passed. " +
            "VeriqanDbContext (veriqan schema) and PrismaDbContext (dbo schema) coexist on the same database " +
            "with distinct migrations-history tables and no interference.");
    }

    // ── Raw SQL helpers ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes a scalar string query. Returns <c>null</c> if no row was returned.
    /// </summary>
    private static async Task<string?> QuerySingleStringAsync(
        SqlConnection connection,
        CancellationToken ct,
        string sql,
        params (string name, object value)[] parameters)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value);
        }

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is DBNull or null ? null : result.ToString();
    }

    /// <summary>
    /// Executes a scalar int query. Returns 0 if no row was returned.
    /// </summary>
    private static async Task<int> QueryScalarIntAsync(
        SqlConnection connection,
        CancellationToken ct,
        string sql)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is DBNull or null ? 0 : Convert.ToInt32(result);
    }

    /// <summary>
    /// Executes a query that returns a list of strings from the first column.
    /// </summary>
    private static async Task<List<string>> QueryColumnAsync(
        SqlConnection connection,
        CancellationToken ct,
        string sql)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<string>();
        while (await reader.ReadAsync(ct))
        {
            var value = reader.IsDBNull(0) ? null : reader.GetString(0);
            if (value is not null)
            {
                results.Add(value);
            }
        }

        return results;
    }
}
