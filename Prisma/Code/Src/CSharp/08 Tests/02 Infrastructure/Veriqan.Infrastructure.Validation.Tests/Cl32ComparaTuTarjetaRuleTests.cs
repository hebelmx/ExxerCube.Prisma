using System;
using System.Collections.Generic;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
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
/// Unit tests for <c>Cl32ComparaTuTarjetaRule</c> (CheckId = "CL-32") — RC1.S6 rework: the rule
/// now consults <see cref="StatementModel.Sections"/> (§11) FIRST, picking up both a text-layer
/// heading-band match and an OCR-escalated one, and only falls back to the original raw
/// <c>NormalizedFullText.Contains</c> search when <c>Sections</c> is empty (extraction predates
/// Story 10.1) or §11's <see cref="DetectedSection.DetectionStatus"/> is Indeterminate.
/// </summary>
public sealed class Cl32ComparaTuTarjetaRuleTests
{
    private const string CheckId = "CL-32";
    private const string ProductId = "TC-CL32-TEST";
    private const int Section11Number = 11;

    // -----------------------------------------------------------------------
    // Shared fixture helpers (mirrors MandatorySectionsPresenceRuleTests)
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

    private static StatementModel ModelWith(
        IReadOnlyList<DetectedSection>? sections = null,
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
            Sections = sections ?? [],
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

    private static IVecValidationRule GetRule()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
        services.AddVeriqanValidation();
        var sp = services.BuildServiceProvider();

        foreach (var rule in sp.GetServices<IVecValidationRule>())
            if (rule.CheckId == CheckId)
                return rule;

        throw new InvalidOperationException($"Rule '{CheckId}' not found in DI — check Scrutor registration.");
    }

    private static DetectedSection Section11(
        bool isPresent,
        SectionDetectionSource source = SectionDetectionSource.TextLayer,
        SectionDetectionStatus status = SectionDetectionStatus.Present) =>
        new(
            SectionNumber: Section11Number,
            Name: "Compara tu tarjeta",
            IsPresent: isPresent,
            IsApplicable: true,
            Locator: isPresent ? P1() : FieldLocator.NoPage())
        {
            DetectionStatus = status,
            Source = source,
        };

    private static List<DetectedSection> SectionsWithSection11(DetectedSection section11)
    {
        var list = new List<DetectedSection>();
        for (var n = 1; n <= 28; n++)
        {
            list.Add(n == Section11Number
                ? section11
                : new DetectedSection(n, $"Section {n}", IsPresent: false, IsApplicable: false, Locator: FieldLocator.NoPage()));
        }

        return list;
    }

    // -----------------------------------------------------------------------
    // InsufficientData — unchanged guard
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_NullStatementModel_ReturnsInsufficientData()
    {
        var rule = GetRule();
        var result = rule.Evaluate(Ctx(model: null), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    [Fact]
    public void Evaluate_EmptyNormalizedFullText_ReturnsInsufficientData()
    {
        var rule = GetRule();
        var model = ModelWith(sections: [], normalizedFullText: "");

        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
    }

    // -----------------------------------------------------------------------
    // New primary path: detected-sections lookup (§11) — text-layer source
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Section11PresentViaTextLayer_ReturnsPass_WithoutConsultingRawText()
    {
        var rule = GetRule();
        var sections = SectionsWithSection11(Section11(isPresent: true, source: SectionDetectionSource.TextLayer));
        // NormalizedFullText deliberately does NOT contain the heading — proves the rule used
        // the detected-sections path, not the raw-text fallback.
        var model = ModelWith(sections: sections, normalizedFullText: "SOME UNRELATED DOCUMENT TEXT");

        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    [Fact]
    public void Evaluate_Section11PresentViaOcrEscalation_ReturnsPass()
    {
        var rule = GetRule();
        var sections = SectionsWithSection11(Section11(isPresent: true, source: SectionDetectionSource.Ocr));
        var model = ModelWith(sections: sections, normalizedFullText: "SOME UNRELATED DOCUMENT TEXT");

        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    [Fact]
    public void Evaluate_Section11AbsentInDetectedSections_ReturnsFail_EvenIfRawTextWouldMatch()
    {
        var rule = GetRule();
        var sections = SectionsWithSection11(Section11(isPresent: false, status: SectionDetectionStatus.Absent));
        // Raw text DOES contain the heading (e.g. a stray reference elsewhere) — the
        // detected-sections path must win over the raw-text fallback per the RC1.S6 rework.
        var model = ModelWith(sections: sections, normalizedFullText: "COMPARA TU TARJETA");

        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
    }

    // -----------------------------------------------------------------------
    // Fallback path — Sections empty (pre-Story-10.1) or §11 Indeterminate
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_SectionsEmpty_FallsBackToRawTextSearch_Pass()
    {
        var rule = GetRule();
        var model = ModelWith(sections: [], normalizedFullText: "ESTADO DE CUENTA ... COMPARA TU TARJETA ...");

        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    [Fact]
    public void Evaluate_SectionsEmpty_FallsBackToRawTextSearch_Fail()
    {
        var rule = GetRule();
        var model = ModelWith(sections: [], normalizedFullText: "ESTADO DE CUENTA SIN LA SECCION");

        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Fail);
    }

    [Fact]
    public void Evaluate_Section11Indeterminate_FallsBackToRawTextSearch()
    {
        var rule = GetRule();
        var sections = SectionsWithSection11(Section11(
            isPresent: false,
            status: SectionDetectionStatus.Indeterminate));
        var model = ModelWith(sections: sections, normalizedFullText: "COMPARA TU TARJETA");

        var result = rule.Evaluate(Ctx(model), TestContext.Current.CancellationToken);

        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass,
            "Indeterminate §11 status must defer to the raw-text fallback, not report Fail from an unreliable detection.");
    }
}
