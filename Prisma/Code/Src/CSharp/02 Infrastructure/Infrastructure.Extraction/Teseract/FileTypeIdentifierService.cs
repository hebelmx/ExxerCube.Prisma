namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

/// <summary>
/// Service for identifying file types based on content analysis (not just extension).
/// </summary>
public class FileTypeIdentifierService : IFileTypeIdentifier
{
    private readonly ILogger<FileTypeIdentifierService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTypeIdentifierService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public FileTypeIdentifierService(ILogger<FileTypeIdentifierService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<FileFormat>> IdentifyFileTypeAsync(
        byte[] fileContent,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        if (fileContent == null || fileContent.Length == 0)
        {
            return Task.FromResult(Result<FileFormat>.WithFailure("File content is null or empty"));
        }

        try
        {
            // Check file signatures (magic numbers) for content-based identification
            var format = IdentifyByContent(fileContent);

            // Fallback to extension if content identification fails
            if (format == null && !string.IsNullOrEmpty(fileName))
            {
                format = IdentifyByExtension(fileName);
            }

            if (format == null)
            {
                _logger.LogWarning("Could not identify file type for file: {FileName}", fileName ?? "unknown");
                return Task.FromResult(Result<FileFormat>.WithFailure("Unable to identify file type"));
            }

            _logger.LogDebug("Identified file type as {Format} for file: {FileName}", format.Value, fileName ?? "unknown");
            return Task.FromResult(Result<FileFormat>.Success(format.Value));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error identifying file type for file: {FileName}", fileName ?? "unknown");
            return Task.FromResult(Result<FileFormat>.WithFailure($"Error identifying file type: {ex.Message}", default(FileFormat), ex));
        }
    }

    private static FileFormat? IdentifyByContent(byte[] content)
    {
        if (content.Length < 4)
        {
            return null;
        }

        // PDF signature: %PDF
        if (content[0] == 0x25 && content[1] == 0x50 && content[2] == 0x44 && content[3] == 0x46)
        {
            return FileFormat.Pdf;
        }

        // XML signature: <?xml or <root
        if (content.Length >= 5)
        {
            var start = System.Text.Encoding.UTF8.GetString(content, 0, Math.Min(100, content.Length));
            if (start.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) ||
                start.TrimStart().StartsWith("<", StringComparison.OrdinalIgnoreCase))
            {
                return FileFormat.Xml;
            }
        }

        // DOCX signature: PK (ZIP format) with specific internal structure
        // DOCX files are ZIP archives, so check for ZIP signature
        if (content[0] == 0x50 && content[1] == 0x4B && content[2] == 0x03 && content[3] == 0x04)
        {
            // Check if it's a DOCX by looking for word/document.xml in the ZIP
            // For now, we'll check if the ZIP contains DOCX-specific markers
            // A more robust check would require parsing the ZIP structure
            var contentStr = System.Text.Encoding.UTF8.GetString(content, 0, Math.Min(2000, content.Length));
            if (contentStr.Contains("word/", StringComparison.OrdinalIgnoreCase) ||
                contentStr.Contains("xl/", StringComparison.OrdinalIgnoreCase))
            {
                // Check if it's DOCX (word/) vs XLSX (xl/)
                if (contentStr.Contains("word/", StringComparison.OrdinalIgnoreCase))
                {
                    return FileFormat.Docx;
                }
            }
            else
            {
                // Could be a generic ZIP file
                return FileFormat.Zip;
            }
        }

        return null;
    }

    private static FileFormat? IdentifyByExtension(string fileName)
    {
        var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => FileFormat.Pdf,
            ".xml" => FileFormat.Xml,
            ".docx" => FileFormat.Docx,
            ".zip" => FileFormat.Zip,
            _ => null
        };
    }
}

