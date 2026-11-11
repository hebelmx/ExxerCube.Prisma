using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Infrastructure.FileStorage;

/// <summary>
/// Service for organizing files based on classification by moving them to appropriate directories.
/// </summary>
public class FileMoverService : IFileMover
{
    private readonly ILogger<FileMoverService> _logger;
    private readonly string _baseStoragePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileMoverService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The file storage options.</param>
    public FileMoverService(
        ILogger<FileMoverService> logger,
        IOptions<FileStorageOptions> options)
    {
        _logger = logger;
        _baseStoragePath = options.Value.BaseStoragePath;
        if (string.IsNullOrWhiteSpace(_baseStoragePath))
        {
            throw new ArgumentException("BaseStoragePath cannot be null or empty.", nameof(options));
        }
        Directory.CreateDirectory(_baseStoragePath); // Ensure base directory exists
    }

    /// <inheritdoc />
    public Task<Result<string>> MoveFileAsync(
        string sourcePath,
        ClassificationResult classification,
        string safeFileName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Moving file from {SourcePath} to classification-based directory", sourcePath);

            if (!File.Exists(sourcePath))
            {
                return Task.FromResult(Result<string>.WithFailure($"Source file does not exist: {sourcePath}"));
            }

            // Build destination directory path based on classification
            var destinationDir = BuildDestinationDirectory(classification);
            
            // Ensure destination directory exists
            Directory.CreateDirectory(destinationDir);

            // Build full destination path
            var destinationPath = Path.Combine(destinationDir, safeFileName);

            // Handle file name conflicts
            destinationPath = EnsureUniqueFileName(destinationPath);

            // Move the file
            File.Move(sourcePath, destinationPath, overwrite: false);

            _logger.LogInformation("File moved from {SourcePath} to {DestinationPath}", sourcePath, destinationPath);
            return Task.FromResult(Result<string>.Success(destinationPath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving file from {SourcePath}", sourcePath);
            return Task.FromResult(Result<string>.WithFailure($"Error moving file: {ex.Message}", default(string), ex));
        }
    }

    private string BuildDestinationDirectory(ClassificationResult classification)
    {
        var pathComponents = new System.Collections.Generic.List<string> { _baseStoragePath };

        // Level 1 category
        pathComponents.Add(classification.Level1.ToString());

        // Level 2 subcategory (if available)
        if (classification.Level2 is not null)
        {
            pathComponents.Add(classification.Level2.ToString());
        }

        // Year subdirectory
        pathComponents.Add(DateTime.Now.Year.ToString());

        return Path.Combine(pathComponents.ToArray());
    }

    private static string EnsureUniqueFileName(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return filePath;
        }

        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        var counter = 1;

        string newPath;
        do
        {
            var newFileName = $"{fileNameWithoutExtension}_{counter}{extension}";
            newPath = Path.Combine(directory, newFileName);
            counter++;
        }
        while (File.Exists(newPath) && counter < 1000);

        if (counter >= 1000)
        {
            throw new InvalidOperationException($"Unable to generate unique filename for: {filePath}");
        }

        return newPath;
    }
}

