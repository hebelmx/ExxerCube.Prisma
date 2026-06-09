using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Imaging.Filters;

namespace ExxerCube.Prisma.Tests.Infrastructure.Imaging;

/// <summary>
/// Mutation-killing tests for <see cref="AnalyticalFilterSelectionStrategy"/> — the pure
/// threshold-based filter selector. Pins the two public switches, the ClassifyQualityLevel
/// ladder (reached via SelectFilter), and the RefineConfig parameter adjustments.
/// </summary>
public class AnalyticalFilterSelectionStrategyMutationTests
{
    private readonly AnalyticalFilterSelectionStrategy _strategy = new();

    private static ImageQualityAssessment A(float blur, float noise, float contrast) =>
        new() { BlurScore = blur, NoiseLevel = noise, ContrastLevel = contrast };

    // ---- SelectFilterByQuality switch ----

    [Fact]
    public void SelectFilterByQuality_Pristine_NoFilter()
    {
        var c = _strategy.SelectFilterByQuality(ImageQualityLevel.Pristine);
        c.FilterType.ShouldBe(ImageFilterType.None);
        c.EnableEnhancement.ShouldBeFalse();
    }

    [Fact]
    public void SelectFilterByQuality_Q1_AggressiveOpenCv()
    {
        var c = _strategy.SelectFilterByQuality(ImageQualityLevel.Q1_Poor);
        c.FilterType.ShouldBe(ImageFilterType.OpenCvAdvanced);
        c.EnableEnhancement.ShouldBeTrue();
        c.OpenCvParams.DenoiseH.ShouldBe(5f);
        c.OpenCvParams.ClaheClip.ShouldBe(1.05f);
        c.OpenCvParams.UnsharpAmount.ShouldBe(1.0f);
    }

    [Fact]
    public void SelectFilterByQuality_Q2_PilNsga()
    {
        var c = _strategy.SelectFilterByQuality(ImageQualityLevel.Q2_MediumPoor);
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(1.1573620712395511f, 1e-6f);
        c.PilParams.MedianSize.ShouldBe(3);
    }

    [Fact]
    public void SelectFilterByQuality_Q3_PilStronger()
    {
        var c = _strategy.SelectFilterByQuality(ImageQualityLevel.Q3_Low);
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(1.5f);
        c.PilParams.MedianSize.ShouldBe(5);
    }

    [Fact]
    public void SelectFilterByQuality_Q4_PilMax()
    {
        var c = _strategy.SelectFilterByQuality(ImageQualityLevel.Q4_VeryLow);
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(2.0f);
        c.PilParams.MedianSize.ShouldBe(7);
    }

    [Fact]
    public void SelectFilterByQuality_UnknownValue_DefaultsToQ2()
    {
        // Unknown (-1) hits the default arm -> Q2 config
        var c = _strategy.SelectFilterByQuality(ImageQualityLevel.Unknown);
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(1.1573620712395511f, 1e-6f);
        c.PilParams.MedianSize.ShouldBe(3);
    }

    // ---- GetFilterConfig switch ----

    [Fact]
    public void GetFilterConfig_None_Pristine()
    {
        var c = _strategy.GetFilterConfig(ImageFilterType.None);
        c.FilterType.ShouldBe(ImageFilterType.None);
        c.EnableEnhancement.ShouldBeFalse();
    }

    [Fact]
    public void GetFilterConfig_PilSimple_Q2()
    {
        var c = _strategy.GetFilterConfig(ImageFilterType.PilSimple);
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(1.1573620712395511f, 1e-6f);
    }

    [Fact]
    public void GetFilterConfig_OpenCv_Q1()
    {
        var c = _strategy.GetFilterConfig(ImageFilterType.OpenCvAdvanced);
        c.FilterType.ShouldBe(ImageFilterType.OpenCvAdvanced);
        c.OpenCvParams.DenoiseH.ShouldBe(5f);
        c.OpenCvParams.ClaheClip.ShouldBe(1.05f);
    }

    [Fact]
    public void GetFilterConfig_Adaptive_ReturnsAdaptive()
    {
        var c = _strategy.GetFilterConfig(ImageFilterType.Adaptive);
        c.FilterType.ShouldBe(ImageFilterType.Adaptive);
    }

    [Fact]
    public void GetFilterConfig_UnknownType_DefaultsToQ2()
    {
        var c = _strategy.GetFilterConfig(ImageFilterType.Polynomial); // value 4 -> default
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(1.1573620712395511f, 1e-6f);
    }

    // ---- SelectFilter -> ClassifyQualityLevel ----

    [Fact]
    public void SelectFilter_Null_Throws()
    {
        Should.Throw<ArgumentNullException>(() => _strategy.SelectFilter(null!));
    }

    [Fact]
    public void SelectFilter_PristineMetrics_NoFilter()
    {
        // Blur>3500 && Noise<0.6 && Contrast>35 -> Pristine -> None (RefineConfig leaves None alone)
        var c = _strategy.SelectFilter(A(blur: 4000, noise: 0.5f, contrast: 40));
        c.FilterType.ShouldBe(ImageFilterType.None);
        c.EnableEnhancement.ShouldBeFalse();
    }

    [Fact]
    public void SelectFilter_PristineBlurButHighNoise_FallsToQ1()
    {
        // fails the Noise<0.6 clause -> not Pristine; Noise<4.5 && Blur>1500 && Contrast>28 -> Q1.
        // Kills the '&&' (an || would still classify Pristine) and the Noise clause.
        var c = _strategy.SelectFilter(A(blur: 4000, noise: 1.0f, contrast: 40));
        c.FilterType.ShouldBe(ImageFilterType.OpenCvAdvanced);
    }

    [Fact]
    public void SelectFilter_PristineBlurButLowContrast_FallsToQ1()
    {
        // fails the Contrast>35 clause -> not Pristine; still Q1.
        var c = _strategy.SelectFilter(A(blur: 4000, noise: 0.5f, contrast: 30));
        c.FilterType.ShouldBe(ImageFilterType.OpenCvAdvanced);
    }

    [Fact]
    public void SelectFilter_Q1Metrics_OpenCvWithNoiseAdjustedDenoise()
    {
        // Q1 (Noise 4 < 4.5, Blur 2000 > 1500, Contrast 30 > 28) -> OpenCv;
        // RefineConfig OpenCv: Noise 4 > 3 -> DenoiseH = Min(5+5,15) = 10.
        var c = _strategy.SelectFilter(A(blur: 2000, noise: 4.0f, contrast: 30));
        c.FilterType.ShouldBe(ImageFilterType.OpenCvAdvanced);
        c.OpenCvParams.DenoiseH.ShouldBe(10f);
    }

    [Fact]
    public void SelectFilter_Q1LowNoise_OpenCvDenoiseUnchanged()
    {
        // Noise 2 not > 3 -> DenoiseH stays 5 (pins the Noise>3 RefineConfig branch).
        var c = _strategy.SelectFilter(A(blur: 2000, noise: 2.0f, contrast: 30));
        c.OpenCvParams.DenoiseH.ShouldBe(5f);
    }

    [Fact]
    public void SelectFilter_Q2LowContrast_PilContrastIncreasedAndMedianFive()
    {
        // not Q1 (Noise 6 >= 4.5); Q2 (Noise 6 < 8 && Blur 1200 > 1000) -> Pil.
        // RefineConfig: Contrast 20 < 25 -> CF = Min(1.15736*1.3, 2.5) = 1.504571; Noise 6 > 5 -> Median 5.
        var c = _strategy.SelectFilter(A(blur: 1200, noise: 6.0f, contrast: 20));
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(1.1573620712395511f * 1.3f, 1e-5f);
        c.PilParams.MedianSize.ShouldBe(5);
    }

    [Fact]
    public void SelectFilter_Q2GoodContrast_PilContrastReduced()
    {
        // Contrast 40 > 35 -> CF = Max(1.15736*0.9, 1.0) = 1.041626.
        var c = _strategy.SelectFilter(A(blur: 1200, noise: 6.0f, contrast: 40));
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(1.1573620712395511f * 0.9f, 1e-5f);
    }

    [Fact]
    public void SelectFilter_Q3HighNoise_PilMedianSeven()
    {
        // not Q2 (Noise 10 not < 8); Q3 (Noise 10 < 12) -> Q3 config (Pil, CF 1.5, Median 5);
        // RefineConfig: Contrast 20 < 25 -> CF = Min(1.5*1.3, 2.5) = 1.95; Noise 10 > 8 -> Median 7.
        var c = _strategy.SelectFilter(A(blur: 1200, noise: 10.0f, contrast: 20));
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(1.95f, 1e-5f);
        c.PilParams.MedianSize.ShouldBe(7);
    }

    [Fact]
    public void SelectFilter_Q4ExtremeNoise_PilMax()
    {
        // Noise 15 not < 12 -> Q4 (Pil, CF 2.0, Median 7). RefineConfig: Contrast 20 < 25 ->
        // CF = Min(2.0*1.3, 2.5) = 2.5 (the upper Min clamp); Noise 15 > 8 -> Median 7.
        var c = _strategy.SelectFilter(A(blur: 500, noise: 15.0f, contrast: 20));
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(2.5f, 1e-5f); // Min(2.6, 2.5)
        c.PilParams.MedianSize.ShouldBe(7);
    }
}
