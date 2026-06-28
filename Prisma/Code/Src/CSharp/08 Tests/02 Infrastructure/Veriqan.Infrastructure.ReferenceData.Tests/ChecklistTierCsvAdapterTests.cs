using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Adapters;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Tests;

/// <summary>
/// Tests for <see cref="CsvReferenceDataAdapter.GetChecklistTiersAsync"/> (Story 1.2).
/// Verifies that the tier map is loaded from <c>checklist-tiers.csv</c> in the demo bundle,
/// that all three tiers resolve correctly, and that a missing file returns an empty map.
/// </summary>
public sealed class ChecklistTierCsvAdapterTests
{
    // -----------------------------------------------------------------------
    // Adapter factory — mirrors the pattern used in CsvReferenceDataAdapterTests
    // -----------------------------------------------------------------------

    private static string CsvRootPath =>
        Path.Combine(AppContext.BaseDirectory, "TestAssets", "csv");

    private static CsvReferenceDataAdapter CreateAdapter(string? rootDir = null)
    {
        var options = Options.Create(new CsvReferenceDataOptions
        {
            RootDirectory = rootDir ?? CsvRootPath
        });
        var validator = new ReferenceBundleSchemaValidator();
        var logger = NullLogger<CsvReferenceDataAdapter>.Instance;
        return new CsvReferenceDataAdapter(options, validator, logger);
    }

    // -----------------------------------------------------------------------
    // Happy path — demo bundle
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetChecklistTiersAsync_DemoBundle_ReturnsSuccessWithNonEmptyMap()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetChecklistTiersAsync(key, ct);

        result.IsSuccess.ShouldBeTrue($"Expected success but got: {result.Error}");
        result.Value.ShouldNotBeNull();
        result.Value!.Count.ShouldBeGreaterThan(0,
            "checklist-tiers.csv in the demo bundle must contain at least one entry.");
    }

    [Fact]
    public async Task GetChecklistTiersAsync_DemoBundle_BankRow_ResolvesCorrectly()
    {
        // CL-35 is a Bank-tier entry in checklist-tiers.csv
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetChecklistTiersAsync(key, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.TryGetValue("CL-35", out var tier).ShouldBeTrue(
            "CL-35 must be present in the tier map.");
        tier.ShouldBe(ChecklistTier.Bank,
            "CL-35 is declared as Bank tier in checklist-tiers.csv.");
    }

    [Fact]
    public async Task GetChecklistTiersAsync_DemoBundle_CondusefRow_ResolvesCorrectly()
    {
        // LAW-§6-SIMULACION is a Condusef-tier entry (§ char is verbatim UTF-8)
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetChecklistTiersAsync(key, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.TryGetValue("LAW-§6-SIMULACION", out var tier).ShouldBeTrue(
            "LAW-§6-SIMULACION (with § character) must be present in the tier map.");
        tier.ShouldBe(ChecklistTier.Condusef,
            "LAW-§6-SIMULACION is declared as Condusef tier in checklist-tiers.csv.");
    }

    [Fact]
    public async Task GetChecklistTiersAsync_DemoBundle_BothRow_ResolvesCorrectly()
    {
        // CL-10 is a Both-tier entry
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetChecklistTiersAsync(key, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.TryGetValue("CL-10", out var tier).ShouldBeTrue(
            "CL-10 must be present in the tier map.");
        tier.ShouldBe(ChecklistTier.Both,
            "CL-10 is declared as Both tier in checklist-tiers.csv.");
    }

    [Fact]
    public async Task GetChecklistTiersAsync_DemoBundle_TotalRowCount_Is56()
    {
        // 10 Bank + 26 Both + 20 Condusef = 56 owner-approved rows
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetChecklistTiersAsync(key, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(56,
            "checklist-tiers.csv must have exactly 56 entries (10 Bank + 26 Both + 20 Condusef).");
    }

    [Fact]
    public async Task GetChecklistTiersAsync_DemoBundle_SlashCheckId_ResolvesCorrectly()
    {
        // CL-27/CL-30/CL-47 is a single CheckId with literal slashes (Bank tier)
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetChecklistTiersAsync(key, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.TryGetValue("CL-27/CL-30/CL-47", out var tier).ShouldBeTrue(
            "CL-27/CL-30/CL-47 (literal slashes) must be present in the tier map.");
        tier.ShouldBe(ChecklistTier.Bank,
            "CL-27/CL-30/CL-47 is declared as Bank tier in checklist-tiers.csv.");
    }

    // -----------------------------------------------------------------------
    // Missing file — returns empty map (graceful degradation)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetChecklistTiersAsync_MissingFile_ReturnsSuccessWithEmptyMap()
    {
        var ct = TestContext.Current.CancellationToken;
        // Point at a temp directory that has no checklist-tiers.csv
        var tempRoot = Path.Combine(Path.GetTempPath(), $"tier-test-{Guid.NewGuid():N}");
        var instDir = Path.Combine(tempRoot, "Demo_Bank_(Iqubica)");
        Directory.CreateDirectory(instDir);

        try
        {
            // Provide only the required bundle-metadata.csv so the adapter can locate the dir
            File.WriteAllText(
                Path.Combine(instDir, "bundle-metadata.csv"),
                "schemaVersion,institution,bundleId,generatedAt,periodLabel,periodStart,periodEnd,sourceMechanism,sourceReference,sourceNotes\n" +
                "1.0.0,Demo Bank (Iqubica),test,,,,,,, ");

            var adapter = CreateAdapter(tempRoot);
            var key = new StatementContextKey("Demo Bank (Iqubica)");

            var result = await adapter.GetChecklistTiersAsync(key, ct);

            result.IsSuccess.ShouldBeTrue(
                "A missing checklist-tiers.csv must not fail — return an empty map.");
            result.Value!.Count.ShouldBe(0,
                "Empty map is the contract when the file is absent.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    // -----------------------------------------------------------------------
    // Caller-default documentation contract:
    // a missing CheckId MUST be treated as Condusef by callers.
    // (The adapter returns empty map; this test confirms the key is absent
    //  so the caller test in VerdictAggregatorTierTests can rely on that.)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetChecklistTiersAsync_DemoBundle_UnknownCheckId_IsAbsent()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetChecklistTiersAsync(key, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.ContainsKey("UNKNOWN-CHECK-XYZ").ShouldBeFalse(
            "An unmapped CheckId must be absent from the tier map; callers default it to Condusef.");
    }
}
