using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.FileStorage;

/// <summary>
/// Service for generating safe, normalized file names based on classification and metadata.
/// </summary>
public class SafeFileNamerService : ISafeFileNamer
{
    private readonly ILogger<SafeFileNamerService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SafeFileNamerService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public SafeFileNamerService(ILogger<SafeFileNamerService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<string>> GenerateSafeFileNameAsync(
        string originalFileName,
        ClassificationResult classification,
        ExtractedMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Generating safe file name for: {OriginalFileName}", originalFileName);

            var extension = Path.GetExtension(originalFileName);
            var baseName = Path.GetFileNameWithoutExtension(originalFileName);

            // Build safe filename components
            var components = new System.Collections.Generic.List<string>();

            // Add classification prefix
            components.Add(classification.Level1.ToString().ToUpperInvariant());
            if (classification.Level2 is not null)
            {
                components.Add(classification.Level2.ToString().ToUpperInvariant());
            }

            // Add expediente number if available
            if (metadata?.Expediente != null && !string.IsNullOrEmpty(metadata.Expediente.NumeroExpediente))
            {
                var expedienteSafe = SanitizeForFileName(metadata.Expediente.NumeroExpediente);
                components.Add(expedienteSafe);
            }

            // Add timestamp for uniqueness
            components.Add(DateTime.Now.ToString("yyyyMMdd-HHmmss"));

            // Combine components
            var safeName = string.Join("_", components);
            
            // Ensure filename is not too long (Windows limit is 255 characters)
            if (safeName.Length > 200)
            {
                safeName = safeName.Substring(0, 200);
            }

            var safeFileName = $"{safeName}{extension}";

            _logger.LogDebug("Generated safe file name: {SafeFileName}", safeFileName);
            return Task.FromResult(Result<string>.Success(safeFileName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating safe file name");
            return Task.FromResult(Result<string>.WithFailure($"Error generating safe file name: {ex.Message}", default(string), ex));
        }
    }

    private static string SanitizeForFileName(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder();

        foreach (var c in input)
        {
            if (invalidChars.Contains(c))
            {
                sanitized.Append('_');
            }
            else if (char.IsWhiteSpace(c))
            {
                sanitized.Append('_');
            }
            else
            {
                sanitized.Append(c);
            }
        }

        return sanitized.ToString();
    }
}

