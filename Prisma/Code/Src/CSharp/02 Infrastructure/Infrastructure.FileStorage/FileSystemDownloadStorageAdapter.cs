using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using IndQuestResults.Operations;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Infrastructure.FileStorage;

/// <summary>
/// File system-based implementation of download storage adapter.
/// All files are encrypted at rest via <see cref="IStorageEncryptor"/> before being written
/// to disk and decrypted transparently on read. On-disk bytes are always ciphertext.
/// </summary>
public class FileSystemDownloadStorageAdapter : IDownloadStorage
{
    /// <summary>
    /// Purpose string used for HKDF sub-key derivation. Supplied at both write and read time
    /// but NEVER written to disk, so on-disk blobs carry no plaintext label.
    /// </summary>
    private const string EncryptionPurpose = "document-download";

    private readonly ILogger<FileSystemDownloadStorageAdapter> _logger;
    private readonly FileStorageOptions _options;
    private readonly IStorageEncryptor _encryptor;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemDownloadStorageAdapter"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The file storage options.</param>
    /// <param name="encryptor">The storage encryptor for at-rest encryption.</param>
    public FileSystemDownloadStorageAdapter(
        ILogger<FileSystemDownloadStorageAdapter> logger,
        IOptions<FileStorageOptions> options,
        IStorageEncryptor encryptor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
    }

    /// <inheritdoc />
    public async Task<Result<string>> SaveFileAsync(
        byte[] fileContent,
        string fileName,
        FileFormat format,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<string>();

        try
        {
            var checksum = ComputeChecksum(fileContent);
            var storagePath = GenerateStoragePath(fileName, format, checksum);

            _logger.LogInformation("Encrypting and saving file {FileName} to {StoragePath}", fileName, storagePath);

            // Encrypt plaintext before touching the file system
            var encryptResult = await _encryptor.EncryptAsync(fileContent, EncryptionPurpose, cancellationToken)
                .ConfigureAwait(false);

            if (encryptResult.IsFailure)
            {
                _logger.LogError("Encryption failed for {FileName}: {Error}", fileName, encryptResult.Error);
                return Result<string>.WithFailure($"Encryption failed: {encryptResult.Error}");
            }

            if (encryptResult.IsCancelled())
                return ResultExtensions.Cancelled<string>();

            var cipherBlob = encryptResult.Value!;

            // Ensure directory exists
            var directory = Path.GetDirectoryName(storagePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(storagePath, cipherBlob, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Successfully saved encrypted file {FileName} to {StoragePath} ({BlobBytes} bytes on disk)",
                fileName, storagePath, cipherBlob.Length);

            return Result<string>.Success(storagePath);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file {FileName}", fileName);
            return Result<string>.WithFailure(value: default, errors: new[] { $"Failed to save file: {ex.Message}" }, exception: ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> ReadFileAsync(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<byte[]>();

        if (string.IsNullOrWhiteSpace(storagePath))
            return Result<byte[]>.WithFailure("Storage path must not be null or empty.");

        try
        {
            if (!File.Exists(storagePath))
                return Result<byte[]>.WithFailure($"File not found at storage path: {storagePath}");

            var cipherBlob = await File.ReadAllBytesAsync(storagePath, cancellationToken).ConfigureAwait(false);

            var decryptResult = await _encryptor.DecryptAsync(cipherBlob, EncryptionPurpose, cancellationToken)
                .ConfigureAwait(false);

            if (decryptResult.IsCancelled())
                return ResultExtensions.Cancelled<byte[]>();

            if (decryptResult.IsFailure)
            {
                _logger.LogError("Decryption failed for {StoragePath}: {Error}", storagePath, decryptResult.Error);
                return Result<byte[]>.WithFailure($"Decryption failed for {storagePath}: {decryptResult.Error}");
            }

            return Result<byte[]>.Success(decryptResult.Value!);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<byte[]>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read file {StoragePath}", storagePath);
            return Result<byte[]>.WithFailure($"Failed to read file: {ex.Message}", default, ex);
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
