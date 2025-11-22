namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the file type identifier service for detecting file types based on content (not just extension).
/// </summary>
public interface IFileTypeIdentifier
{
    /// <summary>
    /// Identifies the file type based on content analysis for PDF, XML, and DOCX files.
    /// </summary>
    /// <param name="fileContent">The file content as a byte array.</param>
    /// <param name="fileName">The filename (optional, used as fallback).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the identified file format or an error.</returns>
    Task<Result<FileFormat>> IdentifyFileTypeAsync(
        byte[] fileContent,
        string? fileName = null,
        CancellationToken cancellationToken = default);
}

