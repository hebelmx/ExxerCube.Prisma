using ExxerCube.Prisma.Domain.Enums;

namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Mutation-killing tests for <see cref="FieldMatchingService"/>'s deterministic surface, driven through a
/// fully mocked <see cref="IMatchingPolicy"/> and mocked extractors (the existing tests note this is preferred
/// over the concrete policy). Pins the guards, per-source extraction branches, the field-value collection
/// (origin/confidence/source), the match/conflict/missing branches, overall-agreement averaging, the
/// <c>CreateExtractedFieldsFromMatchedFields</c> switch, and <c>AggregateValidation</c>.
/// </summary>
/// <remarks>
/// Out of scope (documented dead/unreachable surface, NOT contorted into "kills"):
/// <c>DeriveSlaFromAdditional</c>'s inner branches and the persona/compliance loops in
/// <c>AggregateValidation</c> are unreachable through this method — <c>AdditionalMerged</c>, <c>Personas</c>
/// and <c>ComplianceActions</c> are never populated by <c>MatchFieldsAndGenerateUnifiedRecordAsync</c>. The
/// AdditionalFields conflict-detection is the subject of a reserved finding and is left as documented.
/// </remarks>
public class FieldMatchingServiceMutationTests
{
    private readonly IFieldExtractor<DocxSource> _docx;
    private readonly IFieldExtractor<PdfSource> _pdf;
    private readonly IFieldExtractor<XmlSource> _xml;
    private readonly IMatchingPolicy _policy;
    private readonly ILogger<FieldMatchingService> _logger;
    private readonly FieldMatchingService _service;       // with XML extractor
    private readonly FieldMatchingService _serviceNoXml;  // without XML extractor

    public FieldMatchingServiceMutationTests()
    {
        _docx = Substitute.For<IFieldExtractor<DocxSource>>();
        _pdf = Substitute.For<IFieldExtractor<PdfSource>>();
        _xml = Substitute.For<IFieldExtractor<XmlSource>>();
        _policy = Substitute.For<IMatchingPolicy>();
        _logger = Substitute.For<ILogger<FieldMatchingService>>();

        // Default: every field matched cleanly (no conflict, full agreement), echoing the field name.
        _policy.SelectBestValueAsync(Arg.Any<string>(), Arg.Any<List<FieldValue>>())
            .Returns(ci => Result<FieldMatchResult>.Success(new FieldMatchResult
            {
                FieldName = ci.ArgAt<string>(0),
                MatchedValue = "X",
                AgreementLevel = 1.0f,
                HasConflict = false,
            }));

        _service = new FieldMatchingService(_docx, _pdf, _xml, _policy, _logger);
        _serviceNoXml = new FieldMatchingService(_docx, _pdf, null, _policy, _logger);
    }

    private void DocxReturns(ExtractedFields fields) =>
        _docx.ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(fields));

    private void PdfReturns(ExtractedFields fields) =>
        _pdf.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(fields));

    private void XmlReturns(ExtractedFields fields) =>
        _xml.ExtractFieldsAsync(Arg.Any<XmlSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(fields));

    private Task<Result<UnifiedMetadataRecord>> Run(
        FieldMatchingService svc,
        DocxSource? docx = null, PdfSource? pdf = null, XmlSource? xml = null,
        FieldDefinition[]? defs = null, Expediente? expediente = null, List<string>? requiredFields = null) =>
        svc.MatchFieldsAndGenerateUnifiedRecordAsync(
            docx, pdf, xml,
            defs ?? new[] { new FieldDefinition("Expediente") },
            expediente: expediente, classification: null, requiredFields: requiredFields,
            TestContext.Current.CancellationToken);

    private static FieldDefinition[] Defs(params string[] names) =>
        names.Select(n => new FieldDefinition(n)).ToArray();

    // ============ Guards ============

    [Fact]
    public async Task NoSources_ReturnsExactFailure()
    {
        var result = await Run(_service, docx: null, pdf: null, xml: null);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("At least one source (DOCX, PDF, or XML) must be provided");
    }

    [Fact]
    public async Task NullFieldDefinitions_ReturnsExactFailure()
    {
        var result = await _service.MatchFieldsAndGenerateUnifiedRecordAsync(
            new DocxSource("d.docx"), null, null, null!, null, null, null, TestContext.Current.CancellationToken);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Field definitions cannot be null or empty");
    }

    [Fact]
    public async Task EmptyFieldDefinitions_ReturnsExactFailure()
    {
        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Array.Empty<FieldDefinition>());
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Field definitions cannot be null or empty");
    }

    [Fact]
    public async Task CancelledBeforeStart_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await _service.MatchFieldsAndGenerateUnifiedRecordAsync(
            new DocxSource("d.docx"), null, null, Defs("Expediente"), null, null, null, cts.Token);
        result.IsCancelled().ShouldBeTrue();
        await _docx.DidNotReceive().ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>());
    }

    // ============ Field-value collection: origin / confidence / source per source type ============

    [Fact]
    public async Task DocxSource_CollectsValueWithDocxOriginConfidenceOne()
    {
        DocxReturns(new ExtractedFields { Expediente = "DOCXVAL" });
        await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente"));

        await _policy.Received().SelectBestValueAsync("Expediente",
            Arg.Is<List<FieldValue>>(v => v.Count == 1
                && v[0].Value == "DOCXVAL"
                && v[0].SourceType == "DOCX"
                && v[0].Origin == FieldOrigin.Docx
                && v[0].Confidence == 1.0f));
    }

    [Fact]
    public async Task PdfSource_CollectsValueWithPdfOcrOrigin()
    {
        PdfReturns(new ExtractedFields { Expediente = "PDFVAL" });
        await Run(_service, pdf: new PdfSource("p.pdf"), defs: Defs("Expediente"));

        await _policy.Received().SelectBestValueAsync("Expediente",
            Arg.Is<List<FieldValue>>(v => v.Count == 1
                && v[0].Value == "PDFVAL"
                && v[0].SourceType == "PDF"
                && v[0].Origin == FieldOrigin.PdfOcr));
    }

    [Fact]
    public async Task XmlSource_CollectsValueWithXmlOrigin()
    {
        XmlReturns(new ExtractedFields { Expediente = "XMLVAL" });
        await Run(_service, xml: new XmlSource("x.xml"), defs: Defs("Expediente"));

        await _policy.Received().SelectBestValueAsync("Expediente",
            Arg.Is<List<FieldValue>>(v => v.Count == 1
                && v[0].Value == "XMLVAL"
                && v[0].SourceType == "XML"
                && v[0].Origin == FieldOrigin.Xml));
    }

    [Fact]
    public async Task TwoSources_BothValuesCollectedForSameField()
    {
        DocxReturns(new ExtractedFields { Expediente = "D" });
        PdfReturns(new ExtractedFields { Expediente = "P" });
        await Run(_service, docx: new DocxSource("d.docx"), pdf: new PdfSource("p.pdf"), defs: Defs("Expediente"));

        await _policy.Received().SelectBestValueAsync("Expediente",
            Arg.Is<List<FieldValue>>(v => v.Count == 2));
    }

    [Fact]
    public async Task CollectsCausaAndAccionSolicitada()
    {
        DocxReturns(new ExtractedFields { Causa = "CC", AccionSolicitada = "AA" });
        await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Causa", "AccionSolicitada"));

        await _policy.Received().SelectBestValueAsync("Causa",
            Arg.Is<List<FieldValue>>(v => v.Count == 1 && v[0].Value == "CC"));
        await _policy.Received().SelectBestValueAsync("AccionSolicitada",
            Arg.Is<List<FieldValue>>(v => v.Count == 1 && v[0].Value == "AA"));
    }

    [Fact]
    public async Task CollectsAdditionalFields()
    {
        DocxReturns(new ExtractedFields { AdditionalFields = new Dictionary<string, string?> { ["Foo"] = "bar" } });
        await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Foo"));

        await _policy.Received().SelectBestValueAsync("Foo",
            Arg.Is<List<FieldValue>>(v => v.Count == 1 && v[0].Value == "bar"));
    }

    [Fact]
    public async Task BlankCoreValues_AreNotCollected()
    {
        // Expediente whitespace, Causa empty -> neither collected -> both become missing fields.
        DocxReturns(new ExtractedFields { Expediente = "   ", Causa = "" });
        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente", "Causa"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.MatchedFields!.MissingFields.ShouldContain("Expediente");
        result.Value!.MatchedFields!.MissingFields.ShouldContain("Causa");
        await _policy.DidNotReceive().SelectBestValueAsync("Expediente", Arg.Any<List<FieldValue>>());
    }

    [Fact]
    public async Task BlankAdditionalValue_IsNotCollected()
    {
        DocxReturns(new ExtractedFields { AdditionalFields = new Dictionary<string, string?> { ["Foo"] = "  " } });
        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Foo"));

        result.Value!.MatchedFields!.MissingFields.ShouldContain("Foo");
        await _policy.DidNotReceive().SelectBestValueAsync("Foo", Arg.Any<List<FieldValue>>());
    }

    // ============ Match / conflict / missing branches ============

    [Fact]
    public async Task MatchedField_IsAddedToFieldMatchesWithPolicyValue()
    {
        DocxReturns(new ExtractedFields { Expediente = "raw" });
        _policy.SelectBestValueAsync("Expediente", Arg.Any<List<FieldValue>>())
            .Returns(Result<FieldMatchResult>.Success(new FieldMatchResult
            {
                FieldName = "Expediente",
                MatchedValue = "BEST",
                AgreementLevel = 1.0f,
                HasConflict = false,
            }));

        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente"));

        result.Value!.MatchedFields!.FieldMatches.ShouldContainKey("Expediente");
        result.Value!.MatchedFields!.FieldMatches["Expediente"].MatchedValue.ShouldBe("BEST");
        result.Value!.MatchedFields!.ConflictingFields.ShouldNotContain("Expediente");
    }

    [Fact]
    public async Task ConflictingField_IsAddedToConflictingFields()
    {
        DocxReturns(new ExtractedFields { Expediente = "raw" });
        _policy.SelectBestValueAsync("Expediente", Arg.Any<List<FieldValue>>())
            .Returns(Result<FieldMatchResult>.Success(new FieldMatchResult
            {
                FieldName = "Expediente",
                MatchedValue = "BEST",
                AgreementLevel = 0.2f,
                HasConflict = true,
            }));

        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente"));

        result.Value!.MatchedFields!.ConflictingFields.ShouldContain("Expediente");
    }

    [Fact]
    public async Task PolicyReturnsNullValue_FieldNotAddedNoConflict()
    {
        DocxReturns(new ExtractedFields { Expediente = "raw" });
        _policy.SelectBestValueAsync("Expediente", Arg.Any<List<FieldValue>>())
            .Returns(Result<FieldMatchResult>.Success(null!));

        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.MatchedFields!.FieldMatches.ShouldNotContainKey("Expediente");
    }

    [Fact]
    public async Task MissingField_IsTracked()
    {
        DocxReturns(new ExtractedFields { Expediente = "E" });
        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente", "Causa"));

        result.Value!.MatchedFields!.FieldMatches.ShouldContainKey("Expediente");
        result.Value!.MatchedFields!.MissingFields.ShouldContain("Causa");
        result.Value!.MatchedFields!.MissingFields.ShouldNotContain("Expediente");
    }

    // ============ Overall agreement averaging ============

    [Fact]
    public async Task OverallAgreement_IsAverageOfFieldAgreements()
    {
        DocxReturns(new ExtractedFields { Expediente = "E", Causa = "C" });
        _policy.SelectBestValueAsync("Expediente", Arg.Any<List<FieldValue>>())
            .Returns(Result<FieldMatchResult>.Success(new FieldMatchResult { FieldName = "Expediente", MatchedValue = "E", AgreementLevel = 0.4f }));
        _policy.SelectBestValueAsync("Causa", Arg.Any<List<FieldValue>>())
            .Returns(Result<FieldMatchResult>.Success(new FieldMatchResult { FieldName = "Causa", MatchedValue = "C", AgreementLevel = 0.8f }));

        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente", "Causa"));

        result.Value!.MatchedFields!.OverallAgreement.ShouldBe(0.6f, 0.0001f); // mean(0.4, 0.8) -> kills Min/Max/Sum/First
    }

    [Fact]
    public async Task OverallAgreement_NoMatches_StaysZero()
    {
        DocxReturns(new ExtractedFields()); // nothing collected
        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente"));

        result.Value!.MatchedFields!.FieldMatches.Count.ShouldBe(0);
        result.Value!.MatchedFields!.OverallAgreement.ShouldBe(0f);
    }

    // ============ CreateExtractedFieldsFromMatchedFields switch ============

    [Fact]
    public async Task ExtractedFields_MapsExpedienteCausaAndAccion()
    {
        DocxReturns(new ExtractedFields { Expediente = "e", Causa = "c", AccionSolicitada = "a" });
        _policy.SelectBestValueAsync("Expediente", Arg.Any<List<FieldValue>>())
            .Returns(Result<FieldMatchResult>.Success(new FieldMatchResult { FieldName = "Expediente", MatchedValue = "EXP", AgreementLevel = 1f }));
        _policy.SelectBestValueAsync("Causa", Arg.Any<List<FieldValue>>())
            .Returns(Result<FieldMatchResult>.Success(new FieldMatchResult { FieldName = "Causa", MatchedValue = "CAU", AgreementLevel = 1f }));
        _policy.SelectBestValueAsync("AccionSolicitada", Arg.Any<List<FieldValue>>())
            .Returns(Result<FieldMatchResult>.Success(new FieldMatchResult { FieldName = "AccionSolicitada", MatchedValue = "ACC", AgreementLevel = 1f }));

        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente", "Causa", "AccionSolicitada"));

        result.Value!.ExtractedFields!.Expediente.ShouldBe("EXP");
        result.Value!.ExtractedFields!.Causa.ShouldBe("CAU");
        result.Value!.ExtractedFields!.AccionSolicitada.ShouldBe("ACC");
    }

    [Fact]
    public async Task ExtractedFields_SwitchIsCaseInsensitive()
    {
        // Field name "EXPEDIENTE" upper-cased; the switch lowercases the key (ToLowerInvariant).
        DocxReturns(new ExtractedFields { AdditionalFields = new Dictionary<string, string?> { ["EXPEDIENTE"] = "x" } });
        _policy.SelectBestValueAsync("EXPEDIENTE", Arg.Any<List<FieldValue>>())
            .Returns(Result<FieldMatchResult>.Success(new FieldMatchResult { FieldName = "EXPEDIENTE", MatchedValue = "UPPER", AgreementLevel = 1f }));

        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("EXPEDIENTE"));

        result.Value!.ExtractedFields!.Expediente.ShouldBe("UPPER");
    }

    [Fact]
    public async Task ExtractedFields_AccionSolicitadaUnderscoreAlias_Maps()
    {
        DocxReturns(new ExtractedFields { AdditionalFields = new Dictionary<string, string?> { ["accion_solicitada"] = "x" } });
        _policy.SelectBestValueAsync("accion_solicitada", Arg.Any<List<FieldValue>>())
            .Returns(Result<FieldMatchResult>.Success(new FieldMatchResult { FieldName = "accion_solicitada", MatchedValue = "ALIAS", AgreementLevel = 1f }));

        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("accion_solicitada"));

        result.Value!.ExtractedFields!.AccionSolicitada.ShouldBe("ALIAS");
    }

    // ============ Per-source extraction failure / cancellation branches ============

    [Fact]
    public async Task DocxFailure_ContinuesWithPdf()
    {
        _docx.ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.WithFailure("docx boom"));
        PdfReturns(new ExtractedFields { Expediente = "P" });

        var result = await Run(_service, docx: new DocxSource("d.docx"), pdf: new PdfSource("p.pdf"), defs: Defs("Expediente"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.MatchedFields!.FieldMatches.ShouldContainKey("Expediente");
        await _policy.Received().SelectBestValueAsync("Expediente",
            Arg.Is<List<FieldValue>>(v => v.Count == 1 && v[0].Value == "P"));
    }

    [Fact]
    public async Task DocxCancelled_ReturnsCancelled()
    {
        _docx.ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(ResultExtensions.Cancelled<ExtractedFields>());

        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente"));
        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task PdfCancelled_ReturnsCancelled()
    {
        _pdf.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(ResultExtensions.Cancelled<ExtractedFields>());

        var result = await Run(_service, pdf: new PdfSource("p.pdf"), defs: Defs("Expediente"));
        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task XmlCancelled_ReturnsCancelled()
    {
        _xml.ExtractFieldsAsync(Arg.Any<XmlSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(ResultExtensions.Cancelled<ExtractedFields>());

        var result = await Run(_service, xml: new XmlSource("x.xml"), defs: Defs("Expediente"));
        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task PolicyCancelled_ReturnsCancelled()
    {
        DocxReturns(new ExtractedFields { Expediente = "E" });
        _policy.SelectBestValueAsync("Expediente", Arg.Any<List<FieldValue>>())
            .Returns(ResultExtensions.Cancelled<FieldMatchResult>());

        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente"));
        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task XmlExtractorNull_NoExtractionNoNre()
    {
        // _xmlFieldExtractor != null guard: with the no-XML service, an xmlSource must be ignored (no NRE),
        // and the docx value is still used.
        DocxReturns(new ExtractedFields { Expediente = "D" });
        var result = await _serviceNoXml.MatchFieldsAndGenerateUnifiedRecordAsync(
            new DocxSource("d.docx"), null, new XmlSource("x.xml"),
            Defs("Expediente"), null, null, null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _xml.DidNotReceive().ExtractFieldsAsync(Arg.Any<XmlSource>(), Arg.Any<FieldDefinition[]>());
        await _policy.Received().SelectBestValueAsync("Expediente",
            Arg.Is<List<FieldValue>>(v => v.Count == 1 && v[0].Value == "D"));
    }

    // ============ failure-with-value must NOT be collected (kills `IsSuccess && Value != null` -> `||`) ============

    [Fact]
    public async Task DocxFailureWithValue_IsNotCollected()
    {
        // A failure result that still carries a value must be ignored (requires BOTH IsSuccess AND non-null).
        _docx.ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.WithFailure("docx boom", new ExtractedFields { Expediente = "GHOST" }));

        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.MatchedFields!.MissingFields.ShouldContain("Expediente");
        await _policy.DidNotReceive().SelectBestValueAsync("Expediente", Arg.Any<List<FieldValue>>());
    }

    [Fact]
    public async Task PdfFailureWithValue_IsNotCollected()
    {
        _pdf.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.WithFailure("pdf boom", new ExtractedFields { Expediente = "GHOST" }));

        var result = await Run(_service, pdf: new PdfSource("p.pdf"), defs: Defs("Expediente"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.MatchedFields!.MissingFields.ShouldContain("Expediente");
    }

    [Fact]
    public async Task XmlFailureWithValue_IsNotCollected()
    {
        _xml.ExtractFieldsAsync(Arg.Any<XmlSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.WithFailure("xml boom", new ExtractedFields { Expediente = "GHOST" }));

        var result = await Run(_service, xml: new XmlSource("x.xml"), defs: Defs("Expediente"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.MatchedFields!.MissingFields.ShouldContain("Expediente");
    }

    [Fact]
    public async Task PolicyFailureWithValue_FieldNotAdded()
    {
        DocxReturns(new ExtractedFields { Expediente = "raw" });
        _policy.SelectBestValueAsync("Expediente", Arg.Any<List<FieldValue>>())
            .Returns(Result<FieldMatchResult>.WithFailure("policy boom",
                new FieldMatchResult { FieldName = "Expediente", MatchedValue = "GHOST" }));

        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.MatchedFields!.FieldMatches.ShouldNotContainKey("Expediente");
    }

    // ============ Exception path ============

    [Fact]
    public async Task ExtractorThrows_ReturnsWrappedError()
    {
        _docx.ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>())
            .Returns<Task<Result<ExtractedFields>>>(_ => throw new InvalidOperationException("boom"));

        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente"));
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Error in field matching workflow: boom");
    }

    // ============ AggregateValidation (via passed expediente) ============

    private static Expediente ValidExpediente()
    {
        var e = new Expediente
        {
            NumeroExpediente = "EXP-1",
            NumeroOficio = "OF-1",
            Subdivision = LegalSubdivisionKind.A_AS,
            FechaRecepcion = new DateTime(2026, 1, 1),
            FechaEstimadaConclusion = new DateTime(2026, 1, 10),
        };
        return e;
    }

    [Fact]
    public async Task AggregateValidation_FullExpediente_IsValid()
    {
        DocxReturns(new ExtractedFields { Expediente = "E" });
        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente"), expediente: ValidExpediente());

        result.Value!.Validation.ShouldNotBeNull();
        result.Value!.Validation!.IsValid.ShouldBeTrue();   // all four Require()s satisfied -> nothing missing
        result.Value!.Validation.Missing.Count.ShouldBe(0);
    }

    [Fact]
    public async Task AggregateValidation_NullExpediente_MarksExpedienteMissing()
    {
        DocxReturns(new ExtractedFields { Expediente = "E" });
        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente"), expediente: null);

        result.Value!.Validation!.Missing.ShouldContain("Expediente");
        result.Value!.Validation.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task AggregateValidation_BlankNumeroExpediente_MarksMissing()
    {
        var e = ValidExpediente();
        e.NumeroExpediente = "";
        DocxReturns(new ExtractedFields { Expediente = "E" });
        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente"), expediente: e);

        result.Value!.Validation!.Missing.ShouldContain("Expediente");
    }

    [Fact]
    public async Task AggregateValidation_BlankNumeroOficio_MarksMissing()
    {
        var e = ValidExpediente();
        e.NumeroOficio = "   ";
        DocxReturns(new ExtractedFields { Expediente = "E" });
        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente"), expediente: e);

        result.Value!.Validation!.Missing.ShouldContain("NumeroOficio");
        result.Value!.Validation.Missing.ShouldNotContain("Expediente"); // NumeroExpediente still set
    }

    [Fact]
    public async Task AggregateValidation_UnknownSubdivision_MarksMissing()
    {
        var e = ValidExpediente();
        e.Subdivision = LegalSubdivisionKind.Unknown;
        DocxReturns(new ExtractedFields { Expediente = "E" });
        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente"), expediente: e);

        result.Value!.Validation!.Missing.ShouldContain("Subdivision");
    }

    [Fact]
    public async Task AggregateValidation_DefaultFechaRecepcion_MarksMissing()
    {
        var e = ValidExpediente();
        e.FechaRecepcion = default;
        DocxReturns(new ExtractedFields { Expediente = "E" });
        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente"), expediente: e);

        result.Value!.Validation!.Missing.ShouldContain("FechaRecepcion");
    }

    // NOTE (documented finding): the SUT calls `WarnIf(FechaEstimadaConclusion == default, "FechaEstimadaConclusion")`,
    // but ValidationState.WarnIf warns when the *condition is false*. So the warning fires when the estimated-
    // conclusion date IS present and stays SILENT when it is missing — the warning is inverted vs. intent.
    // These two tests pin the ACTUAL behavior (and kill the WarnIf/`== default` mutants); see the session findings doc.

    [Fact]
    public async Task AggregateValidation_FechaEstimadaConclusionSet_AddsWarning_InvertedBehavior()
    {
        var e = ValidExpediente(); // FechaEstimadaConclusion is set (non-default)
        DocxReturns(new ExtractedFields { Expediente = "E" });
        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente"), expediente: e);

        result.Value!.Validation!.Warnings.ShouldContain("FechaEstimadaConclusion"); // inverted: warns when present
        result.Value!.Validation.IsValid.ShouldBeTrue(); // warning, not a missing requirement
    }

    [Fact]
    public async Task AggregateValidation_DefaultFechaEstimadaConclusion_NoWarning_InvertedBehavior()
    {
        var e = ValidExpediente();
        e.FechaEstimadaConclusion = default;
        DocxReturns(new ExtractedFields { Expediente = "E" });
        var result = await Run(_service, docx: new DocxSource("d.docx"), defs: Defs("Expediente"), expediente: e);

        result.Value!.Validation!.Warnings.ShouldNotContain("FechaEstimadaConclusion"); // inverted: silent when missing
    }
}
