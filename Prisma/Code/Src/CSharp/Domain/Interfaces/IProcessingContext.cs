namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Represents a processing context for tracking individual document processing.
/// </summary>
public interface IProcessingContext
{
    /// <summary>
    /// Gets the document identifier.
    /// </summary>
    string DocumentId { get; }

    /// <summary>
    /// Gets the source path of the document.
    /// </summary>
    string SourcePath { get; }

    /// <summary>
    /// Gets the stopwatch for timing the processing.
    /// </summary>
    Stopwatch Stopwatch { get; }

    /// <summary>
    /// Disposes the processing context.
    /// </summary>
    void Dispose();
}