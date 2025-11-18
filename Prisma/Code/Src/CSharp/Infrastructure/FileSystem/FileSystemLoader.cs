namespace ExxerCube.Prisma.Infrastructure.FileSystem;

/// <summary>
/// File system loader adapter that implements IFileLoader with Railway Oriented Programming.
/// </summary>
public class FileSystemLoader : IFileLoader
{
    private readonly ILogger<FileSystemLoader> _logger;
    private readonly string[] _supportedExtensions = { ".png", ".jpg", ".jpeg", ".tiff", ".tif", ".bmp", ".pdf" };

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemLoader"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public FileSystemLoader(ILogger<FileSystemLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Loads an image from a file path.
    /// </summary>
    /// <param name="filePath">The path to the image file.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the loaded image data or an error.</returns>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task<Result<ImageData>> LoadImageAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogDebug("Running on Windows platform");
            return Result<ImageData>.WithFailure($"Unsupported file extension: OS");
        }

        // Check for cancellation before starting work
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Image loading cancelled before starting");
            return ResultExtensions.Cancelled<ImageData>();
        }

        try
        {
            _logger.LogInformation("Loading image from file {FilePath}", filePath);

            // Validate file path
            var validationResult = await ValidateFilePathAsync(filePath, cancellationToken).ConfigureAwait(false);

            // Propagate cancellation from dependencies
            if (validationResult.IsCancelled())
            {
                _logger.LogWarning("Image loading cancelled during validation");
                return ResultExtensions.Cancelled<ImageData>();
            }

            if (!validationResult.IsSuccess)
            {
                return Result<ImageData>.WithFailure(validationResult.Error!);
            }

            // Check for cancellation before loading
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Image loading cancelled before file load");
                return ResultExtensions.Cancelled<ImageData>();
            }

            // Check file extension
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (!_supportedExtensions.Contains(extension))
            {
                return Result<ImageData>.WithFailure($"Unsupported file extension: {extension}");
            }

            // Load image data - CRITICAL: Pass cancellation token to Task.Run
            byte[] imageData;
            if (extension == ".pdf")
            {
                imageData = await Task.Run(() => LoadPdfAsImage(filePath), cancellationToken);
            }
            else
            {
                imageData = await Task.Run(() => LoadImageFile(filePath), cancellationToken);
            }

            var result = new ImageData
            {
                Data = imageData,
                SourcePath = filePath,
                PageNumber = 1,
                TotalPages = 1
            };

            _logger.LogInformation("Successfully loaded image from {FilePath} ({Size} bytes)",
                filePath, imageData.Length);
            return Result<ImageData>.Success(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Image loading cancelled for {FilePath}", filePath);
            return ResultExtensions.Cancelled<ImageData>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading image from file {FilePath}", filePath);
            return Result<ImageData>.WithFailure($"Failed to load image: {ex.Message}", default, ex);
        }
    }

    /// <summary>
    /// Loads multiple images from a directory.
    /// </summary>
    /// <param name="directoryPath">The path to the directory containing images.</param>
    /// <param name="supportedExtensions">The supported file extensions.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of loaded image data or an error.</returns>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task<Result<List<ImageData>>> LoadImagesFromDirectoryAsync(string directoryPath, string[] supportedExtensions, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogDebug("Running on Windows platform");
            return Result<List<ImageData>>.WithFailure($"Unsupported file extension: OS");
        }
        // Check for cancellation before starting work
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Directory image loading cancelled before starting");
            return ResultExtensions.Cancelled<List<ImageData>>();
        }

        try
        {
            _logger.LogInformation("Loading images from directory {DirectoryPath}", directoryPath);

            if (!Directory.Exists(directoryPath))
            {
                return Result<List<ImageData>>.WithFailure($"Directory does not exist: {directoryPath}");
            }

            var extensions = supportedExtensions.Length > 0 ? supportedExtensions : _supportedExtensions;
            var searchPattern = string.Join("|", extensions.Select(ext => $"*{ext}"));
            var files = Directory.GetFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(file => extensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .ToArray();

            if (!files.Any())
            {
                _logger.LogWarning("No supported image files found in directory {DirectoryPath}", directoryPath);
                return Result<List<ImageData>>.Success(new List<ImageData>());
            }

            var imageDataList = new List<ImageData>();
            var cancelledResults = new List<Result<ImageData>>();
            var errors = new List<string>();

            foreach (var file in files)
            {
                // Check for cancellation between iterations
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Directory image loading cancelled during processing");
                    break;
                }

                var loadResult = await LoadImageAsync(file, cancellationToken).ConfigureAwait(false);

                // Propagate cancellation from dependencies
                if (loadResult.IsCancelled())
                {
                    cancelledResults.Add(loadResult);
                }
                else if (loadResult.IsSuccess)
                {
                    imageDataList.Add(loadResult.Value!);
                }
                else
                {
                    errors.Add($"Failed to load {file}: {loadResult.Error}");
                }
            }

            // Handle cancellation with partial results
            var wasCancelled = cancellationToken.IsCancellationRequested || cancelledResults.Any();

            if (wasCancelled)
            {
                if (imageDataList.Count > 0)
                {
                    // Return partial results with warning about cancellation
                    var totalRequested = files.Length;
                    var completed = imageDataList.Count;
                    var cancelled = cancelledResults.Count;
                    var confidence = (double)completed / totalRequested;
                    var missingDataRatio = (double)(cancelled + errors.Count) / totalRequested;

                    _logger.LogWarning(
                        "Directory image loading cancelled. Returning {CompletedCount} of {TotalCount} loaded images. " +
                        "Cancelled: {CancelledCount}, Failed: {FailedCount}",
                        completed, totalRequested, cancelled, errors.Count);

                    return Result<List<ImageData>>.WithWarnings(
                        warnings: new[] { $"Operation was cancelled. Loaded {completed} of {totalRequested} images." },
                        value: imageDataList,
                        confidence: confidence,
                        missingDataRatio: missingDataRatio
                    );
                }
                else
                {
                    // No partial results - return cancelled
                    _logger.LogWarning("Directory image loading cancelled with no completed results");
                    return ResultExtensions.Cancelled<List<ImageData>>();
                }
            }

            if (errors.Any())
            {
                _logger.LogWarning("Some files failed to load: {ErrorCount} errors", errors.Count);
            }

            _logger.LogInformation("Successfully loaded {SuccessCount} images from directory {DirectoryPath}",
                imageDataList.Count, directoryPath);
            return Result<List<ImageData>>.Success(imageDataList);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Directory image loading cancelled");
            return ResultExtensions.Cancelled<List<ImageData>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading images from directory {DirectoryPath}", directoryPath);
            return Result<List<ImageData>>.WithFailure($"Failed to load images from directory: {ex.Message}", default, ex);
        }
    }

    /// <summary>
    /// Gets the list of supported file extensions.
    /// </summary>
    /// <returns>An array of supported file extensions.</returns>
    public string[] GetSupportedExtensions()
    {
        return _supportedExtensions;
    }

    /// <summary>
    /// Validates if a file path is valid and accessible.
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating validation success or failure.</returns>
    public async Task<Result<bool>> ValidateFilePathAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // Check for cancellation before starting work
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<bool>();
        }

        try
        {
            // CRITICAL: Pass cancellation token to Task.Run
            return await Task.Run(() => ValidateFilePath(filePath), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<bool>();
        }
    }

    /// <summary>
    /// Validates a file path synchronously.
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <returns>A result indicating validation success or failure.</returns>
    private Result<bool> ValidateFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Result<bool>.WithFailure("File path cannot be null or empty");
        }

        // Check for path traversal attacks
        var normalizedPath = Path.GetFullPath(filePath);
        if (!normalizedPath.Equals(filePath, StringComparison.OrdinalIgnoreCase))
        {
            return Result<bool>.WithFailure("Invalid file path");
        }

        if (!File.Exists(filePath))
        {
            return Result<bool>.WithFailure($"File does not exist: {filePath}");
        }

        try
        {
            // Test file access
            using var stream = File.OpenRead(filePath);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.WithFailure($"Cannot access file: {ex.Message}", default, ex);
        }
    }

    /// <summary>
    /// Loads an image file as byte array.
    /// </summary>
    /// <param name="filePath">The path to the image file.</param>
    /// <returns>The image data as byte array.</returns>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private byte[] LoadImageFile(string filePath)
    {
        try
        {
            using var image = Image.FromFile(filePath);
            using var memoryStream = new MemoryStream();
            image.Save(memoryStream, ImageFormat.Png);
            return memoryStream.ToArray();
        }
        catch (System.Runtime.InteropServices.ExternalException ex) when (ex.HResult == unchecked((int)0x80004005))
        {
            // GDI+ error - try reading file directly as bytes instead
            // This handles cases where the image file is valid but GDI+ can't process it
            return File.ReadAllBytes(filePath);
        }
    }

    /// <summary>
    /// Loads a PDF file as image (simplified implementation).
    /// </summary>
    /// <param name="filePath">The path to the PDF file.</param>
    /// <returns>The image data as byte array.</returns>
    private byte[] LoadPdfAsImage(string filePath)
    {
        // Simplified implementation - in real scenario, use a PDF library like iText7 or PdfSharp
        _logger.LogWarning("PDF loading is not fully implemented for {FilePath}", filePath);
        return new byte[0];
    }
}