namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the layout generator service for generating Excel layouts from unified metadata records for SIRO registration systems (FR18).
/// </summary>
public interface ILayoutGenerator
{
    /// <summary>
    /// Generates Excel layout file from unified metadata record for SIRO registration systems.
    /// </summary>
    /// <param name="metadata">The unified metadata record to generate layout from.</param>
    /// <param name="outputStream">The output stream to write the Excel content.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> GenerateExcelLayoutAsync(
        UnifiedMetadataRecord metadata,
        Stream outputStream,
        CancellationToken cancellationToken = default);
}

