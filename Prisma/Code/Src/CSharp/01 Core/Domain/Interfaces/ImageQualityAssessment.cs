namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Represents the quality assessment of an image.
/// Used to select the optimal enhancement filter.
/// </summary>
public class ImageQualityAssessment
{
    /// <summary>
    /// Gets or sets the overall quality level.
    /// </summary>
    public ImageQualityLevel QualityLevel { get; set; } = ImageQualityLevel.Q2_MediumPoor;

    /// <summary>
    /// Gets or sets the confidence score of the assessment (0.0 - 1.0).
    /// </summary>
    public float Confidence { get; set; } = 0.0f;

    /// <summary>
    /// Gets or sets the noise level detected (0.0 - 1.0).
    /// </summary>
    public float NoiseLevel { get; set; } = 0.0f;

    /// <summary>
    /// Gets or sets the contrast level detected (0.0 - 1.0).
    /// </summary>
    public float ContrastLevel { get; set; } = 0.0f;

    /// <summary>
    /// Gets or sets the sharpness level detected (0.0 - 1.0).
    /// </summary>
    public float SharpnessLevel { get; set; } = 0.0f;

    /// <summary>
    /// Gets or sets a value indicating whether watermarks were detected.
    /// </summary>
    public bool WatermarkDetected { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the image appears skewed.
    /// </summary>
    public bool IsSkewed { get; set; } = false;

    /// <summary>
    /// Gets or sets the skew angle in degrees (if detected).
    /// </summary>
    public float SkewAngle { get; set; } = 0.0f;

    /// <summary>
    /// Gets or sets the recommended filter type based on analysis.
    /// </summary>
    public ImageFilterType RecommendedFilter { get; set; } = ImageFilterType.PilSimple;

    /// <summary>
    /// Gets or sets additional diagnostic information.
    /// </summary>
    public Dictionary<string, object> Diagnostics { get; set; } = new();

    /// <summary>
    /// Gets or sets the blur score of the image (0.0 - 1.0).
    /// </summary>
    public float BlurScore { get; set; }
}