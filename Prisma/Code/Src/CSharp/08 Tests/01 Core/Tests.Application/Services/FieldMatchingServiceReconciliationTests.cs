using ExxerCube.Prisma.Domain.Enums;

namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Tests for the additional (non-core) field XML-vs-OCR-vs-DOCX reconciliation added to the used orchestrator
/// (resolves docs/qa/findings/2026-06-08-fieldmatcher-additionalfields-dead-conflict-detection.md in the path
/// that production actually exercises). Additional fields (RFC/CURP/contact/authority names) carry the metadata
/// used upstream to disambiguate homonyms, so cross-source disagreement must be surfaced as a conflict and the
/// (now-populated) AdditionalMerged must drive SLA derivation. Name keys route to the name-aware policy.
/// </summary>
public class FieldMatchingServiceReconciliationTests
{
    private readonly IFieldExtractor<DocxSource> _docx = Substitute.For<IFieldExtractor<DocxSource>>();
    private readonly IFieldExtractor<PdfSource> _pdf = Substitute.For<IFieldExtractor<PdfSource>>();
    private readonly IFieldExtractor<XmlSource> _xml = Substitute.For<IFieldExtractor<XmlSource>>();
    private readonly IMatchingPolicy _policy = Substitute.For<IMatchingPolicy>();
    private readonly INameMatchingPolicy _namePolicy = Substitute.For<INameMatchingPolicy>();
    private readonly FieldMatchingService _service;

    public FieldMatchingServiceReconciliationTests()
    {
        // Default: echo the first value, no conflict. Both policies behave the same unless overridden per-test.
        _policy.SelectBestValueAsync(Arg.Any<string>(), Arg.Any<List<FieldValue>>())
            .Returns(ci => Echo(ci.ArgAt<string>(0), ci.ArgAt<List<FieldValue>>(1), conflict: false));
        _namePolicy.SelectBestValueAsync(Arg.Any<string>(), Arg.Any<List<FieldValue>>())
            .Returns(ci => Echo(ci.ArgAt<string>(0), ci.ArgAt<List<FieldValue>>(1), conflict: false));

        _service = new FieldMatchingService(_docx, _pdf, _xml, _policy,
            Substitute.For<ILogger<FieldMatchingService>>(), _namePolicy);
    }

    private static Result<FieldMatchResult> Echo(string field, List<FieldValue> values, bool conflict) =>
        Result<FieldMatchResult>.Success(new FieldMatchResult
        {
            FieldName = field,
            MatchedValue = values.FirstOrDefault()?.Value,
            AgreementLevel = conflict ? 0.2f : 1.0f,
            HasConflict = conflict,
        });

    private static ExtractedFields Fields(params (string Key, string Value)[] additional)
    {
        var f = new ExtractedFields();
        foreach (var (k, v) in additional)
        {
            f.AdditionalFields[k] = v;
        }
        return f;
    }

    private void DocxReturns(ExtractedFields f) =>
        _docx.ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(f));

    private void PdfReturns(ExtractedFields f) =>
        _pdf.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(f));

    private void XmlReturns(ExtractedFields f) =>
        _xml.ExtractFieldsAsync(Arg.Any<XmlSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(f));

    private Task<Result<UnifiedMetadataRecord>> Run(Expediente? expediente = null) =>
        _service.MatchFieldsAndGenerateUnifiedRecordAsync(
            new DocxSource("d.docx"), new PdfSource("p.pdf"), new XmlSource("x.xml"),
            new[] { new FieldDefinition("Expediente") },
            expediente: expediente, classification: null, requiredFields: null,
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task AdditionalField_DisagreementAcrossSources_RecordedAsConflict()
    {
        // XML says one RFC, PDF/OCR says another -> the policy reports a conflict -> it must be surfaced.
        XmlReturns(Fields(("Rfc", "PEGJ850101AAA")));
        PdfReturns(Fields(("Rfc", "PEGJ850101ZZZ")));
        DocxReturns(new ExtractedFields());
        _policy.SelectBestValueAsync("Rfc", Arg.Any<List<FieldValue>>())
            .Returns(ci => Echo("Rfc", ci.ArgAt<List<FieldValue>>(1), conflict: true));

        var result = await Run();

        result.IsSuccess.ShouldBeTrue();
        result.Value!.AdditionalFieldConflicts.ShouldContain("Rfc");
        result.Value.AdditionalFields.ShouldContainKey("Rfc");
        // The policy was given BOTH sources' values for the key (so it could detect the disagreement).
        await _policy.Received().SelectBestValueAsync("Rfc",
            Arg.Is<List<FieldValue>>(v => v.Count == 2));
    }

    [Fact]
    public async Task AdditionalField_Agreement_NoConflict_AndMerged()
    {
        XmlReturns(Fields(("Rfc", "PEGJ850101AAA")));
        PdfReturns(Fields(("Rfc", "PEGJ850101AAA")));
        DocxReturns(new ExtractedFields());

        var result = await Run();

        result.IsSuccess.ShouldBeTrue();
        result.Value!.AdditionalFieldConflicts.ShouldNotContain("Rfc");
        result.Value.AdditionalFields["Rfc"].ShouldBe("PEGJ850101AAA");
    }

    [Fact]
    public async Task AdditionalNameField_RoutesToNameAwarePolicy()
    {
        XmlReturns(Fields(("NombreSolicitante", "Juan Pérez")));
        PdfReturns(Fields(("NombreSolicitante", "María González")));
        DocxReturns(new ExtractedFields());

        await Run();

        await _namePolicy.Received().SelectBestValueAsync("NombreSolicitante", Arg.Any<List<FieldValue>>());
        await _policy.DidNotReceive().SelectBestValueAsync("NombreSolicitante", Arg.Any<List<FieldValue>>());
    }

    [Fact]
    public async Task NonNameAdditionalField_RoutesToGeneralPolicy()
    {
        XmlReturns(Fields(("Rfc", "A")));
        DocxReturns(new ExtractedFields());
        PdfReturns(new ExtractedFields());

        await Run();

        await _policy.Received().SelectBestValueAsync("Rfc", Arg.Any<List<FieldValue>>());
        await _namePolicy.DidNotReceive().SelectBestValueAsync("Rfc", Arg.Any<List<FieldValue>>());
    }

    [Fact]
    public async Task DefinedFieldNotDoubleCountedAsAdditional()
    {
        // "Expediente" is a defined field; even if it appears in AdditionalFields it must not be reconciled twice.
        XmlReturns(Fields(("Expediente", "A/AS1-2505-088637-PHM")));
        DocxReturns(new ExtractedFields());
        PdfReturns(new ExtractedFields());

        var result = await Run();

        result.IsSuccess.ShouldBeTrue();
        result.Value!.AdditionalFields.ShouldNotContainKey("Expediente"); // handled as a core field, not additional
    }

    [Fact]
    public async Task OriginMagicKey_IsExcludedFromAdditional()
    {
        XmlReturns(Fields(("Origin", "XML"), ("Rfc", "A")));
        DocxReturns(new ExtractedFields());
        PdfReturns(new ExtractedFields());

        var result = await Run();

        result.Value!.AdditionalFields.ShouldNotContainKey("Origin");
        result.Value.AdditionalFields.ShouldContainKey("Rfc");
    }

    [Fact]
    public async Task DeriveSlaFromAdditional_NowFires_FromReconciledFechaPublicacion()
    {
        // Previously dead (AdditionalMerged was never populated). Now the reconciled FechaPublicacion drives
        // the expediente's FechaRecepcion.
        var expediente = new Expediente(); // FechaRecepcion default
        XmlReturns(Fields(("FechaPublicacion", "2026-01-15"), ("DiasPlazo", "10")));
        DocxReturns(new ExtractedFields());
        PdfReturns(new ExtractedFields());

        var result = await Run(expediente);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Expediente.ShouldNotBeNull();
        result.Value.Expediente!.FechaRecepcion.ShouldBe(new DateTime(2026, 1, 15));
        result.Value.Expediente.FechaEstimadaConclusion.ShouldBe(new DateTime(2026, 1, 25)); // +10 days
    }

    [Fact]
    public async Task NoAdditionalFields_LeavesMergedAndConflictsEmpty()
    {
        DocxReturns(new ExtractedFields { Expediente = "A/AS1-2505-088637-PHM" });
        PdfReturns(new ExtractedFields());
        XmlReturns(new ExtractedFields());

        var result = await Run();

        result.IsSuccess.ShouldBeTrue();
        result.Value!.AdditionalFields.Count.ShouldBe(0);
        result.Value.AdditionalFieldConflicts.Count.ShouldBe(0);
    }
}
