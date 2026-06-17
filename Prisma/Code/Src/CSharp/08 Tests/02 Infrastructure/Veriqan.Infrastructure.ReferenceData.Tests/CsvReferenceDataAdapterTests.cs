using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Adapters;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Validation;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Tests;

/// <summary>
/// Tests for <see cref="CsvReferenceDataAdapter"/>.
/// </summary>
public sealed class CsvReferenceDataAdapterTests
{
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

    [Fact]
    public async Task GetBundleAsync_ValidCsv_ProducesSchemaValidBundle()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)", "Sep-Oct 2025");

        var result = await adapter.GetBundleAsync(key, ct);

        result.IsSuccess.ShouldBeTrue($"Expected success but got: {result.Error}");
        var bundle = result.Value!;
        bundle.BundleMetadata.Institution.ShouldBe("Demo Bank (Iqubica)");
        bundle.BundleMetadata.SchemaVersion.ShouldBe("1.0.0");
    }

    [Fact]
    public async Task GetBundleAsync_ValidCsv_LoadsProducts()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetBundleAsync(key, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Products.ShouldNotBeNull();
        result.Value.Products!.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetBundleAsync_ValidCsv_ProductAliasResolves()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetBundleAsync(key, ct);

        result.IsSuccess.ShouldBeTrue();
        var nlProduct = result.Value!.Products!.FirstOrDefault(p => p.ProductId == "TC-NL");
        nlProduct.ShouldNotBeNull();
        nlProduct!.Aliases.ShouldNotBeNull();
        nlProduct.Aliases!.ShouldContain("NL");
    }

    [Fact]
    public async Task GetBundleAsync_ValidCsv_LoadsInterestRates()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetBundleAsync(key, ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.InterestRates.ShouldNotBeNull();
        var nlRates = result.Value.InterestRates!.FirstOrDefault(r => r.ProductId == "TC-NL");
        nlRates.ShouldNotBeNull();
        nlRates!.RatesByPeriod.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetBundleAsync_MissingDirectory_ReturnsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Nonexistent Bank");

        var result = await adapter.GetBundleAsync(key, ct);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetBundleAsync_MissingInterestRatesCsv_ReturnsValidBundle_GracefulDegradation()
    {
        // Create a temp directory with only bundle-metadata (no interest-rates.csv)
        var ct = TestContext.Current.CancellationToken;
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vec-test-{Guid.NewGuid():N}");
        var institutionDir = Path.Combine(tempRoot, "No_Rates_Bank");
        Directory.CreateDirectory(institutionDir);

        try
        {
            File.WriteAllText(
                Path.Combine(institutionDir, "bundle-metadata.csv"),
                "schemaVersion,institution,bundleId,generatedAt,periodLabel,periodStart,periodEnd,sourceMechanism,sourceReference,sourceNotes\n" +
                "1.0.0,No Rates Bank,,,,,,csv,,\n");

            var adapter = CreateAdapter(tempRoot);
            var key = new StatementContextKey("No Rates Bank");

            var result = await adapter.GetBundleAsync(key, ct);

            // Should succeed — missing TASA is graceful degradation
            result.IsSuccess.ShouldBeTrue(
                $"Missing interest-rates.csv should not fail validation: {result.Error}");
            result.Value!.InterestRates.ShouldBeNull("TASA section should be null when file is absent");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GetBundleAsync_CancelledToken_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetBundleAsync(key, cts.Token);

        result.IsSuccess.ShouldBeFalse();
        result.IsCancelled().ShouldBeTrue();
    }
}
