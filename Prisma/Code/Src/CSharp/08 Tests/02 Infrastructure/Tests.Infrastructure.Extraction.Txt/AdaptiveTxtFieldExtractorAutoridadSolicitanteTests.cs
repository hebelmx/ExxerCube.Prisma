namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Txt;

/// <summary>
/// Regression + coverage tests for the "Autoridad solicitante" label fix in
/// <see cref="AdaptiveTxtFieldExtractor"/>'s ExtractAutoridadNombre priority-0 rule.
/// </summary>
/// <remarks>
/// Prior to this fix, AutoridadNombre always returned the CNBV recipient constant whenever
/// "Comisión Nacional Bancaria y de Valores" appeared anywhere in the text — even though that
/// phrase names the RECIPIENT (every SIARA letter is addressed TO CNBV), never the requesting
/// authority. The requesting authority is the ground truth for AutoridadNombre and is carried
/// by the labeled line "Autoridad solicitante: &lt;name&gt;" in the document body.
/// </remarks>
public class AdaptiveTxtFieldExtractorAutoridadSolicitanteTests
{
    private readonly ILogger<AdaptiveTxtFieldExtractor> _logger;
    private readonly AdaptiveTxtFieldExtractor _extractor;

    public AdaptiveTxtFieldExtractorAutoridadSolicitanteTests(ITestOutputHelper output)
    {
        _logger = XUnitLogger.CreateLogger<AdaptiveTxtFieldExtractor>(output);
        _extractor = new AdaptiveTxtFieldExtractor(_logger);
    }

    [Theory]
    [InlineData("Secretaría de Hacienda y Crédito Público")]
    [InlineData("Servicio de Administración Tributaria")]
    [InlineData("Instituto Mexicano del Seguro Social")]
    [InlineData("Poder Judicial de la Federación")]
    public async Task ExtractAutoridadNombre_LabelPresent_ReturnsRequestingAuthority(string authority)
    {
        // Arrange
        var ocrText = $"Autoridad solicitante: {authority}\nOtros datos del oficio.";
        var source = new TxtSource(ocrText);

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "AutoridadNombre");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe(authority);
    }

    [Fact]
    public async Task ExtractAutoridadNombre_SolicitanteLabelAndCnbvRecipientBothPresent_ReturnsSolicitanteNotCnbv()
    {
        // Arrange — regression for the confirmed production defect: the CNBV recipient block
        // must NOT win over the explicit requesting-authority label.
        var ocrText = @"
Autoridad solicitante: Poder Judicial de la Federación

Juan Juan Melón Sandía
Vicepresidente de Supervisión de Procesos Preventivos
Comisión Nacional Bancaria y de Valores
Insurgentes Sur 1971
";
        var source = new TxtSource(ocrText);

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "AutoridadNombre");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("Poder Judicial de la Federación");
        result.Value.Value.ShouldNotBe("Comisión Nacional Bancaria y de Valores");
    }

    [Fact]
    public async Task ExtractAutoridadNombre_PlainOcrShapeNoMarkdownExtraSpaces_CapturesValue()
    {
        // Arrange — plain PDF->OCR text has no markdown markers (unlike the markdown source
        // "**Autoridad solicitante:** name"), and OCR often introduces extra whitespace.
        var ocrText = "Autoridad  solicitante:    Fiscalía General de la República";
        var source = new TxtSource(ocrText);

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "AutoridadNombre");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("Fiscalía General de la República");
    }

    [Fact]
    public async Task ExtractAutoridadNombre_LabelAbsentOnlyCnbvPresent_FallsBackToCnbv()
    {
        // Arrange — proves the pre-existing acronym/full-name ladder still works as the
        // fallback for documents that lack the "Autoridad solicitante" label.
        var ocrText = @"
Comisión Nacional Bancaria y de Valores
Insurgentes Sur 1971
";
        var source = new TxtSource(ocrText);

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "AutoridadNombre");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("Comisión Nacional Bancaria y de Valores");
    }
}
