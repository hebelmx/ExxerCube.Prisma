using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Infrastructure.FileStorage;

/// <summary>
/// File system-based implementation of download storage adapter.
/// </summary>
public class FileSystemDownloadStorageAdapter : IDownloadStorage
{
    private readonly ILogger<FileSystemDownloadStorageAdapter> _logger;
    private readonly FileStorageOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemDownloadStorageAdapter"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The file storage options.</param>
    public FileSystemDownloadStorageAdapter(
        ILogger<FileSystemDownloadStorageAdapter> logger,
        IOptions<FileStorageOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<Result<string>> SaveFileAsync(
        byte[] fileContent,
        string fileName,
        FileFormat format,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var checksum = ComputeChecksum(fileContent);
            var storagePath = GenerateStoragePath(fileName, format, checksum);

            _logger.LogInformation("Saving file {FileName} to {StoragePath}", fileName, storagePath);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(storagePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(storagePath, fileContent, cancellationToken);

            _logger.LogInformation("Successfully saved file {FileName} to {StoragePath}", fileName, storagePath);
            return Result<string>.Success(storagePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file {FileName}", fileName);
            return Result<string>.WithFailure(value: default, errors: new[] { $"Failed to save file: {ex.Message}" }, exception: ex);
        }
    }

    /// <inheritdoc />
    public string GenerateStoragePath(string fileName, FileFormat format, string? checksum = null)
    {
        var baseDirectory = _options.StorageBasePath;
        var formatFolder = format.ToString().ToLowerInvariant();
        var dateFolder = DateTime.UtcNow.ToString("yyyy-MM");

        // Use checksum-based path if checksum provided, otherwise use timestamp-based
        if (!string.IsNullOrEmpty(checksum))
        {
            var sanitizedFileName = SanitizeFileName(fileName);
            return Path.Combine(baseDirectory, formatFolder, dateFolder, checksum, sanitizedFileName);
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var extension = Path.GetExtension(fileName);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var sanitizedName = SanitizeFileName(nameWithoutExtension);
        var newFileName = $"{sanitizedName}_{timestamp}{extension}";

        return Path.Combine(baseDirectory, formatFolder, dateFolder, newFileName);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder(fileName.Length);

        foreach (var c in fileName)
        {
            if (Array.IndexOf(invalidChars, c) == -1)
            {
                sanitized.Append(c);
            }
            else
            {
                sanitized.Append('_');
            }
        }

        return sanitized.ToString();
    }

    private static string ComputeChecksum(byte[] content)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(content);
        return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
    }
}

/// <summary>
/// Configuration options for file storage.
/// </summary>
public class FileStorageOptions
{
    /// <summary>
    /// Gets or sets the base path for file storage.
    /// </summary>
    public string StorageBasePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base storage path for classified/organized files.
    /// </summary>
    public string BaseStoragePath { get; set; } = "Storage/Classified";
}

