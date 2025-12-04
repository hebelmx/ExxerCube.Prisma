namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Classification completion event payload.
/// </summary>
/// <param name="FileId">Unique identifier for the document file.</param>
/// <param name="FileName">Name of the classified file.</param>
/// <param name="ClassificationType">Classification result (e.g., "Invoice", "Contract", "Report").</param>
/// <param name="ConfidenceScore">ML model confidence score (0.0 to 1.0).</param>
/// <param name="CorrelationId">End-to-end tracing identifier.</param>
/// <param name="Timestamp">UTC timestamp when classification completed.</param>
public sealed record ClassificationCompletedEvent(
    Guid FileId,
    string FileName,
    string ClassificationType,
    double ConfidenceScore,
    Guid CorrelationId,
    DateTimeOffset Timestamp);