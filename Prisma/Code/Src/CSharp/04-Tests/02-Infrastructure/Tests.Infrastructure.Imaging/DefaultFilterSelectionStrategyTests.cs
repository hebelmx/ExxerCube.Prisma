using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Infrastructure.Imaging.Filters;

using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.Infrastructure.Imaging;

/// <summary>
/// Tests for the default filter selection strategy.
/// </summary>
public class DefaultFilterSelectionStrategyTests
{
    private readonly DefaultFilterSelectionStrategy _strategy;

    /// <summary>
    /// Initializes the test class.
    /// </summary>
    public DefaultFilterSelectionStrategyTests()
    {
        _strategy = new DefaultFilterSelectionStrategy();
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 0)]
    public void SelectFilterByQuality_ShouldReturnCorrectFilterType(
        int qualityLevelValue,
        int expectedFilterTypeValue)
    {
        // Act
        var qualityLevel = ImageQualityLevel.FromValue(qualityLevelValue);
        var expectedFilterType = ImageFilterType.FromValue(expectedFilterTypeValue);
        var config = _strategy.SelectFilterByQuality(qualityLevel);

        // Assert
        config.FilterType.ShouldBe(expectedFilterType);
    }

    [Fact]
    public void SelectFilterByQuality_ForQ2_ShouldUseNsgaIIOptimizedParams()
    {
        // Act
        var config = _strategy.SelectFilterByQuality(ImageQualityLevel.Q2_MediumPoor);

        // Assert
        config.FilterType.ShouldBe(ImageFilterType.PilSimple);
        config.EnableEnhancement.ShouldBeTrue();
        config.PilParams.ContrastFactor.ShouldBe(1.157f);
        config.PilParams.MedianSize.ShouldBe(3);
    }

    [Fact]
    public void SelectFilterByQuality_ForQ1_ShouldUseAggressiveOpenCv()
    {
        // Act
        var config = _strategy.SelectFilterByQuality(ImageQualityLevel.Q1_Poor);

        // Assert
        config.FilterType.ShouldBe(ImageFilterType.OpenCvAdvanced);
        config.EnableEnhancement.ShouldBeTrue();
        config.OpenCvParams.DenoiseH.ShouldBe(15.0f);
        config.OpenCvParams.ClaheClip.ShouldBe(4.0f);
    }

    [Fact]
    public void SelectFilterByQuality_ForPristine_ShouldDisableEnhancement()
    {
        // Act
        var config = _strategy.SelectFilterByQuality(ImageQualityLevel.Pristine);

        // Assert
        config.FilterType.ShouldBe(ImageFilterType.None);
        config.EnableEnhancement.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GetFilterConfig_ShouldReturnValidConfig(int filterTypeValue)
    {
        // Act
        var filterType = ImageFilterType.FromValue(filterTypeValue);
        var config = _strategy.GetFilterConfig(filterType);

        // Assert
        config.ShouldNotBeNull();
        config.FilterType.ShouldBe(filterType);
    }

    [Fact]
    public void SelectFilter_WithHighNoise_ShouldIncreaseMedianSize()
    {
        // Arrange
        var assessment = new ImageQualityAssessment
        {
            QualityLevel = ImageQualityLevel.Q2_MediumPoor,
            RecommendedFilter = ImageFilterType.PilSimple,
            NoiseLevel = 0.8f,
            Confidence = 0.9f
        };

        // Act
        var config = _strategy.SelectFilter(assessment);

        // Assert
        config.PilParams.MedianSize.ShouldBe(5);
    }

    [Fact]
    public void SelectFilter_WithLowContrast_ShouldIncreaseContrastFactor()
    {
        // Arrange
        var assessment = new ImageQualityAssessment
        {
            QualityLevel = ImageQualityLevel.Q2_MediumPoor,
            RecommendedFilter = ImageFilterType.PilSimple,
            ContrastLevel = 0.2f,
            Confidence = 0.9f
        };

        // Act
        var config = _strategy.SelectFilter(assessment);

        // Assert
        config.PilParams.ContrastFactor.ShouldBeGreaterThan(1.157f);
    }

    [Fact]
    public void SelectFilter_WithNullAssessment_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _strategy.SelectFilter(null!));
    }
}
