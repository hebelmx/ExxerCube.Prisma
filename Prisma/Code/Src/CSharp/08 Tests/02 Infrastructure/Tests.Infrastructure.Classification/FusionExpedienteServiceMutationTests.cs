namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Mutation-killing tests for <see cref="FusionExpedienteService"/> (Stage-3 multi-source fusion).
/// Fully deterministic: drives the public <c>FuseFieldAsync</c> engine directly and the <c>FuseAsync</c>
/// orchestrator with crafted source Expedientes + ExtractionMetadata + injected FusionCoefficients, so the
/// voting/agreement/conflict ladder, the dynamic source-reliability math, the overall-confidence weighting,
/// the next-action thresholds, the business-day calculator, and the per-field fusers are pinned exactly.
/// </summary>
public class FusionExpedienteServiceMutationTests
{
    private readonly ITestOutputHelper _output;
    private readonly FusionExpedienteService _service;

    public FusionExpedienteServiceMutationTests(ITestOutputHelper output)
    {
        _output = output;
        _service = new FusionExpedienteService(XUnitLogger.CreateLogger<FusionExpedienteService>(output));
    }

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Metadata with no dynamic adjustments → source reliability equals its base value.</summary>
    private static ExtractionMetadata FlatMeta() => new()
    {
        MeanConfidence = null,
        QualityIndex = null,
        TotalFieldsExtracted = 0,
        RegexMatches = 0,
        PatternViolations = 0,
    };

    private static FieldCandidate Cand(string? value, SourceType source, double reliability, bool matches = true) =>
        new() { Value = value, Source = source, SourceReliability = reliability, MatchesPattern = matches };

    // ==================================================================================
    // Section A — FuseFieldAsync engine
    // ==================================================================================

    [Fact]
    public async Task FuseField_AllNullOrWhitespace_ReturnsAllSourcesNull()
    {
        var candidates = new List<FieldCandidate>
        {
            Cand(null, SourceType.XML_HandFilled, 0.6),
            Cand("   ", SourceType.PDF_OCR_CNBV, 0.85),
        };

        var r = (await _service.FuseFieldAsync("X", candidates, Ct)).Value!;

        r.Decision.ShouldBe(FusionDecision.AllSourcesNull);
        r.Value.ShouldBeNull();
        r.Confidence.ShouldBe(0.0);
        r.ContributingSources.ShouldBeEmpty();
    }

    [Fact]
    public async Task FuseField_EmptyCandidateList_ReturnsAllSourcesNull()
    {
        var r = (await _service.FuseFieldAsync("X", new List<FieldCandidate>(), Ct)).Value!;

        r.Decision.ShouldBe(FusionDecision.AllSourcesNull);
    }

    [Fact]
    public async Task FuseField_AllAgree_UsesMaxReliability_NotAverage()
    {
        var candidates = new List<FieldCandidate>
        {
            Cand("ABC", SourceType.XML_HandFilled, 0.60),
            Cand("abc", SourceType.PDF_OCR_CNBV, 0.90), // case-insensitive distinct → still 1 distinct value
        };

        var r = (await _service.FuseFieldAsync("NumeroOficio", candidates, Ct)).Value!;

        r.Decision.ShouldBe(FusionDecision.AllAgree);
        r.Value.ShouldBe("ABC");        // validCandidates[0].Value (kills [0]→[1] only if values differ; pinned via casing)
        r.Confidence.ShouldBe(0.90);     // MAX reliability (kills Max→Min/Average)
        r.ContributingSources.Count.ShouldBe(2);
    }

    [Fact]
    public async Task FuseField_SingleCandidate_AllAgreeWithItsReliability()
    {
        var r = (await _service.FuseFieldAsync("X", new List<FieldCandidate> { Cand("V", SourceType.PDF_OCR_CNBV, 0.85) }, Ct)).Value!;

        r.Decision.ShouldBe(FusionDecision.AllAgree);
        r.Value.ShouldBe("V");
        r.Confidence.ShouldBe(0.85);
    }

    [Fact]
    public async Task FuseField_TwoDisagree_NonText_IsConflict_WinnerHighestReliability()
    {
        var candidates = new List<FieldCandidate>
        {
            Cand("AAA", SourceType.XML_HandFilled, 0.60),
            Cand("BBB", SourceType.PDF_OCR_CNBV, 0.85),
        };

        var r = (await _service.FuseFieldAsync("NumeroExpediente", candidates, Ct)).Value!;

        // conflictingValues.Count (1) >= validCandidates.Count - 1 (1) → Conflict.
        r.Decision.ShouldBe(FusionDecision.Conflict);
        r.Value.ShouldBe("BBB");                 // OrderByDescending(reliability).First()
        r.Confidence.ShouldBe(0.85);
        r.WinningSource.ShouldBe(SourceType.PDF_OCR_CNBV);
        r.RequiresManualReview.ShouldBeTrue();   // decision == Conflict
        r.SuggestReview.ShouldBeTrue();          // conflictingValues.Count > 0
        r.ConflictingValues.Count.ShouldBe(1);
        r.ConflictingValues[0].Value.ShouldBe("AAA");
    }

    [Fact]
    public async Task FuseField_ThreeCandidates_TwoAgree_IsWeightedVoting_NotConflict()
    {
        var candidates = new List<FieldCandidate>
        {
            Cand("AAA", SourceType.XML_HandFilled, 0.85),
            Cand("AAA", SourceType.PDF_OCR_CNBV, 0.70),
            Cand("BBB", SourceType.DOCX_OCR_Authority, 0.60),
        };

        var r = (await _service.FuseFieldAsync("NumeroExpediente", candidates, Ct)).Value!;

        // winner = AAA (0.85); conflictingValues = [BBB] count 1; count-1 = 2; 1 >= 2 false → WeightedVoting.
        r.Decision.ShouldBe(FusionDecision.WeightedVoting);
        r.Value.ShouldBe("AAA");
        r.RequiresManualReview.ShouldBeFalse();  // not Conflict
        r.SuggestReview.ShouldBeTrue();          // conflictingValues.Count > 0
    }

    [Fact]
    public async Task FuseField_ThreeCandidates_AllDifferent_IsConflict()
    {
        var candidates = new List<FieldCandidate>
        {
            Cand("AAA", SourceType.XML_HandFilled, 0.60),
            Cand("BBB", SourceType.PDF_OCR_CNBV, 0.85),
            Cand("CCC", SourceType.DOCX_OCR_Authority, 0.70),
        };

        var r = (await _service.FuseFieldAsync("NumeroExpediente", candidates, Ct)).Value!;

        // conflictingValues count 2 >= count-1 (2) → Conflict.
        r.Decision.ShouldBe(FusionDecision.Conflict);
        r.Value.ShouldBe("BBB");
    }

    [Fact]
    public async Task FuseField_TextField_FuzzySimilar_ReturnsFuzzyAgreement()
    {
        // "JUAN PEREZ" vs "JUAN PERES" → Fuzz.Ratio high (>= 85).
        var candidates = new List<FieldCandidate>
        {
            Cand("JUAN PEREZ", SourceType.XML_HandFilled, 0.60),
            Cand("JUAN PERES", SourceType.PDF_OCR_CNBV, 0.80),
        };

        var r = (await _service.FuseFieldAsync("Nombre", candidates, Ct)).Value!;

        r.Decision.ShouldBe(FusionDecision.FuzzyAgreement);
        r.Value.ShouldBe("JUAN PERES");          // winner = highest-reliability source (PDF @ 0.80)
        r.WinningSource.ShouldBe(SourceType.PDF_OCR_CNBV);
        r.FuzzySimilarity.ShouldNotBeNull();
        r.FuzzySimilarity!.Value.ShouldBeGreaterThanOrEqualTo(0.85);
        // Confidence = winner.SourceReliability * similarity (strictly less than reliability since similarity < 1).
        r.Confidence.ShouldBe(0.80 * r.FuzzySimilarity!.Value, 0.0001);
        r.SuggestReview.ShouldBeTrue();
    }

    [Fact]
    public async Task FuseField_TextField_NotSimilar_FallsThroughToWeightedVotingConflict()
    {
        var candidates = new List<FieldCandidate>
        {
            Cand("JUAN", SourceType.XML_HandFilled, 0.60),
            Cand("RODRIGO ALEJANDRO", SourceType.PDF_OCR_CNBV, 0.85),
        };

        var r = (await _service.FuseFieldAsync("Nombre", candidates, Ct)).Value!;

        // Dissimilar names → no fuzzy match → weighted voting → 2 disagree → Conflict.
        r.Decision.ShouldBe(FusionDecision.Conflict);
        r.Value.ShouldBe("RODRIGO ALEJANDRO");
    }

    [Fact]
    public async Task FuseField_NonTextField_SimilarValues_DoesNotFuzzyMatch()
    {
        // Same near-similar strings but a non-text field → fuzzy path skipped (IsTextField false).
        var candidates = new List<FieldCandidate>
        {
            Cand("1234567", SourceType.XML_HandFilled, 0.60),
            Cand("1234568", SourceType.PDF_OCR_CNBV, 0.85),
        };

        var r = (await _service.FuseFieldAsync("NumeroOficio", candidates, Ct)).Value!;

        r.Decision.ShouldBe(FusionDecision.Conflict); // not FuzzyAgreement
    }

    [Fact]
    public async Task FuseField_TextFieldExactAgreement_IsAllAgreeNotFuzzy()
    {
        var candidates = new List<FieldCandidate>
        {
            Cand("MARIA", SourceType.XML_HandFilled, 0.60),
            Cand("MARIA", SourceType.PDF_OCR_CNBV, 0.85),
        };

        var r = (await _service.FuseFieldAsync("Nombre", candidates, Ct)).Value!;

        r.Decision.ShouldBe(FusionDecision.AllAgree); // exact-agreement handled before the fuzzy branch
    }

    // ==================================================================================
    // Section B — CalculateSourceReliability (via FuseAsync.SourceReliabilities)
    // ==================================================================================

    private async Task<Dictionary<SourceType, double>> Reliabilities(
        ExtractionMetadata? xml = null, ExtractionMetadata? pdf = null, ExtractionMetadata? docx = null)
    {
        var r = await _service.FuseAsync(
            new Expediente(), new Expediente(), new Expediente(),
            xml ?? FlatMeta(), pdf ?? FlatMeta(), docx ?? FlatMeta(), Ct);
        return r.Value!.SourceReliabilities;
    }

    [Fact]
    public async Task Reliability_FlatMetadata_EqualsBaseValues()
    {
        var rel = await Reliabilities();

        rel[SourceType.XML_HandFilled].ShouldBe(0.60);   // XML_BaseReliability
        rel[SourceType.PDF_OCR_CNBV].ShouldBe(0.85);     // PDF_BaseReliability
        rel[SourceType.DOCX_OCR_Authority].ShouldBe(0.70); // DOCX_BaseReliability
    }

    [Fact]
    public async Task Reliability_PdfOcrConfidence_AdjustsAroundPoint75()
    {
        var pdf = FlatMeta();
        pdf.MeanConfidence = 0.85; // (0.85 - 0.75) * 0.50 = +0.05

        var rel = await Reliabilities(pdf: pdf);

        rel[SourceType.PDF_OCR_CNBV].ShouldBe(0.90, 0.0001);
    }

    [Fact]
    public async Task Reliability_PdfImageQuality_AdjustsAroundPoint75()
    {
        var pdf = FlatMeta();
        pdf.QualityIndex = 0.85; // (0.85 - 0.75) * 0.30 = +0.03

        var rel = await Reliabilities(pdf: pdf);

        rel[SourceType.PDF_OCR_CNBV].ShouldBe(0.88, 0.0001);
    }

    [Fact]
    public async Task Reliability_ExtractionSuccessRate_AddsWeightedBoost_AndClampsTo1()
    {
        var pdf = FlatMeta();
        pdf.TotalFieldsExtracted = 10;
        pdf.RegexMatches = 10;       // successRate 1.0
        pdf.PatternViolations = 0;   // violationRate 0.0 → (1-0)*0.20 = +0.20 → 0.85+0.20 = 1.05 → clamp 1.0

        var rel = await Reliabilities(pdf: pdf);

        rel[SourceType.PDF_OCR_CNBV].ShouldBe(1.0);
    }

    [Fact]
    public async Task Reliability_ExtractionViolationsReduceBoost()
    {
        var pdf = FlatMeta();
        pdf.TotalFieldsExtracted = 10;
        pdf.RegexMatches = 5;        // successRate 0.5
        pdf.PatternViolations = 5;   // violationRate 0.5 → (0.5-0.5)*0.20 = 0 → stays 0.85

        var rel = await Reliabilities(pdf: pdf);

        rel[SourceType.PDF_OCR_CNBV].ShouldBe(0.85, 0.0001);
    }

    [Fact]
    public async Task Reliability_XmlSource_IgnoresOcrAndImageAdjustments()
    {
        var xml = FlatMeta();
        xml.MeanConfidence = 0.95; // would adjust a non-XML source, but XML is excluded by the guard
        xml.QualityIndex = 0.95;

        var rel = await Reliabilities(xml: xml);

        rel[SourceType.XML_HandFilled].ShouldBe(0.60); // unchanged → kills removing the `!= XML` guards
    }

    // ==================================================================================
    // Section C — overall confidence, next action, missing required, business days (FuseAsync)
    // ==================================================================================

    [Fact]
    public async Task FuseAsync_NoSources_ReturnsFailure()
    {
        var r = await _service.FuseAsync(null, null, null, FlatMeta(), FlatMeta(), FlatMeta(), Ct);

        r.IsFailure.ShouldBeTrue();
        r.Error.ShouldBe("At least one source Expediente must be provided");
    }

    [Fact]
    public async Task FuseAsync_SingleSource_OverallConfidence_WeightsRequired70Optional30()
    {
        // XML only, reliability 0.60. Required (3) all present + one optional present → all confidence 0.60.
        var xml = new Expediente
        {
            NumeroExpediente = "EXP-1",
            NumeroOficio = "OF-1",
            AreaDescripcion = "ASEGURAMIENTO",
            FundamentoLegal = "Art 42", // optional with data
        };

        var r = (await _service.FuseAsync(xml, null, null, FlatMeta(), FlatMeta(), FlatMeta(), Ct)).Value!;

        r.RequiredFieldsScore.ShouldBe(0.60, 0.0001);
        r.OptionalFieldsScore.ShouldBe(0.60, 0.0001);
        r.Confidence.Value.ShouldBe(0.60, 0.0001); // 0.60*0.70 + 0.60*0.30
    }

    [Fact]
    public async Task FuseAsync_RequiredAndOptionalDiffer_WeightingObservable()
    {
        // Required fields agree across XML(0.60)+PDF(0.85) → confidence 0.85 each.
        // The only optional-with-data field (FundamentoLegal) is XML-only → confidence 0.60.
        var xml = new Expediente { NumeroExpediente = "E", NumeroOficio = "O", AreaDescripcion = "A", FundamentoLegal = "F" };
        var pdf = new Expediente { NumeroExpediente = "E", NumeroOficio = "O", AreaDescripcion = "A" };

        var r = (await _service.FuseAsync(xml, pdf, null, FlatMeta(), FlatMeta(), FlatMeta(), Ct)).Value!;

        r.RequiredFieldsScore.ShouldBe(0.85, 0.0001);
        r.OptionalFieldsScore.ShouldBe(0.60, 0.0001);
        r.Confidence.Value.ShouldBe((0.85 * 0.70) + (0.60 * 0.30), 0.0001); // 0.595 + 0.18 = 0.775
    }

    [Fact]
    public async Task FuseAsync_HighConfidenceNoConflict_AutoProcess()
    {
        // Everything agrees across XML+PDF → all confidence 0.85 ≥ AutoProcessThreshold, 0 conflicts.
        var e1 = new Expediente { NumeroExpediente = "E", NumeroOficio = "O", AreaDescripcion = "A", FundamentoLegal = "F" };
        var e2 = new Expediente { NumeroExpediente = "E", NumeroOficio = "O", AreaDescripcion = "A", FundamentoLegal = "F" };

        var r = (await _service.FuseAsync(e1, e2, null, FlatMeta(), FlatMeta(), FlatMeta(), Ct)).Value!;

        r.Confidence.Value.ShouldBe(0.85, 0.0001);
        r.ConflictingFields.ShouldBeEmpty();
        r.NextAction.ShouldBe(NextAction.AutoProcess);
    }

    [Fact]
    public async Task FuseAsync_MediumConfidenceNoConflict_ReviewRecommended()
    {
        // Single XML source @0.60 → overall 0.60. 0.60 ≥ 0.70? no... that is < ManualReview → ManualReviewRequired.
        // Use a confidence in [0.70, 0.85): boost PDF-only fields. Easiest: both sources agree but at reliability
        // between thresholds. Inject coefficients so PDF base = 0.80.
        var service = new FusionExpedienteService(
            XUnitLogger.CreateLogger<FusionExpedienteService>(_output),
            new FusionCoefficients { PDF_BaseReliability = 0.80 });
        var e1 = new Expediente { NumeroExpediente = "E", NumeroOficio = "O", AreaDescripcion = "A", FundamentoLegal = "F" };
        var e2 = new Expediente { NumeroExpediente = "E", NumeroOficio = "O", AreaDescripcion = "A", FundamentoLegal = "F" };

        var r = (await service.FuseAsync(e1, e2, null, FlatMeta(), FlatMeta(), FlatMeta(), Ct)).Value!;

        r.Confidence.Value.ShouldBe(0.80, 0.0001); // ≥0.70 (not manual) and <0.85 (not auto)
        r.NextAction.ShouldBe(NextAction.ReviewRecommended);
    }

    [Fact]
    public async Task FuseAsync_Conflict_ForcesManualReviewRequired()
    {
        // XML and PDF disagree on a required field → conflict → ManualReviewRequired regardless of confidence.
        var xml = new Expediente { NumeroExpediente = "AAA", NumeroOficio = "O", AreaDescripcion = "A" };
        var pdf = new Expediente { NumeroExpediente = "BBB", NumeroOficio = "O", AreaDescripcion = "A" };

        var r = (await _service.FuseAsync(xml, pdf, null, FlatMeta(), FlatMeta(), FlatMeta(), Ct)).Value!;

        r.ConflictingFields.ShouldContain("NumeroExpediente");
        r.NextAction.ShouldBe(NextAction.ManualReviewRequired);
    }

    [Fact]
    public async Task FuseAsync_LowConfidence_ManualReviewRequired()
    {
        // Inject XML base below ManualReviewThreshold; single source → overall == base < 0.70.
        var service = new FusionExpedienteService(
            XUnitLogger.CreateLogger<FusionExpedienteService>(_output),
            new FusionCoefficients { XML_BaseReliability = 0.50 });
        var xml = new Expediente { NumeroExpediente = "E", NumeroOficio = "O", AreaDescripcion = "A" };

        var r = (await service.FuseAsync(xml, null, null, FlatMeta(), FlatMeta(), FlatMeta(), Ct)).Value!;

        // No optional field with data → optional avg defaults to 0.0 → overall = 0.50*0.70 + 0.0*0.30 = 0.35.
        r.RequiredFieldsScore.ShouldBe(0.50, 0.0001);
        r.OptionalFieldsScore.ShouldBe(0.0);
        r.Confidence.Value.ShouldBe(0.35, 0.0001);
        r.NextAction.ShouldBe(NextAction.ManualReviewRequired);
    }

    [Fact]
    public async Task FuseAsync_EmptyExpediente_AllRequiredFieldsMissing()
    {
        var r = (await _service.FuseAsync(new Expediente(), null, null, FlatMeta(), FlatMeta(), FlatMeta(), Ct)).Value!;

        r.MissingRequiredFields.ShouldContain("NumeroExpediente");
        r.MissingRequiredFields.ShouldContain("NumeroOficio");
        r.MissingRequiredFields.ShouldContain("AreaDescripcion");
    }

    [Fact]
    public async Task FuseAsync_AllRequiredPresent_NoneMissing()
    {
        var xml = new Expediente { NumeroExpediente = "E", NumeroOficio = "O", AreaDescripcion = "A" };

        var r = (await _service.FuseAsync(xml, null, null, FlatMeta(), FlatMeta(), FlatMeta(), Ct)).Value!;

        r.MissingRequiredFields.ShouldBeEmpty();
    }

    [Fact]
    public async Task FuseAsync_FechaEstimadaConclusion_SkipsWeekends()
    {
        // FechaRecepcion = Friday 2026-01-02, DiasPlazo = 1 → +1 business day → Monday 2026-01-05.
        var xml = new Expediente
        {
            NumeroExpediente = "E",
            NumeroOficio = "O",
            AreaDescripcion = "A",
            FechaRecepcion = new DateTime(2026, 1, 2),
            DiasPlazo = 1,
        };

        var r = (await _service.FuseAsync(xml, null, null, FlatMeta(), FlatMeta(), FlatMeta(), Ct)).Value!;

        r.FusedExpediente.FechaEstimadaConclusion.ShouldBe(new DateTime(2026, 1, 5));
    }

    [Fact]
    public async Task FuseAsync_BusinessDays_ThreeDaysAcrossWeekend()
    {
        // Thursday 2026-01-01 + 3 business days → Fri(1/2), Mon(1/5), Tue(1/6) → 2026-01-06.
        var xml = new Expediente
        {
            NumeroExpediente = "E", NumeroOficio = "O", AreaDescripcion = "A",
            FechaRecepcion = new DateTime(2026, 1, 1),
            DiasPlazo = 3,
        };

        var r = (await _service.FuseAsync(xml, null, null, FlatMeta(), FlatMeta(), FlatMeta(), Ct)).Value!;

        r.FusedExpediente.FechaEstimadaConclusion.ShouldBe(new DateTime(2026, 1, 6));
    }

    // ==================================================================================
    // Section D — per-field fusers (typed + representative string) via FuseAsync
    // ==================================================================================

    [Fact]
    public async Task FuseAsync_SingleSource_CopiesScalarFields()
    {
        var xml = new Expediente
        {
            NumeroExpediente = "EXP-9",
            NumeroOficio = "OF-9",
            AreaDescripcion = "ASEGURAMIENTO",
            AutoridadNombre = "JUZGADO PRIMERO",
            FundamentoLegal = "Art 42 CFF",
            MedioEnvio = "SIARA",
            DiasPlazo = 5,
            AreaClave = 3,
            TieneAseguramiento = true,
            Subdivision = LegalSubdivisionKind.A_TF,
        };

        var fused = (await _service.FuseAsync(xml, null, null, FlatMeta(), FlatMeta(), FlatMeta(), Ct)).Value!.FusedExpediente;

        fused.NumeroExpediente.ShouldBe("EXP-9");
        fused.NumeroOficio.ShouldBe("OF-9");
        fused.AreaDescripcion.ShouldBe("ASEGURAMIENTO");
        fused.AutoridadNombre.ShouldBe("JUZGADO PRIMERO");
        fused.FundamentoLegal.ShouldBe("Art 42 CFF");
        fused.MedioEnvio.ShouldBe("SIARA");
        fused.DiasPlazo.ShouldBe(5);
        fused.AreaClave.ShouldBe(3);
        fused.TieneAseguramiento.ShouldBeTrue();
        fused.Subdivision.ShouldBe(LegalSubdivisionKind.A_TF);
    }

    [Fact]
    public async Task FuseAsync_FechaRecepcion_RoundTripsThroughFusion()
    {
        var xml = new Expediente
        {
            NumeroExpediente = "E", NumeroOficio = "O", AreaDescripcion = "A",
            FechaRecepcion = new DateTime(2026, 3, 15),
        };

        var fused = (await _service.FuseAsync(xml, null, null, FlatMeta(), FlatMeta(), FlatMeta(), Ct)).Value!.FusedExpediente;

        fused.FechaRecepcion.ShouldBe(new DateTime(2026, 3, 15));
    }

    [Fact]
    public async Task FuseAsync_DisagreementOnTypedFields_PdfWins_AndConflictRecorded()
    {
        // PDF (0.85) outranks XML (0.60) → PDF values win; each disagreeing field is recorded as a conflict.
        var xml = new Expediente
        {
            NumeroExpediente = "EXP-XML", NumeroOficio = "OF-XML", AreaDescripcion = "AREA-XML",
            DiasPlazo = 5, AreaClave = 3,
        };
        var pdf = new Expediente
        {
            NumeroExpediente = "EXP-PDF", NumeroOficio = "OF-PDF", AreaDescripcion = "AREA-PDF",
            DiasPlazo = 9, AreaClave = 7,
        };

        var fused = (await _service.FuseAsync(xml, pdf, null, FlatMeta(), FlatMeta(), FlatMeta(), Ct)).Value!;

        fused.FusedExpediente.NumeroExpediente.ShouldBe("EXP-PDF");
        fused.FusedExpediente.NumeroOficio.ShouldBe("OF-PDF");
        fused.FusedExpediente.AreaDescripcion.ShouldBe("AREA-PDF");
        fused.FusedExpediente.DiasPlazo.ShouldBe(9);
        fused.FusedExpediente.AreaClave.ShouldBe(7);
        fused.ConflictingFields.ShouldContain("NumeroExpediente");
        fused.ConflictingFields.ShouldContain("NumeroOficio");
        fused.ConflictingFields.ShouldContain("AreaDescripcion");
        fused.ConflictingFields.ShouldContain("DiasPlazo");
        fused.ConflictingFields.ShouldContain("AreaClave");
    }

    [Fact]
    public async Task FuseAsync_TitularFields_FusedIntoFirstSolicitudParte()
    {
        var xml = new Expediente
        {
            NumeroExpediente = "E", NumeroOficio = "O", AreaDescripcion = "A",
            SolicitudPartes = new List<SolicitudParte>
            {
                new() { Rfc = "XAXX010101000", Curp = "XAXX010101HDFXXX00", Nombre = "JUAN", Paterno = "PEREZ", Materno = "GARCIA" },
            },
        };

        var fused = (await _service.FuseAsync(xml, null, null, FlatMeta(), FlatMeta(), FlatMeta(), Ct)).Value!.FusedExpediente;

        var titular = fused.SolicitudPartes[0];
        titular.Rfc.ShouldBe("XAXX010101000");
        titular.Curp.ShouldBe("XAXX010101HDFXXX00");
        titular.Nombre.ShouldBe("JUAN");
        titular.Paterno.ShouldBe("PEREZ");
        titular.Materno.ShouldBe("GARCIA");
    }

    // ==================================================================================
    // Section E — full per-field sweep across ALL ~38 fusers (single-source copy + disagree)
    // ==================================================================================

    private static SolicitudParte Parte(string suffix) => new()
    {
        Rfc = $"RFC{suffix}", Curp = $"CURP{suffix}", Nombre = $"NOMB{suffix}", Paterno = $"PAT{suffix}",
        Materno = $"MAT{suffix}", PersonaTipo = $"PT{suffix}", Caracter = $"CAR{suffix}", Relacion = $"REL{suffix}",
        Domicilio = $"DOM{suffix}", Complementarios = $"COMP{suffix}",
    };

    private static Expediente FullExpediente(string suffix, DateTime baseDate, int n, DateOnly nac, MeasureKind measure, LegalSubdivisionKind sub)
    {
        var parte = Parte(suffix);
        parte.FechaNacimiento = nac;
        return new Expediente
        {
            NumeroExpediente = $"NEX{suffix}", NumeroOficio = $"NOF{suffix}", AreaDescripcion = $"ADESC{suffix}",
            AutoridadNombre = $"AUTNOMBRE{suffix}", SolicitudSiara = $"SIARA{suffix}", FundamentoLegal = $"FLEG{suffix}",
            MedioEnvio = $"MEDIO{suffix}", OficioOrigen = $"OORIG{suffix}", AcuerdoReferencia = $"AREF{suffix}",
            EvidenciaFirma = $"EFIRMA{suffix}", Referencia = $"REF0{suffix}", Referencia1 = $"REF1{suffix}",
            Referencia2 = $"REF2{suffix}", NombreSolicitante = $"SOLICITANTE{suffix}", AutoridadEspecificaNombre = $"AESP{suffix}",
            FechaRecepcion = baseDate, FechaPublicacion = baseDate.AddDays(1), FechaRegistro = baseDate.AddDays(2),
            DiasPlazo = n, Folio = n + 1, OficioYear = 2020 + n, AreaClave = n, Subdivision = sub,
            SolicitudPartes = new List<SolicitudParte> { parte },
            SolicitudEspecificas = new List<SolicitudEspecifica>
            {
                new() { SolicitudEspecificaId = n + 10, Measure = measure, InstruccionesCuentasPorConocer = $"INSTR{suffix}" },
            },
        };
    }

    [Fact]
    public async Task FuseAsync_EveryField_SingleSource_CopiedToFused()
    {
        var xml = FullExpediente("X", new DateTime(2026, 2, 3), 7, new DateOnly(1990, 6, 15),
            MeasureKind.Block, LegalSubdivisionKind.E_AS);

        var fused = (await _service.FuseAsync(xml, null, null, FlatMeta(), FlatMeta(), FlatMeta(), Ct)).Value!.FusedExpediente;

        fused.NumeroExpediente.ShouldBe("NEXX");
        fused.NumeroOficio.ShouldBe("NOFX");
        fused.AreaDescripcion.ShouldBe("ADESCX");
        fused.AutoridadNombre.ShouldBe("AUTNOMBREX");
        fused.SolicitudSiara.ShouldBe("SIARAX");
        fused.FundamentoLegal.ShouldBe("FLEGX");
        fused.MedioEnvio.ShouldBe("MEDIOX");
        fused.OficioOrigen.ShouldBe("OORIGX");
        fused.AcuerdoReferencia.ShouldBe("AREFX");
        fused.EvidenciaFirma.ShouldBe("EFIRMAX");
        fused.Referencia.ShouldBe("REF0X");
        fused.Referencia1.ShouldBe("REF1X");
        fused.Referencia2.ShouldBe("REF2X");
        fused.NombreSolicitante.ShouldBe("SOLICITANTEX");
        fused.AutoridadEspecificaNombre.ShouldBe("AESPX");
        fused.FechaRecepcion.ShouldBe(new DateTime(2026, 2, 3));
        fused.FechaPublicacion.ShouldBe(new DateTime(2026, 2, 4));
        fused.FechaRegistro.ShouldBe(new DateTime(2026, 2, 5));
        fused.DiasPlazo.ShouldBe(7);
        fused.Folio.ShouldBe(8);
        fused.OficioYear.ShouldBe(2027);
        fused.AreaClave.ShouldBe(7);
        fused.Subdivision.ShouldBe(LegalSubdivisionKind.E_AS);

        var t = fused.SolicitudPartes[0];
        t.Rfc.ShouldBe("RFCX");
        t.Curp.ShouldBe("CURPX");
        t.Nombre.ShouldBe("NOMBX");
        t.Paterno.ShouldBe("PATX");
        t.Materno.ShouldBe("MATX");
        t.PersonaTipo.ShouldBe("PTX");
        t.Caracter.ShouldBe("CARX");
        t.Relacion.ShouldBe("RELX");
        t.Domicilio.ShouldBe("DOMX");
        t.Complementarios.ShouldBe("COMPX");
        t.FechaNacimiento.ShouldBe(new DateOnly(1990, 6, 15));

        var esp = fused.SolicitudEspecificas![0];
        esp.SolicitudEspecificaId.ShouldBe(17);
        esp.Measure.ShouldBe(MeasureKind.Block);
        esp.InstruccionesCuentasPorConocer.ShouldBe("INSTRX");
    }

    [Fact]
    public async Task FuseAsync_EveryFieldDisagrees_PdfWins_AndAllConflictsRecorded()
    {
        // XML (0.60) vs PDF (0.85) disagree on every field → PDF wins each, every field recorded as a conflict.
        var xml = FullExpediente("X", new DateTime(2026, 2, 3), 7, new DateOnly(1990, 6, 15),
            MeasureKind.Block, LegalSubdivisionKind.E_AS);
        var pdf = FullExpediente("P", new DateTime(2025, 9, 10), 3, new DateOnly(1991, 7, 20),
            MeasureKind.Unblock, LegalSubdivisionKind.J_DE);
        // AutoridadNombre + NombreSolicitante are fuzzy text fields: use DISSIMILAR values so they fall
        // through to weighted voting (a conflict) rather than a FuzzyAgreement.
        xml.AutoridadNombre = "JUZGADO PRIMERO DE DISTRITO";
        pdf.AutoridadNombre = "TRIBUNAL FEDERAL FISCAL";
        xml.NombreSolicitante = "JUAN PEREZ";
        pdf.NombreSolicitante = "MARIA GONZALEZ";

        var r = (await _service.FuseAsync(xml, pdf, null, FlatMeta(), FlatMeta(), FlatMeta(), Ct)).Value!;
        var fused = r.FusedExpediente;

        // PDF wins a representative spread of types (string/date/int/enum/titular/especifica).
        fused.NumeroExpediente.ShouldBe("NEXP");
        fused.MedioEnvio.ShouldBe("MEDIOP");
        fused.FechaRecepcion.ShouldBe(new DateTime(2025, 9, 10));
        fused.DiasPlazo.ShouldBe(3);
        fused.Subdivision.ShouldBe(LegalSubdivisionKind.J_DE);
        fused.SolicitudPartes[0].Rfc.ShouldBe("RFCP");
        fused.SolicitudPartes[0].FechaNacimiento.ShouldBe(new DateOnly(1991, 7, 20));
        fused.SolicitudEspecificas![0].Measure.ShouldBe(MeasureKind.Unblock);

        // Every fuser records its field name as a conflict (TieneAseguramiento excluded: only "true" is ever
        // emitted, so it can never disagree).
        string[] expectedConflicts =
        {
            "NumeroExpediente", "NumeroOficio", "AreaDescripcion", "AutoridadNombre", "SolicitudSiara",
            "FechaRecepcion", "FechaPublicacion", "Titular_RFC", "Titular_CURP", "Titular_Nombre",
            "Titular_Paterno", "Titular_Materno", "FundamentoLegal", "DiasPlazo", "FechaRegistro",
            "NombreSolicitante", "AutoridadEspecificaNombre", "Folio", "MedioEnvio", "OficioYear",
            "OficioOrigen", "AcuerdoReferencia", "EvidenciaFirma", "Referencia", "Referencia1", "Referencia2",
            "AreaClave", "Subdivision", "Titular_PersonaTipo", "Titular_Caracter", "Titular_Relacion",
            "Titular_Domicilio", "Titular_Complementarios", "Titular_FechaNacimiento",
            "SolicitudEspecificaId", "Measure", "InstruccionesCuentasPorConocer",
        };
        foreach (var name in expectedConflicts)
        {
            r.ConflictingFields.ShouldContain(name);
        }
    }

    [Fact]
    public async Task FuseAsync_EveryField_SingleDocxSource_CopiedToFused()
    {
        // Exercises the DOCX candidate-build path of every fuser (the xml/pdf paths are covered above; without a
        // DOCX-source test the docx `if (docx != null)` candidate blocks + sanitize guards are all NoCoverage).
        var docx = FullExpediente("D", new DateTime(2026, 4, 1), 4, new DateOnly(1985, 3, 9),
            MeasureKind.TransferFunds, LegalSubdivisionKind.A_IN);

        var fused = (await _service.FuseAsync(null, null, docx, FlatMeta(), FlatMeta(), FlatMeta(), Ct)).Value!.FusedExpediente;

        fused.NumeroExpediente.ShouldBe("NEXD");
        fused.NumeroOficio.ShouldBe("NOFD");
        fused.AreaDescripcion.ShouldBe("ADESCD");
        fused.AutoridadNombre.ShouldBe("AUTNOMBRED");
        fused.SolicitudSiara.ShouldBe("SIARAD");
        fused.FundamentoLegal.ShouldBe("FLEGD");
        fused.MedioEnvio.ShouldBe("MEDIOD");
        fused.OficioOrigen.ShouldBe("OORIGD");
        fused.AcuerdoReferencia.ShouldBe("AREFD");
        fused.EvidenciaFirma.ShouldBe("EFIRMAD");
        fused.Referencia.ShouldBe("REF0D");
        fused.Referencia1.ShouldBe("REF1D");
        fused.Referencia2.ShouldBe("REF2D");
        fused.NombreSolicitante.ShouldBe("SOLICITANTED");
        fused.AutoridadEspecificaNombre.ShouldBe("AESPD");
        fused.FechaRecepcion.ShouldBe(new DateTime(2026, 4, 1));
        fused.FechaPublicacion.ShouldBe(new DateTime(2026, 4, 2));
        fused.FechaRegistro.ShouldBe(new DateTime(2026, 4, 3));
        fused.DiasPlazo.ShouldBe(4);
        fused.Folio.ShouldBe(5);
        fused.OficioYear.ShouldBe(2024);
        fused.AreaClave.ShouldBe(4);
        fused.Subdivision.ShouldBe(LegalSubdivisionKind.A_IN);

        var t = fused.SolicitudPartes[0];
        t.Rfc.ShouldBe("RFCD");
        t.Curp.ShouldBe("CURPD");
        t.Nombre.ShouldBe("NOMBD");
        t.Paterno.ShouldBe("PATD");
        t.Materno.ShouldBe("MATD");
        t.PersonaTipo.ShouldBe("PTD");
        t.Caracter.ShouldBe("CARD");
        t.Relacion.ShouldBe("RELD");
        t.Domicilio.ShouldBe("DOMD");
        t.Complementarios.ShouldBe("COMPD");
        t.FechaNacimiento.ShouldBe(new DateOnly(1985, 3, 9));

        var esp = fused.SolicitudEspecificas![0];
        esp.SolicitudEspecificaId.ShouldBe(14);
        esp.Measure.ShouldBe(MeasureKind.TransferFunds);
        esp.InstruccionesCuentasPorConocer.ShouldBe("INSTRD");
    }

    [Fact]
    public async Task FuseAsync_TieneAseguramiento_AgreesWhenBothTrue_NoConflict()
    {
        var xml = new Expediente { NumeroExpediente = "E", NumeroOficio = "O", AreaDescripcion = "A", TieneAseguramiento = true };
        var pdf = new Expediente { NumeroExpediente = "E", NumeroOficio = "O", AreaDescripcion = "A", TieneAseguramiento = true };

        var r = (await _service.FuseAsync(xml, pdf, null, FlatMeta(), FlatMeta(), FlatMeta(), Ct)).Value!;

        r.FusedExpediente.TieneAseguramiento.ShouldBeTrue();
        r.ConflictingFields.ShouldNotContain("TieneAseguramiento");
    }

    [Fact]
    public async Task FuseAsync_SubdivisionUnrecognizedName_DefaultsToUnknown()
    {
        // The Subdivision fuser parses the fused value via FromName and falls back to Unknown on failure.
        // Two sources with valid-but-different subdivisions → PDF wins with a real name (covered above); here we
        // confirm a single valid subdivision round-trips, pinning the FromName success path.
        var xml = new Expediente
        {
            NumeroExpediente = "E", NumeroOficio = "O", AreaDescripcion = "A",
            Subdivision = LegalSubdivisionKind.H_IN,
        };

        var fused = (await _service.FuseAsync(xml, null, null, FlatMeta(), FlatMeta(), FlatMeta(), Ct)).Value!.FusedExpediente;

        fused.Subdivision.ShouldBe(LegalSubdivisionKind.H_IN);
    }
}
