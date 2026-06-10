using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Imaging.Strategies;

namespace ExxerCube.Prisma.Tests.Infrastructure.Imaging;

/// <summary>
/// Mutation-killing tests for <see cref="PolynomialFilterSelectionStrategy"/>. It uses stub
/// polynomial models (empty coefficients -> range midpoint), so every predicted parameter is a
/// deterministic constant: PIL CF=1.75, median=round(4)->even->5; OpenCv DenoiseH=10.5,
/// ClaheClip=4.5, BilateralD=9, Sigma=80, UnsharpAmount=1.5, UnsharpRadius=2.75.
/// </summary>
public class PolynomialFilterSelectionStrategyMutationTests
{
    private readonly PolynomialFilterSelectionStrategy _strategy =
        new(Substitute.For<ILogger<PolynomialFilterSelectionStrategy>>());

    private static ImageQualityAssessment A(float blur, float noise, float contrast, float sharpness = 0f) =>
        new() { BlurScore = blur, NoiseLevel = noise, ContrastLevel = contrast, SharpnessLevel = sharpness };

    // ---- SelectFilter -> SelectFilterType heuristic ----

    [Fact]
    public void SelectFilter_Null_Throws()
    {
        Should.Throw<ArgumentNullException>(() => _strategy.SelectFilter(null!));
    }

    [Fact]
    public void SelectFilter_PristineFeatures_SelectsNone()
    {
        // blur 4000/5000=0.8>0.7, noise 0.05<0.1, contrast 0.8>0.6 -> None
        var c = _strategy.SelectFilter(A(blur: 4000, noise: 0.05f, contrast: 0.8f));
        c.FilterType.ShouldBe(ImageFilterType.None);
        c.EnableEnhancement.ShouldBeFalse();
    }

    [Fact]
    public void SelectFilter_SharpButNoisy_NotNone_FallsToOpenCv()
    {
        // noise 0.2 fails the <0.1 clause -> not None; noise 0.2 not>0.5 and blur 0.8 not<0.3
        // -> Pil. Kills the noise<0.1 clause of the None test.
        var c = _strategy.SelectFilter(A(blur: 4000, noise: 0.2f, contrast: 0.8f));
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
    }

    [Fact]
    public void SelectFilter_SharpLowContrast_NotNone()
    {
        // contrast 0.5 fails >0.6 -> not None -> Pil. Kills the contrast>0.6 clause.
        var c = _strategy.SelectFilter(A(blur: 4000, noise: 0.05f, contrast: 0.5f));
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
    }

    [Fact]
    public void SelectFilter_LowBlur_SelectsOpenCvWithPredictedParams()
    {
        // blur 1000/5000=0.2<0.3 -> OpenCv. Pins the stub-predicted OpenCv params.
        var c = _strategy.SelectFilter(A(blur: 1000, noise: 0.05f, contrast: 0.5f));
        c.FilterType.ShouldBe(ImageFilterType.OpenCvAdvanced);
        c.EnableEnhancement.ShouldBeTrue();
        c.OpenCvParams.DenoiseH.ShouldBe(10.5f, 1e-4f);
        c.OpenCvParams.ClaheClip.ShouldBe(4.5f, 1e-4f);
        c.OpenCvParams.BilateralD.ShouldBe(9);
        c.OpenCvParams.SigmaColor.ShouldBe(80f, 1e-3f);
        c.OpenCvParams.SigmaSpace.ShouldBe(80f, 1e-3f);
        c.OpenCvParams.UnsharpAmount.ShouldBe(1.5f, 1e-4f);
        c.OpenCvParams.UnsharpRadius.ShouldBe(2.75f, 1e-4f);
    }

    [Fact]
    public void SelectFilter_HighNoise_SelectsOpenCv_KillsOrShortCircuit()
    {
        // blur 2500/5000=0.5 (not <0.3) but noise 0.6>0.5 -> OpenCv via the || clause.
        var c = _strategy.SelectFilter(A(blur: 2500, noise: 0.6f, contrast: 0.5f));
        c.FilterType.ShouldBe(ImageFilterType.OpenCvAdvanced);
    }

    [Fact]
    public void SelectFilter_ModerateFeatures_SelectsPilWithPredictedParams()
    {
        // blur 0.5 (not>0.7, not<0.3), noise 0.2 (not>0.5), contrast 0.3 -> Pil.
        var c = _strategy.SelectFilter(A(blur: 2500, noise: 0.2f, contrast: 0.3f));
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.EnableEnhancement.ShouldBeTrue();
        c.PilParams.ContrastFactor.ShouldBe(1.75f, 1e-4f);  // midpoint of [0.5,3.0]
        c.PilParams.MedianSize.ShouldBe(5);                 // round(4)->even->++ ->5
    }

    // ---- SelectFilterByQuality switch ----

    [Fact]
    public void SelectFilterByQuality_Pristine_None()
    {
        _strategy.SelectFilterByQuality(ImageQualityLevel.Pristine).FilterType.ShouldBe(ImageFilterType.None);
    }

    [Fact]
    public void SelectFilterByQuality_Q1_OpenCvDefault()
    {
        var c = _strategy.SelectFilterByQuality(ImageQualityLevel.Q1_Poor);
        c.FilterType.ShouldBe(ImageFilterType.OpenCvAdvanced);
        c.OpenCvParams.DenoiseH.ShouldBe(10.0f); // CreateDefault
        c.OpenCvParams.ClaheClip.ShouldBe(2.0f);
        c.OpenCvParams.BilateralD.ShouldBe(9);
    }

    [Fact]
    public void SelectFilterByQuality_Q2_PilDefault()
    {
        var c = _strategy.SelectFilterByQuality(ImageQualityLevel.Q2_MediumPoor);
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(1.157f);
        c.PilParams.MedianSize.ShouldBe(3);
    }

    [Fact]
    public void SelectFilterByQuality_Q3_PilDefault()
    {
        _strategy.SelectFilterByQuality(ImageQualityLevel.Q3_Low).FilterType.ShouldBe(ImageFilterType.PilSimple);
    }

    [Fact]
    public void SelectFilterByQuality_Q4_None()
    {
        _strategy.SelectFilterByQuality(ImageQualityLevel.Q4_VeryLow).FilterType.ShouldBe(ImageFilterType.None);
    }

    [Fact]
    public void SelectFilterByQuality_Unknown_PilDefault()
    {
        var c = _strategy.SelectFilterByQuality(ImageQualityLevel.Unknown);
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(1.157f);
    }

    // ---- GetFilterConfig switch ----

    [Fact]
    public void GetFilterConfig_None()
    {
        _strategy.GetFilterConfig(ImageFilterType.None).FilterType.ShouldBe(ImageFilterType.None);
    }

    [Fact]
    public void GetFilterConfig_PilSimple_Default()
    {
        var c = _strategy.GetFilterConfig(ImageFilterType.PilSimple);
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(1.157f);
    }

    [Fact]
    public void GetFilterConfig_OpenCv_Default()
    {
        var c = _strategy.GetFilterConfig(ImageFilterType.OpenCvAdvanced);
        c.FilterType.ShouldBe(ImageFilterType.OpenCvAdvanced);
        c.OpenCvParams.DenoiseH.ShouldBe(10.0f);
    }

    [Fact]
    public void GetFilterConfig_Adaptive()
    {
        _strategy.GetFilterConfig(ImageFilterType.Adaptive).FilterType.ShouldBe(ImageFilterType.Adaptive);
    }

    [Fact]
    public void GetFilterConfig_Unknown_PilDefault()
    {
        var c = _strategy.GetFilterConfig(ImageFilterType.Polynomial); // value 4 -> default
        c.FilterType.ShouldBe(ImageFilterType.PilSimple);
        c.PilParams.ContrastFactor.ShouldBe(1.157f);
    }

    [Fact]
    public void GetFilterConfig_OpenCv_EnhancementEnabled() =>
        // CreateOpenCvConfigDefault: kills `EnableEnhancement = true` -> false.
        _strategy.GetFilterConfig(ImageFilterType.OpenCvAdvanced).EnableEnhancement.ShouldBeTrue();

    [Fact]
    public void SelectFilterByQuality_Q1_OpenCvEnhancementEnabled() =>
        _strategy.SelectFilterByQuality(ImageQualityLevel.Q1_Poor).EnableEnhancement.ShouldBeTrue();
}
