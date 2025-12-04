namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Polynomial regression coefficients for a single parameter.
/// </summary>
public class ParameterCoefficients
{
    /// <summary>
    /// Gets or sets the parameter name.
    /// </summary>
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the intercept (bias) term.
    /// </summary>
    public double Intercept { get; set; }

    /// <summary>
    /// Gets or sets the polynomial coefficients.
    /// For degree=2 with 4 features: [1, x0, x1, x2, x3, x0², x0x1, x0x2, x0x3, x1², x1x2, x1x3, x2², x2x3, x3²]
    /// Total: 15 coefficients (excluding intercept which is stored separately)
    /// </summary>
    public double[] Coefficients { get; set; } = Array.Empty<double>();

    /// <summary>
    /// Gets or sets the minimum valid value for this parameter.
    /// </summary>
    public double MinValue { get; set; }

    /// <summary>
    /// Gets or sets the maximum valid value for this parameter.
    /// </summary>
    public double MaxValue { get; set; }

    /// <summary>
    /// Gets or sets the R² score from training (coefficient of determination).
    /// </summary>
    public double R2Score { get; set; }

    /// <summary>
    /// Gets or sets the mean absolute error from cross-validation.
    /// </summary>
    public double MeanAbsoluteError { get; set; }
}