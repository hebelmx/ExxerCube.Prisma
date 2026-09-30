using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Tests.Domain.ValueObjects;

/// <summary>
/// Unit tests for the <see cref="Confidence"/> value object and the
/// <see cref="ConfidenceSource"/> enum.
/// </summary>
public class ConfidenceTests
{
    // ── Constructor clamping ─────────────────────────────────────────────────

    [Fact]
    public void Confidence_ClampsBelowZeroToZero()
    {
        var result = new Confidence(-0.5, ConfidenceSource.Ocr);

        result.Value.ShouldBe(0.0);
    }

    [Fact]
    public void Confidence_ClampsAboveOneToOne()
    {
        var result = new Confidence(1.5, ConfidenceSource.Ocr);

        result.Value.ShouldBe(1.0);
    }

    [Fact]
    public void Confidence_ValueWithinRange_IsPreservedExactly()
    {
        var result = new Confidence(0.75, ConfidenceSource.Fusion);

        result.Value.ShouldBe(0.75, tolerance: 1e-15);
    }

    [Fact]
    public void Confidence_Zero_IsPreserved()
    {
        var result = new Confidence(0.0, ConfidenceSource.Quality);

        result.Value.ShouldBe(0.0);
    }

    [Fact]
    public void Confidence_One_IsPreserved()
    {
        var result = new Confidence(1.0, ConfidenceSource.Classification);

        result.Value.ShouldBe(1.0);
    }

    // ── Factory: FromInt ─────────────────────────────────────────────────────

    [Fact]
    public void Confidence_FromInt_ConvertsCorrectly()
    {
        var result = Confidence.FromInt(70);

        result.Value.ShouldBe(0.70, tolerance: 1e-9);
    }

    [Fact]
    public void Confidence_FromInt_SourceIsClassification()
    {
        var result = Confidence.FromInt(50);

        result.Source.ShouldBe(ConfidenceSource.Classification);
    }

    [Fact]
    public void Confidence_FromInt_ZeroYieldsZero()
    {
        var result = Confidence.FromInt(0);

        result.Value.ShouldBe(0.0, tolerance: 1e-15);
    }

    [Fact]
    public void Confidence_FromInt_100YieldsOne()
    {
        var result = Confidence.FromInt(100);

        result.Value.ShouldBe(1.0, tolerance: 1e-15);
    }

    // ── Factory: FromOcr ─────────────────────────────────────────────────────

    [Fact]
    public void Confidence_FromOcr_ConvertsCorrectly()
    {
        var result = Confidence.FromOcr(95.5f);

        result.Value.ShouldBe(0.955, tolerance: 1e-5);
    }

    [Fact]
    public void Confidence_FromOcr_SourceIsOcr()
    {
        var result = Confidence.FromOcr(80.0f);

        result.Source.ShouldBe(ConfidenceSource.Ocr);
    }

    // ── Factory: FromFusion ──────────────────────────────────────────────────

    [Fact]
    public void Confidence_FromFusion_SourceIsFusion()
    {
        var result = Confidence.FromFusion(0.88);

        result.Source.ShouldBe(ConfidenceSource.Fusion);
        result.Value.ShouldBe(0.88, tolerance: 1e-15);
    }

    // ── Factory: FromQuality ─────────────────────────────────────────────────

    [Fact]
    public void Confidence_FromQuality_SourceIsQuality()
    {
        var result = Confidence.FromQuality(0.62);

        result.Source.ShouldBe(ConfidenceSource.Quality);
        result.Value.ShouldBe(0.62, tolerance: 1e-15);
    }

    // ── Min ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Confidence_Min_ReturnsLowest()
    {
        var sources = new[]
        {
            new Confidence(0.9, ConfidenceSource.Ocr),
            new Confidence(0.3, ConfidenceSource.Quality),
            new Confidence(0.7, ConfidenceSource.Fusion)
        };

        var result = Confidence.Min(sources);

        result.Value.ShouldBe(0.3, tolerance: 1e-15);
        result.Source.ShouldBe(ConfidenceSource.Quality);
    }

    [Fact]
    public void Confidence_Min_SingleElement_ReturnsThatElement()
    {
        var sources = new[] { new Confidence(0.55, ConfidenceSource.Fusion) };

        var result = Confidence.Min(sources);

        result.Value.ShouldBe(0.55, tolerance: 1e-15);
        result.Source.ShouldBe(ConfidenceSource.Fusion);
    }

    [Fact]
    public void Confidence_Min_EmptyCollection_ReturnsZeroWithQualitySource()
    {
        var result = Confidence.Min(Array.Empty<Confidence>());

        result.Value.ShouldBe(0.0);
        result.Source.ShouldBe(ConfidenceSource.Quality);
    }

    [Fact]
    public void Confidence_Min_PreservesSourceOfMinimumItem()
    {
        var sources = new[]
        {
            Confidence.FromOcr(90.0f),          // 0.90, Ocr
            Confidence.FromQuality(0.1),         // 0.10, Quality  ← lowest
            Confidence.FromFusion(0.75)          // 0.75, Fusion
        };

        var result = Confidence.Min(sources);

        result.Source.ShouldBe(ConfidenceSource.Quality);
    }

    // ── WeightedAverage ──────────────────────────────────────────────────────

    [Fact]
    public void Confidence_WeightedAverage_ProducesExpectedResult()
    {
        // (0.8 × 0.7) + (0.6 × 0.3) = 0.56 + 0.18 = 0.74
        var weighted = new[]
        {
            (new Confidence(0.8, ConfidenceSource.Ocr),     0.7),
            (new Confidence(0.6, ConfidenceSource.Quality), 0.3)
        };

        var result = Confidence.WeightedAverage(weighted, ConfidenceSource.Fusion);

        result.Value.ShouldBe(0.74, tolerance: 1e-9);
        result.Source.ShouldBe(ConfidenceSource.Fusion);
    }

    [Fact]
    public void Confidence_WeightedAverage_EqualWeights_ProducesArithmeticMean()
    {
        var weighted = new[]
        {
            (new Confidence(0.4, ConfidenceSource.Quality), 1.0),
            (new Confidence(0.8, ConfidenceSource.Ocr),     1.0)
        };

        var result = Confidence.WeightedAverage(weighted, ConfidenceSource.Fusion);

        result.Value.ShouldBe(0.6, tolerance: 1e-9);
    }

    [Fact]
    public void Confidence_WeightedAverage_EmptyCollection_ReturnsZero()
    {
        var result = Confidence.WeightedAverage(
            Array.Empty<(Confidence, double)>(),
            ConfidenceSource.Fusion);

        result.Value.ShouldBe(0.0);
        result.Source.ShouldBe(ConfidenceSource.Fusion);
    }

    [Fact]
    public void Confidence_WeightedAverage_ZeroTotalWeight_ReturnsZero()
    {
        var weighted = new[]
        {
            (new Confidence(0.9, ConfidenceSource.Ocr), 0.0)
        };

        var result = Confidence.WeightedAverage(weighted, ConfidenceSource.Classification);

        result.Value.ShouldBe(0.0);
    }

    // ── Combine ──────────────────────────────────────────────────────────────

    [Fact]
    public void Confidence_Combine_DelegatesToMin()
    {
        var sources = new[]
        {
            new Confidence(0.5, ConfidenceSource.Ocr),
            new Confidence(0.2, ConfidenceSource.Quality),
            new Confidence(0.8, ConfidenceSource.Fusion)
        };

        var combine = Confidence.Combine(sources);
        var min = Confidence.Min(sources);

        combine.ShouldBe(min);
    }

    // ── ToString ─────────────────────────────────────────────────────────────

    [Fact]
    public void Confidence_ToString_ReturnsFormattedString()
    {
        var c = new Confidence(0.95, ConfidenceSource.Ocr);

        c.ToString().ShouldBe("0.95 (Ocr)");
    }

    [Fact]
    public void Confidence_ToString_ZeroValue_FormatsCorrectly()
    {
        var c = new Confidence(0.0, ConfidenceSource.Quality);

        c.ToString().ShouldBe("0.00 (Quality)");
    }

    // ── Value equality (record struct) ───────────────────────────────────────

    [Fact]
    public void Confidence_SameValueAndSource_AreEqual()
    {
        var a = new Confidence(0.75, ConfidenceSource.Fusion);
        var b = new Confidence(0.75, ConfidenceSource.Fusion);

        a.ShouldBe(b);
    }

    [Fact]
    public void Confidence_DifferentSource_AreNotEqual()
    {
        var a = new Confidence(0.75, ConfidenceSource.Fusion);
        var b = new Confidence(0.75, ConfidenceSource.Ocr);

        a.ShouldNotBe(b);
    }
}
