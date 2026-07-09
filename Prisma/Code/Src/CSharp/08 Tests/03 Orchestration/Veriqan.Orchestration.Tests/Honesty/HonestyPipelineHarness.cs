using ExxerCube.Prisma.Veriqan.Application.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
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

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Honesty;

/// <summary>
/// Shared construction helpers for the S3.1 anti-false-confidence honesty suite. Builds the SAME
/// production DI graph the demo/e2e harnesses use (<see cref="VerificationPipelineEndToEndTests"/>,
/// <c>VecChecklistDemoE2ETests</c>) — real <c>AddVeriqanExtraction</c> (the escalating,
/// production <see cref="IStatementFieldExtractor"/>, not the bare positional extractor the
/// existing golden round-trip tests use) fed a synthetic-manifest-driven fake
/// <see cref="IVecReferenceDataProvider"/> bundle so Product resolution and binding succeed
/// exactly as they would in production.
/// </summary>
internal static class HonestyPipelineHarness
{
    /// <summary>
    /// Institution name used for every honesty-suite fake bundle. Arbitrary — the fake
    /// <see cref="IVecReferenceDataProvider"/> ignores the context key and always returns the
    /// bundle built for the specimen under test (same pattern as
    /// <see cref="VerificationPipelineEndToEndTests"/>).
    /// </summary>
    public const string Institution = "Veriqan Honesty Suite";

    /// <summary>Canonical product id used when a specimen's manifest omits <c>bundle</c>.</summary>
    private const string FallbackProductId = "TC-BSSB";

    /// <summary>
    /// Builds a <see cref="VecReferenceBundle"/> from a synthetic specimen's god's-eye
    /// <c>bundle</c> hints, so Product/period/credit-line resolve exactly as the specimen's own
    /// generator intended (mirrors <see cref="VerificationPipelineEndToEndTests.BuildFakeBundle"/>,
    /// but data-driven per specimen instead of hardcoded to one fixture).
    /// </summary>
    public static VecReferenceBundle BuildBundle(SyntheticGoldManifest manifest)
    {
        var productId = manifest.Bundle?.ProductId ?? FallbackProductId;
        var productName = manifest.Bundle?.ProductName ?? "Tarjeta de Crédito BSSB";
        var periodStart = manifest.Bundle?.PeriodStart ?? "2025-07-05";
        var periodEnd = manifest.Bundle?.PeriodEnd ?? "2025-08-04";
        var creditLine = manifest.Bundle?.CreditLine ?? 100000m;

        return new VecReferenceBundle(
            BundleMetadata: new BundleMetadata(
                SchemaVersion: "1.0.0",
                Institution: Institution,
                BundleId: "honesty-suite",
                GeneratedAt: "2026-07-08T00:00:00Z",
                Period: new PeriodRange(Label: "synthetic", Start: periodStart, End: periodEnd),
                Source: new BundleSource(Mechanism: "manual", Reference: "s3.1-honesty-suite", Notes: null)),

            Products: new[]
            {
                new VecProduct(
                    ProductId: productId,
                    ProductName: productName,
                    Aliases: new[] { productName },
                    HasRewardsProgram: false,
                    CardImage: null,
                    ImportantMessageImage: null,
                    Tariffs: new ProductTariffs(
                        AnnualCommission: 1500m,
                        Currency: "MXN",
                        OtherCharges: null))
            },

            InterestRates: new[]
            {
                new InterestRateEntry(
                    ProductId: productId,
                    RatesByPeriod: new[]
                    {
                        new RateByPeriod(
                            AnnualOrdinaryFixedRate: 0.2851m,
                            PeriodLabel: "synthetic",
                            PeriodStart: periodStart,
                            PeriodEnd: periodEnd)
                    })
            },

            ClientAccounts: new[]
            {
                new ClientAccount(
                    ClientId: "CLIENT-HONESTY",
                    ClientName: new ClientName(FirstNames: "Honesty", LastNames: "Suite", Full: "SUITE HONESTY"),
                    Rfc: "HOSU800101ABC",
                    ClientNumber: "00000000",
                    Address: new Address(
                        Street: "Av. Honesty",
                        Number: "1",
                        Neighborhood: "Centro",
                        PostalCode: "00000",
                        State: "CDMX"),
                    Accounts: new[]
                    {
                        new AccountEntry(
                            AccountRef: "ACC-HONESTY",
                            ProductId: productId,
                            CardNumber: "4111XXXXXXXX1111",
                            Clabe: null,
                            BranchNumber: null,
                            CreditLine: creditLine,
                            AccountOpenDate: "2020-01-15")
                    })
            },

            ToleranceConfig: new ToleranceConfig(
                CurrencyToleranceMxn: 0.50m,
                PointsTolerance: 1.00m,
                RewardsPesosToleranceMxn: 1.00m,
                PointsToPesosExchangeRate: 0.10m),

            ValidationConstants: new ValidationConstants(
                RequiredFontFamily: "Aptos",
                BankingYearDays: 360,
                CatAnnualCommissionMxn: 1500m),

            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            PriorStatements: null,
            ExpectedTransactions: null);
    }

    /// <summary>
    /// Builds an isolated DI container wired exactly like the production extraction stack
    /// (<c>AddVeriqanExtraction</c> — the escalating <see cref="IStatementFieldExtractor"/>, the
    /// same one <see cref="VerificationPipeline"/> Stage 2 calls) plus the binding layer needed
    /// for <see cref="IProductResolver"/>. Caller owns disposal (the container holds the
    /// singleton native OCR engine — see <see cref="VerificationPipelineEndToEndTests"/> remarks).
    /// </summary>
    public static ServiceProvider BuildExtractionOnlyContainer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeriqanBinding();
        services.AddVeriqanExtraction();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Builds an isolated DI container running the FULL verdict pipeline
    /// (<see cref="IVerificationPipeline"/>), fed a fake <see cref="IVecReferenceDataProvider"/>
    /// that always returns <paramref name="bundle"/> — same construction pattern as
    /// <see cref="VerificationPipelineEndToEndTests"/>. When <paramref name="extractorOverride"/>
    /// is supplied, it replaces the production <see cref="IStatementFieldExtractor"/> registration
    /// (used by the verdict-flip guard to inject a wrong value post-real-extraction).
    /// </summary>
    public static ServiceProvider BuildFullPipelineContainer(
        VecReferenceBundle bundle,
        Func<IServiceProvider, IStatementFieldExtractor>? extractorOverride = null)
    {
        var fakeProvider = Substitute.For<IVecReferenceDataProvider>();
        fakeProvider
            .GetBundleAsync(Arg.Any<StatementContextKey>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result<VecReferenceBundle>.WithSuccess(bundle)));
        fakeProvider
            .GetChecklistTiersAsync(Arg.Any<StatementContextKey>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Result<IReadOnlyDictionary<string, ChecklistTier>>.WithSuccess(
                    new Dictionary<string, ChecklistTier>() as IReadOnlyDictionary<string, ChecklistTier>)));

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddVeriqanIngestion();
        services.AddVeriqanBinding();
        services.AddVeriqanVerdict();

        services.AddVeriqanExtraction();
        services.AddVeriqanValidation();
        services.AddVeriqanVisual();
        services.AddVeriqanReporting();

        services.Replace(ServiceDescriptor.Scoped<IVecReferenceDataProvider>(_ => fakeProvider));

        services.AddVeriqanInMemoryPersistence();
        services.AddSingleton<VeriqanMetrics>();

        services.AddScoped<IVerificationPipeline, VerificationPipeline>();
        services.AddSingleton(TimeProvider.System);

        if (extractorOverride is not null)
            services.Replace(ServiceDescriptor.Singleton<IStatementFieldExtractor>(extractorOverride));

        return services.BuildServiceProvider();
    }
}
