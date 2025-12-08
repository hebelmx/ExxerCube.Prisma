using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Tests.Infrastructure.FileStorage;

/// <summary>
/// Unit tests for <see cref="SafeFileNamerService"/>.
/// </summary>
public class SafeFileNamerServiceTests
{
    private readonly ILogger<SafeFileNamerService> _logger;
    private readonly SafeFileNamerService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="SafeFileNamerServiceTests"/> class.
    /// </summary>
    public SafeFileNamerServiceTests()
    {
        _logger = Substitute.For<ILogger<SafeFileNamerService>>();
        _service = new SafeFileNamerService(_logger);
    }

    /// <summary>
    /// Tests that safe file name is generated with classification prefix.
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_WithClassification_ReturnsSafeFileName()
    {
        // Arrange
        var originalFileName = "test.pdf";
        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Aseguramiento,
            Level2 = ClassificationLevel2.Especial
        };

        // Act
        var result = await _service.GenerateSafeFileNameAsync(originalFileName, classification, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNullOrEmpty();
        result.Value.ShouldContain("ASEGURAMIENTO");
        result.Value.ShouldContain("ESPECIAL");
        result.Value.ShouldEndWith(".pdf");
    }

    /// <summary>
    /// Tests that expediente number is included in safe file name when available.
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_WithExpediente_IncludesExpediente()
    {
        // Arrange
        var originalFileName = "test.pdf";
        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Aseguramiento
        };
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                NumeroExpediente = "A/AS1-2505-088637-PHM"
            }
        };

        // Act
        var result = await _service.GenerateSafeFileNameAsync(originalFileName, classification, metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain("A_AS1-2505-088637-PHM");
    }

    /// <summary>
    /// Tests that invalid characters are sanitized in file name.
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_WithInvalidCharacters_SanitizesFileName()
    {
        // Arrange
        var originalFileName = "test<file>.pdf";
        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Documentacion
        };
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                NumeroExpediente = "A/AS1-2505-088637-PHM"
            }
        };

        // Act
        var result = await _service.GenerateSafeFileNameAsync(originalFileName, classification, metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldNotContain("<");
        result.Value.ShouldNotContain(">");
    }

    /// <summary>
    /// Tests that timestamp is included in safe file name.
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_Always_IncludesTimestamp()
    {
        // Arrange
        var originalFileName = "test.pdf";
        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Aseguramiento
        };

        // Act
        var result = await _service.GenerateSafeFileNameAsync(originalFileName, classification, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        // Should contain date format yyyyMMdd-HHmmss
        result.Value.ShouldNotBeNull();
        System.Text.RegularExpressions.Regex.IsMatch(result.Value, @"\d{8}-\d{6}").ShouldBeTrue();
    }
}

