using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Imaging.Filters;

namespace ExxerCube.Prisma.Tests.Infrastructure.Imaging;

/// <summary>
/// Mutation-killing supplement for <see cref="DefaultFilterSelectionStrategy"/>. The existing
/// DefaultFilterSelectionStrategyTests assert filter *types*; these pin the exact parameter
/// values per switch arm and the SelectFilter adjustment logic (noise/contrast).
/// </summary>
public class DefaultFilterSelectionStrategyMutationTests
{
    private readonly DefaultFilterSelectionStrategy _strategy = new();

    private static ImageQualityAssessment A(ImageFilterType recommended, float noise = 0f, float contrast = 1f) =>
        new() { RecommendedFilter = recommended, NoiseLevel = noise, ContrastLevel = contrast };

    // ---- SelectFilterByQuality param values ----

    [Fact]
    public void SelectFilterByQuality_Q3_LightPil()
    {
        var c = _strategy.SelectFilterByQuality(ImageQualityLevel.Q3_Low);
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(1.1f);
        c.PilParams.MedianSize.ShouldBe(1);
    }

    [Fact]
    public void SelectFilterByQuality_Q4_PassThrough()
    {
        var c = _strategy.SelectFilterByQuality(ImageQualityLevel.Q4_VeryLow);
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(1.0f);
        c.PilParams.MedianSize.ShouldBe(1);
    }

    [Fact]
    public void SelectFilterByQuality_Q1_Aggressive()
    {
        var c = _strategy.SelectFilterByQuality(ImageQualityLevel.Q1_Poor);
        c.FilterType.ShouldBe(ImageFilterType.OpenCvAdvanced);
        c.OpenCvParams.DenoiseH.ShouldBe(15.0f);
        c.OpenCvParams.ClaheClip.ShouldBe(4.0f);
        c.OpenCvParams.BilateralD.ShouldBe(11);
    }

    [Fact]
    public void SelectFilterByQuality_Unknown_DefaultsToQ2()
    {
        var c = _strategy.SelectFilterByQuality(ImageQualityLevel.Unknown);
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(1.157f);
        c.PilParams.MedianSize.ShouldBe(3);
    }

    // ---- GetFilterConfig param values ----

    [Fact]
    public void GetFilterConfig_None_Disabled()
    {
        var c = _strategy.GetFilterConfig(ImageFilterType.None);
        c.FilterType.ShouldBe(ImageFilterType.None);
        c.EnableEnhancement.ShouldBeFalse();
    }

    [Fact]
    public void GetFilterConfig_PilSimple_Q2Optimized()
    {
        var c = _strategy.GetFilterConfig(ImageFilterType.PilSimple);
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(1.157f);
        c.PilParams.MedianSize.ShouldBe(3);
    }

    [Fact]
    public void GetFilterConfig_OpenCv_Default()
    {
        var c = _strategy.GetFilterConfig(ImageFilterType.OpenCvAdvanced);
        c.FilterType.ShouldBe(ImageFilterType.OpenCvAdvanced);
        c.EnableEnhancement.ShouldBeTrue();
        c.OpenCvParams.DenoiseH.ShouldBe(10.0f); // CreateDefault, not aggressive 15
        c.OpenCvParams.ClaheClip.ShouldBe(2.0f);
    }

    [Fact]
    public void GetFilterConfig_Adaptive()
    {
        _strategy.GetFilterConfig(ImageFilterType.Adaptive).FilterType.ShouldBe(ImageFilterType.Adaptive);
    }

    [Fact]
    public void GetFilterConfig_Unknown_DefaultsToQ2()
    {
        var c = _strategy.GetFilterConfig(ImageFilterType.Polynomial); // value 4 -> default
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(1.157f);
    }

    // ---- SelectFilter adjustments ----

    [Fact]
    public void SelectFilter_HighNoisePil_MedianBecomesFive()
    {
        // RecommendedFilter PilSimple + NoiseLevel 0.8 > 0.7 -> Median 5; contrast 1 (>0.3) untouched.
        var c = _strategy.SelectFilter(A(ImageFilterType.PilSimple, noise: 0.8f, contrast: 1.0f));
        c.PilParams.MedianSize.ShouldBe(5);
    }

    [Fact]
    public void SelectFilter_HighNoiseButNotPil_MedianUnchanged()
    {
        // NoiseLevel 0.8 > 0.7 but RecommendedFilter OpenCv -> the '&&' is false, Median stays 3.
        // Kills '&&' -> '||' (an || would set Median 5 here).
        var c = _strategy.SelectFilter(A(ImageFilterType.OpenCvAdvanced, noise: 0.8f, contrast: 1.0f));
        c.PilParams.MedianSize.ShouldBe(3);
    }

    [Fact]
    public void SelectFilter_LowNoisePil_MedianUnchanged()
    {
        // NoiseLevel 0.5 not > 0.7 -> Median stays 3 (pins the > threshold).
        var c = _strategy.SelectFilter(A(ImageFilterType.PilSimple, noise: 0.5f, contrast: 1.0f));
        c.PilParams.MedianSize.ShouldBe(3);
    }

    [Fact]
    public void SelectFilter_LowContrast_ContrastFactorScaledExactly()
    {
        // ContrastLevel 0.2 < 0.3 -> CF = Min(1.157*1.2, 2.5) = 1.3884.
        // Asserting the exact value kills the Min->Max mutant (Max would give 2.5).
        var c = _strategy.SelectFilter(A(ImageFilterType.PilSimple, noise: 0f, contrast: 0.2f));
        c.PilParams.ContrastFactor.ShouldBe(1.157f * 1.2f, 1e-5f);
    }

    [Fact]
    public void SelectFilter_GoodContrast_ContrastFactorUnchanged()
    {
        // ContrastLevel 0.5 not < 0.3 -> CF stays 1.157 (pins the < threshold).
        var c = _strategy.SelectFilter(A(ImageFilterType.PilSimple, noise: 0f, contrast: 0.5f));
        c.PilParams.ContrastFactor.ShouldBe(1.157f);
    }
}
