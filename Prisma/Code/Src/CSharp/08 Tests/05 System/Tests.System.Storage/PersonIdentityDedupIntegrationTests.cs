using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Infrastructure.Database;
using ExxerCube.Prisma.Infrastructure.Database.EntityFramework;
using ExxerCube.Prisma.Testing.Infrastructure.Fixtures;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.System.Storage;

/// <summary>
/// G-H5 / FR10 / INV-2 — Integration tests proving that
/// <see cref="DbPersonIdentityResolverService"/> deduplicates <see cref="Persona"/> records
/// across documents that present the same RFC in different formats.
/// </summary>
/// <remarks>
/// Each test class provisions its own isolated SQL Server database on the shared assembly-level
/// container via <see cref="SqlServerContainerFixture.CreateIsolatedDatabaseAsync"/> so that
/// write-heavy test classes can run in parallel without colliding (ADR docker-test-isolation-2026-06).
/// Schema is applied via <c>EnsureCreatedAsync</c> — additive only, no live DB touched.
/// </remarks>
public sealed class PersonIdentityDedupIntegrationTests : IDisposable
{
    private readonly DbContextOptions<PrismaDbContext> _dbOptions;
    private readonly string _connectionString;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// Provisions a fresh isolated database and applies the EF model schema.
    /// </summary>
    public PersonIdentityDedupIntegrationTests(
        SqlServerContainerFixture fixture,
        ITestOutputHelper output)
    {
        fixture.EnsureAvailable();

        _connectionString = fixture
            .CreateIsolatedDatabaseAsync(nameof(PersonIdentityDedupIntegrationTests), Ct)
            .GetAwaiter()
            .GetResult();

        _dbOptions = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseSqlServer(_connectionString)
            .Options;

        // Apply schema (EnsureCreated — additive, NOT against a live production DB).
        using var ctx = new PrismaDbContext(_dbOptions);
        ctx.Database.EnsureCreatedAsync(Ct).GetAwaiter().GetResult();

        _ = output; // available if needed for XUnitLogger
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the SUT.  When no <paramref name="auditLogger"/> is supplied a no-op NSubstitute
    /// stub is used so that audit writes are silently absorbed without hitting the DB.
    /// Pass a real <see cref="AuditLoggerService"/> when the test needs to assert on audit rows.
    /// </summary>
    private DbPersonIdentityResolverService CreateSut(PrismaDbContext ctx, IAuditLogger? auditLogger = null)
    {
        var logger = NullLogger<DbPersonIdentityResolverService>.Instance;
        var audit = auditLogger ?? CreateStubAuditLogger();
        return new DbPersonIdentityResolverService(ctx, logger, audit);
    }

    private static IAuditLogger CreateStubAuditLogger()
    {
        var stub = Substitute.For<IAuditLogger>();
        stub.LogAuditAsync(
                Arg.Any<AuditActionType>(),
                Arg.Any<ProcessingStage>(),
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<bool>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<string?>())
            .Returns(Task.FromResult(Result.Success()));
        return stub;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Core dedup test (the G-H5 key deliverable)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// G-H5 gate: two documents carry the same RFC in different formats —
    /// "PEGJ850101ABC" (13-char with name discriminant) and "PEG-850101-ABC" (hyphenated 12-char).
    /// After both calls to <see cref="DbPersonIdentityResolverService.FindOrCreateAsync"/>,
    /// exactly ONE <see cref="Persona"/> row exists in the database.
    /// </summary>
    [Fact]
    public async Task FindOrCreateAsync_TwoDocumentsSameRfcDifferentFormats_PersistsExactlyOnePersona()
    {
        // Arrange — document 1 presents the 13-char form with name-discriminant letter.
        var doc1Persona = new Persona
        {
            Nombre = "Juan",
            Paterno = "Perez",
            Materno = "Garcia",
            Rfc = "PEGJ850101ABC",       // 13-char with 'J' name discriminant
            Caracter = "Contribuyente",
            PersonaTipo = "Fisica",
        };

        // Document 2 presents the hyphenated 12-char cross-format variant.
        var doc2Persona = new Persona
        {
            Nombre = "Juan",
            Paterno = "Perez",
            Materno = "Garcia",
            Rfc = "PEG-850101-ABC",      // hyphenated 12-char — same person
            Caracter = "Contribuyente",
            PersonaTipo = "Fisica",
        };

        // Act — two separate scopes simulate two processing pipelines.
        Persona firstResult;
        await using (var ctx1 = new PrismaDbContext(_dbOptions))
        {
            var sut1 = CreateSut(ctx1);
            var result1 = await sut1.FindOrCreateAsync(doc1Persona, doc1Persona.Rfc, Ct);
            result1.IsSuccess.ShouldBeTrue($"First FindOrCreateAsync failed: {result1.Error}");
            firstResult = result1.Value!;
            firstResult.ShouldNotBeNull();
            firstResult.ParteId.ShouldBeGreaterThan(0, "SQL IDENTITY should have assigned a ParteId");
        }

        Persona secondResult;
        await using (var ctx2 = new PrismaDbContext(_dbOptions))
        {
            var sut2 = CreateSut(ctx2);
            var result2 = await sut2.FindOrCreateAsync(doc2Persona, doc2Persona.Rfc, Ct);
            result2.IsSuccess.ShouldBeTrue($"Second FindOrCreateAsync failed: {result2.Error}");
            secondResult = result2.Value!;
            secondResult.ShouldNotBeNull();
        }

        // Assert — both calls returned the SAME persisted row.
        secondResult.ParteId.ShouldBe(
            firstResult.ParteId,
            "Both RFC formats must resolve to the same Persona (exactly one row)");

        // Assert — database has exactly ONE Persona row for this RFC family.
        await using var queryCtx = new PrismaDbContext(_dbOptions);
        var allPersonas = await queryCtx.Persona.ToListAsync(Ct);
        var dedupCount = allPersonas.Count;
        dedupCount.ShouldBe(1, "Exactly ONE Persona row should exist after two FindOrCreateAsync calls with RFC variants");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FindByRfcAsync — DB lookup contracts
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// FindByRfcAsync returns <c>success(null)</c> when no row exists for the given RFC.
    /// </summary>
    [Fact]
    public async Task FindByRfcAsync_WhenNoRowExists_ReturnsSuccessWithNull()
    {
        await using var ctx = new PrismaDbContext(_dbOptions);
        var sut = CreateSut(ctx);

        var result = await sut.FindByRfcAsync("NOTEXIST000001", Ct);

        result.IsSuccessMayBeNull.ShouldBeTrue("Not-found is a success-with-null, not an error");
        result.Value.ShouldBeNull();
    }

    /// <summary>
    /// FindByRfcAsync returns the persisted row when the 13-char RFC was stored and the
    /// 12-char cross-format variant is queried (variant-matching path).
    /// </summary>
    [Fact]
    public async Task FindByRfcAsync_WithCrossFormatVariant_ReturnsExistingRow()
    {
        // Arrange — persist via the 13-char form.
        await using (var ctx1 = new PrismaDbContext(_dbOptions))
        {
            var sut1 = CreateSut(ctx1);
            var proto = new Persona
            {
                Nombre = "Maria",
                Rfc = "MARG900202XYZ",
                Caracter = "Contribuyente",
                PersonaTipo = "Fisica",
            };
            var persistResult = await sut1.FindOrCreateAsync(proto, proto.Rfc, Ct);
            persistResult.IsSuccess.ShouldBeTrue();
        }

        // Act — query with the hyphenated 12-char cross-format variant.
        await using var ctx2 = new PrismaDbContext(_dbOptions);
        var sut2 = CreateSut(ctx2);
        var findResult = await sut2.FindByRfcAsync("MAR-900202-XYZ", Ct);

        // Assert — the same row is found.
        findResult.IsSuccessMayBeNull.ShouldBeTrue();
        findResult.Value.ShouldNotBeNull("The cross-format variant must locate the persisted Persona");
        findResult.Value!.Rfc.ShouldBe("MARG900202XYZ");
    }

    /// <summary>
    /// Two distinct persons (different RFCs) each get their own Persona row — no over-deduplication.
    /// </summary>
    [Fact]
    public async Task FindOrCreateAsync_TwoDistinctRfcs_PersistesTwoPersonas()
    {
        // Arrange
        var personA = new Persona
        {
            Nombre = "Ana",
            Rfc = "ANAG800101AAA",
            Caracter = "Contribuyente",
            PersonaTipo = "Fisica",
        };

        var personB = new Persona
        {
            Nombre = "Luis",
            Rfc = "LUIM750101BBB",
            Caracter = "Contribuyente",
            PersonaTipo = "Fisica",
        };

        // Act
        await using (var ctx1 = new PrismaDbContext(_dbOptions))
        {
            var sut = CreateSut(ctx1);
            var r = await sut.FindOrCreateAsync(personA, personA.Rfc, Ct);
            r.IsSuccess.ShouldBeTrue();
        }

        await using (var ctx2 = new PrismaDbContext(_dbOptions))
        {
            var sut = CreateSut(ctx2);
            var r = await sut.FindOrCreateAsync(personB, personB.Rfc, Ct);
            r.IsSuccess.ShouldBeTrue();
        }

        // Assert — two distinct rows
        await using var queryCtx = new PrismaDbContext(_dbOptions);
        var count = await queryCtx.Persona.CountAsync(Ct);
        count.ShouldBe(2, "Two distinct RFCs must produce two Persona rows");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IPersonIdentityResolver interface contract — DB impl
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>FindByRfcAsync: empty/whitespace RFC is rejected with failure.</summary>
    [Fact]
    public async Task FindByRfcAsync_WithEmptyRfc_ReturnsFailure()
    {
        await using var ctx = new PrismaDbContext(_dbOptions);
        var sut = CreateSut(ctx);

        var result = await sut.FindByRfcAsync("   ", Ct);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>FindByRfcAsync: pre-cancelled token yields failure, not throw.</summary>
    [Fact]
    public async Task FindByRfcAsync_WhenCancelled_ReturnsFailure()
    {
        await using var ctx = new PrismaDbContext(_dbOptions);
        var sut = CreateSut(ctx);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await sut.FindByRfcAsync("PEGJ850101ABC", cts.Token);
        result.IsFailure.ShouldBeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Audit trail integration test (E4-S1 gap)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// After a brand-new Persona is created by <see cref="DbPersonIdentityResolverService.FindOrCreateAsync"/>,
    /// exactly one <see cref="AuditActionType.IdentityResolved"/> row must exist in the
    /// <c>AuditRecords</c> ledger table (real SQL — not a stub).
    /// </summary>
    [Fact]
    public async Task FindOrCreateAsync_NewIdentity_WritesIdentityResolvedAuditRow()
    {
        // Arrange — use a real AuditLoggerService backed by the same DbContext so the
        // audit write goes to the same isolated SQL database.
        var proto = new Persona
        {
            Nombre = "Carlos",
            Paterno = "Ramirez",
            Materno = "Torres",
            Rfc = "RATC850202EEE",
            Caracter = "Contribuyente",
            PersonaTipo = "Fisica",
        };

        await using (var ctx = new PrismaDbContext(_dbOptions))
        {
            var auditLogger = new AuditLoggerService(
                ctx,
                NullLogger<AuditLoggerService>.Instance);
            var sut = CreateSut(ctx, auditLogger);

            // Act
            var result = await sut.FindOrCreateAsync(proto, proto.Rfc, Ct);

            // Assert — resolution succeeded.
            result.IsSuccess.ShouldBeTrue($"FindOrCreateAsync failed: {result.Error}");
            result.Value.ShouldNotBeNull();
        }

        // Assert — exactly one IdentityResolved audit row was written.
        await using var queryCtx = new PrismaDbContext(_dbOptions);
        var auditRows = await queryCtx.AuditRecords
            .Where(r => r.ActionType == AuditActionType.IdentityResolved)
            .ToListAsync(Ct);

        auditRows.Count.ShouldBe(1,
            "Exactly one IdentityResolved audit row must be written for a new Persona");

        var row = auditRows[0];
        row.Success.ShouldBeTrue();
        row.Stage.ShouldBe(ProcessingStage.DecisionLogic);
        row.UserId.ShouldBeNull("identity resolution is a system action — no user");
        row.ActionDetails.ShouldNotBeNullOrEmpty();
        row.ActionDetails!.ShouldContain("Created");
        row.ActionDetails.ShouldContain("RATC850202EEE");
    }

    /// <inheritdoc/>
    public void Dispose() { /* DbContext disposed in each using block */ }
}
