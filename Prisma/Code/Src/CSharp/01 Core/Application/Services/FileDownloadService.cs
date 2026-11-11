namespace ExxerCube.Prisma.Application.Services;

/// <summary>
/// Service for downloading files from storage.
/// </summary>
public class FileDownloadService
{
    private readonly ILogger<FileDownloadService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileDownloadService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public FileDownloadService(ILogger<FileDownloadService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets file content from storage path.
    /// </summary>
    /// <param name="filePath">The storage path of the file.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the file content and metadata, or an error.</returns>
    public async Task<Result<FileDownloadResult>> GetFileContentAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("File download was cancelled before starting");
            return ResultExtensions.Cancelled<FileDownloadResult>();
        }

        // Input validation
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Result<FileDownloadResult>.WithFailure("File path cannot be null or empty");
        }

        try
        {
            // Security check: Ensure file exists
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File not found at path: {FilePath}", filePath);
                return Result<FileDownloadResult>.WithFailure($"File not found: {filePath}");
            }

            // Read file content
            var fileContent = await File.ReadAllBytesAsync(filePath, cancellationToken)
                .ConfigureAwait(false);

            var fileName = Path.GetFileName(filePath);
            var contentType = GetContentType(filePath);

            _logger.LogInformation(
                "Successfully read file {FileName} from {FilePath} ({Size} bytes)",
                fileName, filePath, fileContent.Length);

            var result = new FileDownloadResult
            {
                FileName = fileName,
                Content = fileContent,
                ContentType = contentType,
                FilePath = filePath
            };

            return Result<FileDownloadResult>.Success(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("File download was cancelled");
            return ResultExtensions.Cancelled<FileDownloadResult>();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Unauthorized access to file: {FilePath}", filePath);
            return Result<FileDownloadResult>.WithFailure($"Unauthorized access to file: {filePath}", default, ex);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error reading file: {FilePath}", filePath);
            return Result<FileDownloadResult>.WithFailure($"IO error reading file: {filePath}", default, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file: {FilePath}", filePath);
            return Result<FileDownloadResult>.WithFailure($"Error reading file: {filePath}", default, ex);
        }
    }

    private static string GetContentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".xml" => "application/xml",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".zip" => "application/zip",
            ".txt" => "text/plain",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }
}



