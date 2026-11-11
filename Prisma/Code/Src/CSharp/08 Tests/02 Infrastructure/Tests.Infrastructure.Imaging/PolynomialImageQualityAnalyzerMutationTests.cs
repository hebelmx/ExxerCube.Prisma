using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Imaging;

namespace ExxerCube.Prisma.Tests.Infrastructure.Imaging;

/// <summary>
/// Mutation-killing tests for <see cref="PolynomialImageQualityAnalyzer"/>.
/// The class wraps native EmguCV feature extraction, but for clean structured images the
/// extracted features are exactly deterministic:
///   half-of-width 0 | other-half v (50x50)  ->  Blur = v²/25, Contrast = v/2,
///   NoiseEstimate = v/25, EdgeDensity = 0.02 (one boundary column = 50 of 2500 px).
/// That lets every deterministic piece be pinned to an exact value: the Compute* helpers
/// (variance/stddev/mean-abs/count-nonzero) via the returned features, the DetermineQualityLevel
/// blur-score ladder, the [0,1] normalization formulas, the diagnostics map, and the guards.
/// </summary>
public class PolynomialImageQualityAnalyzerMutationTests
{
    private readonly PolynomialImageQualityAnalyzer _analyzer =
        new(Substitute.For<ILogger<PolynomialImageQualityAnalyzer>>());

    // ---- image builders (deterministic) ----

    private static Mat Flat(byte v, int w = 50, int h = 50)
    {
        var img = new Image<Gray, byte>(w, h);
        img.SetValue(new Gray(v));
        return img.Mat;
    }

    private static Mat HalfHalf(byte left, byte right, int w = 50, int h = 50)
    {
        var img = new Image<Gray, byte>(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                img.Data[y, x, 0] = x < w / 2 ? left : right;
        return img.Mat;
    }

    private static Mat Stripes(byte a, byte b, int w = 50, int h = 50)
    {
        var img = new Image<Gray, byte>(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                img.Data[y, x, 0] = (x % 2 == 0) ? a : b;
        return img.Mat;
    }

    private static byte[] Png(Mat mat)
    {
        using var buffer = new VectorOfByte();
        CvInvoke.Imencode(".png", mat, buffer);
        return buffer.ToArray();
    }

    // ---- ExtractFeatures (direct, single-channel CopyTo branch) ----

    [Fact]
    public void ExtractFeatures_Null_Throws()
    {
        Should.Throw<ArgumentNullException>(() => _analyzer.ExtractFeatures(null!));
    }

    [Fact]
    public void ExtractFeatures_EmptyMat_ThrowsArgumentException()
    {
        using var empty = new Mat();
        var ex = Should.Throw<ArgumentException>(() => _analyzer.ExtractFeatures(empty));
        ex.Message.ShouldContain("Image is empty");
    }

    [Fact]
    public void ExtractFeatures_FlatImage_AllZero()
    {
        var f = _analyzer.ExtractFeatures(Flat(128));

        f.BlurScore.ShouldBe(0.0, 1e-9);      // ComputeVariance of constant Laplacian = 0
        f.Contrast.ShouldBe(0.0, 1e-9);       // stddev of uniform image = 0
        f.NoiseEstimate.ShouldBe(0.0, 1e-9);  // mean |Laplacian| = 0
        f.EdgeDensity.ShouldBe(0.0, 1e-9);    // no Canny edges
    }

    [Fact]
    public void ExtractFeatures_HalfHalf_ProducesExactFeatureValues()
    {
        // half 0|100 over 50x50: Blur 100²/25=400, Contrast 100/2=50, Noise 100/25=4,
        // Edge 50/2500=0.02. Pins ComputeVariance, ComputeStdDev, ComputeMeanAbsolute,
        // CountNonZero and the edgePixels/(rows*cols) division all at once.
        var f = _analyzer.ExtractFeatures(HalfHalf(0, 100));

        f.BlurScore.ShouldBe(400.0, 1e-6);
        f.Contrast.ShouldBe(50.0, 1e-6);
        f.NoiseEstimate.ShouldBe(4.0, 1e-6);
        f.EdgeDensity.ShouldBe(0.02, 1e-9);
    }

    [Fact]
    public void ExtractFeatures_BrighterHalfHalf_ScalesFeatures()
    {
        // half 0|200: Blur 200²/25=1600, Contrast 100, Noise 8 - a second point so the
        // Compute* arithmetic can't be a constant.
        var f = _analyzer.ExtractFeatures(HalfHalf(0, 200));

        f.BlurScore.ShouldBe(1600.0, 1e-6);
        f.Contrast.ShouldBe(100.0, 1e-6);
        f.NoiseEstimate.ShouldBe(8.0, 1e-6);
    }

    // ---- AnalyzeAsync (decoded 3-channel image -> CvtColor branch) ----

    [Fact]
    public async Task AnalyzeAsync_Null_Throws()
    {
        await Should.ThrowAsync<ArgumentNullException>(() => _analyzer.AnalyzeAsync(null!));
    }

    [Fact]
    public async Task AnalyzeAsync_UndecodableBytes_ReturnsFailure()
    {
        var result = await _analyzer.AnalyzeAsync(new ImageData(new byte[] { 0, 0, 0, 0 }, "x.png"));

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldContain("Failed to decode image");
    }

    [Fact]
    public async Task AnalyzeAsync_HalfHalfImage_ProducesAssessmentWithExactNormalizedValues()
    {
        var result = await _analyzer.AnalyzeAsync(new ImageData(Png(HalfHalf(0, 100)), "x.png"));

        result.IsSuccess.ShouldBeTrue();
        var a = result.Value!;
        a.QualityLevel.ShouldBe(ImageQualityLevel.Q2_MediumPoor); // blur 400 in [200,500)
        a.Confidence.Value.ShouldBe(0.9, 1e-5);
        a.NoiseLevel.ShouldBe(0.08f, 1e-5f);        // Min(1, 4/50)
        a.ContrastLevel.ShouldBe(50f / 60f, 1e-5f); // Min(1, 50/60) = 0.8333
        a.SharpnessLevel.ShouldBe(0.2f, 1e-5f);     // Min(1, 0.02*10)
        a.BlurScore.ShouldBe(400f, 1e-3f);

        ((double)a.Diagnostics["blur_score"]).ShouldBe(400.0, 1e-6);
        ((double)a.Diagnostics["contrast"]).ShouldBe(50.0, 1e-6);
        ((double)a.Diagnostics["noise_estimate"]).ShouldBe(4.0, 1e-6);
        ((double)a.Diagnostics["edge_density"]).ShouldBe(0.02, 1e-9);
        a.Diagnostics.ContainsKey("predicted_params").ShouldBeTrue();
        a.Diagnostics["predicted_params"].ShouldBeOfType<PolynomialFilterParams>();
    }

    [Fact]
    public async Task AnalyzeAsync_FlatImage_ClassifiedQ1Poor()
    {
        // blur 0 < 200 -> first switch arm
        var result = await _analyzer.AnalyzeAsync(new ImageData(Png(Flat(128)), "x.png"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.QualityLevel.ShouldBe(ImageQualityLevel.Q1_Poor);
    }

    [Fact]
    public async Task AnalyzeAsync_BlurInLowBand_ClassifiedQ3Low()
    {
        // half 0|150 -> blur 900 in [500,1500) -> Q3_Low
        var result = await _analyzer.AnalyzeAsync(new ImageData(Png(HalfHalf(0, 150)), "x.png"));

        result.Value!.QualityLevel.ShouldBe(ImageQualityLevel.Q3_Low);
    }

    [Fact]
    public async Task AnalyzeAsync_BlurInVeryLowBand_ClassifiedQ4AndClampsContrast()
    {
        // half 0|255 -> blur 2601 in [1500,3000) -> Q4_VeryLow.
        // Contrast 127.5 -> 127.5/60 = 2.125 -> Min clamps to 1.0; Noise 10.2/50 = 0.204.
        var result = await _analyzer.AnalyzeAsync(new ImageData(Png(HalfHalf(0, 255)), "x.png"));

        var a = result.Value!;
        a.QualityLevel.ShouldBe(ImageQualityLevel.Q4_VeryLow);
        a.ContrastLevel.ShouldBe(1.0f);             // upper clamp
        a.NoiseLevel.ShouldBe(0.204f, 1e-4f);
    }

    [Fact]
    public async Task AnalyzeAsync_HighFrequencyStripes_ClassifiedPristineAndClampsNoise()
    {
        // stripes 0|255 -> blur 260100 >= 3000 -> Pristine (default arm).
        // Noise 510 -> 510/50 = 10.2 -> Min clamps to 1.0.
        var result = await _analyzer.AnalyzeAsync(new ImageData(Png(Stripes(0, 255)), "x.png"));

        var a = result.Value!;
        a.QualityLevel.ShouldBe(ImageQualityLevel.Pristine);
        a.NoiseLevel.ShouldBe(1.0f);                // upper clamp
        a.SharpnessLevel.ShouldBe(0.4f, 1e-5f);     // 0.04 * 10
    }

    // ---- GetQualityLevelAsync ----

    [Fact]
    public async Task GetQualityLevelAsync_Success_ReturnsAssessedLevel()
    {
        var result = await _analyzer.GetQualityLevelAsync(new ImageData(Png(HalfHalf(0, 100)), "x.png"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(ImageQualityLevel.Q2_MediumPoor);
    }

    [Fact]
    public async Task GetQualityLevelAsync_DecodeFailure_PropagatesFailure()
    {
        var result = await _analyzer.GetQualityLevelAsync(new ImageData(new byte[] { 0, 0, 0, 0 }, "x.png"));

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldContain("Failed to decode image");
    }
}
