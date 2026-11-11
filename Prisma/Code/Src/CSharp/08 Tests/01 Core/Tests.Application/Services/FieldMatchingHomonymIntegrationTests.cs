namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// End-to-end integration test of the homonym-disambiguation chain through the WIRED
/// <see cref="FieldMatchingService"/> with the REAL <see cref="NameMatchingPolicy"/> +
/// <see cref="MatchingPolicyService"/> (production policies, not mocks). Two sources reporting different
/// <c>NombreSolicitante</c> additional values must surface as an <c>AdditionalFieldConflicts</c> entry —
/// this locks the name-field routing + the (now-fixed) self-pairing fuzzy matcher together with the
/// production reconciliation path. Companion to the mock-driven FieldMatchingServiceReconciliationTests.
/// </summary>
public class FieldMatchingHomonymIntegrationTests
{
    private readonly IFieldExtractor<DocxSource> _docx = Substitute.For<IFieldExtractor<DocxSource>>();
    private readonly IFieldExtractor<PdfSource> _pdf = Substitute.For<IFieldExtractor<PdfSource>>();
    private readonly IFieldExtractor<XmlSource> _xml = Substitute.For<IFieldExtractor<XmlSource>>();
    private readonly FieldMatchingService _service;

    public FieldMatchingHomonymIntegrationTests(ITestOutputHelper output)
    {
        var generalPolicy = new MatchingPolicyService(
            Options.Create(new MatchingPolicyOptions()),
            XUnitLogger.CreateLogger<MatchingPolicyService>(output));

        var nameOptions = Substitute.For<IOptionsMonitor<NameMatchingOptions>>();
        nameOptions.CurrentValue.Returns(new NameMatchingOptions());
        var namePolicy = new NameMatchingPolicy(nameOptions, XUnitLogger.CreateLogger<NameMatchingPolicy>(output));

        _service = new FieldMatchingService(
            _docx, _pdf, _xml, generalPolicy,
            XUnitLogger.CreateLogger<FieldMatchingService>(output), namePolicy);
    }

    private static ExtractedFields WithAdditional(params (string Key, string Value)[] additional)
    {
        var f = new ExtractedFields();
        foreach (var (k, v) in additional)
        {
            f.AdditionalFields[k] = v;
        }
        return f;
    }

    private Task<Result<UnifiedMetadataRecord>> Run() =>
        _service.MatchFieldsAndGenerateUnifiedRecordAsync(
            new DocxSource("d.docx"), new PdfSource("p.pdf"), new XmlSource("x.xml"),
            new[] { new FieldDefinition("Expediente") },
            expediente: null, classification: null, requiredFields: null,
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task DisagreeingNombreSolicitante_RealNamePolicy_SurfacesConflict()
    {
        _xml.ExtractFieldsAsync(Arg.Any<XmlSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(WithAdditional(("NombreSolicitante", "Juan Pérez García"))));
        _pdf.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(WithAdditional(("NombreSolicitante", "María González López"))));
        _docx.ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(new ExtractedFields()));

        var result = await Run();

        result.IsSuccess.ShouldBeTrue();
        // The real NameMatchingPolicy (diagonal excluded) flags two distinct names as a conflict.
        result.Value!.AdditionalFieldConflicts.ShouldContain("NombreSolicitante");
        result.Value.AdditionalFields.ShouldContainKey("NombreSolicitante");
    }

    [Fact]
    public async Task AgreeingNombreSolicitante_RealNamePolicy_NoConflict()
    {
        _xml.ExtractFieldsAsync(Arg.Any<XmlSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(WithAdditional(("NombreSolicitante", "Juan Pérez García"))));
        _pdf.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(WithAdditional(("NombreSolicitante", "Juan Pérez García"))));
        _docx.ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(new ExtractedFields()));

        var result = await Run();

        result.IsSuccess.ShouldBeTrue();
        result.Value!.AdditionalFieldConflicts.ShouldNotContain("NombreSolicitante");
        result.Value.AdditionalFields["NombreSolicitante"].ShouldBe("Juan Pérez García");
    }
}
