namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the output writer service for saving processing results.
/// </summary>
public interface IOutputWriter
{
    /// <summary>
    /// Writes a processing result to the specified output path.
    /// </summary>
    /// <param name="result">The processing result to write.</param>
    /// <param name="outputPath">The output file path.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result<bool>> WriteResultAsync(ProcessingResult result, string outputPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes multiple processing results to a directory.
    /// </summary>
    /// <param name="results">The list of processing results to write.</param>
    /// <param name="outputDirectory">The output directory path.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result<bool>> WriteResultsAsync(IEnumerable<ProcessingResult> results, string outputDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes results in JSON format.
    /// </summary>
    /// <param name="result">The processing result to write.</param>
    /// <param name="outputPath">The output file path.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result<bool>> WriteJsonAsync(ProcessingResult result, string outputPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes results in text format.
    /// </summary>
    /// <param name="result">The processing result to write.</param>
    /// <param name="outputPath">The output file path.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result<bool>> WriteTextAsync(ProcessingResult result, string outputPath, CancellationToken cancellationToken = default);
}
