using System.Collections.Generic;
using System.Linq;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using IndQuestResults.Operations;
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
/// Unit tests for the conditional CONDUSEF section rules introduced in Story 10.5:
/// <list type="bullet">
///   <item><c>Section23CargosNoReconocidosRule</c> (CheckId = <c>LAW-§23-STATUS</c>)</item>
///   <item><c>Section25ReestructuraRule</c>          (CheckId = <c>LAW-§25-REESTRUCTURA</c>)</item>
/// </list>
/// </summary>
/// <remarks>
/// All tests construct <see cref="StatementModel"/> and <see cref="VerificationContext"/>
/// objects directly — no real PDF extraction is performed.
/// Rules are discovered via Scrutor DI (the same path used in production).
/// </remarks>
public sealed class ConditionalSectionRulesTests
{
    private const string CheckId23 = "LAW-§23-STATUS";
    private const string CheckId25 = "LAW-§25-REESTRUCTURA";
    private const string ProductId = "TC-COND-TEST";

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

    private static VecReferenceBundle MinimalBundle() =>
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

    private static FieldLocator P1() => FieldLocator.PageHint(1);
    private static FieldLocator NoPage() => FieldLocator.NoPage();

    /// <summary>
    /// Builds a <see cref="StatementModel"/> with the supplied sections list
    /// and optional normalized text.
    /// </summary>
    private static StatementModel ModelWith(
        IReadOnlyList<DetectedSection> sections,
        string normalizedFullText = "")
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

    private static VerificationContext Ctx(StatementModel? model)
    {
        var bundle = MinimalBundle();
        return new VerificationContext(
            bundle: bundle,
            resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(bundle),
            priorStatement: null,
            toleranceConfig: null,
            statementModel: model);
    }

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
    /// Builds a full 28-section list where:
    /// <list type="bullet">
    ///   <item>all unconditional sections are present and applicable;</item>
    ///   <item>conditional sections (§16, §23, §25) get their applicability from the supplied
    ///         override dictionary (key = section number, value = (IsPresent, IsApplicable));</item>
    ///   <item>conditional sections NOT in the override default to absent + not-applicable.</item>
    /// </list>
    /// </summary>
    private static List<DetectedSection> SectionsWith(
        System.Collections.Generic.Dictionary<int, (bool IsPresent, bool IsApplicable)>? overrides = null)
    {
        var conditionals = new System.Collections.Generic.HashSet<int> { 16, 23, 25 };
        overrides ??= new System.Collections.Generic.Dictionary<int, (bool IsPresent, bool IsApplicable)>();

        return Enumerable.Range(1, 28)
            .Select(n =>
            {
                if (overrides.TryGetValue(n, out var ov))
                    return new DetectedSection(n, $"Section {n}", ov.IsPresent, ov.IsApplicable, P1());

                if (conditionals.Contains(n))
                    return new DetectedSection(n, $"Section {n}", IsPresent: false, IsApplicable: false, NoPage());

                return new DetectedSection(n, $"Section {n}", IsPresent: true, IsApplicable: true, P1());
            })
            .ToList();
    }

    // -----------------------------------------------------------------------
    // §23 CargosNoReconocidosRule — InsufficientData paths
    // -----------------------------------------------------------------------

    [Fact]
    public void Sec23_NullStatementModel_ReturnsInsufficientData()
    {
        var rule = GetRule(CheckId23);
        var ctx = Ctx(model: null);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Null StatementModel must yield InsufficientData, never Fail.");
        result.Value.CheckId.ShouldBe(CheckId23);
    }

    [Fact]
    public void Sec23_EmptySectionsList_ReturnsInsufficientData()
    {
        var rule = GetRule(CheckId23);
        var model = ModelWith(sections: [], normalizedFullText: "SOME TEXT");
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Empty Sections (detection pass did not run) must yield InsufficientData.");
    }

    [Fact]
    public void Sec23_EmptySections_IsNotFail()
    {
        // Regression: rule must NEVER false-Fail when Sections is empty.
        var rule = GetRule(CheckId23);
        var model = ModelWith(sections: [], normalizedFullText: "SOME TEXT");
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "Empty Sections must never produce Fail.");
    }

    [Fact]
    public void Sec23_Sec23ApplicableButEmptyText_ReturnsInsufficientData()
    {
        // §23 is applicable but NormalizedFullText is empty → cannot check status tokens.
        var rule = GetRule(CheckId23);
        var overrides = new System.Collections.Generic.Dictionary<int, (bool IsPresent, bool IsApplicable)>
        {
            [23] = (IsPresent: true, IsApplicable: true),
        };
        var model = ModelWith(sections: SectionsWith(overrides), normalizedFullText: string.Empty);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "§23 applicable but empty text must yield InsufficientData, not Fail.");
    }

    // -----------------------------------------------------------------------
    // §23 CargosNoReconocidosRule — not-applicable (Pass)
    // -----------------------------------------------------------------------

    [Fact]
    public void Sec23_SectionNotApplicable_ReturnsPass_NotApplicable()
    {
        // §23 is absent and not applicable (trigger not present).
        var rule = GetRule(CheckId23);
        var model = ModelWith(
            sections: SectionsWith(),  // defaults: §23 absent + not-applicable
            normalizedFullText: "RESUMEN DE CUENTA MOVIMIENTOS DEL PERIODO");
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "§23 not applicable must return Pass (not Fail).");
        result.Value.Observed.ShouldNotBeNull();
        result.Value.Observed!.ShouldContain("not applicable",
            Case.Insensitive,
            "Observation must mention not-applicable.");
    }

    // -----------------------------------------------------------------------
    // §23 CargosNoReconocidosRule — Pass (valid status token present)
    // -----------------------------------------------------------------------

    [Fact]
    public void Sec23_SectionPresentWithPendienteEnRevision_ReturnsPass()
    {
        var rule = GetRule(CheckId23);
        var overrides = new System.Collections.Generic.Dictionary<int, (bool IsPresent, bool IsApplicable)>
        {
            [23] = (IsPresent: true, IsApplicable: true),
        };
        var text = "CARGOS NO RECONOCIDOS PENDIENTE EN REVISION CARGO 1 $500.00";
        var model = ModelWith(sections: SectionsWith(overrides), normalizedFullText: text);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Valid status 'PENDIENTE EN REVISION' present → Pass.");
    }

    [Fact]
    public void Sec23_SectionPresentWithConcluidaProcedente_ReturnsPass()
    {
        var rule = GetRule(CheckId23);
        var overrides = new System.Collections.Generic.Dictionary<int, (bool IsPresent, bool IsApplicable)>
        {
            [23] = (IsPresent: true, IsApplicable: true),
        };
        var text = "CARGOS NO RECONOCIDOS CONCLUIDA PROCEDENTE CARGO 2 $200.00";
        var model = ModelWith(sections: SectionsWith(overrides), normalizedFullText: text);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Valid status 'CONCLUIDA PROCEDENTE' present → Pass.");
    }

    [Fact]
    public void Sec23_SectionPresentWithConcluidaImprocedente_ReturnsPass()
    {
        var rule = GetRule(CheckId23);
        var overrides = new System.Collections.Generic.Dictionary<int, (bool IsPresent, bool IsApplicable)>
        {
            [23] = (IsPresent: true, IsApplicable: true),
        };
        var text = "CARGOS NO RECONOCIDOS CONCLUIDA IMPROCEDENTE CARGO 3 $150.00";
        var model = ModelWith(sections: SectionsWith(overrides), normalizedFullText: text);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Valid status 'CONCLUIDA IMPROCEDENTE' present → Pass.");
    }

    // -----------------------------------------------------------------------
    // §23 CargosNoReconocidosRule — Fail (no valid status token)
    // -----------------------------------------------------------------------

    [Fact]
    public void Sec23_SectionPresentWithNoValidStatus_ReturnsFail()
    {
        var rule = GetRule(CheckId23);
        var overrides = new System.Collections.Generic.Dictionary<int, (bool IsPresent, bool IsApplicable)>
        {
            [23] = (IsPresent: true, IsApplicable: true),
        };
        // Text has §23 content but no valid status token from the mandated enum.
        var text = "CARGOS NO RECONOCIDOS FECHA DESCRIPCION MONTO EN PROCESO";
        var model = ModelWith(sections: SectionsWith(overrides), normalizedFullText: text);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail,
            "§23 present with no valid status token must yield Fail.");
        finding.Severity.ShouldBe(FindingSeverity.Critical);
        finding.Observed.ShouldNotBeNull();
    }

    [Fact]
    public void Sec23_SectionPresentWithInvalidStatusOnly_ReturnsFail()
    {
        // Regression: ensure a non-enum status string doesn't count as valid.
        var rule = GetRule(CheckId23);
        var overrides = new System.Collections.Generic.Dictionary<int, (bool IsPresent, bool IsApplicable)>
        {
            [23] = (IsPresent: true, IsApplicable: true),
        };
        var text = "CARGOS NO RECONOCIDOS ESTADO DESCONOCIDO MONTO $999.00";
        var model = ModelWith(sections: SectionsWith(overrides), normalizedFullText: text);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "Non-enum status string must not satisfy the rule — only the mandated tokens count.");
    }

    // -----------------------------------------------------------------------
    // §23 CargosNoReconocidosRule — cancellation
    // -----------------------------------------------------------------------

    [Fact]
    public void Sec23_Cancellation_ReturnsCancelled()
    {
        var rule = GetRule(CheckId23);
        var model = ModelWith(sections: SectionsWith(), normalizedFullText: "SOME TEXT");
        var ctx = Ctx(model);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = rule.Evaluate(ctx, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // §23 CargosNoReconocidosRule — rule metadata
    // -----------------------------------------------------------------------

    [Fact]
    public void Sec23_Rule_CheckId_IsCorrect()
    {
        var rule = GetRule(CheckId23);
        rule.CheckId.ShouldBe(CheckId23);
    }

    [Fact]
    public void Sec23_Rule_DofNumeral_IsNotEmpty()
    {
        var rule = GetRule(CheckId23);
        rule.DofNumeral.ShouldNotBeNullOrWhiteSpace("DofNumeral must be non-empty per IVecValidationRule contract.");
    }

    [Fact]
    public void Sec23_Rule_Classification_IsBaselineLocked()
    {
        var rule = GetRule(CheckId23);
        rule.Classification.ShouldBe(RuleClassification.BaselineLocked);
    }

    [Fact]
    public void Sec23_Rule_Technique_IsDeterministic()
    {
        var rule = GetRule(CheckId23);
        rule.Technique.ShouldBe(TechniqueClass.Deterministic);
    }

    // -----------------------------------------------------------------------
    // §25 ReestructuraRule — InsufficientData paths
    // -----------------------------------------------------------------------

    [Fact]
    public void Sec25_NullStatementModel_ReturnsInsufficientData()
    {
        var rule = GetRule(CheckId25);
        var ctx = Ctx(model: null);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Null StatementModel must yield InsufficientData, never Fail.");
        result.Value.CheckId.ShouldBe(CheckId25);
    }

    [Fact]
    public void Sec25_EmptySectionsList_ReturnsInsufficientData()
    {
        var rule = GetRule(CheckId25);
        var model = ModelWith(sections: [], normalizedFullText: "SOME TEXT");
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Sec25_EmptySections_IsNotFail()
    {
        // Regression: rule must NEVER false-Fail when Sections is empty.
        var rule = GetRule(CheckId25);
        var model = ModelWith(sections: [], normalizedFullText: "REESTRUCTURA");
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldNotBe(FindingVerdict.Fail,
            "Empty Sections must never produce Fail.");
    }

    [Fact]
    public void Sec25_EmptyNormalizedText_ReturnsInsufficientData()
    {
        // NormalizedFullText is empty — cannot evaluate trigger.
        var rule = GetRule(CheckId25);
        var model = ModelWith(sections: SectionsWith(), normalizedFullText: string.Empty);
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData,
            "Empty text must yield InsufficientData, not Fail.");
    }

    // -----------------------------------------------------------------------
    // §25 ReestructuraRule — not-applicable (Pass)
    // -----------------------------------------------------------------------

    [Fact]
    public void Sec25_NoRestructureTrigger_ReturnsPass_NotApplicable()
    {
        // §25 not applicable + text has no restructure keywords → not-applicable Pass.
        var rule = GetRule(CheckId25);
        var model = ModelWith(
            sections: SectionsWith(),  // defaults: §25 absent + not-applicable
            normalizedFullText: "RESUMEN DE CUENTA MOVIMIENTOS DEL PERIODO SALDO TOTAL");
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "No restructure trigger → not-applicable → Pass.");
        result.Value.Observed.ShouldNotBeNull();
        result.Value.Observed!.ShouldContain("not applicable",
            Case.Insensitive,
            "Observation must mention not-applicable.");
    }

    // -----------------------------------------------------------------------
    // §25 ReestructuraRule — Fail (trigger present, §25 absent)
    // -----------------------------------------------------------------------

    [Fact]
    public void Sec25_ReestructuraTriggerInText_Sec25Absent_ReturnsFail()
    {
        // Text contains "REESTRUCTURA" keyword; §25 is not applicable + not present → Fail.
        var rule = GetRule(CheckId25);
        var model = ModelWith(
            sections: SectionsWith(),  // §25 defaults: absent + not-applicable
            normalizedFullText: "TU CUENTA TIENE UNA REESTRUCTURA ACTIVA CONSULTA LOS TERMINOS");
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail,
            "Restructure trigger present but §25 absent → Fail.");
        finding.Severity.ShouldBe(FindingSeverity.Critical);
        finding.Observed.ShouldNotBeNull();
    }

    [Fact]
    public void Sec25_ReestructuracionTriggerInText_Sec25Absent_ReturnsFail()
    {
        // "REESTRUCTURACION" (longer form) also triggers the rule.
        var rule = GetRule(CheckId25);
        var model = ModelWith(
            sections: SectionsWith(),
            normalizedFullText: "PROCESO DE REESTRUCTURACION DE DEUDA EN CURSO");
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "'REESTRUCTURACION' trigger present but §25 absent → Fail.");
    }

    [Fact]
    public void Sec25_IsApplicableTrue_Sec25Absent_ReturnsFail()
    {
        // IsApplicable=true from section detection (Story 10.1); §25 IsPresent=false.
        var rule = GetRule(CheckId25);
        var overrides = new System.Collections.Generic.Dictionary<int, (bool IsPresent, bool IsApplicable)>
        {
            [25] = (IsPresent: false, IsApplicable: true),
        };
        var model = ModelWith(
            sections: SectionsWith(overrides),
            normalizedFullText: "MOVIMIENTOS DEL PERIODO RESUMEN");
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail,
            "IsApplicable=true + §25 absent → Fail.");
    }

    // -----------------------------------------------------------------------
    // §25 ReestructuraRule — Pass (trigger present, §25 present)
    // -----------------------------------------------------------------------

    [Fact]
    public void Sec25_ReestructuraTriggerInText_Sec25Present_ReturnsPass()
    {
        // Text has trigger keyword AND §25 is present.
        var rule = GetRule(CheckId25);
        var overrides = new System.Collections.Generic.Dictionary<int, (bool IsPresent, bool IsApplicable)>
        {
            [25] = (IsPresent: true, IsApplicable: false),  // present but not-applicable from detection
        };
        var model = ModelWith(
            sections: SectionsWith(overrides),
            normalizedFullText: "TU CUENTA TIENE UNA REESTRUCTURA ACTIVA CONSULTA LOS TERMINOS");
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Trigger present and §25 present → Pass.");
    }

    [Fact]
    public void Sec25_IsApplicableTrue_Sec25Present_ReturnsPass()
    {
        // IsApplicable=true from section detection; §25 is also present.
        var rule = GetRule(CheckId25);
        var overrides = new System.Collections.Generic.Dictionary<int, (bool IsPresent, bool IsApplicable)>
        {
            [25] = (IsPresent: true, IsApplicable: true),
        };
        var model = ModelWith(
            sections: SectionsWith(overrides),
            normalizedFullText: "RESUMEN DE CUENTA SIN PALABRAS ESPECIALES");
        var ctx = Ctx(model);

        var result = rule.Evaluate(ctx, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "IsApplicable=true + §25 present → Pass.");
    }

    // -----------------------------------------------------------------------
    // §25 ReestructuraRule — cancellation
    // -----------------------------------------------------------------------

    [Fact]
    public void Sec25_Cancellation_ReturnsCancelled()
    {
        var rule = GetRule(CheckId25);
        var model = ModelWith(sections: SectionsWith(), normalizedFullText: "SOME TEXT");
        var ctx = Ctx(model);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = rule.Evaluate(ctx, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // §25 ReestructuraRule — rule metadata
    // -----------------------------------------------------------------------

    [Fact]
    public void Sec25_Rule_CheckId_IsCorrect()
    {
        var rule = GetRule(CheckId25);
        rule.CheckId.ShouldBe(CheckId25);
    }

    [Fact]
    public void Sec25_Rule_DofNumeral_IsNotEmpty()
    {
        var rule = GetRule(CheckId25);
        rule.DofNumeral.ShouldNotBeNullOrWhiteSpace("DofNumeral must be non-empty per IVecValidationRule contract.");
    }

    [Fact]
    public void Sec25_Rule_Classification_IsBaselineLocked()
    {
        var rule = GetRule(CheckId25);
        rule.Classification.ShouldBe(RuleClassification.BaselineLocked);
    }

    [Fact]
    public void Sec25_Rule_Technique_IsDeterministic()
    {
        var rule = GetRule(CheckId25);
        rule.Technique.ShouldBe(TechniqueClass.Deterministic);
    }
}
