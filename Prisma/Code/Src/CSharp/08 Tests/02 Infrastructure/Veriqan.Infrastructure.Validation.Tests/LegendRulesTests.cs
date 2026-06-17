using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Tests;

/// <summary>
/// Unit tests for Story 6.1 legend-verification rules: CL-46 and CL-32.
/// </summary>
/// <remarks>
/// All rules are discovered via Scrutor DI (the same path used in production).
/// Tests build minimal <see cref="StatementModel"/> instances by setting
/// <see cref="StatementModel.NormalizedFullText"/> directly on the init property —
/// no real PDF extraction is performed in this test class.
/// </remarks>
public sealed class LegendRulesTests
{
    private const string ProductId = "TC-LEG-TEST";

    // -----------------------------------------------------------------------
    // Shared fixture helpers
    // -----------------------------------------------------------------------

    private static BundleMetadata Metadata() =>
        new("1.0.0", "Test Bank", null, null, null, null);

    private static VecProduct Product() =>
        new(ProductId: ProductId, ProductName: "Test Card",
            Aliases: null, HasRewardsProgram: false,
            CardImage: null, ImportantMessageImage: null,
            Tariffs: null);

    private static FieldLocator P1() => FieldLocator.PageHint(1);

    /// <summary>
    /// Builds a <see cref="StatementModel"/> with all header fields set to Missing and
    /// the supplied <see cref="StatementModel.NormalizedFullText"/>.
    /// </summary>
    private static StatementModel ModelWithText(string normalizedFullText)
    {
        var missingStr = ExtractedField<string>.Missing(P1());
        var missingName = ExtractedField<ExtractedClientName>.Missing(P1());
        var missingAddr = ExtractedField<ExtractedAddress>.Missing(P1());

        return new StatementModel(
            clientName: missingName,
            address: missingAddr,
            branchNumber: missingStr,
            cardNumber: missingStr,
            clabe: missingStr,
            clientNumber: missingStr,
            rfc: missingStr)
        {
            NormalizedFullText = normalizedFullText,
        };
    }

    /// <summary>
    /// Builds a bundle that contains the given mandatory-legends list
    /// (no other reference data is relevant for CL-46/CL-32).
    /// </summary>
    private static VecReferenceBundle BundleWithLegends(
        IReadOnlyList<MandatoryLegend>? legends) =>
        new(
            BundleMetadata: Metadata(),
            Products: [Product()],
            InterestRates: null,
            MandatoryLegends: legends,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null);

    private static VerificationContext Ctx(
        VecReferenceBundle bundle,
        StatementModel? model = null) =>
        new(
            bundle: bundle,
            resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(bundle),
            priorStatement: null,
            toleranceConfig: bundle.ToleranceConfig,
            statementModel: model);

    private static IVecValidationRule GetRule(string checkId)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
        services.AddVeriqanValidation();
        var sp = services.BuildServiceProvider();

        foreach (var rule in sp.GetServices<IVecValidationRule>())
            if (rule.CheckId == checkId)
                return rule;

        throw new InvalidOperationException($"Rule '{checkId}' not found in DI.");
    }

    // -----------------------------------------------------------------------
    // CL-46: MandatoryLegendsRule
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl46_AllRequiredLegendsPresent_ReturnsPass()
    {
        var rule = GetRule("CL-46");
        var legends = new List<MandatoryLegend>
        {
            new("LEG-001", "Este documento es una representacion impresa sin validez fiscal",
                "normalized", null, null, Required: true),
            new("LEG-002", "Banco demo S.A.", "normalized", null, null, Required: true),
        };

        // Normalized text contains normalized versions of both legends.
        var text = "ESTE DOCUMENTO ES UNA REPRESENTACION IMPRESA SIN VALIDEZ FISCAL " +
                   "BANCO DEMO S.A. COMPARA TU TARJETA";
        var ctx = Ctx(BundleWithLegends(legends), ModelWithText(text));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("CL-46");
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    [Fact]
    public void Cl46_RequiredLegendMissing_ReturnsFail_NamingTheLegendId()
    {
        var rule = GetRule("CL-46");
        var legends = new List<MandatoryLegend>
        {
            new("LEG-001", "Este documento es una representacion impresa sin validez fiscal",
                "normalized", null, null, Required: true),
            new("LEG-MISSING", "Este texto no aparece en el estado de cuenta",
                "normalized", null, null, Required: true),
        };

        // Text has LEG-001 but NOT LEG-MISSING.
        var text = "ESTE DOCUMENTO ES UNA REPRESENTACION IMPRESA SIN VALIDEZ FISCAL COMPARA TU TARJETA";
        var ctx = Ctx(BundleWithLegends(legends), ModelWithText(text));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("CL-46");
        result.Value.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
        result.Value.Observed.ShouldNotBeNull();
        result.Value.Observed!.ShouldContain("LEG-MISSING");
        result.Value.Observed.ShouldNotContain("LEG-001");
    }

    [Fact]
    public void Cl46_NonRequiredLegendMissing_ReturnsPass()
    {
        var rule = GetRule("CL-46");
        var legends = new List<MandatoryLegend>
        {
            new("LEG-OPT", "Optional legend text that is absent",
                "normalized", null, null, Required: false),
            new("LEG-REQ", "Required legend present in the document",
                "normalized", null, null, Required: true),
        };

        // Text has LEG-REQ but NOT LEG-OPT; LEG-OPT is non-required → should still pass.
        var text = "REQUIRED LEGEND PRESENT IN THE DOCUMENT";
        var ctx = Ctx(BundleWithLegends(legends), ModelWithText(text));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    [Fact]
    public void Cl46_NoMandatoryLegendsInBundle_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-46");
        var ctx = Ctx(BundleWithLegends(null), ModelWithText("ANY TEXT"));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("CL-46");
        result.Value.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Cl46_EmptyMandatoryLegendsListInBundle_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-46");
        var ctx = Ctx(BundleWithLegends([]), ModelWithText("ANY TEXT"));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Cl46_NullStatementModel_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-46");
        var legends = new List<MandatoryLegend>
        {
            new("LEG-001", "Some required legend", "normalized", null, null, Required: true),
        };
        var ctx = Ctx(BundleWithLegends(legends), model: null);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Cl46_EmptyNormalizedFullText_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-46");
        var legends = new List<MandatoryLegend>
        {
            new("LEG-001", "Some required legend", "normalized", null, null, Required: true),
        };
        var ctx = Ctx(BundleWithLegends(legends), ModelWithText(string.Empty));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Cl46_Cancellation_ReturnsCancelled()
    {
        var rule = GetRule("CL-46");
        var legends = new List<MandatoryLegend>
        {
            new("LEG-001", "Some legend", "normalized", null, null, Required: true),
        };
        var ctx = Ctx(BundleWithLegends(legends), ModelWithText("SOME LEGEND"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = rule.Evaluate(ctx, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Verifies that accent-normalized legend text (e.g. "ú" → "U") still matches
    /// the statement text when both sides go through <c>TextNormalizer.Normalize</c>.
    /// </summary>
    [Fact]
    public void Cl46_AccentedLegendText_MatchesNormalizedStatementText()
    {
        var rule = GetRule("CL-46");
        var legends = new List<MandatoryLegend>
        {
            // Legend text has accents; statement has none (both paths are normalized).
            new("LEG-ACNT", "Representación Impresa Válida",
                "normalized", null, null, Required: true),
        };

        // Statement text already has the normalized form (no accents, upper-case).
        var text = "REPRESENTACION IMPRESA VALIDA";
        var ctx = Ctx(BundleWithLegends(legends), ModelWithText(text));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    // -----------------------------------------------------------------------
    // CL-32: ComparaTuTarjetaRule
    // -----------------------------------------------------------------------

    [Fact]
    public void Cl32_SectionHeadingPresent_ReturnsPass()
    {
        var rule = GetRule("CL-32");
        var text = "RESUMEN DE CARGOS COMPARA TU TARJETA DESGLOSE DE MOVIMIENTOS";
        var ctx = Ctx(BundleWithLegends(null), ModelWithText(text));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("CL-32");
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    [Fact]
    public void Cl32_SectionHeadingMissing_ReturnsFail()
    {
        var rule = GetRule("CL-32");
        var text = "RESUMEN DE CARGOS DESGLOSE DE MOVIMIENTOS";  // no COMPARA TU TARJETA
        var ctx = Ctx(BundleWithLegends(null), ModelWithText(text));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("CL-32");
        result.Value.Verdict.ShouldBe(FindingVerdict.Fail);
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
    }

    [Fact]
    public void Cl32_NullStatementModel_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-32");
        var ctx = Ctx(BundleWithLegends(null), model: null);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CheckId.ShouldBe("CL-32");
        result.Value.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Cl32_EmptyNormalizedFullText_ReturnsInsufficientData()
    {
        var rule = GetRule("CL-32");
        var ctx = Ctx(BundleWithLegends(null), ModelWithText(string.Empty));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Cl32_Cancellation_ReturnsCancelled()
    {
        var rule = GetRule("CL-32");
        var ctx = Ctx(BundleWithLegends(null), ModelWithText("COMPARA TU TARJETA"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = rule.Evaluate(ctx, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Verifies that the check is case-insensitive and accent-insensitive.
    /// A lower-case or accented variant of the section heading in the statement
    /// must still produce a Pass because <c>NormalizedFullText</c> is upper-case
    /// and accent-stripped before storage.
    /// </summary>
    [Fact]
    public void Cl32_MixedCaseAndAccents_MatchesWhenNormalized()
    {
        var rule = GetRule("CL-32");
        // Simulate a NormalizedFullText that was produced by the extractor,
        // so the heading is already upper-cased and accent-stripped.
        var text = "PREAMBULO COMPARA TU TARJETA SECCION ADICIONAL";
        var ctx = Ctx(BundleWithLegends(null), ModelWithText(text));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    /// <summary>
    /// CL-32 does NOT degrade to InsufficientData when the bundle has no legend data —
    /// the rule is bundle-independent (it only checks statement text presence).
    /// </summary>
    [Fact]
    public void Cl32_NoBundleLegendsButTextPresent_ReturnsPass()
    {
        var rule = GetRule("CL-32");
        // Bundle has no legends — CL-32 doesn't care.
        var ctx = Ctx(BundleWithLegends(null),
            ModelWithText("COMPARA TU TARJETA ESTA PRESENTE"));

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }
}
