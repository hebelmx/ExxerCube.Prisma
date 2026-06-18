using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Visual.DependencyInjection;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Visual.Tests;

/// <summary>
/// Unit tests for LAW-ADS-PLACEMENT: advertising placement + §12 length rule (Story 12.3).
/// All tests are pure in-memory — no PDF fixtures required.
/// Rules are discovered via the same Scrutor DI path used in production.
/// </summary>
public sealed class AdvertisingPlacementRuleTests
{
    private const string CheckId = "LAW-ADS-PLACEMENT";
    private const string ProductId = "TC-ADS-TEST";

    // -----------------------------------------------------------------------
    // DI factory — mirrors production Scrutor wiring
    // -----------------------------------------------------------------------

    private static IVecValidationRule GetRule()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeriqanVisual();

        using var sp = services.BuildServiceProvider();
        return sp.GetServices<IVecValidationRule>()
            .Single(r => r.CheckId == CheckId);
    }

    // -----------------------------------------------------------------------
    // Fixture helpers
    // -----------------------------------------------------------------------

    private static FieldLocator P1() => FieldLocator.PageHint(1);

    private static BundleMetadata Metadata() =>
        new("1.0.0", "Test Bank", null, null, null, null);

    private static VecProduct Product() =>
        new(ProductId: ProductId, ProductName: "Test Card",
            Aliases: null, HasRewardsProgram: false,
            CardImage: null, ImportantMessageImage: null,
            Tariffs: null);

    private static VecReferenceBundle Bundle() =>
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

    private static VerificationContext CtxWithModel(StatementModel? model)
    {
        var bundle = Bundle();
        return new VerificationContext(
            bundle: bundle,
            resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(bundle),
            priorStatement: null,
            toleranceConfig: null,
            statementModel: model);
    }

    /// <summary>
    /// Builds a minimal <see cref="StatementModel"/> with the given section list.
    /// </summary>
    private static StatementModel ModelWithSections(IReadOnlyList<DetectedSection> sections)
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
            Sections = sections,
        };
    }

    /// <summary>
    /// Builds a <see cref="DetectedSection"/> that is Present with the given text.
    /// </summary>
    private static DetectedSection PresentSection(int number, string text) =>
        new(
            SectionNumber: number,
            Name: $"Section {number}",
            IsPresent: true,
            IsApplicable: true,
            Locator: P1())
        {
            DetectionStatus = SectionDetectionStatus.Present,
            SectionText = text,
        };

    /// <summary>
    /// Builds a <see cref="DetectedSection"/> that is Absent (no text).
    /// </summary>
    private static DetectedSection AbsentSection(int number) =>
        new(
            SectionNumber: number,
            Name: $"Section {number}",
            IsPresent: false,
            IsApplicable: true,
            Locator: FieldLocator.NoPage())
        {
            DetectionStatus = SectionDetectionStatus.Absent,
            SectionText = string.Empty,
        };

    /// <summary>
    /// Returns a benign 500-char string of legal-statement language — no markers.
    /// </summary>
    private static string LegalText500() =>
        new string('A', 500); // 500 neutral chars well within the 700-char ceiling

    /// <summary>
    /// Returns a benign 900-char string — over the 700-char ceiling with no markers.
    /// </summary>
    private static string NeutralText900() =>
        new string('X', 900);

    // -----------------------------------------------------------------------
    // (a) InsufficientData paths
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_NullStatementModel_ReturnsInsufficientData()
    {
        var rule = GetRule();
        var ctx = CtxWithModel(model: null);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.CheckId.ShouldBe(CheckId);
    }

    [Fact]
    public void Evaluate_NullSections_ReturnsInsufficientData()
    {
        // StatementModel.Sections defaults to an empty list; to test the null/empty path
        // we provide an empty list which satisfies both "null" and "Count == 0" guards.
        var rule = GetRule();
        var model = ModelWithSections([]);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_EmptySections_ReturnsInsufficientData()
    {
        var rule = GetRule();
        var model = ModelWithSections([]);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Empty Sections means Epic 10 did not run — rule must abstain, never Fail.");
    }

    // -----------------------------------------------------------------------
    // (b) §12 present with 500 chars + no markers → Pass
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section12Present500Chars_NoMarkers_ReturnsPass()
    {
        var rule = GetRule();
        var sections = new List<DetectedSection>
        {
            PresentSection(12, LegalText500()),
        };
        var model = ModelWithSections(sections);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "§12 under 700 chars with no promotional markers must Pass.");
        result.Value.Observed.ShouldNotBeNullOrEmpty();
        result.Value.Observed!.ShouldContain("500");
    }

    // -----------------------------------------------------------------------
    // (c) §12 present with 900 chars → Fail (over-length, Critical)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section12Present900Chars_ReturnsFail_Critical()
    {
        var rule = GetRule();
        var sections = new List<DetectedSection>
        {
            PresentSection(12, NeutralText900()),
        };
        var model = ModelWithSections(sections);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        finding.Severity.ShouldBe(FindingSeverity.Critical,
            "§12 over-length is a high-confidence finding → Critical severity.");
        finding.Observed.ShouldNotBeNullOrEmpty();
        finding.Observed!.ShouldContain("900");
        finding.Observed.ShouldContain("700");
        finding.Locator.ShouldNotBeNull();
    }

    // -----------------------------------------------------------------------
    // (d) Promotional marker in §3 (non-permitted) → Fail
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_PromotionalMarkerInSection3_ReturnsFail()
    {
        // "CONTRATA YA" is a high-confidence promotional marker.
        const string advertisingText = "Contrata ya tu tarjeta de crédito preferencial.";

        var rule = GetRule();
        var sections = new List<DetectedSection>
        {
            PresentSection(3, advertisingText),
        };
        var model = ModelWithSections(sections);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail,
            "Advertising marker in a non-permitted section must Fail.");
        finding.Observed.ShouldNotBeNullOrEmpty();
        finding.Observed!.ShouldContain("§3");
        finding.Observed.ShouldContain("CONTRATA YA");
    }

    // -----------------------------------------------------------------------
    // (e) Same promotional marker in §21 (permitted) → Pass
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_PromotionalMarkerInSection21_Permitted_ReturnsPass()
    {
        // §21 is the "sección opcional libre" — advertising is lawful there.
        const string advertisingText = "Contrata ya tu tarjeta de crédito preferencial.";

        var rule = GetRule();
        var sections = new List<DetectedSection>
        {
            PresentSection(21, advertisingText),
        };
        var model = ModelWithSections(sections);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "§21 is a permitted advertising section; markers there must NOT trigger Fail.");
    }

    // -----------------------------------------------------------------------
    // (f) §12 Absent + other sections clean → Pass (length sub-check skipped)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section12Absent_OtherSectionsClean_ReturnsPass()
    {
        var rule = GetRule();
        var sections = new List<DetectedSection>
        {
            AbsentSection(12),
            PresentSection(3, "Información legal de su estado de cuenta."),
            PresentSection(5, "Resumen de movimientos del periodo."),
        };
        var model = ModelWithSections(sections);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "§12 absent → length sub-check skipped; clean sections → Pass.");
        result.Value.Observed.ShouldNotBeNullOrEmpty();
        result.Value.Observed!.ShouldContain("skipped");
    }

    // -----------------------------------------------------------------------
    // (g) Legitimate phrase "meses sin intereses" in §12 does NOT cause Fail
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_MesesSinInteresesInSection12_DoesNotFail()
    {
        // "meses sin intereses" is a mandated legal/financial statement term —
        // must NEVER be treated as a promotional marker.
        const string legalContent =
            "Si paga su saldo total no generará intereses. " +
            "Aplican 3 meses sin intereses con tarjetas participantes. " +
            "Consulte CAT promedio 45% sin IVA informativo.";

        var rule = GetRule();
        var sections = new List<DetectedSection>
        {
            PresentSection(12, legalContent),
        };
        var model = ModelWithSections(sections);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "\"meses sin intereses\" is legitimate statement language — must NEVER trigger Fail.");
    }

    // -----------------------------------------------------------------------
    // Additional: marker in §28 (also permitted) → Pass
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_PromotionalMarkerInSection28_Permitted_ReturnsPass()
    {
        // §28 is also a "sección opcional libre".
        const string advertisingText = "Oferta exclusiva para clientes seleccionados.";

        var rule = GetRule();
        var sections = new List<DetectedSection>
        {
            PresentSection(28, advertisingText),
        };
        var model = ModelWithSections(sections);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "§28 is a permitted advertising section; markers there must NOT trigger Fail.");
    }

    // -----------------------------------------------------------------------
    // Additional: §12 over-length AND a marker in non-permitted section
    //             → Fail with Critical (combined finding)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section12OverLength_AndMarkerInNonPermitted_ReturnsFail_Critical()
    {
        var rule = GetRule();
        var sections = new List<DetectedSection>
        {
            PresentSection(12, NeutralText900()),
            PresentSection(5, "Preaprobado — felicidades, usted ha sido preseleccionado."),
        };
        var model = ModelWithSections(sections);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        finding.Severity.ShouldBe(FindingSeverity.Critical,
            "When both over-length §12 and a marker are found, Critical takes precedence.");
    }

    // -----------------------------------------------------------------------
    // Additional: Cancellation → Cancelled result
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Cancelled_ReturnsCancelledResult()
    {
        var rule = GetRule();
        var ctx = CtxWithModel(model: null);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = rule.Evaluate(ctx, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // Additional: rule metadata
    // -----------------------------------------------------------------------

    [Fact]
    public void Rule_CheckId_IsLawAdsPlacement()
    {
        var rule = GetRule();
        rule.CheckId.ShouldBe(CheckId);
    }

    [Fact]
    public void Rule_DofNumeral_IsNotEmpty()
    {
        var rule = GetRule();
        rule.DofNumeral.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Rule_Classification_IsBaselineLocked()
    {
        var rule = GetRule();
        rule.Classification.ShouldBe(RuleClassification.BaselineLocked);
    }

    [Fact]
    public void Rule_Technique_IsDeterministic()
    {
        var rule = GetRule();
        rule.Technique.ShouldBe(TechniqueClass.Deterministic);
    }

    // -----------------------------------------------------------------------
    // Additional: DI registration — discovered by Scrutor
    // -----------------------------------------------------------------------

    [Fact]
    public void AddVeriqanVisual_ContainsLawAdsPlacementRule()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeriqanVisual();

        using var sp = services.BuildServiceProvider();
        var rules = sp.GetServices<IVecValidationRule>().ToList();

        rules.ShouldContain(
            r => r.CheckId == CheckId,
            $"Expected {CheckId} to be discovered by Scrutor.");
    }

    // -----------------------------------------------------------------------
    // Additional: §12 exactly at ceiling (700 chars) → Pass (not a breach)
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section12ExactlyAtCeiling_ReturnsPass()
    {
        var rule = GetRule();
        var sections = new List<DetectedSection>
        {
            PresentSection(12, new string('B', 700)),
        };
        var model = ModelWithSections(sections);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "§12 at exactly 700 chars is not over the ceiling; must Pass.");
    }

    // -----------------------------------------------------------------------
    // Additional: §12 at 701 chars (1 over) → Fail
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section12At701Chars_ReturnsFail()
    {
        var rule = GetRule();
        var sections = new List<DetectedSection>
        {
            PresentSection(12, new string('C', 701)),
        };
        var model = ModelWithSections(sections);
        var ctx = CtxWithModel(model);

        var result = rule.Evaluate(ctx, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "§12 at 701 chars is 1 over the 700-char ceiling — must Fail.");
        result.Value.Severity.ShouldBe(FindingSeverity.Critical);
    }
}
