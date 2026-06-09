using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Imaging.Strategies;

namespace ExxerCube.Prisma.Tests.Infrastructure.Imaging;

/// <summary>
/// Mutation-killing tests for <see cref="FeatureNormalizer"/> (min-max normalization of the
/// four image-quality features into a [0,1] vector). The empirical mins are all 0, so the
/// "- min" subtractions are equivalent; the killable surface is the per-feature scaling
/// divisions and the [0,1] clamp.
/// </summary>
public class FeatureNormalizerMutationTests
{
    private readonly FeatureNormalizer _normalizer = new();

    private static ImageQualityAssessment Assessment(
        float blur = 0f, float noise = 0f, float contrast = 0f, float sharpness = 0f) =>
        new()
        {
            BlurScore = blur,
            NoiseLevel = noise,
            ContrastLevel = contrast,
            SharpnessLevel = sharpness
        };

    [Fact]
    public void Normalize_Null_Throws()
    {
        Should.Throw<ArgumentNullException>(() => _normalizer.Normalize(null!));
    }

    [Fact]
    public void Normalize_MidRangeValues_ReturnsExactPerFeatureScaling()
    {
        // Blur 2500/5000 = 0.5
        // Noise 0.25 -> *100=25 -> /100 = 0.25
        // Contrast 0.125 -> *127.5=15.9375 -> /127.5 = 0.125
        // Sharpness 0.75 -> *50=37.5 -> /50 = 0.75
        var v = _normalizer.Normalize(Assessment(blur: 2500f, noise: 0.25f, contrast: 0.125f, sharpness: 0.75f));

        v.Length.ShouldBe(4);
        v[0].ShouldBe(0.5, 1e-9);
        v[1].ShouldBe(0.25, 1e-9);
        v[2].ShouldBe(0.125, 1e-9);
        v[3].ShouldBe(0.75, 1e-9);
    }

    [Fact]
    public void Normalize_AboveRange_ClampsToOne()
    {
        // Blur 10000/5000 = 2.0 -> clamp 1.0; Noise 2.0 -> 2.0 -> clamp 1.0;
        // Contrast 1.5 -> 1.5 -> clamp 1.0; Sharpness 3.0 -> 3.0 -> clamp 1.0.
        var v = _normalizer.Normalize(Assessment(blur: 10000f, noise: 2.0f, contrast: 1.5f, sharpness: 3.0f));

        v[0].ShouldBe(1.0);
        v[1].ShouldBe(1.0);
        v[2].ShouldBe(1.0);
        v[3].ShouldBe(1.0);
    }

    [Fact]
    public void Normalize_BelowRange_ClampsToZero()
    {
        // negative inputs produce negative ratios -> clamp to 0.
        var v = _normalizer.Normalize(Assessment(blur: -5000f, noise: -1.0f, contrast: -1.0f, sharpness: -1.0f));

        v[0].ShouldBe(0.0);
        v[1].ShouldBe(0.0);
        v[2].ShouldBe(0.0);
        v[3].ShouldBe(0.0);
    }

    [Fact]
    public void CreateFromDataset_ReturnsWorkingDefaultNormalizer()
    {
        // The factory is a placeholder returning a default normalizer; it must still
        // normalize identically to the default instance.
        var custom = FeatureNormalizer.CreateFromDataset(1, 2, 3, 4, 5, 6, 7, 8);

        var v = custom.Normalize(Assessment(blur: 2500f, noise: 0.25f));

        v[0].ShouldBe(0.5, 1e-9);
        v[1].ShouldBe(0.25, 1e-9);
    }
}
