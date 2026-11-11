using ExxerCube.Prisma.Infrastructure.Imaging.Strategies;

namespace ExxerCube.Prisma.Tests.Infrastructure.Imaging;

/// <summary>
/// Mutation-killing tests for <see cref="PolynomialModel"/> — polynomial basis expansion
/// (intercept + linear + quadratic + interaction + cubic), the weighted-sum prediction, the
/// empty-model midpoint fallback, the basis/coefficient length guard, and the output clamp.
/// All expected values are hand-computed.
/// </summary>
public class PolynomialModelMutationTests
{
    [Fact]
    public void Predict_Null_Throws()
    {
        var model = PolynomialModel.CreateLinear("p", 0, new double[] { 1, 1, 1, 1 }, 0, 1);
        Should.Throw<ArgumentNullException>(() => model.Predict(null!));
    }

    [Fact]
    public void DefaultParameterName_IsUnknown()
    {
        new PolynomialModel().ParameterName.ShouldBe("Unknown");
    }

    [Fact]
    public void Predict_NullFeaturesWithEmptyCoefficients_Throws()
    {
        // With empty coefficients the midpoint path returns without ever reading features,
        // so only the ArgumentNullException guard makes this throw (a removed guard would
        // silently return the midpoint).
        var model = new PolynomialModel { OutputRange = (0.0, 10.0) };
        Should.Throw<ArgumentNullException>(() => model.Predict(null!));
    }

    [Fact]
    public void Predict_NoCoefficients_ReturnsRangeMidpoint()
    {
        // empty coefficients -> (Min + Max) / 2 = (0 + 10)/2 = 5
        var model = new PolynomialModel { OutputRange = (0.0, 10.0) };

        model.Predict(new double[] { 1, 2, 3, 4 }).ShouldBe(5.0);
    }

    [Fact]
    public void Predict_NoCoefficients_AsymmetricRange_ReturnsMidpoint()
    {
        // (2 + 8)/2 = 5 -- distinguishes the '+' (would be (2-8)/2=-3) and '/2' (would be 20)
        var model = new PolynomialModel { OutputRange = (2.0, 8.0) };

        model.Predict(new double[] { 0, 0, 0, 0 }).ShouldBe(5.0);
    }

    [Fact]
    public void Predict_LinearModel_ComputesInterceptPlusWeightedLinearTerms()
    {
        // basis = [1, f0, f1, f2, f3]; coeffs = [1, 2, 3, 4, 5]
        // distinct features pin the per-term alignment:
        // 1*1 + 2*1 + 3*10 + 4*100 + 5*1000 = 1 + 2 + 30 + 400 + 5000 = 5433
        var model = PolynomialModel.CreateLinear("p", 1.0, new double[] { 2, 3, 4, 5 }, -100000, 100000);

        model.Predict(new double[] { 1, 10, 100, 1000 }).ShouldBe(5433.0, 1e-9);
    }

    [Fact]
    public void Predict_QuadraticModel_AppliesSquareTerm()
    {
        // first quadratic coeff (index 5 = f0²); f0=3 -> 9
        var model = PolynomialModel.CreateQuadratic(
            "p", 0.0,
            linearCoefficients: new double[] { 0, 0, 0, 0 },
            quadraticCoefficients: new double[] { 1, 0, 0, 0 },
            interactionCoefficients: new double[] { 0, 0, 0, 0, 0, 0 },
            -100000, 100000);

        model.Predict(new double[] { 3, 0, 0, 0 }).ShouldBe(9.0, 1e-9);
    }

    [Fact]
    public void Predict_QuadraticModel_AppliesFirstInteractionTerm()
    {
        // first interaction coeff (f0*f1); f0=2, f1=5 -> 10
        var model = PolynomialModel.CreateQuadratic(
            "p", 0.0,
            linearCoefficients: new double[] { 0, 0, 0, 0 },
            quadraticCoefficients: new double[] { 0, 0, 0, 0 },
            interactionCoefficients: new double[] { 1, 0, 0, 0, 0, 0 },
            -100000, 100000);

        model.Predict(new double[] { 2, 5, 0, 0 }).ShouldBe(10.0, 1e-9);
    }

    [Fact]
    public void Predict_QuadraticModel_AppliesLastInteractionTerm()
    {
        // last interaction coeff (f2*f3); f2=4, f3=6 -> 24 -- pins the i<j loop reaching (2,3)
        var model = PolynomialModel.CreateQuadratic(
            "p", 0.0,
            linearCoefficients: new double[] { 0, 0, 0, 0 },
            quadraticCoefficients: new double[] { 0, 0, 0, 0 },
            interactionCoefficients: new double[] { 0, 0, 0, 0, 0, 1 },
            -100000, 100000);

        model.Predict(new double[] { 0, 0, 4, 6 }).ShouldBe(24.0, 1e-9);
    }

    [Fact]
    public void Predict_CubicModel_AppliesCubeTerm()
    {
        // Degree 3: basis = [1, 4 linear, 4 quad, 6 interaction, 4 cubic] = 19 terms.
        // cubic block starts at index 15 (f0³). f0=2 -> 8.
        var coeffs = new double[19];
        coeffs[15] = 1.0; // f0³
        var model = new PolynomialModel
        {
            Degree = 3,
            Coefficients = coeffs,
            OutputRange = (-100000, 100000)
        };

        model.Predict(new double[] { 2, 0, 0, 0 }).ShouldBe(8.0, 1e-9);
    }

    [Fact]
    public void Predict_DegreeZero_UsesInterceptOnly()
    {
        // Degree 0 -> basis is just [1.0]; single coefficient is the intercept weight.
        var model = new PolynomialModel
        {
            Degree = 0,
            Coefficients = new double[] { 7.0 },
            OutputRange = (-100000, 100000)
        };

        model.Predict(new double[] { 1, 2, 3, 4 }).ShouldBe(7.0, 1e-9);
    }

    [Fact]
    public void Predict_BasisLengthMismatch_ThrowsInvalidOperation()
    {
        // Degree 1 basis is 5 long; 6 coefficients mismatch -> InvalidOperationException.
        var model = new PolynomialModel
        {
            Degree = 1,
            Coefficients = new double[6],
            OutputRange = (0, 1)
        };

        var ex = Should.Throw<InvalidOperationException>(() => model.Predict(new double[] { 1, 1, 1, 1 }));
        ex.Message.ShouldContain("does not match");
    }

    [Fact]
    public void Predict_AboveRange_ClampsToMax()
    {
        // intercept 1000 with wide-ish range clamps to Max.
        var model = PolynomialModel.CreateLinear("p", 1000.0, new double[] { 0, 0, 0, 0 }, 0.0, 10.0);

        model.Predict(new double[] { 1, 1, 1, 1 }).ShouldBe(10.0);
    }

    [Fact]
    public void Predict_BelowRange_ClampsToMin()
    {
        var model = PolynomialModel.CreateLinear("p", -1000.0, new double[] { 0, 0, 0, 0 }, 2.0, 10.0);

        model.Predict(new double[] { 1, 1, 1, 1 }).ShouldBe(2.0);
    }

    [Fact]
    public void CreateStub_HasEmptyCoefficientsAndReturnsMidpoint()
    {
        var stub = PolynomialModel.CreateStub("contrast", 0.5, 2.0);

        stub.ParameterName.ShouldBe("contrast");
        stub.Degree.ShouldBe(2);
        stub.Coefficients.Length.ShouldBe(0);
        // empty coefficients -> midpoint (0.5 + 2.0)/2 = 1.25
        stub.Predict(new double[] { 1, 1, 1, 1 }).ShouldBe(1.25, 1e-9);
    }

    [Fact]
    public void CreateLinear_SetsDegreeOneAndOrdersCoefficients()
    {
        var model = PolynomialModel.CreateLinear("brightness", 0.5, new double[] { 1, 0, 0, 0 }, 0, 100);

        model.Degree.ShouldBe(1);
        model.ParameterName.ShouldBe("brightness");
        // intercept first, then linear coeff for f0 -> 0.5 + 1*f0; f0=3 -> 3.5
        model.Predict(new double[] { 3, 0, 0, 0 }).ShouldBe(3.5, 1e-9);
    }

    [Fact]
    public void CreateQuadratic_SetsDegreeTwoAndConcatenatesCoefficientBlocks()
    {
        var model = PolynomialModel.CreateQuadratic(
            "sharpness", 1.0,
            linearCoefficients: new double[] { 10, 0, 0, 0 },
            quadraticCoefficients: new double[] { 100, 0, 0, 0 },
            interactionCoefficients: new double[] { 1000, 0, 0, 0, 0, 0 },
            -1_000_000, 1_000_000);

        model.Degree.ShouldBe(2);
        // f0=2: intercept 1 + 10*2 (linear) + 100*4 (quad f0²) + 1000*(2*0) (interaction f0*f1)
        // = 1 + 20 + 400 + 0 = 421
        model.Predict(new double[] { 2, 0, 0, 0 }).ShouldBe(421.0, 1e-9);
    }
}
