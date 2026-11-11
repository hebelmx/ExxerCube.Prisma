using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using IndQuestResults.Operations;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="IFusionExpediente"/> — the multi-source reconciliation
/// engine. Split out of the previously-conflated <c>FusionExpedienteServiceContractTests</c>
/// (a real-SUT class merely <em>named</em> "ContractTests") so the interface-generic behaviour is
/// inherited by every implementation and by the mock blueprint (ADR-005).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Behavioural-vs-implementation analysis (owner-requested).</strong> The
/// <see cref="FusionDecision"/> ladder (AllAgree / FuzzyAgreement / WeightedVoting / Conflict /
/// AllSourcesNull) and the <see cref="NextAction"/> routing are the documented interface contract —
/// any correct implementation must produce them for the corresponding agreement patterns. The exact
/// source-reliability weights and the precise confidence arithmetic are implementation detail.
/// </para>
/// <para>
/// <strong>Thresholds as first-class contract clauses.</strong> Each threshold that expresses a real
/// clause is materialised as an overridable property with a minimal-plausible default:
/// <see cref="AutoProcessThreshold"/> ("all sources agree ⇒ confidence clears the auto-process bar"),
/// <see cref="FuzzyMatchThreshold"/> ("a fuzzy agreement clears the fuzzy bar") and
/// <see cref="MinExactMatchConfidence"/> ("an exact field match yields high confidence"). The real
/// implementation overrides them with its actual <c>FusionCoefficients</c> values, pinning the exact
/// bounds (and their kill power); the blueprint uses the minimal defaults.
/// </para>
/// <para>
/// The scenario inputs (agreeing/disagreeing values, source quality) are plain data the contract
/// owns directly — there are no implementation-specific fixtures here. Cancellation tests (one per
/// method) were added in Phase 6 once the impl was fixed to honor the token (carried from the Phase 4
/// gate).
/// </para>
/// </remarks>
public abstract class FusionExpedienteContract
{
    /// <summary>Creates the implementation under test. Called once per test.</summary>
    /// <returns>The <see cref="IFusionExpediente"/> implementation to verify.</returns>
    protected abstract IFusionExpediente CreateSut();

    /// <summary>
    /// Confidence an all-sources-agree fusion must reach to be auto-processed. Minimal-plausible
    /// default 0.5; the real implementation overrides with its <c>AutoProcessThreshold</c> coefficient.
    /// </summary>
    protected virtual double AutoProcessThreshold => 0.5;

    /// <summary>
    /// Similarity a fuzzy agreement must clear. Minimal-plausible default 0.5; the real implementation
    /// overrides with its <c>FuzzyMatchThreshold</c> coefficient.
    /// </summary>
    protected virtual double FuzzyMatchThreshold => 0.5;

    /// <summary>
    /// Confidence an exact field match must reach. Minimal-plausible default 0.5; the real
    /// implementation overrides with its actual bound.
    /// </summary>
    protected virtual double MinExactMatchConfidence => 0.5;

    //
    // Contract: FuseAsync — all sources agree
    //

    /// <summary>Contract: identical data from all sources yields AllAgree + AutoProcess.</summary>
    [Fact]
    public async Task FuseAsync_AllSourcesAgreeExactly_ReturnsHighConfidenceAllAgree()
    {
        var sut = CreateSut();
        var xml = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");
        var pdf = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");
        var docx = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");

        var result = await sut.FuseAsync(
            xml, pdf, docx,
            CreateHighQualityMetadata(SourceType.XML_HandFilled, regexMatches: 5, totalFields: 15, violations: 0),
            CreateHighQualityMetadata(SourceType.PDF_OCR_CNBV, regexMatches: 3, totalFields: 3, violations: 0),
            CreateHighQualityMetadata(SourceType.DOCX_OCR_Authority, regexMatches: 3, totalFields: 3, violations: 0),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        var fusion = result.Value;

        fusion.FusedExpediente.ShouldNotBeNull();
        fusion.FusedExpediente.NumeroExpediente.ShouldBe("A/AS1-1111-222222-AAA");
        fusion.FusedExpediente.AreaDescripcion.ShouldBe("ASEGURAMIENTO");

        fusion.Confidence.Value.ShouldBeGreaterThanOrEqualTo(AutoProcessThreshold);
        fusion.NextAction.ShouldBe(NextAction.AutoProcess);
        fusion.ConflictingFields.ShouldBeEmpty();

        fusion.FieldResults["NumeroExpediente"].Decision.ShouldBe(FusionDecision.AllAgree);
        fusion.FieldResults["AreaDescripcion"].Decision.ShouldBe(FusionDecision.AllAgree);
    }

    //
    // Contract: FuseAsync — fuzzy agreement
    //

    /// <summary>Contract: accent/typo variants of a name yield FuzzyAgreement above the fuzzy bar.</summary>
    [Fact]
    public async Task FuseAsync_MinorTypoInName_ReturnsFuzzyAgreement()
    {
        var sut = CreateSut();
        var xml = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");
        xml.AutoridadNombre = "SUBDELEGACION 8 SAN ANGEL";
        var pdf = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");
        pdf.AutoridadNombre = "SUBDELEGACIÓN 8 SAN ANGEL";
        var docx = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");
        docx.AutoridadNombre = "SUBDELEGACION 8 SAN ÁNGEL";

        var result = await sut.FuseAsync(
            xml, pdf, docx,
            CreateHighQualityMetadata(SourceType.XML_HandFilled),
            CreateHighQualityMetadata(SourceType.PDF_OCR_CNBV),
            CreateHighQualityMetadata(SourceType.DOCX_OCR_Authority),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        var fusion = result.Value;

        fusion.FieldResults["AutoridadNombre"].Decision.ShouldBe(FusionDecision.FuzzyAgreement);
        fusion.FieldResults["AutoridadNombre"].FuzzySimilarity.ShouldNotBeNull();
        fusion.FieldResults["AutoridadNombre"].FuzzySimilarity!.Value.ShouldBeGreaterThanOrEqualTo(FuzzyMatchThreshold);
        fusion.FieldResults["AutoridadNombre"].Value.ShouldNotBeNullOrWhiteSpace();
    }

    //
    // Contract: FuseAsync — weighted voting
    //

    /// <summary>Contract: two agreeing sources outvote one disagreeing source.</summary>
    [Fact]
    public async Task FuseAsync_TwoSourcesAgreeOneDisagrees_ReturnsWeightedVoting()
    {
        var sut = CreateSut();
        var xml = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");
        var pdf = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");
        var docx = CreateTestExpediente("A/AS1-1111-222222-BBB", "HACENDARIO");

        var result = await sut.FuseAsync(
            xml, pdf, docx,
            CreateHighQualityMetadata(SourceType.XML_HandFilled),
            CreateHighQualityMetadata(SourceType.PDF_OCR_CNBV),
            CreateLowQualityMetadata(SourceType.DOCX_OCR_Authority),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        var fusion = result.Value;

        fusion.FusedExpediente.ShouldNotBeNull();
        fusion.FusedExpediente.NumeroExpediente.ShouldBe("A/AS1-1111-222222-AAA");
        fusion.FieldResults["NumeroExpediente"].Decision.ShouldBe(FusionDecision.WeightedVoting);
        fusion.FieldResults["NumeroExpediente"].WinningSource.ShouldNotBeNull();
        fusion.ConflictingFields.ShouldContain("NumeroExpediente");
    }

    /// <summary>Contract: a higher-quality source wins over a lower-quality disagreeing source.</summary>
    [Fact]
    public async Task FuseAsync_HighQualityPDFVsLowQualityXML_PDFWins()
    {
        var sut = CreateSut();
        var xml = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");
        var pdf = CreateTestExpediente("A/AS1-1111-222222-BBB", "HACENDARIO");
        Expediente? docx = null;

        var result = await sut.FuseAsync(
            xml, pdf, docx,
            CreateLowQualityMetadata(SourceType.XML_HandFilled),
            CreateHighQualityMetadata(SourceType.PDF_OCR_CNBV),
            CreateEmptyMetadata(SourceType.DOCX_OCR_Authority),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        var fusion = result.Value;

        fusion.FusedExpediente.ShouldNotBeNull();
        fusion.FusedExpediente.NumeroExpediente.ShouldBe("A/AS1-1111-222222-BBB");
        fusion.FieldResults["NumeroExpediente"].WinningSource.ShouldBe(SourceType.PDF_OCR_CNBV);
    }

    //
    // Contract: FuseAsync — conflict
    //

    /// <summary>Contract: three mutually-disagreeing sources yield Conflict + ManualReviewRequired.</summary>
    [Fact]
    public async Task FuseAsync_AllThreeSourcesDisagree_ReturnsConflict()
    {
        var sut = CreateSut();
        var xml = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");
        var pdf = CreateTestExpediente("A/AS1-1111-222222-BBB", "HACENDARIO");
        var docx = CreateTestExpediente("A/AS1-1111-222222-CCC", "PENAL");

        var result = await sut.FuseAsync(
            xml, pdf, docx,
            CreateHighQualityMetadata(SourceType.XML_HandFilled),
            CreateHighQualityMetadata(SourceType.PDF_OCR_CNBV),
            CreateHighQualityMetadata(SourceType.DOCX_OCR_Authority),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        var fusion = result.Value;

        fusion.FieldResults["NumeroExpediente"].Decision.ShouldBe(FusionDecision.Conflict);
        fusion.FieldResults["NumeroExpediente"].RequiresManualReview.ShouldBeTrue();
        fusion.ConflictingFields.ShouldContain("NumeroExpediente");
        fusion.NextAction.ShouldBe(NextAction.ManualReviewRequired);
    }

    //
    // Contract: FuseAsync — null handling
    //

    /// <summary>Contract: all sources null yields a failure.</summary>
    [Fact]
    public async Task FuseAsync_AllSourcesNull_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.FuseAsync(
            null, null, null,
            CreateEmptyMetadata(SourceType.XML_HandFilled),
            CreateEmptyMetadata(SourceType.PDF_OCR_CNBV),
            CreateEmptyMetadata(SourceType.DOCX_OCR_Authority),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        (result.Error ?? string.Empty).ShouldContain("At least one source");
    }

    /// <summary>Contract: a single low-reliability source still fuses but routes to manual review.</summary>
    [Fact]
    public async Task FuseAsync_TwoSourcesNullOneValid_ReturnsValidFusion()
    {
        var sut = CreateSut();
        var xml = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");

        var result = await sut.FuseAsync(
            xml, null, null,
            CreateHighQualityMetadata(SourceType.XML_HandFilled),
            CreateEmptyMetadata(SourceType.PDF_OCR_CNBV),
            CreateEmptyMetadata(SourceType.DOCX_OCR_Authority),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        var fusion = result.Value;

        fusion.FusedExpediente.ShouldNotBeNull();
        fusion.FusedExpediente.NumeroExpediente.ShouldBe("A/AS1-1111-222222-AAA");
        fusion.NextAction.ShouldBe(NextAction.ManualReviewRequired);
    }

    //
    // Contract: FuseFieldAsync
    //

    /// <summary>Contract: an exact field match across sources yields AllAgree with high confidence.</summary>
    [Fact]
    public async Task FuseFieldAsync_ExactMatch_ReturnsAllAgree()
    {
        var sut = CreateSut();
        var candidates = new List<FieldCandidate>
        {
            new() { Value = "ASEGURAMIENTO", Source = SourceType.XML_HandFilled, SourceReliability = 0.75 },
            new() { Value = "ASEGURAMIENTO", Source = SourceType.PDF_OCR_CNBV, SourceReliability = 0.90 },
            new() { Value = "ASEGURAMIENTO", Source = SourceType.DOCX_OCR_Authority, SourceReliability = 0.80 }
        };

        var result = await sut.FuseFieldAsync("AreaDescripcion", candidates, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Decision.ShouldBe(FusionDecision.AllAgree);
        result.Value.Value.ShouldBe("ASEGURAMIENTO");
        result.Value.Confidence.ShouldBeGreaterThanOrEqualTo(MinExactMatchConfidence);
    }

    /// <summary>Contract: all-null candidates yield AllSourcesNull with a null value.</summary>
    [Fact]
    public async Task FuseFieldAsync_AllNull_ReturnsAllSourcesNull()
    {
        var sut = CreateSut();
        var candidates = new List<FieldCandidate>
        {
            new() { Value = null, Source = SourceType.XML_HandFilled, SourceReliability = 0.75 },
            new() { Value = null, Source = SourceType.PDF_OCR_CNBV, SourceReliability = 0.90 },
            new() { Value = null, Source = SourceType.DOCX_OCR_Authority, SourceReliability = 0.80 }
        };

        var result = await sut.FuseFieldAsync("OptionalField", candidates, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Decision.ShouldBe(FusionDecision.AllSourcesNull);
        result.Value.Value.ShouldBeNull();
    }

    //
    // Cancellation Tests (Phase 6 — repository-wide CancellationToken mandate, ADR-005 §5)
    //

    /// <summary>Contract: a pre-cancelled token short-circuits FuseAsync to Cancelled (never a throw).</summary>
    [Fact]
    public async Task FuseAsync_WhenCancellationRequested_ReturnsCancelled()
    {
        var sut = CreateSut();
        var xml = CreateTestExpediente("A/AS1-1111-222222-AAA", "ASEGURAMIENTO");

        var result = await sut.FuseAsync(
            xml, null, null,
            CreateHighQualityMetadata(SourceType.XML_HandFilled),
            CreateEmptyMetadata(SourceType.PDF_OCR_CNBV),
            CreateEmptyMetadata(SourceType.DOCX_OCR_Authority),
            new CancellationToken(canceled: true));

        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>Contract: a pre-cancelled token short-circuits FuseFieldAsync to Cancelled.</summary>
    [Fact]
    public async Task FuseFieldAsync_WhenCancellationRequested_ReturnsCancelled()
    {
        var sut = CreateSut();
        var candidates = new List<FieldCandidate>
        {
            new() { Value = "ASEGURAMIENTO", Source = SourceType.XML_HandFilled, SourceReliability = 0.75 }
        };

        var result = await sut.FuseFieldAsync("AreaDescripcion", candidates, new CancellationToken(canceled: true));

        result.IsCancelled().ShouldBeTrue();
    }

    //
    // Shared scenario data (plain inputs the contract owns directly).
    //

    /// <summary>Builds a test Expediente with the given key fields.</summary>
    protected static Expediente CreateTestExpediente(string numeroExpediente, string areaDescripcion) => new()
    {
        NumeroExpediente = numeroExpediente,
        AreaDescripcion = areaDescripcion,
        NumeroOficio = "123/ABC/-4444444444/2025",
        SolicitudSiara = "TEST/2025/000001",
        AutoridadNombre = "TEST AUTORIDAD"
    };

    /// <summary>Builds high-quality extraction metadata for the given source.</summary>
    protected static ExtractionMetadata CreateHighQualityMetadata(
        SourceType source,
        int regexMatches = 5,
        int totalFields = 15,
        int violations = 0) => new()
    {
        Source = source,
        RegexMatches = regexMatches,
        TotalFieldsExtracted = totalFields,
        CatalogValidations = 2,
        PatternViolations = violations,
        MeanConfidence = source == SourceType.XML_HandFilled ? null : 0.85,
        MinConfidence = source == SourceType.XML_HandFilled ? null : 0.75,
        QualityIndex = source == SourceType.XML_HandFilled ? null : 0.80
    };

    /// <summary>Builds low-quality extraction metadata for the given source.</summary>
    protected static ExtractionMetadata CreateLowQualityMetadata(SourceType source) => new()
    {
        Source = source,
        RegexMatches = 1,
        TotalFieldsExtracted = 5,
        CatalogValidations = 0,
        PatternViolations = 3,
        MeanConfidence = source == SourceType.XML_HandFilled ? null : 0.55,
        MinConfidence = source == SourceType.XML_HandFilled ? null : 0.40,
        QualityIndex = source == SourceType.XML_HandFilled ? null : 0.50
    };

    /// <summary>Builds empty extraction metadata for the given source (source absent).</summary>
    protected static ExtractionMetadata CreateEmptyMetadata(SourceType source) => new()
    {
        Source = source,
        RegexMatches = 0,
        TotalFieldsExtracted = 0,
        CatalogValidations = 0,
        PatternViolations = 0
    };
}
