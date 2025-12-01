namespace ExxerCube.Prisma.Infrastructure.FileSystem;

/// <summary>
/// File system output writer adapter that implements IOutputWriter with Railway Oriented Programming.
/// </summary>
public class FileSystemOutputWriter : IOutputWriter
{
    private readonly ILogger<FileSystemOutputWriter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemOutputWriter"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public FileSystemOutputWriter(ILogger<FileSystemOutputWriter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Writes a processing result to the specified output path.
    /// </summary>
    /// <param name="result">The processing result to write.</param>
    /// <param name="outputPath">The output file path.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result<bool>> WriteResultAsync(ProcessingResult result, string outputPath, CancellationToken cancellationToken = default)
    {
        // Check for cancellation before starting work
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Result writing cancelled before starting");
            return ResultExtensions.Cancelled<bool>();
        }

        try
        {
            _logger.LogInformation("Writing processing result to {OutputPath}", outputPath);

            // Validate output path
            var validationResult = ValidateOutputPath(outputPath);
            if (!validationResult.IsSuccess)
            {
                return Result<bool>.WithFailure(validationResult.Error!);
            }

            // Check for cancellation before writing
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Result writing cancelled before file write");
                return ResultExtensions.Cancelled<bool>();
            }

            // Determine output format based on file extension
            var extension = Path.GetExtension(outputPath).ToLowerInvariant();
            switch (extension)
            {
                case ".json":
                    return await WriteJsonAsync(result, outputPath, cancellationToken).ConfigureAwait(false);
                case ".txt":
                    return await WriteTextAsync(result, outputPath, cancellationToken).ConfigureAwait(false);
                default:
                    return Result<bool>.WithFailure($"Unsupported output format: {extension}");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Result writing cancelled for {OutputPath}", outputPath);
            return ResultExtensions.Cancelled<bool>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing processing result to {OutputPath}", outputPath);
            return Result<bool>.WithFailure($"Failed to write result: {ex.Message}", default, ex);
        }
    }

    /// <summary>
    /// Writes multiple processing results to a directory.
    /// </summary>
    /// <param name="results">The list of processing results to write.</param>
    /// <param name="outputDirectory">The output directory path.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result<bool>> WriteResultsAsync(IEnumerable<ProcessingResult> results, string outputDirectory, CancellationToken cancellationToken = default)
    {
        // Check for cancellation before starting work
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Batch result writing cancelled before starting");
            return ResultExtensions.Cancelled<bool>();
        }

        try
        {
            _logger.LogInformation("Writing {ResultCount} processing results to directory {OutputDirectory}", 
                results.Count(), outputDirectory);

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var resultsList = results.ToList();
            var errors = new List<string>();
            var cancelledWrites = 0;

            foreach (var result in resultsList)
            {
                // Check for cancellation between iterations
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Batch result writing cancelled during processing");
                    break;
                }

                var fileName = Path.GetFileNameWithoutExtension(result.SourcePath);
                var jsonPath = Path.Combine(outputDirectory, $"{fileName}.json");
                var textPath = Path.Combine(outputDirectory, $"{fileName}.txt");

                var jsonResult = await WriteJsonAsync(result, jsonPath, cancellationToken).ConfigureAwait(false);
                var textResult = await WriteTextAsync(result, textPath, cancellationToken).ConfigureAwait(false);

                // Propagate cancellation from dependencies
                if (jsonResult.IsCancelled() || textResult.IsCancelled())
                {
                    cancelledWrites++;
                    break;
                }

                if (!jsonResult.IsSuccess)
                {
                    errors.Add($"Failed to write JSON for {fileName}: {jsonResult.Error}");
                }

                if (!textResult.IsSuccess)
                {
                    errors.Add($"Failed to write text for {fileName}: {textResult.Error}");
                }
            }

            // Handle cancellation
            var wasCancelled = cancellationToken.IsCancellationRequested || cancelledWrites > 0;
            
            if (wasCancelled && errors.Count == 0)
            {
                _logger.LogWarning("Batch result writing cancelled");
                return ResultExtensions.Cancelled<bool>();
            }

            if (errors.Any())
            {
                _logger.LogWarning("Some results failed to write: {ErrorCount} errors", errors.Count);
                return Result<bool>.WithFailure($"Some results failed to write: {string.Join("; ", errors)}");
            }

            _logger.LogInformation("Successfully wrote {ResultCount} processing results to directory {OutputDirectory}", 
                resultsList.Count, outputDirectory);
            return Result<bool>.Success(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Batch result writing cancelled");
            return ResultExtensions.Cancelled<bool>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing processing results to directory {OutputDirectory}", outputDirectory);
            return Result<bool>.WithFailure($"Failed to write results: {ex.Message}", default, ex);
        }
    }

    /// <summary>
    /// Writes results in JSON format.
    /// </summary>
    /// <param name="result">The processing result to write.</param>
    /// <param name="outputPath">The output file path.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result<bool>> WriteJsonAsync(ProcessingResult result, string outputPath, CancellationToken cancellationToken = default)
    {
        // Check for cancellation before starting work
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("JSON writing cancelled before starting");
            return ResultExtensions.Cancelled<bool>();
        }

        try
        {
            _logger.LogInformation("Writing JSON result to {OutputPath}", outputPath);

            var jsonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };

            var json = JsonConvert.SerializeObject(result, jsonSettings);
            
            // CRITICAL: Pass cancellation token to File.WriteAllTextAsync
            await File.WriteAllTextAsync(outputPath, json, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully wrote JSON result to {OutputPath}", outputPath);
            return Result<bool>.Success(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("JSON writing cancelled for {OutputPath}", outputPath);
            return ResultExtensions.Cancelled<bool>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing JSON result to {OutputPath}", outputPath);
            return Result<bool>.WithFailure($"Failed to write JSON: {ex.Message}", default, ex);
        }
    }

    /// <summary>
    /// Writes results in text format.
    /// </summary>
    /// <param name="result">The processing result to write.</param>
    /// <param name="outputPath">The output file path.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result<bool>> WriteTextAsync(ProcessingResult result, string outputPath, CancellationToken cancellationToken = default)
    {
        // Check for cancellation before starting work
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Text writing cancelled before starting");
            return ResultExtensions.Cancelled<bool>();
        }

        try
        {
            _logger.LogInformation("Writing text result to {OutputPath}", outputPath);

            var textLines = new List<string>
            {
                $"Processing Result for: {result.SourcePath}",
                $"Page Number: {result.PageNumber}",
                "",
                "OCR Result:",
                $"Text: {result.OCRResult.Text}",
                $"Confidence Average: {result.OCRResult.ConfidenceAvg:F2}%",
                $"Confidence Median: {result.OCRResult.ConfidenceMedian:F2}%",
                $"Language Used: {result.OCRResult.LanguageUsed}",
                "",
                "Extracted Fields:",
                $"Expediente: {result.ExtractedFields.Expediente ?? "Not found"}",
                $"Causa: {result.ExtractedFields.Causa ?? "Not found"}",
                $"Acción Solicitada: {result.ExtractedFields.AccionSolicitada ?? "Not found"}",
                "",
                "Dates:",
            };

            if (result.ExtractedFields.Fechas.Any())
            {
                foreach (var date in result.ExtractedFields.Fechas)
                {
                    textLines.Add($"  - {date}");
                }
            }
            else
            {
                textLines.Add("  - No dates found");
            }

            textLines.Add("");
            textLines.Add("Monetary Amounts:");

            if (result.ExtractedFields.Montos.Any())
            {
                foreach (var amount in result.ExtractedFields.Montos)
                {
                    textLines.Add($"  - {amount.Value:C} {amount.Currency} (from: {amount.OriginalText})");
                }
            }
            else
            {
                textLines.Add("  - No amounts found");
            }

            if (result.ProcessingErrors.Any())
            {
                textLines.Add("");
                textLines.Add("Processing Errors:");
                foreach (var error in result.ProcessingErrors)
                {
                    textLines.Add($"  - {error}");
                }
            }

            // CRITICAL: Pass cancellation token to File.WriteAllLinesAsync
            await File.WriteAllLinesAsync(outputPath, textLines, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully wrote text result to {OutputPath}", outputPath);
            return Result<bool>.Success(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Text writing cancelled for {OutputPath}", outputPath);
            return ResultExtensions.Cancelled<bool>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing text result to {OutputPath}", outputPath);
            return Result<bool>.WithFailure($"Failed to write text: {ex.Message}", default, ex);
        }
    }

    /// <summary>
    /// Validates the output path for writing.
    /// </summary>
    /// <param name="outputPath">The output path to validate.</param>
    /// <returns>A result indicating validation success or failure.</returns>
    private Result<bool> ValidateOutputPath(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return Result<bool>.WithFailure("Output path cannot be null or empty");
        }

        // Check for path traversal attacks
        var normalizedPath = Path.GetFullPath(outputPath);
        if (!normalizedPath.Equals(outputPath, StringComparison.OrdinalIgnoreCase))
        {
            return Result<bool>.WithFailure("Invalid output path");
        }

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.WithFailure($"Cannot create output directory: {ex.Message}", default, ex);
        }
    }
}
