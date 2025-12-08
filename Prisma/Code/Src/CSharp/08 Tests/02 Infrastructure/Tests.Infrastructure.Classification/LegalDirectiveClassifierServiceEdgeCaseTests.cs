namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Edge case tests for <see cref="LegalDirectiveClassifierService"/> covering null handling, cancellation, and exceptions.
/// </summary>
public class LegalDirectiveClassifierServiceEdgeCaseTests
{
    private readonly ILogger<LegalDirectiveClassifierService> _logger;
    private readonly LegalDirectiveClassifierService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="LegalDirectiveClassifierServiceEdgeCaseTests"/> class.
    /// </summary>
    public LegalDirectiveClassifierServiceEdgeCaseTests()
    {
        _logger = Substitute.For<ILogger<LegalDirectiveClassifierService>>();
        _service = new LegalDirectiveClassifierService(_logger);
    }

    /// <summary>
    /// Tests that ClassifyDirectivesAsync handles null document text correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyDirectivesAsync_WithNullDocumentText_ReturnsFailure()
    {
        // Act
        var result = await _service.ClassifyDirectivesAsync(null!, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Document text cannot be null or empty");
    }

    /// <summary>
    /// Tests that ClassifyDirectivesAsync handles empty document text correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyDirectivesAsync_WithEmptyDocumentText_ReturnsFailure()
    {
        // Act
        var result = await _service.ClassifyDirectivesAsync(string.Empty, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Document text cannot be null or empty");
    }

    /// <summary>
    /// Tests that ClassifyDirectivesAsync handles whitespace-only document text correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyDirectivesAsync_WithWhitespaceDocumentText_ReturnsFailure()
    {
        // Act
        var result = await _service.ClassifyDirectivesAsync("   ", null, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Document text cannot be null or empty");
    }

    /// <summary>
    /// Tests that ClassifyDirectivesAsync handles cancellation token correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyDirectivesAsync_WithCancellationRequested_ReturnsCancelled()
    {
        // Arrange
        var documentText = "Se ordena el BLOQUEO";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _service.ClassifyDirectivesAsync(documentText, null, cts.Token);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("cancelled");
    }

    /// <summary>
    /// Tests that DetectLegalInstrumentsAsync handles null document text correctly.
    /// </summary>
    [Fact]
    public async Task DetectLegalInstrumentsAsync_WithNullDocumentText_ReturnsFailure()
    {
        // Act
        var result = await _service.DetectLegalInstrumentsAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Document text cannot be null or empty");
    }

    /// <summary>
    /// Tests that DetectLegalInstrumentsAsync handles empty document text correctly.
    /// </summary>
    [Fact]
    public async Task DetectLegalInstrumentsAsync_WithEmptyDocumentText_ReturnsFailure()
    {
        // Act
        var result = await _service.DetectLegalInstrumentsAsync(string.Empty, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Document text cannot be null or empty");
    }

    /// <summary>
    /// Tests that DetectLegalInstrumentsAsync handles cancellation token correctly.
    /// </summary>
    [Fact]
    public async Task DetectLegalInstrumentsAsync_WithCancellationRequested_ReturnsCancelled()
    {
        // Arrange
        var documentText = "Acuerdo 105/2021";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _service.DetectLegalInstrumentsAsync(documentText, cts.Token);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("cancelled");
    }

    /// <summary>
    /// Tests that MapToComplianceActionAsync handles null directive text correctly.
    /// </summary>
    [Fact]
    public async Task MapToComplianceActionAsync_WithNullDirectiveText_ReturnsFailure()
    {
        // Act
        var result = await _service.MapToComplianceActionAsync(null!, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Directive text cannot be null or empty");
    }

    /// <summary>
    /// Tests that MapToComplianceActionAsync handles empty directive text correctly.
    /// </summary>
    [Fact]
    public async Task MapToComplianceActionAsync_WithEmptyDirectiveText_ReturnsFailure()
    {
        // Act
        var result = await _service.MapToComplianceActionAsync(string.Empty, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Directive text cannot be null or empty");
    }

    /// <summary>
    /// Tests that MapToComplianceActionAsync handles cancellation token correctly.
    /// </summary>
    [Fact]
    public async Task MapToComplianceActionAsync_WithCancellationRequested_ReturnsCancelled()
    {
        // Arrange
        var directiveText = "BLOQUEO de cuenta";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _service.MapToComplianceActionAsync(directiveText, null, cts.Token);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("cancelled");
    }

    /// <summary>
    /// Tests that ClassifyDirectivesAsync handles very long document text correctly.
    /// </summary>
    [Fact]
    public async Task ClassifyDirectivesAsync_WithVeryLongDocumentText_HandlesCorrectly()
    {
        // Arrange
        var longText = new string('A', 100000) + " BLOQUEO " + new string('B', 100000);

        // Act
        var result = await _service.ClassifyDirectivesAsync(longText, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
    }
}

