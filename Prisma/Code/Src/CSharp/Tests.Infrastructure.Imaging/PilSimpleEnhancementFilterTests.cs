using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Imaging.Filters;

namespace ExxerCube.Prisma.Tests.Infrastructure.Imaging;

/// <summary>
/// Tests for the PIL simple enhancement filter.
/// </summary>
public class PilSimpleEnhancementFilterTests
{
    private readonly ILogger<PilSimpleEnhancementFilter> _logger;
    private readonly PilSimpleEnhancementFilter _filter;

    /// <summary>
    /// Initializes the test class with test logger.
    /// </summary>
    public PilSimpleEnhancementFilterTests()
    {
        _logger = Substitute.For<ILogger<PilSimpleEnhancementFilter>>();
        _filter = new PilSimpleEnhancementFilter(_logger);
    }

    [Fact]
    public void FilterType_ShouldBePilSimple()
    {
        // Assert
        _filter.FilterType.ShouldBe(ImageFilterType.PilSimple);
    }

    [Fact]
    public void FilterName_ShouldBeCorrect()
    {
        // Assert
        _filter.FilterName.ShouldBe("PIL Simple Enhancement");
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

    [Fact
    //    (Skip = "Requires native EmguCV libraries - run manually when OpenCV is installed")
    ]
    public async Task EnhanceAsync_WithInvalidImageData_ShouldReturnFailure()
    {
        // Arrange
        var imageData = new ImageData(new byte[] { 0, 0, 0, 0 }, "test.png");
        var config = ImageFilterConfig.CreateQ2Optimized();

        // Act
        var result = await _filter.EnhanceAsync(imageData, config);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        // Note: Error message depends on whether native EmguCV libraries are installed
    }

    [Fact
        //(Skip = "Requires native EmguCV libraries - run manually when OpenCV is installed")
        ]
    public async Task EnhanceAsync_WithValidImage_ShouldReturnEnhancedImage()
    {
        // Arrange
        var imageData = new ImageData(CreateTestPngBytes(), "test.png");
        var config = ImageFilterConfig.CreateQ2Optimized();

        // Act
        var result = await _filter.EnhanceAsync(imageData, config);

        // Assert - Requires native EmguCV/OpenCV libraries
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Data.ShouldNotBeEmpty();
        result.Value!.SourcePath.ShouldBe("test.png");
    }

    [Fact]
    public async Task EnhanceAsync_WithNullImageData_ShouldThrowArgumentNullException()
    {
        // Arrange
        var config = ImageFilterConfig.CreateQ2Optimized();

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
