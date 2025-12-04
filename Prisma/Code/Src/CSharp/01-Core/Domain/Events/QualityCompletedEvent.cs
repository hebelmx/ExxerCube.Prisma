namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Quality analysis completion event payload.
/// </summary>
/// <param name="FileId">Unique identifier for the document file.</param>
/// <param name="FileName">Name of the analyzed file.</param>
/// <param name="QualityScore">Quality score (0.0 to 1.0).</param>
/// <param name="IsAcceptable">Whether quality meets acceptance criteria.</param>
/// <param name="CorrelationId">End-to-end tracing identifier.</param>
/// <param name="Timestamp">UTC timestamp when quality analysis completed.</param>
public sealed record QualityCompletedEvent(
    Guid FileId,
    string FileName,
    double QualityScore,
    bool IsAcceptable,
    Guid CorrelationId,
    DateTimeOffset Timestamp);