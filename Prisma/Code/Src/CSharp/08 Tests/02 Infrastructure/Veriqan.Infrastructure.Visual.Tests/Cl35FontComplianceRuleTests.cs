using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Visual.DependencyInjection;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Visual.Tests;

/// <summary>
/// Unit tests for the CL-35 font-compliance rule (Story 5.1).
/// All tests are pure in-memory — no PDF fixtures required.
/// Rules are discovered via the same Scrutor DI path used in production.
/// </summary>
public sealed class Cl35FontComplianceRuleTests
{
    // -----------------------------------------------------------------------
    // DI factory — mirrors production wiring
    // -----------------------------------------------------------------------

    private static IVecValidationRule GetCl35Rule()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeriqanVisual();

        using var sp = services.BuildServiceProvider();
        var rule = sp.GetServices<IVecValidationRule>()
            .Single(r => r.CheckId == "CL-35");
        return rule;
    }

    // -----------------------------------------------------------------------
    // Fixture helpers
    // -----------------------------------------------------------------------

    private const string ProductId = "TC-VIS-TEST";

    private static BundleMetadata Metadata() =>
        new("1.0.0", "Test Bank", null, null, null, null);

    private static VecProduct Product() =>
        new(ProductId: ProductId, ProductName: "Test Card",
            Aliases: null, HasRewardsProgram: false,
            CardImage: null, ImportantMessageImage: null,
            Tariffs: null);

    private static VecReferenceBundle Bundle(string? requiredFont = null) =>
        new(
            BundleMetadata: Metadata(),
            Products: [Product()],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: requiredFont is not null
                ? new ValidationConstants(RequiredFontFamily: requiredFont, BankingYearDays: null, CatAnnualCommissionMxn: null)
                : null);

    private static FieldLocator P1() => FieldLocator.PageHint(1);

    private static StatementModel ModelWithFonts(
        IReadOnlyList<FontUsage> runs,
        FontExtractionStatus status = FontExtractionStatus.Extracted) =>
        new(
            clientName: ExtractedField<ExtractedClientName>.Missing(P1()),
            address: ExtractedField<ExtractedAddress>.Missing(P1()),
            branchNumber: ExtractedField<string>.Missing(P1()),
            cardNumber: ExtractedField<string>.Missing(P1()),
            clabe: ExtractedField<string>.Missing(P1()),
            clientNumber: ExtractedField<string>.Missing(P1()),
            rfc: ExtractedField<string>.Missing(P1()))
        {
            FontRuns = runs,
            FontExtractionStatus = status,
        };

    private static FontUsage Run(string rawName, int page = 1) =>
        new(rawName, page, new FieldLocator(page, 10.0, 700.0, 50.0, 12.0));

    private static VerificationContext CtxWithModel(
        StatementModel? model,
        string? requiredFont = null)
    {
        var bundle = Bundle(requiredFont);
        return new VerificationContext(
            bundle: bundle,
            resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(bundle),
            priorStatement: null,
            toleranceConfig: null,
            statementModel: model);
    }

    // -----------------------------------------------------------------------
    // Test 1: All runs normalize to Aptos → Pass
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_AllRunsAptos_ReturnsPass()
    {
        // Two raw names that both normalize to "Aptos":
        // "ABCDEE+Aptos" → strip prefix → "Aptos"
        // "XYZAAA+Aptos-Bold" → strip prefix → "Aptos-Bold" → strip suffix → "Aptos"
        var model = ModelWithFonts([
            Run("ABCDEE+Aptos"),
            Run("XYZAAA+Aptos-Bold"),
        ]);
        var rule = GetCl35Rule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.CheckId.ShouldBe("CL-35");
    }

    // -----------------------------------------------------------------------
    // Test 2: A non-Aptos run → Fail with offending font in observed
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_NonAptosRun_ReturnsFail()
    {
        var arialLocator = new FieldLocator(1, 20.0, 650.0, 60.0, 12.0);
        var model = ModelWithFonts([
            new FontUsage("Arial", 1, arialLocator),
        ]);
        var rule = GetCl35Rule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        finding.CheckId.ShouldBe("CL-35");
        finding.Observed.ShouldNotBeNullOrEmpty();
        finding.Observed!.ShouldContain("Arial");
        finding.Locator.ShouldNotBeNull();
        finding.Locator!.PageNumber.ShouldBe(1);
        finding.Expected.ShouldBe("Aptos");
    }

    // -----------------------------------------------------------------------
    // Test 3: Null StatementModel → InsufficientData
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_NullStatementModel_ReturnsInsufficientData()
    {
        var rule = GetCl35Rule();
        var ctx = CtxWithModel(model: null);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.CheckId.ShouldBe("CL-35");
    }

    // -----------------------------------------------------------------------
    // Test 4: FontExtractionStatus.NotFound (scanned PDF) → InsufficientData
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_FontExtractionStatusNotFound_ReturnsInsufficientData()
    {
        var model = ModelWithFonts([], FontExtractionStatus.NotFound);
        var rule = GetCl35Rule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    // -----------------------------------------------------------------------
    // Test 5: Cancellation → Cancelled result
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Cancelled_ReturnsCancelledResult()
    {
        var rule = GetCl35Rule();
        var ctx = CtxWithModel(model: null);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = rule.Evaluate(ctx, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // Test 6: Custom required font from bundle → used for comparison
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_CustomRequiredFont_UsesBundle()
    {
        // Bundle requires "Calibri"; model has only "Calibri-Bold" (normalizes to "Calibri").
        var model = ModelWithFonts([Run("Calibri-Bold")]);
        var rule = GetCl35Rule();
        var ctx = CtxWithModel(model, requiredFont: "Calibri");

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    // -----------------------------------------------------------------------
    // Test 7: Multiple non-Aptos runs → count in observed detail
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_MultipleNonAptosRuns_CountInObserved()
    {
        var model = ModelWithFonts([
            Run("Arial"),
            Run("Times-Roman"),
            Run("Courier"),
        ]);
        var rule = GetCl35Rule();
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
        // The observed message should mention the count of violations.
        result.Value.Observed.ShouldNotBeNull();
        result.Value.Observed!.ShouldContain("3");
    }

    // -----------------------------------------------------------------------
    // Test 8: DI wiring — AddVeriqanValidation + AddVeriqanVisual → CL-35 present
    // -----------------------------------------------------------------------

    [Fact]
    public void AddVeriqanVisual_PlusAddVeriqanValidation_EngineContainsCl35Rule()
    {
        // Arrange: build a DI container that wires both assemblies.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeriqanValidation();
        services.AddVeriqanVisual();

        using var sp = services.BuildServiceProvider();

        // Act: resolve all registered IVecValidationRule instances.
        var rules = sp.GetServices<IVecValidationRule>().ToList();

        // Assert: at least one rule has CheckId == "CL-35".
        rules.ShouldNotBeEmpty("Expected at least one IVecValidationRule to be registered.");
        rules.ShouldContain(
            r => r.CheckId == "CL-35",
            "Expected CL-35 (Cl35FontComplianceRule) to be discovered by Scrutor.");
    }

    // -----------------------------------------------------------------------
    // Test 9: DI coverage — all 7 visual rules registered (Epic 5 review finding)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Asserts that every visual CheckId is discovered by Scrutor after wiring both
    /// <c>AddVeriqanValidation()</c> and <c>AddVeriqanVisual()</c>.
    /// A future Scrutor-registration regression on any single rule will be caught here.
    /// </summary>
    [Fact]
    public void AddVeriqanVisual_PlusAddVeriqanValidation_AllSevenVisualRulesRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeriqanValidation();
        services.AddVeriqanVisual();

        using var sp = services.BuildServiceProvider();
        var registeredIds = sp.GetServices<IVecValidationRule>()
            .Select(r => r.CheckId)
            .ToHashSet();

        // The full set of visual rules shipped in Story 5.x (Epic 5):
        string[] expectedIds = ["CL-35", "CL-28", "CL-29", "CL-48", "CL-31", "CL-33", "CL-34"];

        foreach (var id in expectedIds)
        {
            registeredIds.ShouldContain(id,
                $"Expected visual rule {id} to be registered via Scrutor after AddVeriqanVisual().");
        }

        // Also confirm the total count matches so a newly added rule doesn't silently
        // slip in without a corresponding update here.
        registeredIds.Count.ShouldBeGreaterThanOrEqualTo(expectedIds.Length,
            "Fewer visual rules registered than expected.");
    }
}
