namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Unit tests for <see cref="MatchingPolicyService"/>.
/// </summary>
public class MatchingPolicyServiceTests
{
    private readonly ILogger<MatchingPolicyService> _logger;
    private readonly MatchingPolicyOptions _options;
    private readonly MatchingPolicyService _service;

    public MatchingPolicyServiceTests()
    {
        _logger = Substitute.For<ILogger<MatchingPolicyService>>();
        _options = new MatchingPolicyOptions
        {
            ConflictThreshold = 0.5f,
            MinimumConfidence = 0.3f,
            SourcePriority = new List<string> { "XML", "DOCX", "PDF" }
        };
        var optionsWrapper = Options.Create(_options);
        _service = new MatchingPolicyService(optionsWrapper, _logger);
    }

    [Fact]
    public async Task SelectBestValueAsync_WithSingleValue_ReturnsThatValue()
    {
        // Arrange
        var values = new List<FieldValue>
        {
            new FieldValue("Expediente", "A/AS1-2505-088637-PHM", 1.0f, "DOCX")
        };

        // Act
        var result = await _service.SelectBestValueAsync("Expediente", values);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.MatchedValue.ShouldBe("A/AS1-2505-088637-PHM");
        result.Value.Confidence.ShouldBe(1.0f);
        result.Value.HasConflict.ShouldBeFalse();
        result.Value.AgreementLevel.ShouldBe(1.0f);
    }

    [Fact]
    public async Task SelectBestValueAsync_WithMatchingValues_ReturnsBestValueWithHighAgreement()
    {
        // Arrange
        var values = new List<FieldValue>
        {
            new FieldValue("Expediente", "A/AS1-2505-088637-PHM", 1.0f, "DOCX"),
            new FieldValue("Expediente", "A/AS1-2505-088637-PHM", 0.9f, "PDF"),
            new FieldValue("Expediente", "A/AS1-2505-088637-PHM", 0.8f, "XML")
        };

        // Act
        var result = await _service.SelectBestValueAsync("Expediente", values);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.MatchedValue.ShouldBe("A/AS1-2505-088637-PHM");
        result.Value.HasConflict.ShouldBeFalse();
        result.Value.AgreementLevel.ShouldBe(1.0f);
    }

    [Fact]
    public async Task SelectBestValueAsync_WithConflictingValues_ReturnsMostCommonValue()
    {
        // Arrange
        var values = new List<FieldValue>
        {
            new FieldValue("Expediente", "A/AS1-2505-088637-PHM", 1.0f, "DOCX"),
            new FieldValue("Expediente", "A/AS1-2505-088637-PHM", 0.9f, "PDF"),
            new FieldValue("Expediente", "B/BS2-3006-099748-QRS", 0.8f, "XML")
        };

        // Act
        var result = await _service.SelectBestValueAsync("Expediente", values);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.MatchedValue.ShouldBe("A/AS1-2505-088637-PHM"); // Most common
        result.Value.HasConflict.ShouldBeTrue(); // Has conflict
        result.Value.AgreementLevel.ShouldBeLessThan(1.0f);
    }

    [Fact]
    public async Task SelectBestValueAsync_WithSourcePriority_PrefersHigherPrioritySource()
    {
        // Arrange
        var values = new List<FieldValue>
        {
            new FieldValue("Expediente", "A/AS1-2505-088637-PHM", 0.9f, "DOCX"),
            new FieldValue("Expediente", "A/AS1-2505-088637-PHM", 1.0f, "PDF"),
            new FieldValue("Expediente", "A/AS1-2505-088637-PHM", 0.8f, "XML") // XML has highest priority
        };

        // Act
        var result = await _service.SelectBestValueAsync("Expediente", values);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.MatchedValue.ShouldBe("A/AS1-2505-088637-PHM");
        result.Value.SourceType.ShouldBe("XML"); // Should prefer XML based on priority
    }

    [Fact]
    public async Task CalculateAgreementLevelAsync_WithAllMatching_ReturnsOne()
    {
        // Arrange
        var values = new List<FieldValue>
        {
            new FieldValue("Expediente", "A/AS1-2505-088637-PHM", 1.0f, "DOCX"),
            new FieldValue("Expediente", "A/AS1-2505-088637-PHM", 0.9f, "PDF")
        };

        // Act
        var result = await _service.CalculateAgreementLevelAsync(values);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(1.0f);
    }

    [Fact]
    public async Task CalculateAgreementLevelAsync_WithPartialMatching_ReturnsPartialAgreement()
    {
        // Arrange
        var values = new List<FieldValue>
        {
            new FieldValue("Expediente", "A/AS1-2505-088637-PHM", 1.0f, "DOCX"),
            new FieldValue("Expediente", "A/AS1-2505-088637-PHM", 0.9f, "PDF"),
            new FieldValue("Expediente", "B/BS2-3006-099748-QRS", 0.8f, "XML")
        };

        // Act
        var result = await _service.CalculateAgreementLevelAsync(values);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(2.0f / 3.0f); // 2 out of 3 match
    }

    [Fact]
    public async Task HasConflictAsync_WithMatchingValues_ReturnsFalse()
    {
        // Arrange
        var values = new List<FieldValue>
        {
            new FieldValue("Expediente", "A/AS1-2505-088637-PHM", 1.0f, "DOCX"),
            new FieldValue("Expediente", "A/AS1-2505-088637-PHM", 0.9f, "PDF")
        };

        // Act
        var result = await _service.HasConflictAsync(values);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task HasConflictAsync_WithConflictingValues_ReturnsTrue()
    {
        // Arrange
        var values = new List<FieldValue>
        {
            new FieldValue("Expediente", "A/AS1-2505-088637-PHM", 1.0f, "DOCX"),
            new FieldValue("Expediente", "B/BS2-3006-099748-QRS", 0.9f, "PDF")
        };

        // Act
        var result = await _service.HasConflictAsync(values);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task SelectBestValueAsync_WithEmptyValues_ReturnsFailure()
    {
        // Arrange
        var values = new List<FieldValue>();

        // Act
        var result = await _service.SelectBestValueAsync("Expediente", values);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task SelectBestValueAsync_WithNullValues_ReturnsFailure()
    {
        // Act
        var result = await _service.SelectBestValueAsync("Expediente", null!);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }
}

