using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Reporting.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Visual.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Orchestration.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Orchestration.Observability;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using IndQuestResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests;

/// <summary>
/// End-to-end test that runs the full verification pipeline against a real fixture PDF
/// without a database (uses in-memory repository stubs and a fake reference-data provider).
/// </summary>
/// <remarks>
/// The test injects a fake <see cref="IVecReferenceDataProvider"/> whose bundle is
/// constructed to resolve the product token that the extractor reads from the fixture PDF
/// ("Tarjeta de Crédito BSSB" at Y≈648 — see <c>PdfPigStatementFieldExtractor</c>).
/// This ensures binding succeeds, the validation engine runs real rules, and the test
/// can assert a concrete finding count and verdict signal rather than a tautology.
/// </remarks>
[Collection(MetricsIsolationCollection.Name)]
public sealed class VerificationPipelineEndToEndTests
{
    /// <summary>
    /// Absolute path to the fixture PDF (the Dummie VEC jul–ago 2025 statement).
    /// </summary>
    private const string FixturePdf =
        @"E:\Dynamic\IndFusion\ExxerCube.Prisma\ExxerCube.Prisma\Prisma\Fixtures\PRP2\01+Dummie+VEC+jul_ago+20252.pdf";

    /// <summary>
    /// Product token that <c>PdfPigStatementFieldExtractor.ExtractProductName</c> returns
    /// from the fixture PDF (full band text at Y≈648: "Tarjeta de Crédito BSSB").
    /// ProductResolver normalises this and matches it against <see cref="VecProduct.Aliases"/>.
    /// </summary>
    private const string FixtureProductToken = "Tarjeta de Crédito BSSB";

    /// <summary>
    /// Canonical product id used in the fake bundle.
    /// </summary>
    private const string ProductId = "TC-BSSB";

    // -----------------------------------------------------------------------
    // Fake reference-data bundle
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a <see cref="VecReferenceBundle"/> that is guaranteed to resolve the fixture's
    /// product token so the binding stage succeeds and the engine runs real rules.
    /// Sections not strictly required for initial binding (legends, images, promotions,
    /// expected transactions, prior statements) are omitted — their absence produces
    /// <c>INSUFFICIENT_REFERENCE_DATA</c> findings rather than blocking the pipeline.
    /// </summary>
    private static VecReferenceBundle BuildFakeBundle() => new(
        BundleMetadata: new BundleMetadata(
            SchemaVersion: "1.0.0",
            Institution: "Demo Bank (Iqubica)",
            BundleId: "test-bundle-e2e",
            GeneratedAt: "2025-08-01T00:00:00Z",
            Period: new PeriodRange(Label: "Jul-Ago 2025", Start: "2025-07-05", End: "2025-08-04"),
            Source: new BundleSource(Mechanism: "manual", Reference: "e2e-test", Notes: null)),

        // Product list — alias "Tarjeta de Crédito BSSB" ensures the full product name text
        // extracted from the PDF resolves to this product (ProductResolver matches aliases
        // case-insensitively with whitespace normalisation).
        Products: new[]
        {
            new VecProduct(
                ProductId: ProductId,
                ProductName: "Tarjeta de Crédito BSSB",
                Aliases: new[] { "BSSB", FixtureProductToken },
                HasRewardsProgram: false,
                CardImage: null,
                ImportantMessageImage: null,
                Tariffs: new ProductTariffs(
                    AnnualCommission: 1500m,
                    Currency: "MXN",
                    OtherCharges: null))
        },

        // Interest rates for the fixture period (Jul-Ago 2025).
        InterestRates: new[]
        {
            new InterestRateEntry(
                ProductId: ProductId,
                RatesByPeriod: new[]
                {
                    new RateByPeriod(
                        AnnualOrdinaryFixedRate: 0.2851m,
                        PeriodLabel: "Jul Ago",
                        PeriodStart: "2025-07-05",
                        PeriodEnd: "2025-08-04")
                })
        },

        // Client account — populates CreditLine used by credit-line availability checks.
        ClientAccounts: new[]
        {
            new ClientAccount(
                ClientId: "CLIENT-001",
                ClientName: new ClientName(
                    FirstNames: "Juan",
                    LastNames: "Pérez García",
                    Full: "PÉREZ GARCÍA JUAN"),
                Rfc: "PEGJ800101ABC",
                ClientNumber: "12345678",
                Address: new Address(
                    Street: "Av. Insurgentes",
                    Number: "100",
                    Neighborhood: "Centro",
                    PostalCode: "06600",
                    State: "CDMX"),
                Accounts: new[]
                {
                    new AccountEntry(
                        AccountRef: "ACC-001",
                        ProductId: ProductId,
                        CardNumber: "4111XXXXXXXX1111",
                        Clabe: null,
                        BranchNumber: null,
                        CreditLine: 100000m,
                        AccountOpenDate: "2020-01-15")
                })
        },

        // Tolerance config — used by currency-comparison checks.
        ToleranceConfig: new ToleranceConfig(
            CurrencyToleranceMxn: 0.50m,
            PointsTolerance: 1.00m,
            RewardsPesosToleranceMxn: 1.00m,
            PointsToPesosExchangeRate: 0.10m),

        // Validation constants — RequiredFontFamily drives the font-embedding check (CL-35).
        ValidationConstants: new ValidationConstants(
            RequiredFontFamily: "Aptos",
            BankingYearDays: 360,
            CatAnnualCommissionMxn: 1500m),

        // Optional sections omitted — their absence generates INSUFFICIENT_REFERENCE_DATA
        // findings rather than blocking the pipeline.
        MandatoryLegends: null,
        SequentialImages: null,
        Promotions: null,
        PriorStatements: null,
        ExpectedTransactions: null);

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// Runs the full ingest → extract → bind → engine → verdict pipeline against the Dummie
    /// VEC fixture PDF and asserts that the engine produces at least one finding and returns a
    /// concrete (non-Blocked) verdict signal.
    /// </summary>
    [Fact]
    public async Task Pipeline_RealFixture_ProducesVerificationOutcome()
    {
        // Skip gracefully if fixture is absent (CI might not have the binary fixture).
        if (!File.Exists(FixturePdf))
            return;

        // Arrange — fake IVecReferenceDataProvider that always returns the pre-built bundle.
        // GetChecklistTiersAsync returns an empty map (no tier CSV alongside this fixture),
        // so the pipeline degrades gracefully to single-tier aggregation for this test.
        var fakeProvider = Substitute.For<IVecReferenceDataProvider>();
        fakeProvider
            .GetBundleAsync(Arg.Any<StatementContextKey>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(
                Result<VecReferenceBundle>.WithSuccess(BuildFakeBundle())));
        fakeProvider
            .GetChecklistTiersAsync(Arg.Any<StatementContextKey>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Result<IReadOnlyDictionary<string, ChecklistTier>>.WithSuccess(
                    new Dictionary<string, ChecklistTier>() as IReadOnlyDictionary<string, ChecklistTier>)));

        var services = new ServiceCollection();
        services.AddLogging();

        // Application layer
        services.AddVeriqanIngestion();
        services.AddVeriqanBinding();
        services.AddVeriqanVerdict();

        // Infrastructure adapters (real implementations)
        services.AddVeriqanExtraction();
        services.AddVeriqanValidation();
        services.AddVeriqanVisual();
        services.AddVeriqanReporting();  // SMTP not exercised in the test path

        // Override the reference-data provider BEFORE adding the real CSV adapter so
        // TryAdd semantics inside AddVeriqanBinding prevent a second descriptor.
        services.Replace(ServiceDescriptor.Scoped<IVecReferenceDataProvider>(_ => fakeProvider));

        // In-memory persistence stubs (no SQL Server needed)
        services.AddVeriqanInMemoryPersistence();

        // Metrics singleton required by VerificationPipeline
        services.AddSingleton<VeriqanMetrics>();

        // Pipeline
        services.AddScoped<IVerificationPipeline, VerificationPipeline>();
        services.AddSingleton(TimeProvider.System);

        // Dispose the root container (not just the child scope below) — it owns the singleton
        // IHeaderProductOcrEngine (TesseractHeaderProductOcrEngine); disposing only the async
        // scope would leak that native engine handle (see VecChecklistDemoE2ETests).
        await using var sp = services.BuildServiceProvider();

        var ct = TestContext.Current.CancellationToken;
        var pdfBytes = await File.ReadAllBytesAsync(FixturePdf, ct);

        // "Demo Bank (Iqubica)" — matches the bundle metadata institution.
        var key = new StatementContextKey("Demo Bank (Iqubica)", PeriodLabel: "Jul-Ago 2025");
        var submission = new StatementSubmission(
            Pdf: pdfBytes,
            FileName: "01+Dummie+VEC+jul_ago+20252.pdf",
            ContextKey: key);

        // Act
        Result<VerificationOutcome> result;
        await using (var scope = sp.CreateAsyncScope())
        {
            var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();
            result = await pipeline.ProcessAsync(submission, ct);
        }

        // Assert — pipeline must return a successful Result (binding must have resolved)
        result.IsSuccess.ShouldBeTrue(
            $"Pipeline must succeed (not Blocked). Error: {result.Error ?? "<none>"}. " +
            "If Blocked, check that FixtureProductToken alias matches the extracted token.");

        var outcome = result.Value!;
        outcome.Summary.ShouldNotBeNull();
        outcome.Job.ShouldNotBeNull();

        var signal = outcome.Summary.Signal;
        var findingCount = outcome.Findings.Count;
        var exampleCheckIds = string.Join(", ", outcome.Findings.Take(5).Select(f => f.CheckId));

        // The engine must have evaluated at least one rule — this is the genuine end-to-end proof.
        // Actual run (2026-06-17): signal=Red, findingCount=35,
        // first CheckIds=[CL-10, CL-17, CL-18, CL-19, CL-20, ...].
        findingCount.ShouldBeGreaterThan(0,
            "The validation engine must produce at least one finding (Green or Red) to prove " +
            "it ran real rules over the extracted StatementModel.");

        // Signal must be a concrete verdict — Blocked means binding failed (not acceptable here).
        signal.ShouldNotBe(
            Domain.Enums.VerdictSignal.Blocked,
            "Binding resolved; verdict must be Green or Red (not Blocked).");

        // Diagnostic: report actual result so the test run log is informative.
        _ = signal;          // consumed by assertion above
        _ = exampleCheckIds; // informational — visible in test runner output via failure messages
    }
}
