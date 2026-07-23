using System;
using System.Collections.Generic;
using System.Globalization;
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
/// The manifest (<see cref="ManifestFileName"/>) is a signed <strong>manifest format v2</strong>
/// document: a two-line header —
/// <c>bundle: &lt;institution-directory-leaf-name&gt;</c> then
/// <c>generatedAt: &lt;ISO-8601 UTC timestamp&gt;</c> — followed by one
/// <c>&lt;hex-sha256&gt;  &lt;relative-filename&gt;</c> line per <c>*.csv</c> file in the bundle
/// directory (hex case is accepted case-insensitively and normalized to lowercase internally;
/// files are written in lowercase hex by <see cref="BundleManifestWriter"/>).
/// The signature (<see cref="SignatureFileName"/>) is the lowercase-hex HMAC-SHA256 of the
/// exact manifest file bytes (header included), keyed with the UTF-8 bytes of the configured
/// bundle HMAC key.
/// </para>
/// <para>
/// The <c>bundle:</c> header binds the signed manifest to the specific institution directory
/// it was generated for: <see cref="VerifyAsync"/> compares it (ordinal) against the actual
/// leaf name of the directory being verified. This defeats "whole-directory replay" — copying
/// a validly-signed bundle from one institution's directory into another's no longer passes,
/// because the copied manifest's <c>bundle:</c> value will not match the destination directory
/// name. There is currently no freshness/TTL policy on <c>generatedAt:</c>, so replaying an
/// old, still-validly-signed snapshot back into the <em>same</em> institution's directory
/// (a rollback) is a documented residual risk — see the authoring guide.
/// </para>
/// <para>
/// Verification is <strong>fail-closed</strong>: a missing manifest, a missing signature, a
/// signature mismatch, a missing or malformed header, a <c>bundle:</c> mismatch, an
/// unparseable <c>generatedAt:</c>, a missing listed file, a hash mismatch, or a <c>*.csv</c>
/// file present on disk but absent from the manifest (a "rogue file") are all treated as
/// failures. Callers must not proceed to parse the bundle when this returns a failed
/// <see cref="Result{T}"/>.
/// </para>
/// <para>
/// On success, the returned value carries every verified file's exact bytes (filename →
/// content, keyed ordinally) as they were read and hashed during verification. Callers must
/// parse from these bytes rather than re-opening the files from disk, to close the
/// verify-then-reread (TOCTOU) window between verification and parsing.
/// </para>
/// </remarks>
public static class BundleIntegrityVerifier
{
    /// <summary>
    /// Name of the SHA-256 manifest file (format v2): a <c>bundle:</c> / <c>generatedAt:</c>
    /// header followed by one <c>&lt;hex-sha256&gt;  &lt;filename&gt;</c> line per covered CSV file.
    /// </summary>
    public const string ManifestFileName = "bundle-manifest.sha256";

    /// <summary>
    /// Name of the detached HMAC-SHA256 signature file. Contents are the lowercase-hex
    /// HMAC-SHA256 of the exact bytes of <see cref="ManifestFileName"/>.
    /// </summary>
    public const string SignatureFileName = "bundle-manifest.hmac";

    private const string BundleHeaderPrefix = "bundle: ";
    private const string GeneratedAtHeaderPrefix = "generatedAt: ";

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
    /// A successful <see cref="Result{T}"/> carrying every verified file's bytes (filename →
    /// content, <see cref="StringComparer.Ordinal"/>-keyed) when the manifest header, signature,
    /// and every listed file hash match, and no untracked <c>*.csv</c> file is present;
    /// otherwise a failure result describing the first problem found.
    /// </returns>
    public static async Task<Result<IReadOnlyDictionary<string, byte[]>>> VerifyAsync(
        string directoryPath,
        string hmacKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        ArgumentNullException.ThrowIfNull(hmacKey);

        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<IReadOnlyDictionary<string, byte[]>>();

        var manifestPath = Path.Combine(directoryPath, ManifestFileName);
        var signaturePath = Path.Combine(directoryPath, SignatureFileName);

        if (!File.Exists(manifestPath))
            return Result<IReadOnlyDictionary<string, byte[]>>.WithFailure(
                $"Bundle integrity manifest not found: '{manifestPath}'.");

        if (!File.Exists(signaturePath))
            return Result<IReadOnlyDictionary<string, byte[]>>.WithFailure(
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
            return ResultExtensions.Cancelled<IReadOnlyDictionary<string, byte[]>>();
        }
        catch (IOException ex)
        {
            return Result<IReadOnlyDictionary<string, byte[]>>.WithFailure(
                $"Error reading bundle integrity files: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result<IReadOnlyDictionary<string, byte[]>>.WithFailure(
                $"Error reading bundle integrity files: {ex.Message}");
        }

        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<IReadOnlyDictionary<string, byte[]>>();

        if (!TryDecodeHex(signatureText.Trim(), out var expectedSignature))
            return Result<IReadOnlyDictionary<string, byte[]>>.WithFailure(
                $"Bundle integrity signature file is malformed (not lowercase-hex): '{signaturePath}'.");

        var computedSignature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(hmacKey), manifestBytes);

        if (!CryptographicOperations.FixedTimeEquals(computedSignature, expectedSignature))
            return Result<IReadOnlyDictionary<string, byte[]>>.WithFailure(
                "Bundle integrity signature mismatch (HMAC-SHA256 verification failed).");

        if (!TryParseManifest(manifestBytes, out var bundleName, out var generatedAt, out var manifestEntries))
            return Result<IReadOnlyDictionary<string, byte[]>>.WithFailure(
                $"Bundle integrity manifest is malformed or missing its 'bundle:'/'generatedAt:' header: '{manifestPath}'.");

        // Identity binding: the signed manifest must name THIS directory, not merely any
        // validly-signed manifest — defeats cross-institution / stale-directory replay.
        var expectedBundleName = GetDirectoryLeafName(directoryPath);
        if (!string.Equals(bundleName, expectedBundleName, StringComparison.Ordinal))
            return Result<IReadOnlyDictionary<string, byte[]>>.WithFailure(
                $"Bundle integrity manifest 'bundle:' header ('{bundleName}') does not match the " +
                $"directory being verified ('{expectedBundleName}'). Refusing to load — this manifest " +
                "was signed for a different bundle directory.");

        if (!DateTimeOffset.TryParse(
                generatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal, out _))
            return Result<IReadOnlyDictionary<string, byte[]>>.WithFailure(
                $"Bundle integrity manifest 'generatedAt:' header is not a valid ISO-8601 timestamp: '{generatedAt}'.");

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
            return Result<IReadOnlyDictionary<string, byte[]>>.WithFailure(
                "Bundle contains CSV file(s) not covered by the integrity manifest: " +
                string.Join(", ", rogueFiles) + ".");

        var verifiedFiles = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var entry in manifestEntries)
        {
            if (cancellationToken.IsCancellationRequested)
                return ResultExtensions.Cancelled<IReadOnlyDictionary<string, byte[]>>();

            var filePath = Path.Combine(directoryPath, entry.FileName);
            if (!File.Exists(filePath))
                return Result<IReadOnlyDictionary<string, byte[]>>.WithFailure(
                    $"Bundle integrity manifest lists a file that is missing on disk: '{entry.FileName}'.");

            byte[] fileBytes;
            try
            {
                fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return ResultExtensions.Cancelled<IReadOnlyDictionary<string, byte[]>>();
            }
            catch (IOException ex)
            {
                return Result<IReadOnlyDictionary<string, byte[]>>.WithFailure(
                    $"Error reading '{entry.FileName}' during integrity check: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result<IReadOnlyDictionary<string, byte[]>>.WithFailure(
                    $"Error reading '{entry.FileName}' during integrity check: {ex.Message}");
            }

            var actualHashHex = Convert.ToHexStringLower(SHA256.HashData(fileBytes));
            if (!string.Equals(actualHashHex, entry.HashHex, StringComparison.Ordinal))
                return Result<IReadOnlyDictionary<string, byte[]>>.WithFailure(
                    $"Bundle integrity hash mismatch for '{entry.FileName}': the file on disk does not match the signed manifest.");

            verifiedFiles[entry.FileName] = fileBytes;
        }

        return Result<IReadOnlyDictionary<string, byte[]>>.WithSuccess(verifiedFiles);
    }

    /// <summary>
    /// Derives the "bundle name" used in the manifest header: the leaf (final path segment)
    /// of <paramref name="directoryPath"/>, after trimming any trailing directory separator.
    /// </summary>
    internal static string GetDirectoryLeafName(string directoryPath)
    {
        var trimmed = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFileName(trimmed);
    }

    private static bool TryParseManifest(
        byte[] manifestBytes,
        out string bundleName,
        out string generatedAt,
        out List<ManifestEntry> entries)
    {
        entries = [];
        bundleName = string.Empty;
        generatedAt = string.Empty;

        // Encoding.UTF8.GetString uses a replacement fallback by default, so it never throws —
        // there is no decoder-fallback failure mode to guard against here.
        var text = Encoding.UTF8.GetString(manifestBytes);
        var lines = text.Split('\n');

        if (lines.Length < 2)
            return false;

        var headerLine1 = lines[0].TrimEnd('\r');
        var headerLine2 = lines[1].TrimEnd('\r');

        if (!headerLine1.StartsWith(BundleHeaderPrefix, StringComparison.Ordinal))
            return false;
        var parsedBundleName = headerLine1[BundleHeaderPrefix.Length..];
        if (parsedBundleName.Length == 0)
            return false;

        if (!headerLine2.StartsWith(GeneratedAtHeaderPrefix, StringComparison.Ordinal))
            return false;
        var parsedGeneratedAt = headerLine2[GeneratedAtHeaderPrefix.Length..];
        if (parsedGeneratedAt.Length == 0)
            return false;

        for (var i = 2; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
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

        bundleName = parsedBundleName;
        generatedAt = parsedGeneratedAt;
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
