namespace ExxerCube.Prisma.Domain.ValueObjects;

using ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Represents file classification results with confidence scores.
/// </summary>
public class ClassificationResult
{
    /// <summary>
    /// Gets or sets the Level 1 main category (Aseguramiento, Desembargo, Documentacion, etc.).
    /// </summary>
    public ClassificationLevel1 Level1 { get; set; } = ClassificationLevel1.Unknown;

    /// <summary>
    /// Gets or sets the Level 2 subcategory (Especial, Judicial, Hacendario) (nullable).
    /// </summary>
    public ClassificationLevel2? Level2 { get; set; }

    /// <summary>
    /// Gets or sets the detailed scoring information.
    /// </summary>
    public ClassificationScores Scores { get; set; } = new();

    /// <summary>
    /// Gets or sets the overall confidence score (0-100).
    /// </summary>
    public int Confidence { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassificationResult"/> class.
    /// </summary>
    public ClassificationResult()
    {
    }
}

