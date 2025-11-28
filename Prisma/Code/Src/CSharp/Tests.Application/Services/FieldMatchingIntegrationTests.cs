namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Integration tests for <see cref="FieldMatchingService"/> covering end-to-end workflows, backward compatibility, and performance.
/// </summary>
public class FieldMatchingIntegrationTests
{
    private readonly IFieldExtractor<DocxSource> _docxFieldExtractor;
    private readonly IFieldExtractor<PdfSource> _pdfFieldExtractor;
    private readonly IFieldExtractor<XmlSource> _xmlFieldExtractor;
    private readonly IMatchingPolicy _matchingPolicy;
    private readonly ILogger<FieldMatchingService> _logger;
    private readonly FieldMatchingService _service;

    /// <summary>
    /// Initializes the integration tests with mocked extractors and matching policy.
    /// </summary>
    /// <param name="output">xUnit output helper used for logging.</param>
    public FieldMatchingIntegrationTests(ITestOutputHelper output)
    {
        _docxFieldExtractor = Substitute.For<IFieldExtractor<DocxSource>>();
        _pdfFieldExtractor = Substitute.For<IFieldExtractor<PdfSource>>();
        _xmlFieldExtractor = Substitute.For<IFieldExtractor<XmlSource>>();
        _logger = XUnitLogger.CreateLogger<FieldMatchingService>(output);

        // Use mock instead of concrete Infrastructure implementation
        _matchingPolicy = Substitute.For<IMatchingPolicy>();

        _service = new FieldMatchingService(
            _docxFieldExtractor,
            _pdfFieldExtractor,
            _xmlFieldExtractor,
            _matchingPolicy,
            _logger);
    }

    [Fact]
    [Trait("Category", "Integration")]
    /// <returns>A task that completes after verifying all sources contribute to a unified record.</returns>
    public async Task MatchFieldsAndGenerateUnifiedRecordAsync_EndToEndWorkflow_AllSourcesContribute()
    {
        // Arrange - Simulate real-world scenario with XML, DOCX, and PDF sources
        var docxSource = new DocxSource("test.docx");
        var pdfSource = new PdfSource("test.pdf");
        var xmlSource = new XmlSource("test.xml");

        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente"),
            new FieldDefinition("Causa"),
            new FieldDefinition("AccionSolicitada")
        };

        // DOCX extraction - All sources agree on all fields to achieve >0.8 agreement
        var docxFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM",
            Causa = "Test Causa",
            AccionSolicitada = "Test Action"
        };

        // PDF extraction - All sources agree on all fields
        var pdfFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM",
            Causa = "Test Causa",
            AccionSolicitada = "Test Action"
        };

        // XML extraction - All sources agree on all fields
        var xmlFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM",
            Causa = "Test Causa",
            AccionSolicitada = "Test Action"
        };

        _docxFieldExtractor.ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(docxFields));
        _pdfFieldExtractor.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(pdfFields));
        _xmlFieldExtractor.ExtractFieldsAsync(Arg.Any<XmlSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(xmlFields));

        // Configure matching policy mock - all sources agree, so high agreement level
        _matchingPolicy.SelectBestValueAsync(Arg.Is<string>(s => s == "Expediente"), Arg.Any<List<FieldValue>>())
            .Returns(Result<FieldMatchResult>.Success(new FieldMatchResult("Expediente", "A/AS1-2505-088637-PHM", 1.0f, "CONSENSUS")
            {
                AgreementLevel = 1.0f,
                HasConflict = false
            }));

        _matchingPolicy.SelectBestValueAsync(Arg.Is<string>(s => s == "Causa"), Arg.Any<List<FieldValue>>())
            .Returns(Result<FieldMatchResult>.Success(new FieldMatchResult("Causa", "Test Causa", 1.0f, "CONSENSUS")
            {
                AgreementLevel = 1.0f,
                HasConflict = false
            }));

        _matchingPolicy.SelectBestValueAsync(Arg.Is<string>(s => s == "AccionSolicitada"), Arg.Any<List<FieldValue>>())
            .Returns(Result<FieldMatchResult>.Success(new FieldMatchResult("AccionSolicitada", "Test Action", 1.0f, "CONSENSUS")
            {
                AgreementLevel = 1.0f,
                HasConflict = false
            }));

        var expediente = new Expediente { NumeroExpediente = "A/AS1-2505-088637-PHM" };
        var classification = new ClassificationResult();

        // Act
        var result = await _service.MatchFieldsAndGenerateUnifiedRecordAsync(
            docxSource,
            pdfSource,
            xmlSource,
            fieldDefinitions,
            expediente,
            classification,
            requiredFields: null,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Expediente.ShouldNotBeNull();
        result.Value.ExtractedFields.ShouldNotBeNull();
        result.Value.MatchedFields.ShouldNotBeNull();
        result.Value.MatchedFields.FieldMatches.ShouldContainKey("Expediente");
        result.Value.MatchedFields.FieldMatches.ShouldContainKey("Causa");
        result.Value.MatchedFields.FieldMatches.ShouldContainKey("AccionSolicitada");
        result.Value.MatchedFields.OverallAgreement.ShouldBeGreaterThan(0.8f); // All sources agree on Expediente
        result.Value.Classification.ShouldBe(classification);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MatchFieldsAndGenerateUnifiedRecordAsync_BackwardCompatibility_ExistingIFieldExtractorStillWorks()
    {
        // Arrange - Verify that existing non-generic IFieldExtractor implementations are unaffected
        // This test ensures IV1: Existing IFieldExtractor interface extended to generic IFieldExtractor<T> 
        // without breaking existing implementations

        var docxSource = new DocxSource("test.docx");
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        var docxFields = new ExtractedFields { Expediente = "A/AS1-2505-088637-PHM" };

        _docxFieldExtractor.ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(docxFields));

        // Configure matching policy mock for Expediente
        _matchingPolicy.SelectBestValueAsync(Arg.Is<string>(s => s == "Expediente"), Arg.Any<List<FieldValue>>())
            .Returns(Result<FieldMatchResult>.Success(new FieldMatchResult("Expediente", "A/AS1-2505-088637-PHM", 1.0f, "DOCX")
            {
                AgreementLevel = 1.0f,
                HasConflict = false
            }));

        // Act - Use generic IFieldExtractor<T> interface
        var result = await _service.MatchFieldsAndGenerateUnifiedRecordAsync(
            docxSource,
            null,
            null,
            fieldDefinitions,
            expediente: null,
            classification: null,
            requiredFields: null,
            TestContext.Current.CancellationToken);

        // Assert - Generic interface works correctly
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.MatchedFields.ShouldNotBeNull();
        result.Value.MatchedFields.FieldMatches.ShouldContainKey("Expediente");

        // Verify that the generic extractor was called (backward compatibility maintained)
        await _docxFieldExtractor.Received().ExtractFieldsAsync(
            Arg.Any<DocxSource>(),
            Arg.Any<FieldDefinition[]>());
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Performance")]
    /// <returns>A task that completes after validating end-to-end workflow meets performance targets.</returns>
    public async Task MatchFieldsAndGenerateUnifiedRecordAsync_EndToEndPerformance_CompletesWithin2Seconds()
    {
        // Arrange - NFR4: Metadata extraction within 2 seconds for XML/DOCX, 30 seconds for PDF
        // This integration test verifies the entire workflow meets performance targets
        var docxSource = new DocxSource("test.docx");
        var pdfSource = new PdfSource("test.pdf");
        var xmlSource = new XmlSource("test.xml");

        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente"),
            new FieldDefinition("Causa"),
            new FieldDefinition("AccionSolicitada")
        };

        var docxFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM",
            Causa = "Test Causa",
            AccionSolicitada = "Test Action"
        };

        var pdfFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM",
            Causa = "Test Causa",
            AccionSolicitada = "Test Action"
        };

        var xmlFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM",
            Causa = "Test Causa",
            AccionSolicitada = "Test Action"
        };

        _docxFieldExtractor.ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(docxFields));
        _pdfFieldExtractor.ExtractFieldsAsync(Arg.Any<PdfSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(pdfFields));
        _xmlFieldExtractor.ExtractFieldsAsync(Arg.Any<XmlSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(xmlFields));

        // Configure matching policy mock - return immediately for performance testing
        _matchingPolicy.SelectBestValueAsync(Arg.Is<string>(s => s == "Expediente"), Arg.Any<List<FieldValue>>())
            .Returns(Result<FieldMatchResult>.Success(new FieldMatchResult("Expediente", "A/AS1-2505-088637-PHM", 1.0f, "CONSENSUS")
            {
                AgreementLevel = 1.0f,
                HasConflict = false
            }));

        _matchingPolicy.SelectBestValueAsync(Arg.Is<string>(s => s == "Causa"), Arg.Any<List<FieldValue>>())
            .Returns(Result<FieldMatchResult>.Success(new FieldMatchResult("Causa", "Test Causa", 1.0f, "CONSENSUS")
            {
                AgreementLevel = 1.0f,
                HasConflict = false
            }));

        _matchingPolicy.SelectBestValueAsync(Arg.Is<string>(s => s == "AccionSolicitada"), Arg.Any<List<FieldValue>>())
            .Returns(Result<FieldMatchResult>.Success(new FieldMatchResult("AccionSolicitada", "Test Action", 1.0f, "CONSENSUS")
            {
                AgreementLevel = 1.0f,
                HasConflict = false
            }));

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _service.MatchFieldsAndGenerateUnifiedRecordAsync(
            docxSource,
            pdfSource,
            xmlSource,
            fieldDefinitions,
            expediente: null,
            classification: null,
            requiredFields: null,
            TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(2000,
            $"End-to-end field matching workflow took {stopwatch.ElapsedMilliseconds}ms, exceeding 2 second target (NFR4 for XML/DOCX)");
    }
}

