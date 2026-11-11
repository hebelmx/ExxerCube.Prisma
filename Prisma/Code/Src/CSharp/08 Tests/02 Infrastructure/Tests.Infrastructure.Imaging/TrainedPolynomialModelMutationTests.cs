using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Infrastructure.Imaging.Strategies;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Tests.Infrastructure.Imaging;

/// <summary>
/// Mutation-killing tests for <see cref="TrainedPolynomialModel"/> — the StandardScaler
/// normalization (x-mean)/scale, the degree-2 polynomial feature expansion, the per-parameter
/// dot-product prediction, and the clamp. Tests drive it with hand-built options so every
/// arithmetic step yields an exact, asserted value.
///
/// Fixture: ScalerMean=[1,2,3,4], ScalerScale=[2,2,2,2], features=[5,6,9,10]
/// -> normalized x = [2, 2, 3, 3]
/// -> polyFeatures = [1, 2,2,3,3, 4,4,6,6, 4,6,6, 9,9,9]
///    (indices: 0 bias; 1-4 linear x0..x3; 5 x0²; 6 x0x1; 7 x0x2; 8 x0x3;
///     9 x1²; 10 x1x2; 11 x1x3; 12 x2²; 13 x2x3; 14 x3²)
/// </summary>
public class TrainedPolynomialModelMutationTests
{
    private static readonly double[] Mean = { 1, 2, 3, 4 };
    private static readonly double[] Scale = { 2, 2, 2, 2 };

    private static ImagePropertyFeatures Features() => new()
    {
        BlurScore = 5,
        Contrast = 6,
        NoiseEstimate = 9,
        EdgeDensity = 10
    };

    private static ParameterCoefficients Model(double intercept, int coeffIndex, double coeffValue,
        double min = -1_000_000, double max = 1_000_000)
    {
        var coeffs = new double[15];
        if (coeffIndex >= 0)
            coeffs[coeffIndex] = coeffValue;
        return new ParameterCoefficients
        {
            Intercept = intercept,
            Coefficients = coeffs,
            MinValue = min,
            MaxValue = max
        };
    }

    private static ParameterCoefficients ModelMulti(params (int Index, double Value)[] terms)
    {
        var coeffs = new double[15];
        foreach (var (i, v) in terms)
            coeffs[i] = v;
        return new ParameterCoefficients
        {
            Intercept = 0,
            Coefficients = coeffs,
            MinValue = -1_000_000,
            MaxValue = 1_000_000
        };
    }

    private static TrainedPolynomialModel WithOptions(PolynomialModelOptions opts)
    {
        var monitor = Substitute.For<IOptionsMonitor<PolynomialModelOptions>>();
        monitor.CurrentValue.Returns(opts);
        return new TrainedPolynomialModel(monitor);
    }

    [Fact]
    public void Predict_Null_Throws()
    {
        Should.Throw<ArgumentNullException>(() => WithOptions(new PolynomialModelOptions()).Predict(null!));
    }

    [Fact]
    public void Constructor_SubscribesToOptionsChanges()
    {
        // The ctor wires hot-reload by subscribing to IOptionsMonitor.OnChange; a removed
        // subscription statement is caught by Received(1).
        var monitor = Substitute.For<IOptionsMonitor<PolynomialModelOptions>>();
        monitor.CurrentValue.Returns(new PolynomialModelOptions());

        _ = new TrainedPolynomialModel(monitor);

        monitor.Received(1).OnChange(Arg.Any<Action<PolynomialModelOptions, string?>>());
    }

    [Fact]
    public void Predict_RemainingInteractionAndQuadraticTerms_ProduceExactValues()
    {
        // Pins the polynomial terms not covered above: x0*x2, x0*x3, x1², x1*x2, x1*x3, x2².
        var opts = new PolynomialModelOptions
        {
            ScalerMean = Mean,
            ScalerScale = Scale,
            ContrastModel = ModelMulti((7, 1), (8, 1)),    // x0*x2 + x0*x3 = 6 + 6 = 12
            BrightnessModel = Model(intercept: 0, coeffIndex: 9, coeffValue: 1),  // x1² = 4
            SharpnessModel = ModelMulti((10, 1), (11, 1)), // x1*x2 + x1*x3 = 6 + 6 = 12
            UnsharpRadiusModel = Model(intercept: 0, coeffIndex: 12, coeffValue: 1), // x2² = 9
            UnsharpPercentModel = Model(intercept: 0, coeffIndex: -1, coeffValue: 0)
        };

        var p = WithOptions(opts).Predict(Features());

        p.Contrast.ShouldBe(12f, 1e-3f);
        p.Brightness.ShouldBe(4f, 1e-4f);
        p.Sharpness.ShouldBe(12f, 1e-3f);
        p.UnsharpRadius.ShouldBe(9f, 1e-4f);
    }

    [Fact]
    public void Predict_LinearTermsAndIntercept_ProducePerFeatureExactValues()
    {
        // Each parameter isolates one normalized feature (or the intercept) so the
        // (x-mean)/scale normalization of each of the four features is pinned independently.
        var opts = new PolynomialModelOptions
        {
            ScalerMean = Mean,
            ScalerScale = Scale,
            ContrastModel = Model(intercept: 0, coeffIndex: 1, coeffValue: 3),   // 3 * x0(=2) = 6
            BrightnessModel = Model(intercept: 0, coeffIndex: 2, coeffValue: 1), // x1(=2) = 2
            SharpnessModel = Model(intercept: 0, coeffIndex: 3, coeffValue: 1),  // x2(=3) = 3
            UnsharpRadiusModel = Model(intercept: 0, coeffIndex: 4, coeffValue: 1), // x3(=3) = 3
            UnsharpPercentModel = Model(intercept: 7, coeffIndex: -1, coeffValue: 0) // intercept only = 7
        };

        var p = WithOptions(opts).Predict(Features());

        p.Contrast.ShouldBe(6f, 1e-4f);
        p.Brightness.ShouldBe(2f, 1e-4f);
        p.Sharpness.ShouldBe(3f, 1e-4f);
        p.UnsharpRadius.ShouldBe(3f, 1e-4f);
        p.UnsharpPercent.ShouldBe(7f, 1e-4f);
    }

    [Fact]
    public void Predict_QuadraticInteractionAndClamp_ProduceExactValues()
    {
        var opts = new PolynomialModelOptions
        {
            ScalerMean = Mean,
            ScalerScale = Scale,
            ContrastModel = Model(intercept: 0, coeffIndex: 5, coeffValue: 1),   // x0² = 4
            BrightnessModel = Model(intercept: 0, coeffIndex: 6, coeffValue: 1), // x0*x1 = 4
            SharpnessModel = Model(intercept: 0, coeffIndex: 14, coeffValue: 1), // x3² = 9
            UnsharpRadiusModel = Model(intercept: 1000, coeffIndex: -1, coeffValue: 0, min: 0, max: 5),    // clamp -> 5
            UnsharpPercentModel = Model(intercept: -1000, coeffIndex: -1, coeffValue: 0, min: 0, max: 250) // clamp -> 0
        };

        var p = WithOptions(opts).Predict(Features());

        p.Contrast.ShouldBe(4f, 1e-4f);       // x0² pins the square term
        p.Brightness.ShouldBe(4f, 1e-4f);     // x0*x1 pins the first interaction
        p.Sharpness.ShouldBe(9f, 1e-4f);      // x3² pins the last quadratic term
        p.UnsharpRadius.ShouldBe(5f);         // upper clamp Math.Min(max, value)
        p.UnsharpPercent.ShouldBe(0f);        // lower clamp Math.Max(min, value)
    }

    [Fact]
    public void Predict_MidInteractionTerm_PinsLoopAlignment()
    {
        // index 13 = x2*x3 = 3*3 = 9, with coefficient 2 -> 18 (a non-unit coeff also pins
        // the dot-product multiply against '/').
        var opts = new PolynomialModelOptions
        {
            ScalerMean = Mean,
            ScalerScale = Scale,
            ContrastModel = Model(intercept: 0, coeffIndex: 13, coeffValue: 2) // 2 * (x2*x3=9) = 18
        };

        WithOptions(opts).Predict(Features()).Contrast.ShouldBe(18f, 1e-3f);
    }

    [Fact]
    public void Predict_ParameterlessCtor_UsesDefaultsAndClampsWithinRanges()
    {
        // Covers the default-options ctor path; exact coefficients are production data, so
        // assert each output lands inside its configured clamp range.
        var defaults = new PolynomialModelOptions();
        var p = new TrainedPolynomialModel().Predict(Features());

        p.Contrast.ShouldBeInRange((float)defaults.ContrastModel.MinValue, (float)defaults.ContrastModel.MaxValue);
        p.Brightness.ShouldBeInRange((float)defaults.BrightnessModel.MinValue, (float)defaults.BrightnessModel.MaxValue);
        p.Sharpness.ShouldBeInRange((float)defaults.SharpnessModel.MinValue, (float)defaults.SharpnessModel.MaxValue);
        p.UnsharpRadius.ShouldBeInRange((float)defaults.UnsharpRadiusModel.MinValue, (float)defaults.UnsharpRadiusModel.MaxValue);
        p.UnsharpPercent.ShouldBeInRange((float)defaults.UnsharpPercentModel.MinValue, (float)defaults.UnsharpPercentModel.MaxValue);
    }
}
