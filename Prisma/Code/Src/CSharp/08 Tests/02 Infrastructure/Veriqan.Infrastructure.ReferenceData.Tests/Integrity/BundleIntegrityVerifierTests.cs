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
}
