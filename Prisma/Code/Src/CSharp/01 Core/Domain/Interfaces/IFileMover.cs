namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the file mover service for organizing files based on classification.
/// </summary>
public interface IFileMover
{
    /// <summary>
    /// Moves a file to the appropriate directory based on its classification.
    /// </summary>
    /// <param name="sourcePath">The source file path.</param>
    /// <param name="classification">The document classification result.</param>
    /// <param name="safeFileName">The safe file name to use for the destination.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the destination file path or an error.</returns>
    Task<Result<string>> MoveFileAsync(
        string sourcePath,
        ClassificationResult classification,
        string safeFileName,
        CancellationToken cancellationToken = default);
}

