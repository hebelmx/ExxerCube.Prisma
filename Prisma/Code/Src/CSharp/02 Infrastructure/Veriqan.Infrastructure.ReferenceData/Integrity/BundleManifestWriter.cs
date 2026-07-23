using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Integrity;

/// <summary>
/// Authoring helper that generates (or overwrites) the SHA-256 manifest and detached
/// HMAC-SHA256 signature for a Veriqan reference-bundle directory.
/// </summary>
/// <remarks>
/// Bundle authors and tests use this to (re-)sign a bundle after adding or editing any
/// <c>*.csv</c> file, producing the pair of files that
/// <see cref="BundleIntegrityVerifier"/> checks at load time:
/// <see cref="BundleIntegrityVerifier.ManifestFileName"/> and
/// <see cref="BundleIntegrityVerifier.SignatureFileName"/>.
/// </remarks>
public static class BundleManifestWriter
{
    /// <summary>
    /// Generates (or overwrites) <see cref="BundleIntegrityVerifier.ManifestFileName"/> and
    /// <see cref="BundleIntegrityVerifier.SignatureFileName"/> in <paramref name="directoryPath"/>,
    /// covering every <c>*.csv</c> file currently present in that directory.
    /// </summary>
    /// <param name="directoryPath">Absolute path of the institution bundle directory.</param>
    /// <param name="hmacKey">The HMAC key used to sign the generated manifest.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// A successful <see cref="Result"/> once both files are written; a failure result if the
    /// directory does not exist, contains no CSV files, or an I/O error occurs.
    /// </returns>
    public static async Task<Result> WriteManifestAsync(
        string directoryPath,
        string hmacKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        ArgumentNullException.ThrowIfNull(hmacKey);

        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled();

        if (!Directory.Exists(directoryPath))
            return Result.WithFailure($"Bundle directory not found: '{directoryPath}'.");

        var csvFileNames = Directory.EnumerateFiles(directoryPath, "*.csv")
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        if (csvFileNames.Count == 0)
            return Result.WithFailure($"No CSV files found to sign in bundle directory: '{directoryPath}'.");

        var manifestLines = new List<string>(csvFileNames.Count);

        foreach (var fileName in csvFileNames)
        {
            if (cancellationToken.IsCancellationRequested)
                return ResultExtensions.Cancelled();

            var filePath = Path.Combine(directoryPath, fileName);
            byte[] fileBytes;
            try
            {
                fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return ResultExtensions.Cancelled();
            }
            catch (IOException ex)
            {
                return Result.WithFailure($"Error reading '{fileName}' while building the manifest: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result.WithFailure($"Error reading '{fileName}' while building the manifest: {ex.Message}");
            }

            var hashHex = Convert.ToHexStringLower(SHA256.HashData(fileBytes));
            manifestLines.Add($"{hashHex}  {fileName}");
        }

        var manifestBytes = Encoding.UTF8.GetBytes(string.Join('\n', manifestLines) + "\n");
        var manifestPath = Path.Combine(directoryPath, BundleIntegrityVerifier.ManifestFileName);
        var signaturePath = Path.Combine(directoryPath, BundleIntegrityVerifier.SignatureFileName);

        var signatureHex = Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(hmacKey), manifestBytes));

        try
        {
            await File.WriteAllBytesAsync(manifestPath, manifestBytes, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(signaturePath, signatureHex, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ResultExtensions.Cancelled();
        }
        catch (IOException ex)
        {
            return Result.WithFailure($"Error writing manifest/signature files: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result.WithFailure($"Error writing manifest/signature files: {ex.Message}");
        }

        return Result.Success();
    }
}
