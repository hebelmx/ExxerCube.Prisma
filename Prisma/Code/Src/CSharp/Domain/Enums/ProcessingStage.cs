namespace ExxerCube.Prisma.Domain.Enums;

/// <summary>
/// Represents the processing stage where an action occurred.
/// </summary>
public enum ProcessingStage
{
    /// <summary>
    /// Document ingestion stage (Stage 1).
    /// </summary>
    Ingestion = 0,

    /// <summary>
    /// Metadata extraction stage (Stage 2).
    /// </summary>
    Extraction = 1,

    /// <summary>
    /// Decision logic stage (Stage 3).
    /// </summary>
    DecisionLogic = 2,

    /// <summary>
    /// Export generation stage (Stage 4).
    /// </summary>
    Export = 3
}

