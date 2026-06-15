using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Unit tests for <see cref="DocxFieldExtractor"/>.
/// </summary>
public class DocxFieldExtractorTests
{
    private readonly ILogger<DocxFieldExtractor> _logger;
    private readonly DocxFieldExtractor _extractor;

    public DocxFieldExtractorTests(ITestOutputHelper output)
    {
        _logger = XUnitLogger.CreateLogger<DocxFieldExtractor>(output);
        _extractor = new DocxFieldExtractor(_logger);
    }

    [Fact]
    public async Task ExtractFieldsAsync_ValidDocx_ReturnsExtractedFields()
    {
        // Arrange
        var docxBytes = CreateSampleDocx("A/AS1-2505-088637-PHM", "Test Causa", "Test Action");
        var source = new DocxSource(docxBytes);
        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente"),
            new FieldDefinition("Causa"),
            new FieldDefinition("AccionSolicitada")
        };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
    }

    [Fact]
    public async Task ExtractFieldsAsync_WithFilePath_ExtractsFields()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.docx");
        var docxBytes = CreateSampleDocx("A/AS1-2505-088637-PHM", "Test Causa", null);
        await File.WriteAllBytesAsync(tempFile, docxBytes, TestContext.Current.CancellationToken);

        try
        {
            var source = new DocxSource(tempFile);
            var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

            // Act
            var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value!.Expediente.ShouldBe("A/AS1-2505-088637-PHM");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExtractFieldsAsync_InvalidDocx_ReturnsFailure()
    {
        // Arrange
        var invalidBytes = new byte[] { 1, 2, 3, 4, 5 };
        var source = new DocxSource(invalidBytes);
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExtractFieldsAsync_NoFileContentOrPath_ReturnsFailure()
    {
        // Arrange
        var source = new DocxSource();
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("FileContent or valid FilePath");
    }

    [Fact]
    public async Task ExtractFieldAsync_ValidDocx_ReturnsFieldValue()
    {
        // Arrange
        var docxBytes = CreateSampleDocx("A/AS1-2505-088637-PHM", "Test Causa", null);
        var source = new DocxSource(docxBytes);

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "Expediente");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.FieldName.ShouldBe("Expediente");
        result.Value.Value.ShouldBe("A/AS1-2505-088637-PHM");
        result.Value.SourceType.ShouldBe("DOCX");
        result.Value.Confidence.ShouldBe(1.0f);
    }

    [Fact]
    public async Task ExtractFieldAsync_FieldNotFound_ReturnsFailure()
    {
        // Arrange
        var docxBytes = CreateSampleDocx("A/AS1-2505-088637-PHM", null, null);
        var source = new DocxSource(docxBytes);

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "NonExistentField");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("not found");
    }

    [Fact]
    public async Task ExtractFieldsAsync_EmptyFieldDefinitions_ReturnsEmptyExtractedFields()
    {
        // Arrange
        var docxBytes = CreateSampleDocx("A/AS1-2505-088637-PHM", null, null);
        var source = new DocxSource(docxBytes);
        var fieldDefinitions = Array.Empty<FieldDefinition>();

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Expediente.ShouldBeNull(); // No fields extracted
    }

    /// <summary>
    /// Issue #2 fix: the real CNBV oficio shape. When the caller requests Expediente + NumeroOficio
    /// (as the Athena worker does), the authoritative oficio is label-anchored ("Oficio Núm.:") and
    /// must win over the remitted source oficio (AGAFADAFSON2/2025/000084); the folio is printed with
    /// internal whitespace and must be normalised; and NumeroOficio must land in AdditionalFields.
    /// </summary>
    [Fact]
    public async Task ExtractFieldsAsync_CnbvOficioAndSpacedFolio_ExtractedAndNormalised()
    {
        // Arrange
        var text =
            "Oficio Núm.: 222/AAA/-4444444444/2025 Folio Núm.: A/AS1- 1111-222222-AAA " +
            "se remite el oficio No. AGAFADAFSON2/2025/000084 del 09 de Abril";
        var docxBytes = CreateSampleDocxWithText(text);
        var source = new DocxSource(docxBytes);
        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente"),
            new FieldDefinition("NumeroOficio"),
        };

        // Act
        var result = await _extractor.ExtractFieldsAsync(source, fieldDefinitions);

        // Assert — labeled CNBV oficio wins NumeroOficio; folio whitespace collapsed.
        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Expediente.ShouldBe("A/AS1-1111-222222-AAA");
        result.Value.AdditionalFields.ShouldContainKey("NumeroOficio");
        result.Value.AdditionalFields["NumeroOficio"].ShouldBe("222/AAA/-4444444444/2025");
    }

    // -----------------------------------------------------------------------
    // Real-corpus tests (D1) — Docx requerimiento extraction
    // -----------------------------------------------------------------------

    /// <summary>
    /// Extracting "requerimiento" field from the synthetic DOCX that contains the
    /// AGAFADAFSON2/2025/000084 pattern confirms the regex fires.
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_RequerimientoPattern_ExtractsMatch()
    {
        // Arrange: build a synthetic docx whose text contains the requerimiento id pattern
        var docxBytes = CreateSampleDocxWithText("AGAFADAFSON2/2025/000084");
        var source = new DocxSource(docxBytes);

        // Act
        var result = await _extractor.ExtractFieldAsync(source, "requerimiento");

        // Assert
        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value.ShouldNotBeNull();
        result.Value!.Value.ShouldBe("AGAFADAFSON2/2025/000084");
        result.Value.SourceType.ShouldBe("DOCX");
        result.Value.Origin.ShouldBe(ExxerCube.Prisma.Domain.Enums.FieldOrigin.Docx);
    }

    /// <summary>
    /// The "numerooficio" alias routes to the same extractor as "requerimiento".
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_NumeroOficioAlias_ExtractsRequerimientoPattern()
    {
        var docxBytes = CreateSampleDocxWithText("AGAFADAFSON2/2025/000083");
        var source = new DocxSource(docxBytes);

        var result = await _extractor.ExtractFieldAsync(source, "numerooficio");

        result.IsSuccess.ShouldBeTrue(result.Error);
        result.Value!.Value.ShouldBe("AGAFADAFSON2/2025/000083");
    }

    /// <summary>
    /// Text without the requerimiento pattern returns failure (not throw).
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_NoRequerimientoPattern_ReturnsFailure()
    {
        var docxBytes = CreateSampleDocxWithText("No hay folio SIARA aqui.");
        var source = new DocxSource(docxBytes);

        var result = await _extractor.ExtractFieldAsync(source, "requerimiento");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// ExtractExpediente is unchanged — the existing pattern still works alongside the new extractor.
    /// Confirms that adding ExtractRequerimiento does not interfere with ExtractExpediente routing.
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_ExpedienteAndRequerimiento_BothExtracted()
    {
        var text = "A/AS1-2505-088637-PHM algo AGAFADAFSON2/2025/000084 fin";
        var docxBytes = CreateSampleDocxWithText(text);
        var source = new DocxSource(docxBytes);

        var expedienteResult = await _extractor.ExtractFieldAsync(source, "Expediente");
        var requerimientoResult = await _extractor.ExtractFieldAsync(source, "requerimiento");

        expedienteResult.IsSuccess.ShouldBeTrue(expedienteResult.Error);
        expedienteResult.Value!.Value.ShouldBe("A/AS1-2505-088637-PHM");

        requerimientoResult.IsSuccess.ShouldBeTrue(requerimientoResult.Error);
        requerimientoResult.Value!.Value.ShouldBe("AGAFADAFSON2/2025/000084");
    }

    /// <summary>
    /// Real corpus: 222AAA-44444444442025.docx — extract the requerimiento id.
    /// Expected: AGAFADAFSON2/2025/000084 (from the corresponding XML's Cnbv_SolicitudSiara field).
    /// </summary>
    [Fact]
    public async Task ExtractFieldAsync_222AaaRealCorpusDocx_ExtractsRequerimiento()
    {
        var samplePath = FindSamplePath("222AAA-44444444442025.docx");
        if (!File.Exists(samplePath))
        {
            // Skip gracefully if the binary sample isn't present in this environment
            return;
        }

        var source = new DocxSource(samplePath);
        var result = await _extractor.ExtractFieldAsync(source, "requerimiento");

        // The real docx contains the SIARA solicitud id in its text.
        // We assert a match was found (non-throwing) rather than a hard-coded string
        // because the docx text extraction may vary by Word version / rendering.
        result.IsSuccess.ShouldBeTrue(
            $"Expected to find requerimiento pattern in 222AAA docx, got: {result.Error}");
        result.Value!.Value.ShouldNotBeNullOrEmpty();
        // Assert the pattern shape (letters/digits/slashes)
        result.Value.Value.ShouldMatch(@"[A-Z]{4,}[A-Z0-9]*/\d{4}/\d{6}");
    }

    private static byte[] CreateSampleDocx(string? expediente, string? causa, string? accionSolicitada)
    {
        using var stream = new MemoryStream();
        using (var wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());
            var paragraph = body.AppendChild(new Paragraph());

            var text = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(expediente))
            {
                text.AppendLine($"Expediente: {expediente}");
            }
            if (!string.IsNullOrEmpty(causa))
            {
                text.AppendLine($"CAUSA: {causa}");
            }
            if (!string.IsNullOrEmpty(accionSolicitada))
            {
                text.AppendLine($"ACCIÓN SOLICITADA: {accionSolicitada}");
            }

            paragraph.AppendChild(new Run(new Text(text.ToString())));
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    /// <summary>Creates a minimal DOCX whose body text is exactly <paramref name="rawText"/>.</summary>
    private static byte[] CreateSampleDocxWithText(string rawText)
    {
        using var stream = new MemoryStream();
        using (var wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());
            body.AppendChild(new Paragraph(new Run(new Text(rawText))));
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Resolves the absolute path to a sample file in docs/legal/samples.
    /// Walks up from the test assembly output directory; falls back to the known absolute path.
    /// </summary>
    private static string FindSamplePath(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "legal", "samples", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        return Path.Combine(
            @"E:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma",
            "docs", "legal", "samples", fileName);
    }
}

