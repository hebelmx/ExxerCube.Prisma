using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Application.Services;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using IndQuestResults;
using IndQuestResults.Operations;
using Meziantou.Extensions.Logging.Xunit.v3;
using NSubstitute;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Veriqan.Application.Tests;

/// <summary>
/// Tests for <see cref="BundleBinder"/> and <see cref="ProductResolver"/> covering
/// context binding, product resolution, and graceful degradation (FR-3, FR-20; Story 2.3).
/// </summary>
public sealed class BundleBinderTests
{
    // -----------------------------------------------------------------------
    // Helpers — bundle factories
    // -----------------------------------------------------------------------

    private static BundleMetadata MinimalMetadata() =>
        new("1.0.0", "Demo Bank", null, null, null, null);

    /// <summary>
    /// Returns a product list with one product that has "TC-NL" as its canonical id
    /// and "NL" as an alias.
    /// </summary>
    private static IReadOnlyList<VecProduct> OneProduct() =>
    [
        new VecProduct(
            ProductId: "TC-NL",
            ProductName: "Tarjeta de Crédito NL",
            Aliases: ["NL", "nl"],
            HasRewardsProgram: false,
            CardImage: null,
            ImportantMessageImage: null,
            Tariffs: null)
    ];

    /// <summary>
    /// A minimal bundle with <see cref="OneProduct"/> but NO interest-rate section.
    /// Used to verify that the Rate capability is marked InsufficientData while
    /// Tolerances (which IS present) stays Available.
    /// </summary>
    private static VecReferenceBundle BundleNoTasa() =>
        new(
            BundleMetadata: MinimalMetadata(),
            Products: OneProduct(),
            InterestRates: null,                    // ← missing TASA
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: new ToleranceConfig(0.5m, 1m, 1m, 0.1m),  // ← present
            ValidationConstants: null);

    /// <summary>
    /// A fully-populated bundle (all optional sections non-empty) for the "all capabilities
    /// available" sanity test.
    /// </summary>
    private static VecReferenceBundle FullBundle() =>
        new(
            BundleMetadata: MinimalMetadata(),
            Products: OneProduct(),
            InterestRates:
            [
                new InterestRateEntry("TC-NL",
                [
                    new RateByPeriod(0.1975m, "Sep Oct", "2025-09-01", "2025-10-31")
                ])
            ],
            MandatoryLegends:   [new MandatoryLegend("L1", "Leyenda", null, null, null, null)],
            SequentialImages:   [new SequentialImage(1, new ImageRef(null, null, null, null, null, null, null), null)],
            Promotions:
            [
                new Promotion("P1",
                    new ImageRef(null, null, null, null, null, null, null),
                    "2025-09-01", "2025-10-31", null)
            ],
            ClientAccounts: null,
            PriorStatements:
            [
                new PriorStatement("ACC-001",
                    new PeriodRange("Aug-Sep 2025", "2025-08-01", "2025-09-01"),
                    new ClosingBalances(1000m, 2000m, null, null),
                    null, null)
            ],
            ExpectedTransactions:
            [
                new ExpectedTransactionGroup("ACC-001",
                [
                    new ExpectedTransaction("Compra", 100m, "2025-09-05", "2025-09-06", "-")
                ])
            ],
            ToleranceConfig: new ToleranceConfig(0.5m, 1m, 1m, 0.1m),
            ValidationConstants: new ValidationConstants("Aptos", 360, 1500m));

    /// <summary>
    /// Builds a <see cref="BundleBinder"/> with a provider stubbed to return
    /// <paramref name="bundleResult"/>.
    /// </summary>
    private static BundleBinder BuildBinder(Result<VecReferenceBundle> bundleResult)
    {
        var provider = Substitute.For<IVecReferenceDataProvider>();
        provider
            .GetBundleAsync(Arg.Any<StatementContextKey>(), Arg.Any<CancellationToken>())
            .Returns(bundleResult);

        var resolver = new ProductResolver();
        var logger = XUnitLogger.CreateLogger<BundleBinder>();

        return new BundleBinder(provider, resolver, logger);
    }

    /// <summary>Builds a minimal <see cref="VerificationJob"/> for test purposes.</summary>
    private static VerificationJob BuildJob() =>
        new(Guid.NewGuid(), "abc123", DateTimeOffset.UtcNow, VerificationJobStatus.Pending);

    private static StatementContextKey DefaultKey() =>
        new("Demo Bank", "Sep-Oct 2025");

    // -----------------------------------------------------------------------
    // Test 1: Missing TASA section → Rate = InsufficientData; Tolerances = Available
    // (AC: "only rate-dependent Checks report INSUFFICIENT_DATA; all other Checks still run")
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the bundle has products and ToleranceConfig but no InterestRates,
    /// the Rate capability is InsufficientData while Tolerances stays Available.
    /// </summary>
    [Fact]
    public async Task Bind_BundleMissingTasaSection_RateChecksAreInsufficientData_OthersRunnable()
    {
        var ct = TestContext.Current.CancellationToken;
        var binder = BuildBinder(Result<VecReferenceBundle>.WithSuccess(BundleNoTasa()));
        var job = BuildJob();

        // Act
        var result = await binder.BindAsync(job, DefaultKey(), "NL", ct);

        // Assert: binding succeeded
        result.IsSuccess.ShouldBeTrue();
        var ctx = result.Value!;

        // Rate (TASA) section missing → InsufficientData
        ctx.Availability.StatusOf(ReferenceCapability.Rate)
            .ShouldBe(ReferenceCapabilityStatus.InsufficientData);
        ctx.Availability.IsInsufficientData(ReferenceCapability.Rate)
            .ShouldBeTrue();

        // Tolerances section IS present → Available
        ctx.Availability.StatusOf(ReferenceCapability.Tolerances)
            .ShouldBe(ReferenceCapabilityStatus.Available);
        ctx.Availability.IsInsufficientData(ReferenceCapability.Tolerances)
            .ShouldBeFalse();

        // Other absent sections are also InsufficientData (not Available)
        ctx.Availability.IsInsufficientData(ReferenceCapability.PriorStatement).ShouldBeTrue();
        ctx.Availability.IsInsufficientData(ReferenceCapability.Legends).ShouldBeTrue();

        // Product was still resolved
        ctx.ResolvedProduct.ProductId.ShouldBe("TC-NL");
    }

    // -----------------------------------------------------------------------
    // Test 2: Unknown product token → BLOCKED UnknownProduct, no silent default
    // -----------------------------------------------------------------------

    /// <summary>
    /// A product token that matches no ProductId or alias produces a BLOCKED failure
    /// with reason <see cref="BlockReason.UnknownProduct"/>. No arbitrary product is selected.
    /// </summary>
    [Fact]
    public async Task Bind_UnknownProduct_ReturnsBlockedUnknownProduct()
    {
        var ct = TestContext.Current.CancellationToken;
        var binder = BuildBinder(Result<VecReferenceBundle>.WithSuccess(FullBundle()));
        var job = BuildJob();

        // Act — "GOLD" is not in the bundle
        var result = await binder.BindAsync(job, DefaultKey(), "GOLD", ct);

        // Assert: binding failed
        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Value.ShouldBeNull();

        // Assert: error encodes BlockedOutcome with UnknownProduct reason
        BlockedOutcome.TryParse(result.Error, out var outcome).ShouldBeTrue();
        outcome.ShouldNotBeNull();
        outcome!.Reason.ShouldBe(BlockReason.UnknownProduct);
        outcome.Detail.ShouldNotBeNullOrWhiteSpace();
    }

    // -----------------------------------------------------------------------
    // Test 3: Alias resolution — "NL" → canonical "TC-NL"
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the product token matches an alias (not the canonical ProductId),
    /// the resolver returns the correct canonical product.
    /// </summary>
    [Fact]
    public void Resolve_ByAlias_ResolvesCanonicalProduct()
    {
        // Arrange
        var resolver = new ProductResolver();
        var bundle = BundleNoTasa();   // has product TC-NL with alias "NL"

        // Act
        var result = resolver.Resolve("NL", bundle);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.ProductId.ShouldBe("TC-NL");
    }

    // -----------------------------------------------------------------------
    // Test 4: Full bundle → all capabilities Available, context fully populated
    // -----------------------------------------------------------------------

    /// <summary>
    /// A fully-populated bundle results in all capabilities being Available,
    /// the product resolved, the context carrying bundle + product + tolerances.
    /// </summary>
    [Fact]
    public async Task Bind_FullBundle_AllCapabilitiesAvailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var binder = BuildBinder(Result<VecReferenceBundle>.WithSuccess(FullBundle()));
        var job = BuildJob();

        // Act
        var result = await binder.BindAsync(job, DefaultKey(), "TC-NL", ct);

        // Assert: success
        result.IsSuccess.ShouldBeTrue();
        var ctx = result.Value!;

        // All capabilities backed by sections in FullBundle → Available
        foreach (ReferenceCapability cap in Enum.GetValues<ReferenceCapability>())
        {
            ctx.Availability.StatusOf(cap).ShouldBe(
                ReferenceCapabilityStatus.Available,
                customMessage: $"Expected capability {cap} to be Available");
        }

        // Core context fields are populated
        ctx.Bundle.ShouldNotBeNull();
        ctx.ResolvedProduct.ProductId.ShouldBe("TC-NL");
        ctx.ToleranceConfig.ShouldNotBeNull();
        ctx.ToleranceConfig!.CurrencyToleranceMxn.ShouldBe(0.5m);

        // StatementModel is null (Epic 3 placeholder)
        ctx.StatementModel.ShouldBeNull();
    }

    // -----------------------------------------------------------------------
    // Test 5: Provider failure → BLOCKED InvalidBundle, not a silent skip
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the reference-data provider returns a failure (e.g. schema validation rejected
    /// the stored bundle), BindAsync must surface a BLOCKED result with reason
    /// <see cref="BlockReason.InvalidBundle"/> — it must NOT silently succeed or swallow the error.
    /// </summary>
    [Fact]
    public async Task Bind_ProviderReturnsFailure_ReturnsBlockedInvalidBundle()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange: provider always fails (simulates a schema-rejected bundle)
        var providerFailure = Result<VecReferenceBundle>.WithFailure("simulated schema failure");
        var binder = BuildBinder(providerFailure);
        var job = BuildJob();

        // Act
        var result = await binder.BindAsync(job, DefaultKey(), "TC-NL", ct);

        // Assert: binding must fail
        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();

        // Assert: the error encodes a BlockedOutcome with reason InvalidBundle
        BlockedOutcome.TryParse(result.Error, out var outcome).ShouldBeTrue(
            $"Expected error to be a BLOCKED outcome string but got: '{result.Error}'");
        outcome.ShouldNotBeNull();
        outcome!.Reason.ShouldBe(BlockReason.InvalidBundle);
        outcome.Detail.ShouldNotBeNullOrWhiteSpace();
    }

    // -----------------------------------------------------------------------
    // Test 6: ProductResolver — token with extra internal spaces still resolves
    // -----------------------------------------------------------------------

    /// <summary>
    /// <see cref="ProductResolver.Resolve"/> normalises internal whitespace before comparing,
    /// so a token with extra internal spaces resolves to the same product as the clean token.
    /// </summary>
    [Fact]
    public void Resolve_TokenWithExtraInternalSpaces_StillResolves()
    {
        // Arrange: add a product whose ProductId contains a single internal space ("TC NL")
        var resolver = new ProductResolver();
        var bundleWithSpacedId = new VecReferenceBundle(
            BundleMetadata: MinimalMetadata(),
            Products:
            [
                new VecProduct(
                    ProductId: "TC NL",
                    ProductName: "Tarjeta de Crédito NL (spaced)",
                    Aliases: null,
                    HasRewardsProgram: false,
                    CardImage: null,
                    ImportantMessageImage: null,
                    Tariffs: null)
            ],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null);

        // Act: token has extra leading/trailing/internal whitespace
        var result = resolver.Resolve("  TC   NL  ", bundleWithSpacedId);

        // Assert: normalisation collapses the spaces and the product is found
        result.IsSuccess.ShouldBeTrue(
            $"Token '  TC   NL  ' should resolve after whitespace collapse. Error: {result.Error}");
        result.Value!.ProductId.ShouldBe("TC NL");
    }

    // -----------------------------------------------------------------------
    // Test 7: Pre-cancelled token → cancelled result, no provider call
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the cancellation token is already cancelled before the call,
    /// BindAsync returns a cancelled result without calling the provider.
    /// </summary>
    [Fact]
    public async Task Bind_Cancelled_ReturnsCancelledResult()
    {
        var provider = Substitute.For<IVecReferenceDataProvider>();
        var resolver = new ProductResolver();
        var logger = XUnitLogger.CreateLogger<BundleBinder>();
        var binder = new BundleBinder(provider, resolver, logger);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var job = BuildJob();
        var result = await binder.BindAsync(job, DefaultKey(), "NL", cts.Token);

        // Assert: cancelled
        result.IsFailure.ShouldBeTrue();
        result.IsCancelled().ShouldBeTrue();

        // Provider must never have been called
        await provider.DidNotReceive()
            .GetBundleAsync(Arg.Any<StatementContextKey>(), Arg.Any<CancellationToken>());
    }
}
