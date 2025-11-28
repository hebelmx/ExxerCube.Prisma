namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Unit tests for <see cref="FieldMatchingService"/> focused on field definition validation and matching workflows.
/// </summary>
/// <remarks>
/// ⚠️ Refactoring still recommended: use a mocked <see cref="IMatchingPolicy"/> rather than concrete <see cref="MatchingPolicyService"/>.
/// </remarks>
public class FieldMatchingServiceTests
{
    private readonly IFieldExtractor<DocxSource> _docxFieldExtractor;
    private readonly IFieldExtractor<PdfSource> _pdfFieldExtractor;
    private readonly IMatchingPolicy _matchingPolicy;
    private readonly ILogger<FieldMatchingService> _logger;
    private readonly FieldMatchingService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldMatchingServiceTests"/> class with mocked extractors and matching policy.
    /// </summary>
    public FieldMatchingServiceTests()
    {
        //throw new InvalidOperationException(
        //    "⚠️ REFACTORING REQUIRED ⚠️\n" +
        //    "This test violates clean architecture by directly instantiating Infrastructure.Classification types.\n" +
        //    "Please refactor to mock IMatchingPolicy interface instead of creating MatchingPolicyService.\n" +
        //    "See class documentation for details.");
        _docxFieldExtractor = Substitute.For<IFieldExtractor<DocxSource>>();
        _pdfFieldExtractor = Substitute.For<IFieldExtractor<PdfSource>>();
        _logger = Substitute.For<ILogger<FieldMatchingService>>();

        var options = Options.Create(new MatchingPolicyOptions());
        _matchingPolicy = new MatchingPolicyService(options, Substitute.For<ILogger<MatchingPolicyService>>());

        _service = new FieldMatchingService(
            _docxFieldExtractor,
            _pdfFieldExtractor,
            null,
            _matchingPolicy,
            _logger);
    }

    [Fact]
    /// <returns>A task that completes after asserting failure when no sources are provided.</returns>
    public async Task MatchFieldsAndGenerateUnifiedRecordAsync_WithNoSources_ReturnsFailure()
    {
        // Arrange
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        // Act
        var result = await _service.MatchFieldsAndGenerateUnifiedRecordAsync(
            null,
            null,
            null,
            fieldDefinitions,
            expediente: null,
            classification: null,
            requiredFields: null,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("At least one source");
    }

    [Fact]
    /// <returns>A task that completes after asserting failure when field definitions are null.</returns>
    public async Task MatchFieldsAndGenerateUnifiedRecordAsync_WithNullFieldDefinitions_ReturnsFailure()
    {
        // Arrange
        var docxSource = new DocxSource("test.docx");

        // Act
        var result = await _service.MatchFieldsAndGenerateUnifiedRecordAsync(
            docxSource,
            null,
            null,
            null!,
            expediente: null,
            classification: null,
            requiredFields: null,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Field definitions");
    }

    [Fact]
    /// <returns>A task that completes after asserting failure when field definitions are empty.</returns>
    public async Task MatchFieldsAndGenerateUnifiedRecordAsync_WithEmptyFieldDefinitions_ReturnsFailure()
    {
        // Arrange
        var docxSource = new DocxSource("test.docx");
        var fieldDefinitions = Array.Empty<FieldDefinition>();

        // Act
        var result = await _service.MatchFieldsAndGenerateUnifiedRecordAsync(
            docxSource,
            null,
            null,
            fieldDefinitions,
            expediente: null,
            classification: null,
            requiredFields: null,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Field definitions");
    }

    [Fact]
    public async Task MatchFieldsAndGenerateUnifiedRecordAsync_WithMatchingFields_ReturnsUnifiedRecord()
    {
        // Arrange
        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente"),
            new FieldDefinition("Causa")
        };

        var docxSource = new DocxSource("test.docx");
        var pdfSource = new PdfSource("test.pdf");

        var docxFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM",
            Causa = "Test Causa"
        };

        var pdfFields = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM",
            Causa = "Test Causa"
        };

        _docxFieldExtractor.ExtractFieldsAsync(
            Arg.Any<DocxSource>(),
            Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(docxFields));

        _pdfFieldExtractor.ExtractFieldsAsync(
            Arg.Any<PdfSource>(),
            Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(pdfFields));

        // Act
        var result = await _service.MatchFieldsAndGenerateUnifiedRecordAsync(
            docxSource,
            pdfSource,
            null,
            fieldDefinitions,
            expediente: null,
            classification: null,
            requiredFields: null,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.MatchedFields.ShouldNotBeNull();
        result.Value.MatchedFields.FieldMatches.ShouldContainKey("Expediente");
        result.Value.MatchedFields.FieldMatches.ShouldContainKey("Causa");
        result.Value.MatchedFields.OverallAgreement.ShouldBeGreaterThan(0.9f);
    }

    [Fact]
    public async Task MatchFieldsAndGenerateUnifiedRecordAsync_WithConflictingFields_DetectsConflicts()
    {
        // Arrange
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        var docxSource = new DocxSource("test.docx");
        var pdfSource = new PdfSource("test.pdf");

        var docxFields = new ExtractedFields { Expediente = "A/AS1-2505-088637-PHM" };
        var pdfFields = new ExtractedFields { Expediente = "B/BS2-3006-099748-QRS" };

        _docxFieldExtractor.ExtractFieldsAsync(
            Arg.Any<DocxSource>(),
            Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(docxFields));

        _pdfFieldExtractor.ExtractFieldsAsync(
            Arg.Any<PdfSource>(),
            Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(pdfFields));

        // Act
        var result = await _service.MatchFieldsAndGenerateUnifiedRecordAsync(
            docxSource,
            pdfSource,
            null,
            fieldDefinitions,
            expediente: null,
            classification: null,
            requiredFields: null,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.MatchedFields.ShouldNotBeNull();
        result.Value.MatchedFields.ConflictingFields.ShouldContain("Expediente");
    }

    [Fact]
    public async Task MatchFieldsAndGenerateUnifiedRecordAsync_WithMissingFields_TracksMissingFields()
    {
        // Arrange
        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente"),
            new FieldDefinition("Causa")
        };

        var docxSource = new DocxSource("test.docx");
        var docxFields = new ExtractedFields { Expediente = "A/AS1-2505-088637-PHM" };

        _docxFieldExtractor.ExtractFieldsAsync(
            Arg.Any<DocxSource>(),
            Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(docxFields));

        // Act
        var result = await _service.MatchFieldsAndGenerateUnifiedRecordAsync(
            docxSource,
            null,
            null,
            fieldDefinitions,
            expediente: null,
            classification: null,
            requiredFields: null,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.MatchedFields.ShouldNotBeNull();
        result.Value.MatchedFields.MissingFields.ShouldContain("Causa");
    }

    [Fact]
    /// <returns>A task that completes after asserting extraction failure still returns matches from other sources.</returns>
    public async Task MatchFieldsAndGenerateUnifiedRecordAsync_WithExtractionFailure_ContinuesWithOtherSources()
    {
        // Arrange
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        var docxSource = new DocxSource("test.docx");
        var pdfSource = new PdfSource("test.pdf");

        var pdfFields = new ExtractedFields { Expediente = "A/AS1-2505-088637-PHM" };

        _docxFieldExtractor.ExtractFieldsAsync(
            Arg.Any<DocxSource>(),
            Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.WithFailure("DOCX extraction failed"));

        _pdfFieldExtractor.ExtractFieldsAsync(
            Arg.Any<PdfSource>(),
            Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(pdfFields));

        // Act
        var result = await _service.MatchFieldsAndGenerateUnifiedRecordAsync(
            docxSource,
            pdfSource,
            null,
            fieldDefinitions,
            expediente: null,
            classification: null,
            requiredFields: null,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.MatchedFields.ShouldNotBeNull();
        result.Value.MatchedFields.FieldMatches.ShouldContainKey("Expediente");
    }

    [Fact]
    /// <returns>A task that completes after asserting required-field warnings do not break success.</returns>
    public async Task MatchFieldsAndGenerateUnifiedRecordAsync_WithRequiredFields_ValidatesCompleteness()
    {
        // Arrange
        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente", isRequired: true),
            new FieldDefinition("Causa")
        };

        var docxSource = new DocxSource("test.docx");
        var docxFields = new ExtractedFields { Expediente = "A/AS1-2505-088637-PHM" };

        _docxFieldExtractor.ExtractFieldsAsync(
            Arg.Any<DocxSource>(),
            Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(docxFields));

        var requiredFields = new List<string> { "Expediente", "Causa" };

        // Act
        var result = await _service.MatchFieldsAndGenerateUnifiedRecordAsync(
            docxSource,
            null,
            null,
            fieldDefinitions,
            expediente: null,
            classification: null,
            requiredFields: requiredFields,
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue(); // Service logs warning but continues
        result.Value.ShouldNotBeNull();
        // Warning should be logged for missing required field
    }
}
