namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Full processing completion event payload.
/// </summary>
/// <param name="FileId">Unique identifier for the document file.</param>
/// <param name="FileName">Name of the processed file.</param>
/// <param name="Status">Processing status ("Success", "Failed", "PartialSuccess").</param>
/// <param name="ProcessingDuration">Total time taken to process the document.</param>
/// <param name="CorrelationId">End-to-end tracing identifier.</param>
/// <param name="Timestamp">UTC timestamp when processing completed.</param>
public sealed record ProcessingCompletedEvent(
    Guid FileId,
    string FileName,
    string Status,
    TimeSpan ProcessingDuration,
    Guid CorrelationId,
    DateTimeOffset Timestamp);