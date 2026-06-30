// Story 6.1 — Durable EF persistence for IVerificationResultStore and IReprocessAuditRepository.
// Tests run against a real SQL Server Testcontainer; each test provisions its own isolated DB.

using System.Collections.Generic;
using ExxerCube.Prisma.Testing.Infrastructure.Fixtures;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using ExxerCube.Prisma.Veriqan.Orchestration.Reprocess;
using ExxerCube.Prisma.Veriqan.Orchestration.Repositories;
using ExxerCube.Prisma.Veriqan.Orchestration.Stores;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="EfVerificationResultStore"/> and
/// <see cref="EfReprocessAuditRepository"/> exercised against a real SQL Server Testcontainer.
/// </summary>
/// <remarks>
/// Each test provisions its own isolated database via
/// <see cref="SqlServerContainerFixture.CreateIsolatedDatabaseAsync"/> and applies EF migrations
/// before running. Tests may be run in parallel without shared-state collisions.
/// </remarks>
public sealed class DurableStoresIntegrationTests
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initialises the test class.
    /// <paramref name="fixture"/> is injected by the xUnit v3 assembly-fixture mechanism.
    /// </summary>
    public DurableStoresIntegrationTests(
        SqlServerContainerFixture fixture,
        ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async Task<(IServiceProvider Provider, string ConnectionString)> BuildProviderAsync(
        string dbName,
        CancellationToken ct)
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(dbName, ct);

        // Migrate the schema.
        var migrateOptions = new DbContextOptionsBuilder<VeriqanDbContext>()
            .UseSqlServer(
                connectionString,
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "veriqan"))
            .Options;

        await using (var ctx = new VeriqanDbContext(migrateOptions))
        {
            await ctx.Database.MigrateAsync(ct).ConfigureAwait(false);
        }

        // Build a DI container with exactly the services required by the durable stores.
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(new XUnitLoggerProvider(_output)));

        services.AddDbContext<VeriqanDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "veriqan")));

        // Register the EF stores directly (as singletons; they create scopes internally).
        services.AddSingleton<IVerificationResultStore, EfVerificationResultStore>();
        services.AddSingleton<IReprocessAuditRepository, EfReprocessAuditRepository>();

        return (services.BuildServiceProvider(), connectionString);
    }

    /// <summary>
    /// Builds a minimal <see cref="VerificationOutcome"/> with a non-zero processing duration,
    /// using a new <see cref="VerificationJob"/> seeded into the isolated database first.
    /// </summary>
    private static VerificationOutcome BuildOutcome(string contentHash, TimeSpan duration)
    {
        var jobId = Guid.NewGuid();
        var job = new VerificationJob(
            id: jobId,
            contentHash: contentHash,
            receivedAtUtc: DateTimeOffset.UtcNow,
            status: VerificationJobStatus.Completed);

        IReadOnlyList<RuleFinding> findings =
        [
            RuleFinding.Pass("CL-DURABLE-P", TechniqueClass.Deterministic, "1.0.0", "ok"),
        ];

        var summary = VerdictSummary.Green(
            passCount: 1,
            insufficientDataCount: 0,
            insufficientDataCheckIds: []);

        return new VerificationOutcome(job, summary, findings, duration);
    }

    // -----------------------------------------------------------------------
    // EfVerificationResultStore tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// SaveOutcomeAsync then GetOutcomeAsync round-trips a full <see cref="VerificationOutcome"/>
    /// including a non-zero <see cref="VerificationOutcome.ProcessingDuration"/>.
    /// </summary>
    [Fact]
    public async Task EfResultStore_SaveAndGet_RoundTripsFullOutcomeWithDuration()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sp, _) = await BuildProviderAsync("durable_result_roundtrip", ct);
        await using var scope = ((ServiceProvider)sp).CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IVerificationResultStore>();

        const string hash = "aabbccddeeff001122334455667788990011223344556677889900aabbccddee";
        var duration = TimeSpan.FromSeconds(3.5);
        var original = BuildOutcome(hash, duration);

        // Act — save then retrieve
        var saveResult = await store.SaveOutcomeAsync(hash, original, ct);
        saveResult.IsSuccess.ShouldBeTrue($"SaveOutcomeAsync failed: {saveResult.Error}");

        var getResult = await store.GetOutcomeAsync(hash, ct);
        getResult.IsSuccess.ShouldBeTrue($"GetOutcomeAsync failed: {getResult.Error}");

        // Assert
        var restored = getResult.Value;
        restored.ShouldNotBeNull("GetOutcomeAsync returned null for a saved hash.");
        restored!.ProcessingDuration.ShouldBe(duration,
            "ProcessingDuration must survive JSON round-trip via TimeSpanTicksJsonConverter.");
        restored.Summary.Signal.ShouldBe(original.Summary.Signal);
        restored.Summary.PassCount.ShouldBe(original.Summary.PassCount);
        restored.Summary.FailCount.ShouldBe(original.Summary.FailCount);
        restored.Summary.Confidence.ShouldBe(original.Summary.Confidence);
        restored.Findings.Count.ShouldBe(original.Findings.Count);
    }

    /// <summary>
    /// IsCompletedAsync returns <c>false</c> before saving and <c>true</c> after.
    /// </summary>
    [Fact]
    public async Task EfResultStore_IsCompletedAsync_FalseBeforeSaveTrueAfter()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sp, _) = await BuildProviderAsync("durable_result_completed", ct);
        await using var scope = ((ServiceProvider)sp).CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IVerificationResultStore>();
        const string hash = "1100220033004400550066007700880099001100220033004400550066007700";

        // Before save
        var before = await store.IsCompletedAsync(hash, ct);
        before.IsSuccess.ShouldBeTrue();
        before.Value.ShouldBeFalse("IsCompletedAsync must return false before any save.");

        // After save
        await store.SaveOutcomeAsync(hash, BuildOutcome(hash, TimeSpan.FromSeconds(1)), ct);

        var after = await store.IsCompletedAsync(hash, ct);
        after.IsSuccess.ShouldBeTrue();
        after.Value.ShouldBeTrue("IsCompletedAsync must return true after SaveOutcomeAsync.");
    }

    /// <summary>
    /// Calling SaveOutcomeAsync twice with the same hash is a first-write-wins no-op
    /// (no exception; original value is preserved).
    /// </summary>
    [Fact]
    public async Task EfResultStore_SaveTwiceSameHash_FirstWriteWins()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sp, _) = await BuildProviderAsync("durable_result_firstwrite", ct);
        await using var scope = ((ServiceProvider)sp).CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IVerificationResultStore>();
        const string hash = "ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100";

        var first = BuildOutcome(hash, TimeSpan.FromSeconds(1));
        var second = BuildOutcome(hash, TimeSpan.FromSeconds(9)); // different duration — must not persist

        var save1 = await store.SaveOutcomeAsync(hash, first, ct);
        save1.IsSuccess.ShouldBeTrue();

        // Second save must succeed (no exception) but the stored outcome remains the first.
        var save2 = await store.SaveOutcomeAsync(hash, second, ct);
        save2.IsSuccess.ShouldBeTrue("Second SaveOutcomeAsync must not throw on duplicate key.");

        var getResult = await store.GetOutcomeAsync(hash, ct);
        getResult.IsSuccess.ShouldBeTrue();
        getResult.Value!.ProcessingDuration.ShouldBe(
            TimeSpan.FromSeconds(1),
            "First-write-wins: the original duration must be retained after a duplicate save.");
    }

    /// <summary>
    /// ReplaceOutcomeAsync overwrites an existing snapshot and updates <c>ReplacedAtUtc</c>.
    /// </summary>
    [Fact]
    public async Task EfResultStore_ReplaceOutcomeAsync_OverwritesExisting()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sp, _) = await BuildProviderAsync("durable_result_replace", ct);
        await using var scope = ((ServiceProvider)sp).CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IVerificationResultStore>();
        var ctx = scope.ServiceProvider.GetRequiredService<VeriqanDbContext>();
        const string hash = "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff";

        var original = BuildOutcome(hash, TimeSpan.FromSeconds(1));
        var updated = BuildOutcome(hash, TimeSpan.FromSeconds(7));

        await store.SaveOutcomeAsync(hash, original, ct);
        await store.ReplaceOutcomeAsync(hash, updated, ct);

        // Confirm the JSON was overwritten.
        var getResult = await store.GetOutcomeAsync(hash, ct);
        getResult.Value!.ProcessingDuration.ShouldBe(
            TimeSpan.FromSeconds(7),
            "ReplaceOutcomeAsync must overwrite the stored JSON with the new outcome.");

        // Confirm ReplacedAtUtc was stamped.
        var entity = await ctx.OutcomeSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ContentHash == hash, ct);
        entity.ShouldNotBeNull();
        entity!.ReplacedAtUtc.ShouldNotBeNull(
            "ReplaceOutcomeAsync must set ReplacedAtUtc on the entity.");
    }

    // -----------------------------------------------------------------------
    // EfReprocessAuditRepository tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// AppendAsync called twice for the same ContentHash stores both rows, and
    /// GetForContentHashAsync returns them ordered by ReprocessedAtUtc.
    /// A null BeforeSignal round-trips correctly.
    /// </summary>
    [Fact]
    public async Task EfAuditRepository_AppendTwice_ReturnsBothOrderedByDate()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sp, _) = await BuildProviderAsync("durable_audit_append", ct);
        await using var scope = ((ServiceProvider)sp).CreateAsyncScope();

        var repo = scope.ServiceProvider.GetRequiredService<IReprocessAuditRepository>();

        const string hash = "deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef";
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-5);

        // First entry: BeforeSignal is null (no prior outcome existed).
        var entry1 = new ReprocessAuditEntry(
            Id: Guid.NewGuid(),
            ContentHash: hash,
            Actor: "system",
            Reason: "initial reprocess",
            BeforeSignal: null,
            AfterSignal: VerdictSignal.Green,
            ReprocessedAtUtc: baseTime);

        // Second entry: BeforeSignal set.
        var entry2 = new ReprocessAuditEntry(
            Id: Guid.NewGuid(),
            ContentHash: hash,
            Actor: "reviewer",
            Reason: "manual override",
            BeforeSignal: VerdictSignal.Green,
            AfterSignal: VerdictSignal.Red,
            ReprocessedAtUtc: baseTime.AddMinutes(2));

        var append1 = await repo.AppendAsync(entry1, ct);
        append1.IsSuccess.ShouldBeTrue($"AppendAsync(1) failed: {append1.Error}");

        var append2 = await repo.AppendAsync(entry2, ct);
        append2.IsSuccess.ShouldBeTrue($"AppendAsync(2) failed: {append2.Error}");

        // Retrieve and verify ordering + BeforeSignal null round-trip.
        var getResult = await repo.GetForContentHashAsync(hash, ct);
        getResult.IsSuccess.ShouldBeTrue($"GetForContentHashAsync failed: {getResult.Error}");

        var entries = getResult.Value!;
        entries.Count.ShouldBe(2, "Expected exactly 2 audit rows for the content hash.");

        // Ordered ascending by ReprocessedAtUtc.
        entries[0].Id.ShouldBe(entry1.Id, "First entry (older) must be first.");
        entries[0].BeforeSignal.ShouldBeNull(
            "Null BeforeSignal must round-trip as null through int? column.");
        entries[0].AfterSignal.ShouldBe(VerdictSignal.Green);

        entries[1].Id.ShouldBe(entry2.Id, "Second entry (newer) must be second.");
        entries[1].BeforeSignal.ShouldBe(VerdictSignal.Green,
            "Non-null BeforeSignal must round-trip correctly through int? column.");
        entries[1].AfterSignal.ShouldBe(VerdictSignal.Red);

        // Verify Actor and Reason also round-trip.
        entries[0].Actor.ShouldBe("system");
        entries[0].Reason.ShouldBe("initial reprocess");
        entries[1].Actor.ShouldBe("reviewer");
    }

    // -----------------------------------------------------------------------
    // F-2: Non-default RuleFinding + VerdictSummary fields survive round-trip
    // -----------------------------------------------------------------------

    /// <summary>
    /// Saves a <see cref="VerificationOutcome"/> whose <see cref="RuleFinding"/> and
    /// <see cref="VerdictSummary"/> carry non-default values for the fields most at risk of
    /// silent drop on JSON deserialise:
    /// <list type="bullet">
    ///   <item><see cref="RuleFinding.DofNumeral"/> — non-empty string.</item>
    ///   <item><see cref="RuleFinding.Confidence"/> — below 1.0.</item>
    ///   <item><see cref="RuleFinding.LegalBaselineVerdict"/> — diverges from primary verdict
    ///     (tenant-stricter scenario: primary=Fail, baseline=Pass).</item>
    ///   <item><see cref="VerdictSummary.CondusefTierVerdict"/> — explicit non-default value.</item>
    ///   <item><see cref="VerdictSummary.TenantDeviations"/> — non-empty collection.</item>
    ///   <item><see cref="VerdictSummary.TenantOnlyFailCheckIds"/> — non-empty list.</item>
    ///   <item><see cref="VerdictSummary.Confidence"/> — below 1.0.</item>
    /// </list>
    /// If any field drops silently on deserialise this test catches it as a real serialisation
    /// defect in the converter that must be fixed — not papered over.
    /// </summary>
    [Fact]
    public async Task EfResultStore_NonDefaultValues_SurviveJsonRoundTrip()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sp, _) = await BuildProviderAsync("durable_result_nondefault", ct);
        await using var scope = ((ServiceProvider)sp).CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IVerificationResultStore>();

        const string hash = "1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";

        // RuleFinding: Fail with DofNumeral, Confidence < 1.0, and a diverging LegalBaselineVerdict
        // (tenant threshold is stricter than the CONDUSEF legal floor).
        var failFinding = RuleFinding.Fail(
            "CL-21",
            TechniqueClass.Deterministic,
            FindingSeverity.Critical,
            "1.0.0",
            expected: "$500.00",
            observed: "$488.56",
            toleranceApplied: 0.50m,
            legalBaselineVerdict: FindingVerdict.Pass)  // legal floor = Pass; tenant bar = Fail
        with
        {
            DofNumeral = "Acuerdo §21",
            Confidence = 0.72,
        };

        // VerdictSummary: Yellow signal (bank-only fail, CONDUSEF tier is Green),
        // with a TenantDeviation and a non-empty TenantOnlyFailCheckIds list.
        var deviation = new TenantDeviation(
            CheckId: "CL-21",
            RequestedValue: 0.25m,
            LegalDefaultUsed: 0.50m,
            Reason: "Tenant threshold below legal minimum — reverted to CONDUSEF floor.");

        var summary = VerdictSummary.Red(
            failCount: 1,
            passCount: 1,
            insufficientDataCount: 0,
            failCheckIds: ["CL-21"],
            insufficientDataCheckIds: [],
            tenantDeviations: [deviation],
            legalBreachCheckIds: [],
            tenantOnlyFailCheckIds: ["CL-21"],
            bankFailCheckIds: ["CL-21"],
            condusefFailCheckIds: [],
            bankTierVerdict: VerdictSignal.Yellow,
            condusefTierVerdict: VerdictSignal.Green,
            signal: VerdictSignal.Yellow,
            confidence: 0.72);

        IReadOnlyList<RuleFinding> findings =
        [
            failFinding,
            RuleFinding.Pass("CL-PASS-X", TechniqueClass.Deterministic, "1.0.0"),
        ];

        var job = new VerificationJob(
            id: Guid.NewGuid(),
            contentHash: hash,
            receivedAtUtc: DateTimeOffset.UtcNow,
            status: VerificationJobStatus.Completed);

        var original = new VerificationOutcome(job, summary, findings, TimeSpan.FromSeconds(4.25));

        // Act — save then retrieve in a fresh scope.
        var saveResult = await store.SaveOutcomeAsync(hash, original, ct);
        saveResult.IsSuccess.ShouldBeTrue($"SaveOutcomeAsync failed: {saveResult.Error}");

        var getResult = await store.GetOutcomeAsync(hash, ct);
        getResult.IsSuccess.ShouldBeTrue($"GetOutcomeAsync failed: {getResult.Error}");

        var restored = getResult.Value;
        restored.ShouldNotBeNull("GetOutcomeAsync must return the saved outcome.");

        // --- VerdictSummary non-default fields ---
        restored!.Summary.Signal.ShouldBe(VerdictSignal.Yellow,
            "Non-Green Signal must survive JSON round-trip.");
        restored.Summary.BankTierVerdict.ShouldBe(VerdictSignal.Yellow,
            "BankTierVerdict must survive JSON round-trip.");
        restored.Summary.CondusefTierVerdict.ShouldBe(VerdictSignal.Green,
            "CondusefTierVerdict must survive JSON round-trip.");
        restored.Summary.Confidence.ShouldBe(0.72,
            "VerdictSummary.Confidence must survive JSON round-trip.");
        restored.Summary.TenantDeviations.Count.ShouldBe(1,
            "TenantDeviations collection must survive JSON round-trip with correct count.");
        restored.Summary.TenantOnlyFailCheckIds.ShouldContain("CL-21",
            "TenantOnlyFailCheckIds must survive JSON round-trip.");

        // --- RuleFinding non-default fields (located by CheckId) ---
        var restoredFail = restored.Findings.FirstOrDefault(f => f.CheckId == "CL-21");
        restoredFail.ShouldNotBeNull("CL-21 RuleFinding must survive JSON round-trip.");
        restoredFail!.DofNumeral.ShouldBe("Acuerdo §21",
            "RuleFinding.DofNumeral must survive JSON round-trip.");
        restoredFail.Confidence.ShouldBe(0.72,
            "RuleFinding.Confidence must survive JSON round-trip.");
        restoredFail.LegalBaselineVerdict.ShouldBe(FindingVerdict.Pass,
            "RuleFinding.LegalBaselineVerdict must survive JSON round-trip (diverges from primary Fail).");
    }
}
