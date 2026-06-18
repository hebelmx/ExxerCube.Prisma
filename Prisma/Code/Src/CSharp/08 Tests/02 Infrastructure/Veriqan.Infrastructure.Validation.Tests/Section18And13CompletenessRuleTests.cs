using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Tests;

/// <summary>
/// Unit tests for Story 10.6 completeness rules:
/// <see cref="Section18BenefitProgramCompletenessRule"/> (CheckId = "LAW-§18-COMPLETE") and
/// <see cref="Section13TransferenciaCompletenessRule"/> (CheckId = "LAW-§13-TRANSFERENCIA").
/// </summary>
/// <remarks>
/// All tests construct <see cref="StatementModel"/> and <see cref="VerificationContext"/>
/// objects directly — no real PDF extraction is performed.
/// Rules are discovered via Scrutor DI (production path).
/// </remarks>
public sealed class Section18And13CompletenessRuleTests
{
    private const string CheckId18 = "LAW-§18-COMPLETE";
    private const string CheckId13 = "LAW-§13-TRANSFERENCIA";
    private const string ProductId = "TC-COMP-TEST";

    // -----------------------------------------------------------------------
    // Shared fixture helpers
    // -----------------------------------------------------------------------

    private static BundleMetadata Metadata() =>
        new("1.0.0", "Test Bank", null, null, null, null);

    private static VecProduct Product(bool hasRewards = true) =>
        new(ProductId: ProductId, ProductName: "Test Card",
            Aliases: null, HasRewardsProgram: hasRewards,
            CardImage: null, ImportantMessageImage: null,
            Tariffs: null);

    private static VecReferenceBundle MinimalBundle(bool hasRewards = true) =>
        new(
            BundleMetadata: Metadata(),
            Products: [Product(hasRewards)],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null);

    private static FieldLocator P1() => FieldLocator.PageHint(1);

    /// <summary>
    /// Builds a minimal <see cref="StatementModel"/> with the supplied sections list and
    /// normalized full text.
    /// </summary>
    private static StatementModel ModelWithSectionsAndText(
        IReadOnlyList<DetectedSection> sections,
        string normalizedFullText)
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
            NormalizedFullText = normalizedFullText,
        };
    }

    private static VerificationContext Ctx(StatementModel? model, bool hasRewards = true)
    {
        var bundle = MinimalBundle(hasRewards);
        return new VerificationContext(
            bundle: bundle,
            resolvedProduct: Product(hasRewards),
            availability: ReferenceDataAvailability.FromBundle(bundle),
            priorStatement: null,
            toleranceConfig: null,
            statementModel: model);
    }

    /// <summary>
    /// Resolves a rule by CheckId from DI (production Scrutor path).
    /// </summary>
    private static IVecValidationRule GetRule(string checkId)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
        services.AddVeriqanValidation();
        var sp = services.BuildServiceProvider();

        foreach (var rule in sp.GetServices<IVecValidationRule>())
            if (rule.CheckId == checkId)
                return rule;

        throw new InvalidOperationException(
            $"Rule '{checkId}' not found in DI — check Scrutor registration.");
    }

    /// <summary>
    /// Builds a 28-section list where the specified section is present and applicable.
    /// All other sections are also present and applicable.
    /// </summary>
    private static List<DetectedSection> SectionsWithPresent(int sectionNumber)
    {
        return Enumerable.Range(1, 28)
            .Select(n => new DetectedSection(
                SectionNumber: n,
                Name: $"Section {n}",
                IsPresent: true,
                IsApplicable: true,
                Locator: P1()))
            .ToList();
    }

    /// <summary>
    /// Builds a 28-section list where the specified section is absent but applicable
    /// (i.e. it is a mandatory section that is simply missing from the PDF text).
    /// </summary>
    private static List<DetectedSection> SectionsWithAbsentApplicable(int sectionNumber)
    {
        return Enumerable.Range(1, 28)
            .Select(n => new DetectedSection(
                SectionNumber: n,
                Name: $"Section {n}",
                IsPresent: n != sectionNumber,
                IsApplicable: true,
                Locator: n != sectionNumber ? P1() : FieldLocator.NoPage()))
            .ToList();
    }

    /// <summary>
    /// Builds a 28-section list where the specified section is absent AND not applicable
    /// (i.e. a conditional section whose trigger is absent).
    /// </summary>
    private static List<DetectedSection> SectionsWithNotApplicable(int sectionNumber)
    {
        return Enumerable.Range(1, 28)
            .Select(n => new DetectedSection(
                SectionNumber: n,
                Name: $"Section {n}",
                IsPresent: n != sectionNumber,
                IsApplicable: n != sectionNumber,
                Locator: n != sectionNumber ? P1() : FieldLocator.NoPage()))
            .ToList();
    }

    /// <summary>
    /// Builds a normalized-full-text string containing all nine §18 mandated concept labels.
    /// </summary>
    private static string TextWithAllSection18Concepts() =>
        $"PROGRAMAS DE BENEFICIOS " +
        $"{Section18BenefitProgramCompletenessRule.ConceptSaldoInicial} 0 " +
        $"{Section18BenefitProgramCompletenessRule.ConceptGenerados} 150 " +
        $"{Section18BenefitProgramCompletenessRule.ConceptRedimidos} 0 " +
        $"{Section18BenefitProgramCompletenessRule.ConceptVencidos} 0 " +
        $"{Section18BenefitProgramCompletenessRule.ConceptPorVencer} 0 " +
        $"{Section18BenefitProgramCompletenessRule.ConceptSaldoFinal} 150 " +
        $"{Section18BenefitProgramCompletenessRule.ConceptUnidad} PUNTOS " +
        $"{Section18BenefitProgramCompletenessRule.ConceptEquivalenciaEnPesos} $15.00 " +
        $"{Section18BenefitProgramCompletenessRule.ConceptContacto} 800-000-0000";

    // -----------------------------------------------------------------------
    // §18 — InsufficientData paths
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate18_NullStatementModel_ReturnsInsufficientData()
    {
        var rule = GetRule(CheckId18);
        var ctx = Ctx(model: null);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Null StatementModel must yield InsufficientData, never Fail.");
        result.Value.CheckId.ShouldBe(CheckId18);
    }

    [Fact]
    public void Evaluate18_EmptySections_ReturnsInsufficientData()
    {
        var rule = GetRule(CheckId18);
        var model = ModelWithSectionsAndText([], "SOME TEXT");
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Empty Sections must yield InsufficientData — detection pass did not run.");
    }

    [Fact]
    public void Evaluate18_EmptySections_IsNotFail()
    {
        // Regression guard: empty Sections must NEVER produce Fail.
        var rule = GetRule(CheckId18);
        var model = ModelWithSectionsAndText([], "SOME TEXT");
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "Empty Sections must never produce Fail.");
    }

    [Fact]
    public void Evaluate18_Section18NotApplicable_ReturnsInsufficientData()
    {
        // §18 is not applicable (no rewards program trigger detected).
        var rule = GetRule(CheckId18);
        var sections = SectionsWithNotApplicable(18);
        var model = ModelWithSectionsAndText(sections, "SOME TEXT");
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "§18 not applicable must yield InsufficientData, never Fail.");
    }

    [Fact]
    public void Evaluate18_Section18AbsentButApplicable_ReturnsInsufficientData()
    {
        // §18 is applicable but absent from the PDF — MandatorySectionsPresenceRule covers that.
        var rule = GetRule(CheckId18);
        var sections = SectionsWithAbsentApplicable(18);
        var model = ModelWithSectionsAndText(sections, "SOME TEXT");
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "§18 absent (applicable) must yield InsufficientData — absence is covered by the presence rule.");
    }

    [Fact]
    public void Evaluate18_EmptyNormalizedFullText_ReturnsInsufficientData()
    {
        // §18 is present and applicable but the full-text is empty (unreadable PDF).
        var rule = GetRule(CheckId18);
        var sections = SectionsWithPresent(18);
        var model = ModelWithSectionsAndText(sections, string.Empty);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Empty NormalizedFullText (unreadable text layer) must yield InsufficientData.");
    }

    // -----------------------------------------------------------------------
    // §18 — Pass
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate18_AllConceptsPresent_ReturnsPass()
    {
        var rule = GetRule(CheckId18);
        var sections = SectionsWithPresent(18);
        var text = TextWithAllSection18Concepts();
        var model = ModelWithSectionsAndText(sections, text);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "All nine §18 concept labels present must yield Pass.");
        result.Value.CheckId.ShouldBe(CheckId18);
    }

    [Fact]
    public void Evaluate18_AllConceptsPresentWithZeroValues_ReturnsPass()
    {
        // CL-38 requirement: even "0" values (zero balance) must be shown.
        // The check looks for the LABEL, not for a non-zero value.
        // All concepts present with explicit "0" amounts → Pass.
        var rule = GetRule(CheckId18);
        var sections = SectionsWithPresent(18);

        // Build text where every balance is 0.
        var text =
            $"PROGRAMAS DE BENEFICIOS " +
            $"{Section18BenefitProgramCompletenessRule.ConceptSaldoInicial} 0 " +
            $"{Section18BenefitProgramCompletenessRule.ConceptGenerados} 0 " +
            $"{Section18BenefitProgramCompletenessRule.ConceptRedimidos} 0 " +
            $"{Section18BenefitProgramCompletenessRule.ConceptVencidos} 0 " +
            $"{Section18BenefitProgramCompletenessRule.ConceptPorVencer} 0 " +
            $"{Section18BenefitProgramCompletenessRule.ConceptSaldoFinal} 0 " +
            $"{Section18BenefitProgramCompletenessRule.ConceptUnidad} PUNTOS " +
            $"{Section18BenefitProgramCompletenessRule.ConceptEquivalenciaEnPesos} $0.00 " +
            $"{Section18BenefitProgramCompletenessRule.ConceptContacto} 800-000-0000";

        var model = ModelWithSectionsAndText(sections, text);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "All labels present with '0' values must still yield Pass — CL-38 requires disclosure even at zero.");
    }

    // -----------------------------------------------------------------------
    // §18 — Fail
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate18_MissingEquivalenciaEnPesos_ReturnsFail()
    {
        var rule = GetRule(CheckId18);
        var sections = SectionsWithPresent(18);

        // Build text with all concepts EXCEPT equivalencia en pesos.
        var text =
            $"PROGRAMAS DE BENEFICIOS " +
            $"{Section18BenefitProgramCompletenessRule.ConceptSaldoInicial} 100 " +
            $"{Section18BenefitProgramCompletenessRule.ConceptGenerados} 50 " +
            $"{Section18BenefitProgramCompletenessRule.ConceptRedimidos} 10 " +
            $"{Section18BenefitProgramCompletenessRule.ConceptVencidos} 0 " +
            $"{Section18BenefitProgramCompletenessRule.ConceptPorVencer} 140 " +
            $"{Section18BenefitProgramCompletenessRule.ConceptSaldoFinal} 140 " +
            $"{Section18BenefitProgramCompletenessRule.ConceptUnidad} PUNTOS " +
            // ConceptEquivalenciaEnPesos intentionally omitted
            $"{Section18BenefitProgramCompletenessRule.ConceptContacto} 800-000-0000";

        var model = ModelWithSectionsAndText(sections, text);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        finding.Severity.ShouldBe(FindingSeverity.Critical);
        finding.Observed.ShouldNotBeNull("Observed must name the missing concept(s).");
        finding.Observed!.ShouldContain(Section18BenefitProgramCompletenessRule.ConceptEquivalenciaEnPesos);
    }

    [Fact]
    public void Evaluate18_MissingContacto_ReturnsFail()
    {
        var rule = GetRule(CheckId18);
        var sections = SectionsWithPresent(18);

        var text =
            $"PROGRAMAS DE BENEFICIOS " +
            $"{Section18BenefitProgramCompletenessRule.ConceptSaldoInicial} 0 " +
            $"{Section18BenefitProgramCompletenessRule.ConceptGenerados} 0 " +
            $"{Section18BenefitProgramCompletenessRule.ConceptRedimidos} 0 " +
            $"{Section18BenefitProgramCompletenessRule.ConceptVencidos} 0 " +
            $"{Section18BenefitProgramCompletenessRule.ConceptPorVencer} 0 " +
            $"{Section18BenefitProgramCompletenessRule.ConceptSaldoFinal} 0 " +
            $"{Section18BenefitProgramCompletenessRule.ConceptUnidad} PUNTOS " +
            $"{Section18BenefitProgramCompletenessRule.ConceptEquivalenciaEnPesos} $0.00";
        // ConceptContacto intentionally omitted

        var model = ModelWithSectionsAndText(sections, text);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        finding.Observed.ShouldNotBeNull();
        finding.Observed!.ShouldContain(Section18BenefitProgramCompletenessRule.ConceptContacto);
    }

    [Fact]
    public void Evaluate18_MultipleConceptsMissing_FailNamesAllMissing()
    {
        var rule = GetRule(CheckId18);
        var sections = SectionsWithPresent(18);

        // Only saldo inicial and saldo final present — everything else missing.
        var text =
            $"{Section18BenefitProgramCompletenessRule.ConceptSaldoInicial} 0 " +
            $"{Section18BenefitProgramCompletenessRule.ConceptSaldoFinal} 0";

        var model = ModelWithSectionsAndText(sections, text);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        finding.Observed.ShouldNotBeNull();
        finding.Observed!.ShouldContain(Section18BenefitProgramCompletenessRule.ConceptGenerados);
        finding.Observed.ShouldContain(Section18BenefitProgramCompletenessRule.ConceptRedimidos);
        finding.Observed.ShouldContain(Section18BenefitProgramCompletenessRule.ConceptVencidos);
        finding.Observed.ShouldContain(Section18BenefitProgramCompletenessRule.ConceptPorVencer);
        finding.Observed.ShouldContain(Section18BenefitProgramCompletenessRule.ConceptUnidad);
        finding.Observed.ShouldContain(Section18BenefitProgramCompletenessRule.ConceptEquivalenciaEnPesos);
        finding.Observed.ShouldContain(Section18BenefitProgramCompletenessRule.ConceptContacto);
    }

    // -----------------------------------------------------------------------
    // §18 — rule metadata
    // -----------------------------------------------------------------------

    [Fact]
    public void Rule18_CheckId_IsLawSection18Complete()
    {
        var rule = GetRule(CheckId18);
        rule.CheckId.ShouldBe(CheckId18);
    }

    [Fact]
    public void Rule18_DofNumeral_IsAcuerdoSection18()
    {
        var rule = GetRule(CheckId18);
        rule.DofNumeral.ShouldBe("Acuerdo §18");
    }

    [Fact]
    public void Rule18_Classification_IsBaselineLocked()
    {
        var rule = GetRule(CheckId18);
        rule.Classification.ShouldBe(RuleClassification.BaselineLocked,
            "§18 completeness is law-defined; tenants may not relax it.");
    }

    [Fact]
    public void Rule18_Technique_IsDeterministic()
    {
        var rule = GetRule(CheckId18);
        rule.Technique.ShouldBe(TechniqueClass.Deterministic);
    }

    // -----------------------------------------------------------------------
    // §13 — InsufficientData paths
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate13_NullStatementModel_ReturnsInsufficientData()
    {
        var rule = GetRule(CheckId13);
        var ctx = Ctx(model: null);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Null StatementModel must yield InsufficientData, never Fail.");
        result.Value.CheckId.ShouldBe(CheckId13);
    }

    [Fact]
    public void Evaluate13_EmptySections_ReturnsInsufficientData()
    {
        var rule = GetRule(CheckId13);
        var model = ModelWithSectionsAndText([], "SOME TEXT");
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate13_EmptySections_IsNotFail()
    {
        var rule = GetRule(CheckId13);
        var model = ModelWithSectionsAndText([], "SOME TEXT");
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "Empty Sections must never produce Fail.");
    }

    [Fact]
    public void Evaluate13_Section13NotApplicable_ReturnsInsufficientData()
    {
        var rule = GetRule(CheckId13);
        var sections = SectionsWithNotApplicable(13);
        var model = ModelWithSectionsAndText(sections, "NIVEL DE USO SOME TEXT");
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "§13 not applicable must yield InsufficientData, never Fail.");
    }

    [Fact]
    public void Evaluate13_Section13AbsentButApplicable_ReturnsInsufficientData()
    {
        // §13 applicable but absent — covered by MandatorySectionsPresenceRule.
        var rule = GetRule(CheckId13);
        var sections = SectionsWithAbsentApplicable(13);
        var model = ModelWithSectionsAndText(sections, "SOME TEXT");
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "§13 absent (applicable) must yield InsufficientData — absence is covered by the presence rule.");
    }

    [Fact]
    public void Evaluate13_EmptyNormalizedFullText_ReturnsInsufficientData()
    {
        var rule = GetRule(CheckId13);
        var sections = SectionsWithPresent(13);
        var model = ModelWithSectionsAndText(sections, string.Empty);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Empty NormalizedFullText must yield InsufficientData.");
    }

    // -----------------------------------------------------------------------
    // §13 — Pass
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate13_TransferenciaLabelPresent_ReturnsPass()
    {
        var rule = GetRule(CheckId13);
        var sections = SectionsWithPresent(13);

        var text =
            $"NIVEL DE USO CREDITO DISPONIBLE {Section13TransferenciaCompletenessRule.LabelTransferencia} DE OTRAS TARJETAS $5000.00";

        var model = ModelWithSectionsAndText(sections, text);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Transferencia de saldo label present in §13 must yield Pass.");
        result.Value.CheckId.ShouldBe(CheckId13);
    }

    // -----------------------------------------------------------------------
    // §13 — Fail
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate13_TransferenciaLabelMissing_ReturnsFail()
    {
        var rule = GetRule(CheckId13);
        var sections = SectionsWithPresent(13);

        // §13 present but no mention of transferencia de saldo.
        var text = "NIVEL DE USO CREDITO DISPONIBLE $10000.00 CREDITO DISPONIBLE EN EFECTIVO $2000.00";

        var model = ModelWithSectionsAndText(sections, text);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        finding.Severity.ShouldBe(FindingSeverity.Critical);
        finding.Expected.ShouldBe(Section13TransferenciaCompletenessRule.LabelTransferencia,
            "Expected must be the required label.");
        finding.Observed.ShouldNotBeNull("Observed must describe the missing label.");
        finding.Observed!.ShouldContain(Section13TransferenciaCompletenessRule.LabelTransferencia);
    }

    // -----------------------------------------------------------------------
    // §13 — rule metadata
    // -----------------------------------------------------------------------

    [Fact]
    public void Rule13_CheckId_IsLawSection13Transferencia()
    {
        var rule = GetRule(CheckId13);
        rule.CheckId.ShouldBe(CheckId13);
    }

    [Fact]
    public void Rule13_DofNumeral_IsAcuerdoSection13()
    {
        var rule = GetRule(CheckId13);
        rule.DofNumeral.ShouldBe("Acuerdo §13");
    }

    [Fact]
    public void Rule13_Classification_IsBaselineLocked()
    {
        var rule = GetRule(CheckId13);
        rule.Classification.ShouldBe(RuleClassification.BaselineLocked,
            "§13 completeness is law-defined; tenants may not relax it.");
    }

    [Fact]
    public void Rule13_Technique_IsDeterministic()
    {
        var rule = GetRule(CheckId13);
        rule.Technique.ShouldBe(TechniqueClass.Deterministic);
    }
}
