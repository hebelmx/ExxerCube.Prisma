using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Integrity;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Tests.Integrity;

/// <summary>
/// Tests for <see cref="BundleManifestWriter"/> — the authoring helper that (re-)signs a
/// reference-bundle directory with a SHA-256 manifest + detached HMAC-SHA256 signature.
/// </summary>
public sealed class BundleManifestWriterTests
{
    private const string HmacKey = "test-signing-key-do-not-use-in-prod";

    private static string DemoBundleSourcePath =>
        Path.Combine(AppContext.BaseDirectory, "TestAssets", "csv", "Demo_Bank_(Iqubica)");

    /// <summary>
    /// Copies the real demo bundle's CSV files into a fresh temp directory so mutation tests
    /// (tampering, deleting files) never touch the shared TestAssets copy used by other
    /// (potentially parallel) tests.
    /// </summary>
    private static string CreateIsolatedBundleCopy()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"bundle-integrity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        foreach (var csvFile in Directory.EnumerateFiles(DemoBundleSourcePath, "*.csv"))
        {
            File.Copy(csvFile, Path.Combine(tempDir, Path.GetFileName(csvFile)));
        }

        return tempDir;
    }

    [Fact]
    public async Task WriteManifestAsync_SignedDirectory_ManifestAndSignatureFilesExist()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempDir = CreateIsolatedBundleCopy();

        try
        {
            var result = await BundleManifestWriter.WriteManifestAsync(tempDir, HmacKey, ct);

            result.IsSuccess.ShouldBeTrue($"Expected success but got: {result.Error}");
            File.Exists(Path.Combine(tempDir, BundleIntegrityVerifier.ManifestFileName)).ShouldBeTrue();
            File.Exists(Path.Combine(tempDir, BundleIntegrityVerifier.SignatureFileName)).ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task WriteManifestAsync_ThenVerifyAsync_RoundTripsSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempDir = CreateIsolatedBundleCopy();

        try
        {
            var writeResult = await BundleManifestWriter.WriteManifestAsync(tempDir, HmacKey, ct);
            writeResult.IsSuccess.ShouldBeTrue($"Sign step failed: {writeResult.Error}");

            var verifyResult = await BundleIntegrityVerifier.VerifyAsync(tempDir, HmacKey, ct);

            verifyResult.IsSuccess.ShouldBeTrue($"Verification of a freshly-signed bundle must pass: {verifyResult.Error}");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task WriteManifestAsync_EmptyDirectory_ReturnsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempDir = Path.Combine(Path.GetTempPath(), $"bundle-integrity-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var result = await BundleManifestWriter.WriteManifestAsync(tempDir, HmacKey, ct);

            result.IsSuccess.ShouldBeFalse("A directory with no CSV files has nothing to sign.");
            result.Error.ShouldNotBeNullOrEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task WriteManifestAsync_MissingDirectory_ReturnsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var missingDir = Path.Combine(Path.GetTempPath(), $"bundle-integrity-missing-{Guid.NewGuid():N}");

        var result = await BundleManifestWriter.WriteManifestAsync(missingDir, HmacKey, ct);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task WriteManifestAsync_CancelledToken_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var tempDir = CreateIsolatedBundleCopy();

        try
        {
            var result = await BundleManifestWriter.WriteManifestAsync(tempDir, HmacKey, cts.Token);

            result.IsSuccess.ShouldBeFalse();
            result.IsCancelled().ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task WriteManifestAsync_RewritingAfterCsvEdit_ProducesManifestThatVerifiesAgainstNewContent()
    {
        // Simulates the "rotation" workflow: edit a CSV, then re-sign.
        var ct = TestContext.Current.CancellationToken;
        var tempDir = CreateIsolatedBundleCopy();

        try
        {
            var firstSign = await BundleManifestWriter.WriteManifestAsync(tempDir, HmacKey, ct);
            firstSign.IsSuccess.ShouldBeTrue();

            // Edit a CSV after the first signature.
            var productsPath = Path.Combine(tempDir, "products.csv");
            await File.AppendAllTextAsync(productsPath, Environment.NewLine, ct);

            // The old signature must now fail...
            var staleVerify = await BundleIntegrityVerifier.VerifyAsync(tempDir, HmacKey, ct);
            staleVerify.IsSuccess.ShouldBeFalse("Editing a CSV after signing must invalidate the old manifest.");

            // ...but re-signing must produce a manifest that verifies again.
            var secondSign = await BundleManifestWriter.WriteManifestAsync(tempDir, HmacKey, ct);
            secondSign.IsSuccess.ShouldBeTrue();

            var freshVerify = await BundleIntegrityVerifier.VerifyAsync(tempDir, HmacKey, ct);
            freshVerify.IsSuccess.ShouldBeTrue($"Re-signing after edit must verify: {freshVerify.Error}");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // ---------------------------------------------------------------------------
    // B1 — manifest format v2 header (bundle: / generatedAt:)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Minimal <see cref="TimeProvider"/> stub that always returns a fixed instant, so the
    /// <c>generatedAt:</c> header can be asserted deterministically.
    /// </summary>
    private sealed class FixedTimeProvider(DateTimeOffset fixedNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => fixedNow;
    }

    [Fact]
    public async Task WriteManifestAsync_InjectedTimeProvider_WritesExpectedGeneratedAtHeader()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempDir = CreateIsolatedBundleCopy();
        var fixedNow = new DateTimeOffset(2026, 7, 23, 17, 22, 38, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(fixedNow);

        try
        {
            var result = await BundleManifestWriter.WriteManifestAsync(tempDir, HmacKey, ct, timeProvider);
            result.IsSuccess.ShouldBeTrue($"Expected success but got: {result.Error}");

            var manifestLines = await File.ReadAllLinesAsync(
                Path.Combine(tempDir, BundleIntegrityVerifier.ManifestFileName), ct);

            manifestLines[0].ShouldBe($"bundle: {Path.GetFileName(tempDir)}");
            manifestLines[1].ShouldStartWith("generatedAt: 2026-07-23T17:22:38");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task WriteManifestAsync_NoTimeProviderInjected_DefaultsToSystemTimeProvider()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempDir = CreateIsolatedBundleCopy();
        var beforeCall = DateTimeOffset.UtcNow;

        try
        {
            var result = await BundleManifestWriter.WriteManifestAsync(tempDir, HmacKey, ct);
            result.IsSuccess.ShouldBeTrue($"Expected success but got: {result.Error}");

            var afterCall = DateTimeOffset.UtcNow;
            var manifestLines = await File.ReadAllLinesAsync(
                Path.Combine(tempDir, BundleIntegrityVerifier.ManifestFileName), ct);

            var generatedAtRaw = manifestLines[1]["generatedAt: ".Length..];
            var generatedAt = DateTimeOffset.Parse(generatedAtRaw, System.Globalization.CultureInfo.InvariantCulture);

            generatedAt.ShouldBeInRange(beforeCall.AddSeconds(-1), afterCall.AddSeconds(1));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
