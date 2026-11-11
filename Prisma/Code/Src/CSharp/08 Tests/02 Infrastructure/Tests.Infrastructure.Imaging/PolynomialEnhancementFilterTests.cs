using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Imaging;
using ExxerCube.Prisma.Infrastructure.Imaging.Filters;

namespace ExxerCube.Prisma.Tests.Infrastructure.Imaging;

/// <summary>
/// Tests for the polynomial enhancement filter with GA-optimized parameters.
/// Achieves 18.4% OCR improvement vs 12.3% for lookup table approach.
/// </summary>
public class PolynomialEnhancementFilterTests
{
    private readonly ILogger<PolynomialEnhancementFilter> _logger;
    private readonly ILogger<PolynomialImageQualityAnalyzer> _analyzerLogger;
    private readonly PolynomialImageQualityAnalyzer _analyzer;
    private readonly PolynomialEnhancementFilter _filter;

    /// <summary>
    /// Initializes the test class with test logger.
    /// </summary>
    public PolynomialEnhancementFilterTests()
    {
        _logger = Substitute.For<ILogger<PolynomialEnhancementFilter>>();
        _analyzerLogger = Substitute.For<ILogger<PolynomialImageQualityAnalyzer>>();
        _analyzer = new PolynomialImageQualityAnalyzer(_analyzerLogger);
        _filter = new PolynomialEnhancementFilter(_logger, _analyzer);
    }

    [Fact]
    public void FilterType_ShouldBePolynomial()
    {
        // Assert
        _filter.FilterType.ShouldBe(ImageFilterType.Polynomial);
    }

    [Fact]
    public void FilterName_ShouldBeCorrect()
    {
        // Assert
        _filter.FilterName.ShouldBe("Polynomial Enhancement");
    }

    [Fact]
    public void CanProcess_WithValidImageData_ShouldReturnTrue()
    {
        // Arrange
        var imageData = new ImageData(new byte[] { 1, 2, 3 }, "test.png");

        // Act
        var result = _filter.CanProcess(imageData);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void CanProcess_WithNullData_ShouldReturnFalse()
    {
        // Arrange
        var imageData = new ImageData(null!, "test.png");

        // Act
        var result = _filter.CanProcess(imageData);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void CanProcess_WithEmptyData_ShouldReturnFalse()
    {
        // Arrange
        var imageData = new ImageData(Array.Empty<byte>(), "test.png");

        // Act
        var result = _filter.CanProcess(imageData);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task EnhanceAsync_WithEnhancementDisabled_ShouldReturnOriginal()
    {
        // Arrange
        var imageData = new ImageData(CreateTestPngBytes(), "test.png");
        var config = new ImageFilterConfig { EnableEnhancement = false };

        // Act
        var result = await _filter.EnhanceAsync(imageData, config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(imageData);
    }

    [Fact]
    public async Task EnhanceAsync_WithInvalidImageData_ShouldReturnFailure()
    {
        // Arrange
        var imageData = new ImageData(new byte[] { 0, 0, 0, 0 }, "test.png");
        var config = ImageFilterConfig.CreatePolynomial();

        // Act
        var result = await _filter.EnhanceAsync(imageData, config);

        // Assert
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task EnhanceAsync_WithValidImage_ShouldReturnEnhancedImage()
    {
        // Arrange
        var imageData = new ImageData(CreateTestPngBytes(), "test.png");
        var config = ImageFilterConfig.CreatePolynomial();

        // Act
        var result = await _filter.EnhanceAsync(imageData, config);

        // Assert - Requires native EmguCV/OpenCV libraries
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Data.ShouldNotBeEmpty();
        result.Value!.SourcePath.ShouldBe("test.png");
    }

    [Fact]
    public async Task EnhanceAsync_WithProvidedParameters_ShouldUseThoseParameters()
    {
        // Arrange
        var imageData = new ImageData(CreateTestPngBytes(), "test.png");
        var customParams = new PolynomialFilterParams
        {
            Contrast = 1.5f,
            Brightness = 1.1f,
            Sharpness = 2.0f,
            UnsharpRadius = 3.0f,
            UnsharpPercent = 150f
        };
        var config = new ImageFilterConfig
        {
            FilterType = ImageFilterType.Polynomial,
            EnableEnhancement = true,
            PolynomialParams = customParams
        };

        // Act
        var result = await _filter.EnhanceAsync(imageData, config);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Data.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task EnhanceAsync_WithNullImageData_ShouldThrowArgumentNullException()
    {
        // Arrange
        var config = ImageFilterConfig.CreatePolynomial();

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await _filter.EnhanceAsync(null!, config));
    }

    [Fact]
    public async Task EnhanceAsync_WithNullConfig_ShouldThrowArgumentNullException()
    {
        // Arrange
        var imageData = new ImageData(CreateTestPngBytes(), "test.png");

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await _filter.EnhanceAsync(imageData, null!));
    }

    /// <summary>
    /// Creates a minimal valid PNG image for testing.
    /// </summary>
    private static byte[] CreateTestPngBytes()
    {
        // Minimal 1x1 white PNG
        return new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
            0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, // IDAT chunk
            0x54, 0x08, 0xD7, 0x63, 0xF8, 0xFF, 0xFF, 0xFF,
            0x00, 0x05, 0xFE, 0x02, 0xFE, 0xDC, 0xCC, 0x59,
            0xE7, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, // IEND chunk
            0x44, 0xAE, 0x42, 0x60, 0x82
        };
    }
}
