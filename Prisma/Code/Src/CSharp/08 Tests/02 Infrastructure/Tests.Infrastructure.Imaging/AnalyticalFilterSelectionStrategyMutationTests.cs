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

    // ---- ClassifyQualityLevel threshold BOUNDARIES (kill the `>=`/`<=` relational mutants) ----
    // Each test sits a metric EXACTLY on a threshold T where the real `>`/`<` is false but the
    // mutated `>=`/`<=` would be true, flipping the classification. The opposite (`<`/`>`) mutants
    // are already killed by the far-from-boundary tests above. FilterType / ContrastFactor isolates the flip.

    [Fact]
    public void Boundary_PristineBlur_AtThreshold_NotPristine() =>
        // Blur 3500 == PristineBlurThreshold -> `>` false -> Q1 (OpenCv), not Pristine (None).
        _strategy.SelectFilter(A(3500f, 0f, 40f)).FilterType.ShouldBe(ImageFilterType.OpenCvAdvanced);

    [Fact]
    public void Boundary_PristineNoise_AtThreshold_NotPristine() =>
        // Noise 0.6 == PristineNoiseThreshold -> `<` false -> Q1 (OpenCv).
        _strategy.SelectFilter(A(4000f, 0.6f, 40f)).FilterType.ShouldBe(ImageFilterType.OpenCvAdvanced);

    [Fact]
    public void Boundary_PristineContrast_AtThreshold_NotPristine() =>
        // Contrast 35 == GoodContrastThreshold -> `>` false -> Q1 (OpenCv).
        _strategy.SelectFilter(A(4000f, 0f, 35f)).FilterType.ShouldBe(ImageFilterType.OpenCvAdvanced);

    [Fact]
    public void Boundary_Q1Noise_AtThreshold_FallsToQ2() =>
        // Noise 4.5 == LightNoiseThreshold -> Q1 `<` false -> Q2 (Pil).
        _strategy.SelectFilter(A(2000f, 4.5f, 40f)).FilterType.ShouldBe(ImageFilterType.PilSimple);

    [Fact]
    public void Boundary_Q1Blur_AtThreshold_FallsToQ2() =>
        // Blur 1500 == GoodBlurThreshold -> Q1 `>` false -> Q2 (Pil).
        _strategy.SelectFilter(A(1500f, 0f, 40f)).FilterType.ShouldBe(ImageFilterType.PilSimple);

    [Fact]
    public void Boundary_Q1Contrast_AtThreshold_FallsToQ2() =>
        // Contrast 28 == PoorContrastThreshold -> Q1 `>` false -> Q2 (Pil).
        _strategy.SelectFilter(A(2000f, 0f, 28f)).FilterType.ShouldBe(ImageFilterType.PilSimple);

    [Fact]
    public void Boundary_Q2Noise_AtThreshold_FallsToQ3()
    {
        // Noise 8 == ModerateNoiseThreshold -> Q2 `<` false -> Q3 (Pil, base CF 1.5; contrast 30 -> no adjust).
        var c = _strategy.SelectFilter(A(1200f, 8f, 30f));
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(1.5f, 1e-5f); // Q3 base, distinguishes from Q2's 1.157
    }

    [Fact]
    public void Boundary_Q2Blur_AtThreshold_FallsToQ3()
    {
        // Blur 1000 == the Q2 blur literal -> Q2 `>` false -> Q3 (CF 1.5).
        var c = _strategy.SelectFilter(A(1000f, 0f, 30f));
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(1.5f, 1e-5f);
    }

    [Fact]
    public void Boundary_Q3Noise_AtThreshold_FallsToQ4()
    {
        // Noise 12 == the Q3 noise literal -> Q3 `<` false -> Q4 (CF 2.0).
        var c = _strategy.SelectFilter(A(500f, 12f, 30f));
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(2.0f, 1e-5f); // Q4 base, distinguishes from Q3's 1.5
    }

    // ---- RefineConfig threshold BOUNDARIES ----

    [Fact]
    public void Boundary_RefineContrastLow_AtThreshold_NoIncrease()
    {
        // Contrast 25 == 25 -> `< 25` false -> NO contrast increase (CF stays Q2 base).
        var c = _strategy.SelectFilter(A(1200f, 6f, 25f));
        c.PilParams.ContrastFactor.ShouldBe(1.1573620712395511f, 1e-5f);
    }

    [Fact]
    public void Boundary_RefineContrastHigh_AtThreshold_NoDecrease()
    {
        // Contrast 35 == 35 -> `> 35` false -> NO contrast decrease (CF stays Q2 base).
        var c = _strategy.SelectFilter(A(1200f, 6f, 35f));
        c.PilParams.ContrastFactor.ShouldBe(1.1573620712395511f, 1e-5f);
    }

    [Fact]
    public void Boundary_RefineNoiseHigh_AtThreshold_MedianFiveNotSeven()
    {
        // Noise 8 -> Q3 (CF 1.5, base Median 5); RefineConfig `> 8` false -> stays Median 5 (not 7).
        var c = _strategy.SelectFilter(A(1200f, 8f, 30f));
        c.PilParams.MedianSize.ShouldBe(5);
    }

    [Fact]
    public void Boundary_RefineNoiseMid_AtThreshold_MedianThreeNotFive()
    {
        // Noise 5 -> Q2 (base Median 3); RefineConfig `> 5` false -> stays Median 3 (not 5).
        var c = _strategy.SelectFilter(A(1200f, 5f, 30f));
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.MedianSize.ShouldBe(3);
    }

    [Fact]
    public void Boundary_RefineOpenCvNoise_AtThreshold_DenoiseUnchanged()
    {
        // Noise 3 -> Q1 (OpenCv); RefineConfig `> 3` false -> DenoiseH stays 5 (not 10).
        var c = _strategy.SelectFilter(A(2000f, 3f, 40f));
        c.FilterType.ShouldBe(ImageFilterType.OpenCvAdvanced);
        c.OpenCvParams.DenoiseH.ShouldBe(5f);
    }
}
