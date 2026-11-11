namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Statistics about collected OCR sessions.
/// </summary>
public class OcrSessionStatistics
{
    /// <summary>
    /// Gets or sets the total number of sessions.
    /// </summary>
    public int TotalSessions { get; set; }

    /// <summary>
    /// Gets or sets the number of reviewed sessions.
    /// </summary>
    public int ReviewedSessions { get; set; }

    /// <summary>
    /// Gets or sets the number of sessions ready for training.
    /// </summary>
    public int TrainingReadySessions { get; set; }

    /// <summary>
    /// Gets or sets the average improvement percentage.
    /// </summary>
    public double AverageImprovementPercent { get; set; }

    /// <summary>
    /// Gets or sets the average quality rating.
    /// </summary>
    public double AverageQualityRating { get; set; }

    /// <summary>
    /// Gets or sets sessions grouped by quality level.
    /// </summary>
    public Dictionary<string, int> SessionsByQualityLevel { get; set; } = new();

    /// <summary>
    /// Gets or sets sessions grouped by filter type.
    /// </summary>
    public Dictionary<string, int> SessionsByFilterType { get; set; } = new();

    /// <summary>
    /// Gets or sets sessions grouped by model version.
    /// </summary>
    public Dictionary<string, int> SessionsByModelVersion { get; set; } = new();
}