namespace ExxerCube.Prisma.Infrastructure.Imaging.Strategies;

/// <summary>
/// Polynomial regression model for predicting filter parameters from image quality metrics.
/// Uses polynomial basis functions (linear, quadratic, interaction terms) for continuous parameter prediction.
/// </summary>
public class PolynomialModel
{
    /// <summary>
    /// Gets or sets the polynomial coefficients.
    /// Order: [intercept, linear terms, quadratic terms, interaction terms]
    /// </summary>
    public double[] Coefficients { get; set; } = Array.Empty<double>();

    /// <summary>
    /// Gets or sets the polynomial degree (1=linear, 2=quadratic, 3=cubic).
    /// </summary>
    public int Degree { get; set; } = 2;

    /// <summary>
    /// Gets or sets the output parameter range for clamping.
    /// </summary>
    public (double Min, double Max) OutputRange { get; set; } = (0.0, 1.0);

    /// <summary>
    /// Gets or sets the parameter name for logging/debugging.
    /// </summary>
    public string ParameterName { get; set; } = "Unknown";

    /// <summary>
    /// Initializes a new instance of the <see cref="PolynomialModel"/> class.
    /// </summary>
    public PolynomialModel()
    {
    }

    /// <summary>
    /// Predicts the parameter value from normalized input features.
    /// </summary>
    /// <param name="features">Normalized feature vector [blur, noise, contrast, sharpness].</param>
    /// <returns>Predicted parameter value clamped to valid range.</returns>
    public double Predict(double[] features)
    {
        ArgumentNullException.ThrowIfNull(features);

        if (Coefficients.Length == 0)
        {
            // No model trained yet - return middle of range
            return (OutputRange.Min + OutputRange.Max) / 2.0;
        }

        // Generate polynomial basis functions
        var basis = GeneratePolynomialBasis(features, Degree);

        if (basis.Length != Coefficients.Length)
        {
            throw new InvalidOperationException(
                $"Feature basis length ({basis.Length}) does not match coefficient count ({Coefficients.Length})");
        }

        // Compute weighted sum: prediction = Σ(coefficient_i * basis_i)
        double prediction = 0.0;
        for (int i = 0; i < Coefficients.Length; i++)
        {
            prediction += Coefficients[i] * basis[i];
        }

        // Clamp to valid parameter range
        return Math.Clamp(prediction, OutputRange.Min, OutputRange.Max);
    }

    /// <summary>
    /// Generates polynomial basis functions from input features.
    /// </summary>
    /// <param name="features">Input feature vector.</param>
    /// <param name="degree">Polynomial degree.</param>
    /// <returns>Expanded basis vector.</returns>
    private double[] GeneratePolynomialBasis(double[] features, int degree)
    {
        var basis = new List<double>();

        // Intercept term
        basis.Add(1.0);

        if (degree >= 1)
        {
            // Linear terms: f1, f2, f3, f4
            basis.AddRange(features);
        }

        if (degree >= 2)
        {
            // Quadratic terms: f1², f2², f3², f4²
            foreach (var feature in features)
            {
                basis.Add(feature * feature);
            }

            // Interaction terms: f1*f2, f1*f3, f1*f4, f2*f3, f2*f4, f3*f4
            for (int i = 0; i < features.Length; i++)
            {
                for (int j = i + 1; j < features.Length; j++)
                {
                    basis.Add(features[i] * features[j]);
                }
            }
        }

        if (degree >= 3)
        {
            // Cubic terms: f1³, f2³, f3³, f4³
            foreach (var feature in features)
            {
                basis.Add(feature * feature * feature);
            }
        }

        return basis.ToArray();
    }

    /// <summary>
    /// Creates a stub model for testing before training data is available.
    /// Returns the midpoint of the output range.
    /// </summary>
    /// <param name="parameterName">Name of the parameter.</param>
    /// <param name="min">Minimum valid value.</param>
    /// <param name="max">Maximum valid value.</param>
    /// <returns>Stub polynomial model.</returns>
    public static PolynomialModel CreateStub(string parameterName, double min, double max)
    {
        return new PolynomialModel
        {
            ParameterName = parameterName,
            OutputRange = (min, max),
            Degree = 2,
            Coefficients = Array.Empty<double>() // Empty = use default behavior
        };
    }

    /// <summary>
    /// Creates a linear model from coefficients.
    /// </summary>
    /// <param name="parameterName">Name of the parameter.</param>
    /// <param name="intercept">Intercept coefficient.</param>
    /// <param name="linearCoefficients">Linear coefficients for [blur, noise, contrast, sharpness].</param>
    /// <param name="min">Minimum valid value.</param>
    /// <param name="max">Maximum valid value.</param>
    /// <returns>Linear polynomial model.</returns>
    public static PolynomialModel CreateLinear(
        string parameterName,
        double intercept,
        double[] linearCoefficients,
        double min,
        double max)
    {
        var coefficients = new List<double> { intercept };
        coefficients.AddRange(linearCoefficients);

        return new PolynomialModel
        {
            ParameterName = parameterName,
            Degree = 1,
            Coefficients = coefficients.ToArray(),
            OutputRange = (min, max)
        };
    }

    /// <summary>
    /// Creates a quadratic model from coefficients.
    /// </summary>
    /// <param name="parameterName">Name of the parameter.</param>
    /// <param name="intercept">Intercept coefficient.</param>
    /// <param name="linearCoefficients">Linear coefficients [blur, noise, contrast, sharpness].</param>
    /// <param name="quadraticCoefficients">Quadratic coefficients [blur², noise², contrast², sharpness²].</param>
    /// <param name="interactionCoefficients">Interaction coefficients [blur*noise, blur*contrast, blur*sharp, noise*contrast, noise*sharp, contrast*sharp].</param>
    /// <param name="min">Minimum valid value.</param>
    /// <param name="max">Maximum valid value.</param>
    /// <returns>Quadratic polynomial model.</returns>
    public static PolynomialModel CreateQuadratic(
        string parameterName,
        double intercept,
        double[] linearCoefficients,
        double[] quadraticCoefficients,
        double[] interactionCoefficients,
        double min,
        double max)
    {
        var coefficients = new List<double> { intercept };
        coefficients.AddRange(linearCoefficients);
        coefficients.AddRange(quadraticCoefficients);
        coefficients.AddRange(interactionCoefficients);

        return new PolynomialModel
        {
            ParameterName = parameterName,
            Degree = 2,
            Coefficients = coefficients.ToArray(),
            OutputRange = (min, max)
        };
    }
}
