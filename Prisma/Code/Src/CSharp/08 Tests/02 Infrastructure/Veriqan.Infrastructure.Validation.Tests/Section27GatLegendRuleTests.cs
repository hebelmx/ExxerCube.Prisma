using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Tests;

/// <summary>
/// Unit tests for <c>Section27GatLegendRule</c> (LAW-DUC-ART27-GAT / E10.C5).
/// </summary>
/// <remarks>
/// <para>
/// <b>Critical invariant — abstain-safe:</b> when the GAT legend is absent the rule
/// must return <see cref="FindingVerdict.InsufficientData"/>, NEVER
/// <see cref="FindingVerdict.Fail"/>.  Failing on absence would produce false REDs on
/// every credit-card statement because GAT only applies to operaciones pasivas
/// (Disposición Única CONDUSEF Art. 27) and the system has no product-type discriminator.
/// </para>
/// <para>
/// Rules are resolved through the Scrutor DI scan (<see cref="VeriqanValidationServiceCollectionExtensions.AddVeriqanValidation"/>)
/// so these tests exercise the same auto-discovery path used in production.
/// </para>
/// </remarks>
public sealed class Section27GatLegendRuleTests
{
    private const string CheckId = "LAW-DUC-ART27-GAT";
    private const string ProductId = "TC-GAT-TEST";

    // -----------------------------------------------------------------------
    // Fixture helpers
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
    /// Minimal bundle with no reference data (the GAT rule needs none — it only checks text).
    /// </summary>
    private static VecReferenceBundle EmptyBundle() =>
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
            ValidationConstants: null);

    private static VerificationContext Ctx(StatementModel? model)
    {
        var bundle = EmptyBundle();
        return new(
            bundle: bundle,
            resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(bundle),
            priorStatement: null,
            toleranceConfig: null,
            statementModel: model);
    }

    /// <summary>
    /// Resolves <c>LAW-DUC-ART27-GAT</c> from the DI-registered rules (Scrutor scan path).
    /// </summary>
    private static IVecValidationRule GetRule()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
        services.AddVeriqanValidation();
        var sp = services.BuildServiceProvider();

        foreach (var rule in sp.GetServices<IVecValidationRule>())
            if (rule.CheckId == CheckId)
                return rule;

        throw new InvalidOperationException($"Rule '{CheckId}' not found in DI — check Scrutor scan.");
    }

    // -----------------------------------------------------------------------
    // Test 1 — "GAT" abbreviation present → Pass
    // -----------------------------------------------------------------------

    /// <summary>
    /// When <see cref="StatementModel.NormalizedFullText"/> contains the abbreviation "GAT"
    /// the rule must return <see cref="FindingVerdict.Pass"/> and the correct CheckId.
    /// </summary>
    [Fact]
    public void Evaluate_NormalizedTextContainsGat_ReturnsPass()
    {
        var ct = TestContext.Current.CancellationToken;
        var model = ModelWithText(
            "SALDO ANTERIOR 1000.00 GAT PROMEDIO 4.50% ANUAL PAGO MINIMO 200.00");
        var ctx = Ctx(model);
        var rule = GetRule();

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue($"Rule threw unexpectedly: {result.Error}");
        result.Value!.CheckId.ShouldBe(CheckId);
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass,
            "When 'GAT' is present in the normalized statement text the rule must return Pass.");
    }

    // -----------------------------------------------------------------------
    // Test 2 — long form "GANANCIA ANUAL TOTAL" present (no standalone "GAT") → Pass
    // -----------------------------------------------------------------------

    /// <summary>
    /// When <see cref="StatementModel.NormalizedFullText"/> contains the full phrase
    /// "GANANCIA ANUAL TOTAL" — but does NOT contain the three-character sequence "GAT"
    /// — the rule must still return <see cref="FindingVerdict.Pass"/>.
    /// </summary>
    /// <remarks>
    /// "GANANCIA ANUAL TOTAL" does not embed the substring "GAT" (G-A-N at the start, not G-A-T),
    /// so this test isolates the long-form detection branch.
    /// </remarks>
    [Fact]
    public void Evaluate_NormalizedTextContainsGananciaAnualTotalOnly_ReturnsPass()
    {
        var ct = TestContext.Current.CancellationToken;
        // "GANANCIA ANUAL TOTAL" does not contain the 3-char sequence "GAT".
        var model = ModelWithText(
            "INFORMACION SOBRE RENDIMIENTO GANANCIA ANUAL TOTAL 5.20% ANUAL BRUTO");
        var ctx = Ctx(model);
        var rule = GetRule();

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue($"Rule threw unexpectedly: {result.Error}");
        result.Value!.CheckId.ShouldBe(CheckId);
        result.Value.Verdict.ShouldBe(FindingVerdict.Pass,
            "When 'GANANCIA ANUAL TOTAL' (long form) is present the rule must return Pass " +
            "even when the abbreviation 'GAT' does not appear separately.");
    }

    // -----------------------------------------------------------------------
    // Test 3 — neither form present → InsufficientData (NEVER Fail)
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the statement text contains neither "GAT" nor "GANANCIA ANUAL TOTAL" the rule
    /// must abstain with <see cref="FindingVerdict.InsufficientData"/>, never
    /// <see cref="FindingVerdict.Fail"/>.  A Fail here would produce a false RED on every
    /// credit-card statement.
    /// </summary>
    [Fact]
    public void Evaluate_NeitherGatNorLongFormPresent_ReturnsInsufficientData_NotFail()
    {
        var ct = TestContext.Current.CancellationToken;
        // Typical credit-card statement text — no GAT legend.
        var model = ModelWithText(
            "SALDO ANTERIOR 1000.00 COMPRAS 500.00 INTERESES 25.00 " +
            "PAGO MINIMO 100.00 FECHA CORTE 15 MAYO 2026");
        var ctx = Ctx(model);
        var rule = GetRule();

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue($"Rule threw unexpectedly: {result.Error}");
        result.Value!.CheckId.ShouldBe(CheckId);
        result.Value.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "When neither 'GAT' nor 'GANANCIA ANUAL TOTAL' is found the rule must abstain " +
            "(InsufficientData) — product type is unknown, GAT only applies to operaciones pasivas.");
        result.Value.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "The GAT rule must NEVER emit Fail — that would falsely RED every credit-card statement.");
    }

    // -----------------------------------------------------------------------
    // Test 3b — "GAT" embedded in an unrelated word/merchant name → InsufficientData
    //           (regression: word-boundary match, not bare substring)
    // -----------------------------------------------------------------------

    /// <summary>
    /// The "GAT" abbreviation must be matched as a WHOLE WORD.  A credit-card statement whose
    /// DESGLOSE contains a merchant name like "GATORADE" — or a normalized word such as
    /// "DELEGATARIO" — embeds the trigram "GAT" but is NOT a GAT legend.  The rule must abstain
    /// (InsufficientData), never report a false Pass ("GAT legend found").
    /// </summary>
    [Theory]
    [InlineData("SALDO ANTERIOR 1000.00 GATORADE OXXO 85.00 PAGO MINIMO 100.00")]
    [InlineData("DELEGATARIO FIDUCIARIO COMPRAS 500.00 INTERESES 25.00")]
    [InlineData("AGUA GATO NEGRO 120.00 SUPERGAT TIENDA 60.00")]
    public void Evaluate_GatOnlyAsSubstringOfAnotherWord_ReturnsInsufficientData_NotPass(string text)
    {
        var ct = TestContext.Current.CancellationToken;
        var model = ModelWithText(text);
        var ctx = Ctx(model);
        var rule = GetRule();

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue($"Rule threw unexpectedly: {result.Error}");
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "'GAT' embedded inside another word (GATORADE / DELEGATARIO / SUPERGAT) is not a GAT " +
            "legend — the rule must abstain, not report a false Pass.");
    }

    // -----------------------------------------------------------------------
    // Test 4a — null StatementModel → InsufficientData
    // -----------------------------------------------------------------------

    /// <summary>
    /// A null <see cref="StatementModel"/> (extraction has not run) must produce
    /// <see cref="FindingVerdict.InsufficientData"/>.
    /// </summary>
    [Fact]
    public void Evaluate_NullStatementModel_ReturnsInsufficientData()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = Ctx(model: null);
        var rule = GetRule();

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue($"Rule threw unexpectedly: {result.Error}");
        result.Value!.CheckId.ShouldBe(CheckId);
        result.Value.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "A null StatementModel must produce InsufficientData.");
    }

    // -----------------------------------------------------------------------
    // Test 4b — empty NormalizedFullText → InsufficientData
    // -----------------------------------------------------------------------

    /// <summary>
    /// An empty <see cref="StatementModel.NormalizedFullText"/> (PDF has no text layer)
    /// must produce <see cref="FindingVerdict.InsufficientData"/>.
    /// </summary>
    [Fact]
    public void Evaluate_EmptyNormalizedFullText_ReturnsInsufficientData()
    {
        var ct = TestContext.Current.CancellationToken;
        var model = ModelWithText(string.Empty);
        var ctx = Ctx(model);
        var rule = GetRule();

        var result = rule.Evaluate(ctx, ct);

        result.IsSuccess.ShouldBeTrue($"Rule threw unexpectedly: {result.Error}");
        result.Value!.CheckId.ShouldBe(CheckId);
        result.Value.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "An empty NormalizedFullText must produce InsufficientData.");
    }
}
