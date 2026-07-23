using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Integrity;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Tests.Integrity;

/// <summary>
/// Tests for <see cref="BundleIntegrityVerifier"/> — fail-closed SHA-256 manifest +
/// HMAC-SHA256 signature verification of a reference-bundle directory.
/// </summary>
public sealed class BundleIntegrityVerifierTests
{
    private const string HmacKey = "test-signing-key-do-not-use-in-prod";
    private const string WrongHmacKey = "a-completely-different-key";

    private static string DemoBundleSourcePath =>
        Path.Combine(AppContext.BaseDirectory, "TestAssets", "csv", "Demo_Bank_(Iqubica)");

    /// <summary>
    /// Copies the demo bundle CSVs into a fresh temp directory and signs them, so each test
    /// starts from a known-good, isolated, signed bundle.
    /// </summary>
    private static async Task<string> CreateSignedIsolatedBundleAsync(CancellationToken ct)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"bundle-integrity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        foreach (var csvFile in Directory.EnumerateFiles(DemoBundleSourcePath, "*.csv"))
        {
            File.Copy(csvFile, Path.Combine(tempDir, Path.GetFileName(csvFile)));
        }

        var signResult = await BundleManifestWriter.WriteManifestAsync(tempDir, HmacKey, ct);
        signResult.IsSuccess.ShouldBeTrue($"Test setup: signing must succeed: {signResult.Error}");

        return tempDir;
    }

    [Fact]
    public async Task VerifyAsync_SignedBundle_ReturnsSuccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempDir = await CreateSignedIsolatedBundleAsync(ct);

        try
        {
            var result = await BundleIntegrityVerifier.VerifyAsync(tempDir, HmacKey, ct);

            result.IsSuccess.ShouldBeTrue($"Expected success but got: {result.Error}");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyAsync_TamperedCsvContent_ReturnsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempDir = await CreateSignedIsolatedBundleAsync(ct);

        try
        {
            // Tamper a CSV file after it was signed.
            var productsPath = Path.Combine(tempDir, "products.csv");
            await File.AppendAllTextAsync(productsPath, "TAMPERED,ROW,HERE" + Environment.NewLine, ct);

            var result = await BundleIntegrityVerifier.VerifyAsync(tempDir, HmacKey, ct);

            result.IsSuccess.ShouldBeFalse("A modified CSV must fail hash verification.");
            result.Error.ShouldNotBeNullOrEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyAsync_MissingManifestFile_ReturnsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempDir = await CreateSignedIsolatedBundleAsync(ct);

        try
        {
            File.Delete(Path.Combine(tempDir, BundleIntegrityVerifier.ManifestFileName));

            var result = await BundleIntegrityVerifier.VerifyAsync(tempDir, HmacKey, ct);

            result.IsSuccess.ShouldBeFalse();
            result.Error!.ShouldContain("manifest");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyAsync_MissingSignatureFile_ReturnsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempDir = await CreateSignedIsolatedBundleAsync(ct);

        try
        {
            File.Delete(Path.Combine(tempDir, BundleIntegrityVerifier.SignatureFileName));

            var result = await BundleIntegrityVerifier.VerifyAsync(tempDir, HmacKey, ct);

            result.IsSuccess.ShouldBeFalse();
            result.Error!.ShouldContain("signature");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyAsync_WrongKey_ReturnsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempDir = await CreateSignedIsolatedBundleAsync(ct);

        try
        {
            var result = await BundleIntegrityVerifier.VerifyAsync(tempDir, WrongHmacKey, ct);

            result.IsSuccess.ShouldBeFalse("Verifying with the wrong HMAC key must fail.");
            result.Error!.ShouldContain("signature");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyAsync_RogueCsvFileNotInManifest_ReturnsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempDir = await CreateSignedIsolatedBundleAsync(ct);

        try
        {
            // Drop in a CSV that was never part of the signed manifest.
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "rogue-extra.csv"),
                "col1,col2\nvalue1,value2\n",
                ct);

            var result = await BundleIntegrityVerifier.VerifyAsync(tempDir, HmacKey, ct);

            result.IsSuccess.ShouldBeFalse("An untracked CSV file must be rejected (rogue-file guard).");
            result.Error!.ShouldContain("rogue-extra.csv");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyAsync_ManifestListsFileMissingFromDisk_ReturnsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempDir = await CreateSignedIsolatedBundleAsync(ct);

        try
        {
            // Delete a file that the (already-signed) manifest still lists.
            File.Delete(Path.Combine(tempDir, "products.csv"));

            var result = await BundleIntegrityVerifier.VerifyAsync(tempDir, HmacKey, ct);

            result.IsSuccess.ShouldBeFalse("A manifest-listed file missing from disk must fail verification.");
            result.Error!.ShouldContain("products.csv");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyAsync_CancelledToken_ReturnsCancelled()
    {
        var setupCt = TestContext.Current.CancellationToken;
        var tempDir = await CreateSignedIsolatedBundleAsync(setupCt);

        try
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await BundleIntegrityVerifier.VerifyAsync(tempDir, HmacKey, cts.Token);

            result.IsSuccess.ShouldBeFalse();
            result.IsCancelled().ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyAsync_MissingBundleDirectory_ReturnsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var missingDir = Path.Combine(Path.GetTempPath(), $"bundle-integrity-missing-{Guid.NewGuid():N}");

        var result = await BundleIntegrityVerifier.VerifyAsync(missingDir, HmacKey, ct);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    // ---------------------------------------------------------------------------
    // B1 — identity binding (manifest format v2 "bundle:" header)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// A manifest signed for one institution directory (<c>Bank_A</c>) must NOT verify when
    /// the whole directory (CSVs + manifest + signature) is copied verbatim into a
    /// differently-named directory (<c>Bank_B</c>) — even though the HMAC signature itself is
    /// still valid, because the signed <c>bundle:</c> header no longer matches the directory
    /// being verified. This is the cross-institution / whole-directory replay attack B1 closes.
    /// </summary>
    [Fact]
    public async Task VerifyAsync_CrossInstitutionReplay_ReturnsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), $"bundle-integrity-replay-{Guid.NewGuid():N}");
        var bankADir = Path.Combine(root, "Bank_A");
        var bankBDir = Path.Combine(root, "Bank_B");
        Directory.CreateDirectory(bankADir);
        Directory.CreateDirectory(bankBDir);

        try
        {
            foreach (var csvFile in Directory.EnumerateFiles(DemoBundleSourcePath, "*.csv"))
            {
                File.Copy(csvFile, Path.Combine(bankADir, Path.GetFileName(csvFile)));
            }

            var signResult = await BundleManifestWriter.WriteManifestAsync(bankADir, HmacKey, ct);
            signResult.IsSuccess.ShouldBeTrue($"Test setup: signing Bank_A must succeed: {signResult.Error}");

            // Verifying in place (same directory it was signed for) must succeed.
            var sameDirResult = await BundleIntegrityVerifier.VerifyAsync(bankADir, HmacKey, ct);
            sameDirResult.IsSuccess.ShouldBeTrue(
                $"Sanity check: verifying Bank_A in its own directory must pass: {sameDirResult.Error}");

            // Replay: copy the entire signed bundle (CSVs + manifest + signature) into Bank_B.
            foreach (var file in Directory.EnumerateFiles(bankADir))
            {
                File.Copy(file, Path.Combine(bankBDir, Path.GetFileName(file)));
            }

            var replayResult = await BundleIntegrityVerifier.VerifyAsync(bankBDir, HmacKey, ct);

            replayResult.IsSuccess.ShouldBeFalse(
                "A validly-signed bundle replayed into a different institution's directory must be rejected.");
            replayResult.Error!.ShouldContain("bundle:");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// A v1-style manifest (no <c>bundle:</c>/<c>generatedAt:</c> header — just hash lines) must
    /// be rejected as malformed, even if a signature file happens to accompany it. Manifest
    /// format v2 is mandatory now that no signed bundles exist yet.
    /// </summary>
    [Fact]
    public async Task VerifyAsync_V1StyleManifestWithoutHeader_ReturnsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempDir = Path.Combine(Path.GetTempPath(), $"bundle-integrity-v1-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            foreach (var csvFile in Directory.EnumerateFiles(DemoBundleSourcePath, "*.csv"))
            {
                File.Copy(csvFile, Path.Combine(tempDir, Path.GetFileName(csvFile)));
            }

            // Hand-craft a v1-style manifest: hash lines only, no header.
            var manifestLines = Directory.EnumerateFiles(tempDir, "*.csv")
                .OrderBy(f => f, StringComparer.Ordinal)
                .Select(f =>
                {
                    var bytes = File.ReadAllBytes(f);
                    var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
                    return $"{hash}  {Path.GetFileName(f)}";
                });
            var manifestBytes = System.Text.Encoding.UTF8.GetBytes(string.Join('\n', manifestLines) + "\n");
            var manifestPath = Path.Combine(tempDir, BundleIntegrityVerifier.ManifestFileName);
            await File.WriteAllBytesAsync(manifestPath, manifestBytes, ct);

            var signatureHex = Convert.ToHexStringLower(
                System.Security.Cryptography.HMACSHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(HmacKey), manifestBytes));
            await File.WriteAllTextAsync(Path.Combine(tempDir, BundleIntegrityVerifier.SignatureFileName), signatureHex, ct);

            var result = await BundleIntegrityVerifier.VerifyAsync(tempDir, HmacKey, ct);

            result.IsSuccess.ShouldBeFalse("A v1-style manifest without the header must be rejected.");
            result.Error!.ShouldContain("header");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// Editing the <c>bundle:</c> header line in-place (without re-signing) must fail HMAC
    /// verification, since the header is covered by the signed manifest byte stream.
    /// </summary>
    [Fact]
    public async Task VerifyAsync_BundleHeaderTamperedWithoutResigning_ReturnsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempDir = await CreateSignedIsolatedBundleAsync(ct);

        try
        {
            var manifestPath = Path.Combine(tempDir, BundleIntegrityVerifier.ManifestFileName);
            var lines = (await File.ReadAllLinesAsync(manifestPath, ct)).ToList();
            lines[0] = "bundle: some-other-directory-name";
            await File.WriteAllLinesAsync(manifestPath, lines, ct);

            var result = await BundleIntegrityVerifier.VerifyAsync(tempDir, HmacKey, ct);

            result.IsSuccess.ShouldBeFalse("Tampering the header without re-signing must fail HMAC verification.");
            result.Error!.ShouldContain("signature");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // ---------------------------------------------------------------------------
    // M1 — TOCTOU: VerifyAsync must return the exact bytes it hashed
    // ---------------------------------------------------------------------------

    /// <summary>
    /// The bytes returned in a successful <see cref="Result{T}"/> must be byte-for-byte
    /// identical to what is currently on disk (i.e. what was actually hashed) — this is the
    /// structural guarantee that lets callers parse from the verified bytes instead of
    /// re-reading the file (closing the TOCTOU window between verification and parsing).
    /// </summary>
    [Fact]
    public async Task VerifyAsync_SignedBundle_ReturnedBytesMatchDiskContentExactly()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempDir = await CreateSignedIsolatedBundleAsync(ct);

        try
        {
            var result = await BundleIntegrityVerifier.VerifyAsync(tempDir, HmacKey, ct);
            result.IsSuccess.ShouldBeTrue($"Expected success but got: {result.Error}");

            var verifiedFiles = result.Value!;
            verifiedFiles.Count.ShouldBeGreaterThan(0);

            foreach (var (fileName, verifiedBytes) in verifiedFiles)
            {
                var diskBytes = await File.ReadAllBytesAsync(Path.Combine(tempDir, fileName), ct);
                verifiedBytes.ShouldBe(diskBytes, $"Verified bytes for '{fileName}' must match disk content exactly.");
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
