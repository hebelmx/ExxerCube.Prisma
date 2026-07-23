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
/// Verifies the SHA-256 manifest and detached HMAC-SHA256 signature of a Veriqan
/// reference-bundle directory before its CSV files are trusted for parsing.
/// </summary>
/// <remarks>
/// <para>
/// The manifest (<see cref="ManifestFileName"/>) lists every <c>*.csv</c> file in the bundle
/// directory as one line of <c>&lt;lowercase-hex-sha256&gt;  &lt;relative-filename&gt;</c>.
/// The signature (<see cref="SignatureFileName"/>) is the lowercase-hex HMAC-SHA256 of the
/// exact manifest file bytes, keyed with the UTF-8 bytes of the configured bundle HMAC key.
/// </para>
/// <para>
/// Verification is <strong>fail-closed</strong>: a missing manifest, a missing signature, a
/// signature mismatch, a missing listed file, a hash mismatch, or a <c>*.csv</c> file present
/// on disk but absent from the manifest (a "rogue file") are all treated as failures. Callers
/// must not proceed to parse the bundle when this returns a failed <see cref="Result"/>.
/// </para>
/// </remarks>
public static class BundleIntegrityVerifier
{
    /// <summary>
    /// Name of the SHA-256 manifest file, one line per covered CSV file:
    /// <c>&lt;lowercase-hex-sha256&gt;  &lt;relative-filename&gt;</c>.
    /// </summary>
    public const string ManifestFileName = "bundle-manifest.sha256";

    /// <summary>
    /// Name of the detached HMAC-SHA256 signature file. Contents are the lowercase-hex
    /// HMAC-SHA256 of the exact bytes of <see cref="ManifestFileName"/>.
    /// </summary>
    public const string SignatureFileName = "bundle-manifest.hmac";

    /// <summary>
    /// Verifies the integrity of every <c>*.csv</c> file in <paramref name="directoryPath"/>
    /// against the manifest and HMAC signature found in that same directory.
    /// </summary>
    /// <param name="directoryPath">Absolute path of the institution bundle directory.</param>
    /// <param name="hmacKey">
    /// The HMAC key (already known to be non-empty by the caller) used to verify the
    /// detached signature over the manifest bytes.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the verification.</param>
    /// <returns>
    /// A successful <see cref="Result"/> when the manifest, signature, and every listed file
    /// hash match, and no untracked <c>*.csv</c> file is present; otherwise a failure result
    /// describing the first problem found.
    /// </returns>
    public static async Task<Result> VerifyAsync(
        string directoryPath,
        string hmacKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        ArgumentNullException.ThrowIfNull(hmacKey);

        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled();

        var manifestPath = Path.Combine(directoryPath, ManifestFileName);
        var signaturePath = Path.Combine(directoryPath, SignatureFileName);

        if (!File.Exists(manifestPath))
            return Result.WithFailure(
                $"Bundle integrity manifest not found: '{manifestPath}'.");

        if (!File.Exists(signaturePath))
            return Result.WithFailure(
                $"Bundle integrity signature not found: '{signaturePath}'.");

        byte[] manifestBytes;
        string signatureText;
        try
        {
            manifestBytes = await File.ReadAllBytesAsync(manifestPath, cancellationToken).ConfigureAwait(false);
            signatureText = await File.ReadAllTextAsync(signaturePath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ResultExtensions.Cancelled();
        }
        catch (IOException ex)
        {
            return Result.WithFailure($"Error reading bundle integrity files: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result.WithFailure($"Error reading bundle integrity files: {ex.Message}");
        }

        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled();

        if (!TryDecodeHex(signatureText.Trim(), out var expectedSignature))
            return Result.WithFailure(
                $"Bundle integrity signature file is malformed (not lowercase-hex): '{signaturePath}'.");

        var computedSignature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(hmacKey), manifestBytes);

        if (!CryptographicOperations.FixedTimeEquals(computedSignature, expectedSignature))
            return Result.WithFailure(
                "Bundle integrity signature mismatch (HMAC-SHA256 verification failed).");

        if (!TryParseManifest(manifestBytes, out var manifestEntries))
            return Result.WithFailure(
                $"Bundle integrity manifest is malformed: '{manifestPath}'.");

        // Rogue-file guard: every *.csv on disk must be tracked by the manifest.
        var manifestFileNames = new HashSet<string>(
            manifestEntries.Select(e => e.FileName), StringComparer.Ordinal);

        var rogueFiles = Directory.EnumerateFiles(directoryPath, "*.csv")
            .Select(Path.GetFileName)
            .Where(name => name is not null && !manifestFileNames.Contains(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        if (rogueFiles.Count > 0)
            return Result.WithFailure(
                "Bundle contains CSV file(s) not covered by the integrity manifest: " +
                string.Join(", ", rogueFiles) + ".");

        foreach (var entry in manifestEntries)
        {
            if (cancellationToken.IsCancellationRequested)
                return ResultExtensions.Cancelled();

            var filePath = Path.Combine(directoryPath, entry.FileName);
            if (!File.Exists(filePath))
                return Result.WithFailure(
                    $"Bundle integrity manifest lists a file that is missing on disk: '{entry.FileName}'.");

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
                return Result.WithFailure($"Error reading '{entry.FileName}' during integrity check: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result.WithFailure($"Error reading '{entry.FileName}' during integrity check: {ex.Message}");
            }

            var actualHashHex = Convert.ToHexStringLower(SHA256.HashData(fileBytes));
            if (!string.Equals(actualHashHex, entry.HashHex, StringComparison.Ordinal))
                return Result.WithFailure(
                    $"Bundle integrity hash mismatch for '{entry.FileName}': the file on disk does not match the signed manifest.");
        }

        return Result.Success();
    }

    private static bool TryParseManifest(byte[] manifestBytes, out List<ManifestEntry> entries)
    {
        entries = [];
        string text;
        try
        {
            text = Encoding.UTF8.GetString(manifestBytes);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // sha256sum-style: "<64-hex-chars>  <filename>" (one or more spaces as separator).
            var separatorIndex = line.IndexOf(' ');
            if (separatorIndex <= 0)
                return false;

            var hash = line[..separatorIndex];
            var fileName = line[separatorIndex..].TrimStart(' ');

            if (hash.Length != 64 || fileName.Length == 0 || !IsHex(hash))
                return false;

            entries.Add(new ManifestEntry(fileName, hash.ToLowerInvariant()));
        }

        return true;
    }

    private static bool IsHex(string value)
    {
        foreach (var c in value)
        {
            if (!Uri.IsHexDigit(c))
                return false;
        }

        return true;
    }

    private static bool TryDecodeHex(string hex, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromHexString(hex);
            return true;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }

    private readonly record struct ManifestEntry(string FileName, string HashHex);
}
