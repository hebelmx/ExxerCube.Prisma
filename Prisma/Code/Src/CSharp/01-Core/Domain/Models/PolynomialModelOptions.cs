namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Configuration options for polynomial filter model.
/// Supports hot-reload via IOptionsMonitor for production model updates.
/// </summary>
public class PolynomialModelOptions
{
    /// <summary>
    /// Gets or sets the configuration section name.
    /// </summary>
    public const string SectionName = "PolynomialModelOptions";

    /// <summary>
    /// Gets or sets the model version identifier.
    /// </summary>
    public string ModelVersion { get; set; } = "polynomial_v1";

    /// <summary>
    /// Gets or sets when this model was trained.
    /// </summary>
    public DateTime? TrainedDate { get; set; }

    /// <summary>
    /// Gets or sets the number of training samples used.
    /// </summary>
    public int TrainingDataSize { get; set; }

    /// <summary>
    /// Gets or sets the StandardScaler mean values for normalization.
    /// Order: [BlurScore, Contrast, NoiseEstimate, EdgeDensity]
    /// </summary>
    public double[] ScalerMean { get; set; } = { 565.758125, 29.1111, 15.2173, 4.3990 };

    /// <summary>
    /// Gets or sets the StandardScaler scale values for normalization.
    /// Order: [BlurScore, Contrast, NoiseEstimate, EdgeDensity]
    /// </summary>
    public double[] ScalerScale { get; set; } = { 1225.0172, 5.8784, 18.2808, 2.5322 };

    /// <summary>
    /// Gets or sets the polynomial degree (1=linear, 2=quadratic, 3=cubic).
    /// </summary>
    public int PolynomialDegree { get; set; } = 2;

    /// <summary>
    /// Gets or sets the coefficients for contrast parameter prediction.
    /// </summary>
    public ParameterCoefficients ContrastModel { get; set; } = new()
    {
        ParameterName = "Contrast",
        Intercept = 1.0011,
        Coefficients = new[]
        {
            0.0, 0.1096, -0.4311, 0.0875, 0.1243,
            0.0147, 0.2337, -0.3083, -0.4505,
            0.1190, -0.1537, 0.0157,
            0.2605, 0.3578, 0.0455
        },
        MinValue = 0.5,
        MaxValue = 2.0,
        R2Score = 0.949,
        MeanAbsoluteError = 0.052
    };

    /// <summary>
    /// Gets or sets the coefficients for brightness parameter prediction.
    /// </summary>
    public ParameterCoefficients BrightnessModel { get; set; } = new()
    {
        ParameterName = "Brightness",
        Intercept = 1.0735,
        Coefficients = new[]
        {
            0.0, 0.0030, 0.0185, 0.0022, -0.0428,
            -0.0066, -0.0039, 0.0132, -0.0546,
            -0.0031, 0.0135, 0.0006,
            0.0068, -0.0222, 0.0009
        },
        MinValue = 0.8,
        MaxValue = 1.3,
        R2Score = 0.987,
        MeanAbsoluteError = 0.004
    };

    /// <summary>
    /// Gets or sets the coefficients for sharpness parameter prediction.
    /// </summary>
    public ParameterCoefficients SharpnessModel { get; set; } = new()
    {
        ParameterName = "Sharpness",
        Intercept = 2.2279,
        Coefficients = new[]
        {
            0.0, 0.0314, 0.3771, 0.0752, -0.1629,
            0.0101, -0.1100, 0.1108, -0.2895,
            0.1683, -0.1517, -0.2783,
            -0.0136, -0.0722, -0.0408
        },
        MinValue = 0.5,
        MaxValue = 3.0,
        R2Score = 0.947,
        MeanAbsoluteError = 0.089
    };

    /// <summary>
    /// Gets or sets the coefficients for unsharp radius parameter prediction.
    /// </summary>
    public ParameterCoefficients UnsharpRadiusModel { get; set; } = new()
    {
        ParameterName = "UnsharpRadius",
        Intercept = 2.5032,
        Coefficients = new[]
        {
            0.0, -0.1262, -0.0350, -0.1980, -0.6791,
            -0.0582, -0.2502, 0.3043, -0.1707,
            -0.4983, 0.8135, 0.5182,
            -0.1420, -0.6486, 0.0650
        },
        MinValue = 0.0,
        MaxValue = 5.0,
        R2Score = 0.938,
        MeanAbsoluteError = 0.197
    };

    /// <summary>
    /// Gets or sets the coefficients for unsharp percent parameter prediction.
    /// </summary>
    public ParameterCoefficients UnsharpPercentModel { get; set; } = new()
    {
        ParameterName = "UnsharpPercent",
        Intercept = 173.38,
        Coefficients = new[]
        {
            0.0, -59.83, 42.22, -57.19, 20.20,
            21.89, -107.31, 80.69, 257.05,
            -62.80, 111.17, 24.40,
            -124.69, -111.20, -3.62
        },
        MinValue = 0.0,
        MaxValue = 250.0,
        R2Score = 0.897,
        MeanAbsoluteError = 16.4
    };
}