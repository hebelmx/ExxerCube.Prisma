namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Tests for <see cref="ConfigurationValidationService"/> ensuring configuration changes are validated without code modifications.
/// </summary>
public class ConfigurationValidationServiceTests
{
    private readonly ILogger<ConfigurationValidationService> _logger;
    private readonly ConfigurationValidationService _validationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationValidationServiceTests"/> class with a mocked logger.
    /// </summary>
    public ConfigurationValidationServiceTests()
    {
        _logger = Substitute.For<ILogger<ConfigurationValidationService>>();
        _validationService = new ConfigurationValidationService(_logger);
    }

    /// <summary>
    /// Tests that valid configurations pass validation.
    /// </summary>
    /// <returns>An assertion on the validation result.</returns>
    [Fact]
    public void ValidateConfiguration_ValidConfig_ReturnsSuccess()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeTrue();
        result.Value!.Errors.ShouldBeEmpty();
        result.Value!.Warnings.ShouldBeEmpty();
    }

    /// <summary>
    /// Tests that invalid OCR language is detected.
    /// </summary>
    /// <returns>An assertion on error detection.</returns>
    [Fact]
    public void ValidateConfiguration_InvalidOcrLanguage_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.OCRConfig.Language = "invalid_language";

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeFalse();
        result.Value!.Errors.ShouldContain(e => e.Contains("Invalid OCR language"));
    }

    /// <summary>
    /// Tests that invalid OEM settings are detected.
    /// </summary>
    /// <returns>An assertion on OEM validation errors.</returns>
    [Fact]
    public void ValidateConfiguration_InvalidOEM_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.OCRConfig.OEM = 5; // Invalid OEM value

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeFalse();
        result.Value!.Errors.ShouldContain(e => e.Contains("Invalid OCR Engine Mode"));
    }

    /// <summary>
    /// Tests that invalid PSM settings are detected.
    /// </summary>
    [Fact]
    public void ValidateConfiguration_InvalidPSM_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.OCRConfig.PSM = 15; // Invalid PSM value

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeFalse();
        result.Value!.Errors.ShouldContain(e => e.Contains("Invalid Page Segmentation Mode"));
    }

    /// <summary>
    /// Tests that invalid confidence threshold is detected.
    /// </summary>
    [Fact]
    public void ValidateConfiguration_InvalidConfidenceThreshold_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.OCRConfig.ConfidenceThreshold = 1.5f; // Invalid confidence value

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeFalse();
        result.Value!.Errors.ShouldContain(e => e.Contains("Invalid confidence threshold"));
    }

    /// <summary>
    /// Tests that high confidence threshold generates a warning.
    /// </summary>
    [Fact]
    public void ValidateConfiguration_HighConfidenceThreshold_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.OCRConfig.ConfidenceThreshold = 0.98f; // High confidence threshold

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeTrue(); // Still valid, just a warning
        result.Value!.Warnings.ShouldContain(w => w.Contains("High confidence threshold"));
    }

    /// <summary>
    /// Tests that low confidence threshold generates a warning.
    /// </summary>
    [Fact]
    public void ValidateConfiguration_LowConfidenceThreshold_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.OCRConfig.ConfidenceThreshold = 0.3f; // Low confidence threshold

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeTrue(); // Still valid, just a warning
        result.Value!.Warnings.ShouldContain(w => w.Contains("Low confidence threshold"));
    }

    /// <summary>
    /// Tests that invalid timeout settings are detected.
    /// </summary>
    [Fact]
    public void ValidateConfiguration_InvalidTimeout_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.TimeoutSeconds = 0; // Invalid timeout

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeFalse();
        result.Value!.Errors.ShouldContain(e => e.Contains("Timeout must be greater than 0"));
    }

    /// <summary>
    /// Tests that excessive timeout generates a warning.
    /// </summary>
    [Fact]
    public void ValidateConfiguration_ExcessiveTimeout_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.TimeoutSeconds = 4000; // Excessive timeout

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeFalse();
        result.Value!.Errors.ShouldContain(e => e.Contains("Timeout cannot exceed 3600 seconds"));
    }

    /// <summary>
    /// Tests that invalid retry settings are detected.
    /// </summary>
    [Fact]
    public void ValidateConfiguration_InvalidRetries_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.MaxRetries = -1; // Invalid retry count

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeFalse();
        result.Value!.Errors.ShouldContain(e => e.Contains("Maximum retries cannot be negative"));
    }

    /// <summary>
    /// Tests that high retry count generates a warning.
    /// </summary>
    [Fact]
    public void ValidateConfiguration_HighRetryCount_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.MaxRetries = 15; // High retry count

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeTrue(); // Still valid, just a warning
        result.Value!.Warnings.ShouldContain(w => w.Contains("High retry count"));
    }

    /// <summary>
    /// Tests that invalid output format is detected.
    /// </summary>
    [Fact]
    public void ValidateConfiguration_InvalidOutputFormat_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.OutputFormat = "invalid_format";

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeFalse();
        result.Value!.Errors.ShouldContain(e => e.Contains("Invalid output format"));
    }

    /// <summary>
    /// Tests that valid output formats are accepted.
    /// </summary>
    [Theory]
    [InlineData("json")]
    [InlineData("xml")]
    [InlineData("csv")]
    [InlineData("txt")]
    [InlineData("pdf")]
    public void ValidateConfiguration_ValidOutputFormats_ReturnsSuccess(string format)
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.OutputFormat = format;

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeTrue();
        result.Value!.Errors.ShouldBeEmpty();
    }

    /// <summary>
    /// Tests that invalid file size limit is detected.
    /// </summary>
    [Fact]
    public void ValidateConfiguration_InvalidFileSize_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.MaxFileSizeMB = 0; // Invalid file size

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeFalse();
        result.Value!.Errors.ShouldContain(e => e.Contains("Maximum file size must be greater than 0"));
    }

    /// <summary>
    /// Tests that large file size limit generates a warning.
    /// </summary>
    [Fact]
    public void ValidateConfiguration_LargeFileSize_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.MaxFileSizeMB = 150; // Large file size

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeTrue(); // Still valid, just a warning
        result.Value!.Warnings.ShouldContain(w => w.Contains("Large file size limit"));
    }

    /// <summary>
    /// Tests that invalid concurrency settings are detected.
    /// </summary>
    [Fact]
    public void ValidateConfiguration_InvalidConcurrency_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.MaxConcurrency = 0; // Invalid concurrency

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeFalse();
        result.Value!.Errors.ShouldContain(e => e.Contains("Maximum concurrency must be greater than 0"));
    }

    /// <summary>
    /// Tests that high concurrency generates a warning.
    /// </summary>
    [Fact]
    public void ValidateConfiguration_HighConcurrency_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.MaxConcurrency = 25; // High concurrency

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeTrue(); // Still valid, just a warning
        result.Value!.Warnings.ShouldContain(w => w.Contains("High concurrency"));
    }

    /// <summary>
    /// Tests that invalid batch size is detected.
    /// </summary>
    [Fact]
    public void ValidateConfiguration_InvalidBatchSize_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.BatchSize = 0; // Invalid batch size

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeFalse();
        result.Value!.Errors.ShouldContain(e => e.Contains("Batch size must be greater than 0"));
    }

    /// <summary>
    /// Tests that large batch size generates a warning.
    /// </summary>
    [Fact]
    public void ValidateConfiguration_LargeBatchSize_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.BatchSize = 150; // Large batch size

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeTrue(); // Still valid, just a warning
        result.Value!.Warnings.ShouldContain(w => w.Contains("Large batch size"));
    }

    /// <summary>
    /// Tests that invalid memory limit is detected.
    /// </summary>
    [Fact]
    public void ValidateConfiguration_InvalidMemoryLimit_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.MaxMemoryUsageMB = 0; // Invalid memory limit

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeFalse();
        result.Value!.Errors.ShouldContain(e => e.Contains("Maximum memory usage must be greater than 0"));
    }

    /// <summary>
    /// Tests that high memory limit generates a warning.
    /// </summary>
    [Fact]
    public void ValidateConfiguration_HighMemoryLimit_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.MaxMemoryUsageMB = 4096; // High memory limit

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeTrue(); // Still valid, just a warning
        result.Value!.Warnings.ShouldContain(w => w.Contains("High memory limit"));
    }

    /// <summary>
    /// Tests that multiple validation errors are collected.
    /// </summary>
    [Fact]
    public void ValidateConfiguration_MultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.OCRConfig.Language = "invalid_language";
        config.TimeoutSeconds = 0;
        config.MaxRetries = -1;

        // Act
        var result = _validationService.ValidateConfiguration(config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.IsValid.ShouldBeFalse();
        result.Value!.Errors.Count.ShouldBe(3);
        result.Value!.Errors.ShouldContain(e => e.Contains("Invalid OCR language"));
        result.Value!.Errors.ShouldContain(e => e.Contains("Timeout must be greater than 0"));
        result.Value!.Errors.ShouldContain(e => e.Contains("Maximum retries cannot be negative"));
    }

    /// <summary>
    /// Tests that default configuration is valid.
    /// </summary>
    [Fact]
    public void CreateDefaultConfiguration_ReturnsValidConfig()
    {
        // Act
        var config = ConfigurationValidationService.CreateDefaultConfiguration();

        // Assert
        config.ShouldNotBeNull();
        config.OCRConfig.Language.ShouldBe("spa");
        config.OCRConfig.OEM.ShouldBe(3);
        config.OCRConfig.PSM.ShouldBe(6);
        config.OCRConfig.FallbackLanguage.ShouldBe("eng");
        config.OCRConfig.ConfidenceThreshold.ShouldBe(0.7f);
        config.TimeoutSeconds.ShouldBe(300);
        config.MaxRetries.ShouldBe(3);
        config.OutputFormat.ShouldBe("json");
        config.MaxConcurrency.ShouldBe(5);
    }

    /// <summary>
    /// Tests that high-performance configuration is valid.
    /// </summary>
    [Fact]
    public void CreateHighPerformanceConfiguration_ReturnsValidConfig()
    {
        // Act
        var config = ConfigurationValidationService.CreateHighPerformanceConfiguration();

        // Assert
        config.ShouldNotBeNull();
        config.OCRConfig.ConfidenceThreshold.ShouldBe(0.8f);
        config.TimeoutSeconds.ShouldBe(180);
        config.MaxRetries.ShouldBe(2);
        config.MaxConcurrency.ShouldBe(10);
        config.BatchSize.ShouldBe(20);
        config.MaxMemoryUsageMB.ShouldBe(2048);
    }

    /// <summary>
    /// Tests that conservative configuration is valid.
    /// </summary>
    [Fact]
    public void CreateConservativeConfiguration_ReturnsValidConfig()
    {
        // Act
        var config = ConfigurationValidationService.CreateConservativeConfiguration();

        // Assert
        config.ShouldNotBeNull();
        config.OCRConfig.ConfidenceThreshold.ShouldBe(0.9f);
        config.TimeoutSeconds.ShouldBe(600);
        config.MaxRetries.ShouldBe(5);
        config.MaxConcurrency.ShouldBe(3);
        config.BatchSize.ShouldBe(5);
        config.MaxMemoryUsageMB.ShouldBe(512);
    }

    /// <summary>
    /// Creates a valid configuration for testing.
    /// </summary>
    /// <returns>A valid processing configuration.</returns>
    private static ProcessingConfig CreateValidConfiguration()
    {
        return new ProcessingConfig
        {
            RemoveWatermark = true,
            Deskew = true,
            Binarize = true,
            ExtractSections = true,
            NormalizeText = true,
            OCRConfig = new OCRConfig
            {
                Language = "spa",
                OEM = 3,
                PSM = 6,
                FallbackLanguage = "eng",
                ConfidenceThreshold = 0.7f
            },
            TimeoutSeconds = 300,
            MaxRetries = 3,
            RetryDelaySeconds = 5,
            OutputFormat = "json",
            MaxFileSizeMB = 50,
            MaxConcurrency = 5,
            BatchSize = 10,
            MaxMemoryUsageMB = 1024
        };
    }
}
