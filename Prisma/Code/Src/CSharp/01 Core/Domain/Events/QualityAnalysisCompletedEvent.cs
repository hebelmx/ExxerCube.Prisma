namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Image quality analysis completed (EmguCV).
/// </summary>
public record QualityAnalysisCompletedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the analyzed file.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the overall quality level determined by analysis.
    /// </summary>
    public ImageQualityLevel QualityLevel { get; init; } = ImageQualityLevel.Unknown;

    /// <summary>
    /// Gets the blur score (higher values indicate more blur).
    /// </summary>
    public decimal BlurScore { get; init; }

    /// <summary>
    /// Gets the noise score (higher values indicate more noise).
    /// </summary>
    public decimal NoiseScore { get; init; }

    /// <summary>
    /// Gets the contrast score (higher values indicate better contrast).
    /// </summary>
    public decimal ContrastScore { get; init; }

    /// <summary>
    /// Gets the sharpness score (higher values indicate sharper image).
    /// </summary>
    public decimal SharpnessScore { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="QualityAnalysisCompletedEvent"/> class.
    /// </summary>
    public QualityAnalysisCompletedEvent()
    {
        EventType = nameof(QualityAnalysisCompletedEvent);
    }
}