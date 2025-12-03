namespace Prisma.Athena.Processing;

/// <summary>
/// Result of a successful document processing operation.
/// </summary>
/// <param name="FileId">Unique identifier of the processed file.</param>
/// <param name="CorrelationId">End-to-end correlation identifier for tracing.</param>
/// <param name="TotalProcessingTime">Total time taken to process the document through all pipeline stages.</param>
/// <param name="StagesCompleted">Number of pipeline stages successfully completed.</param>
/// <param name="AutoProcessed">True if document was fully auto-processed, false if manual review required.</param>
public sealed record ProcessingResult(
    Guid FileId,
    Guid? CorrelationId,
    TimeSpan TotalProcessingTime,
    int StagesCompleted,
    bool AutoProcessed);
