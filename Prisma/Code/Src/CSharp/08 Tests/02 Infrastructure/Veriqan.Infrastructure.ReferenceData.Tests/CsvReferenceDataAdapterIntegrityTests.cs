using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Adapters;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Integrity;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Tests;

/// <summary>
/// Tests for <see cref="CsvReferenceDataAdapter"/>'s bundle-integrity wiring (RC6 item 3.5):
/// verification is skipped (with a one-time warning) when no
/// <see cref="CsvReferenceDataOptions.BundleHmacKey"/> is configured, and enforced fail-closed
/// when one is.
/// </summary>
public sealed class CsvReferenceDataAdapterIntegrityTests
{
    private const string HmacKey = "test-signing-key-do-not-use-in-prod";

    private static string DemoBundleSourcePath =>
        Path.Combine(AppContext.BaseDirectory, "TestAssets", "csv", "Demo_Bank_(Iqubica)");

    private static CsvReferenceDataAdapter CreateAdapter(
        string rootDir,
        string hmacKey = "",
        ILogger<CsvReferenceDataAdapter>? logger = null)
    {
        var options = Options.Create(new CsvReferenceDataOptions
        {
            RootDirectory = rootDir,
            BundleHmacKey = hmacKey
        });
        var validator = new ReferenceBundleSchemaValidator();
        return new CsvReferenceDataAdapter(options, validator, logger ?? NullLogger<CsvReferenceDataAdapter>.Instance);
    }

    /// <summary>
    /// Copies the demo bundle into a fresh temp root (so tests can sign/tamper it without
    /// touching the shared TestAssets copy) at the same institution sub-directory name the
    /// real bundle uses.
    /// </summary>
    private static string CreateIsolatedBundleRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"adapter-integrity-{Guid.NewGuid():N}");
        var instDir = Path.Combine(tempRoot, "Demo_Bank_(Iqubica)");
        Directory.CreateDirectory(instDir);

        foreach (var csvFile in Directory.EnumerateFiles(DemoBundleSourcePath, "*.csv"))
        {
            File.Copy(csvFile, Path.Combine(instDir, Path.GetFileName(csvFile)));
        }

        return tempRoot;
    }

    [Fact]
    public async Task GetBundleAsync_NoKeyConfigured_LoadsSuccessfully_UnsignedBundleUnaffected()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempRoot = CreateIsolatedBundleRoot();

        try
        {
            var adapter = CreateAdapter(tempRoot); // hmacKey defaults to "" — verification disabled
            var key = new StatementContextKey("Demo Bank (Iqubica)");

            var result = await adapter.GetBundleAsync(key, ct);

            result.IsSuccess.ShouldBeTrue(
                $"An unsigned bundle must still load when no BundleHmacKey is configured: {result.Error}");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GetBundleAsync_NoKeyConfigured_LogsDisabledWarningExactlyOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempRoot = CreateIsolatedBundleRoot();

        try
        {
            var logger = new CapturingLogger<CsvReferenceDataAdapter>();
            var adapter = CreateAdapter(tempRoot, logger: logger);
            var key = new StatementContextKey("Demo Bank (Iqubica)");

            await adapter.GetBundleAsync(key, ct);
            await adapter.GetChecklistTiersAsync(key, ct);
            await adapter.GetBundleAsync(key, ct);

            var disabledWarnings = logger.Entries.Count(e =>
                e.Level == LogLevel.Warning && e.Message.Contains("integrity verification disabled", StringComparison.Ordinal));

            disabledWarnings.ShouldBe(1,
                "The 'verification disabled' warning must be logged at most once per adapter instance, " +
                "even across multiple calls.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GetBundleAsync_KeyConfigured_SignedBundle_LoadsSuccessfully_EndToEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempRoot = CreateIsolatedBundleRoot();

        try
        {
            var instDir = Path.Combine(tempRoot, "Demo_Bank_(Iqubica)");
            var signResult = await BundleManifestWriter.WriteManifestAsync(instDir, HmacKey, ct);
            signResult.IsSuccess.ShouldBeTrue($"Test setup: signing must succeed: {signResult.Error}");

            var adapter = CreateAdapter(tempRoot, HmacKey);
            var key = new StatementContextKey("Demo Bank (Iqubica)");

            var result = await adapter.GetBundleAsync(key, ct);

            result.IsSuccess.ShouldBeTrue($"A correctly signed bundle must load green end-to-end: {result.Error}");
            result.Value!.BundleMetadata.Institution.ShouldBe("Demo Bank (Iqubica)");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GetBundleAsync_KeyConfigured_TamperedCsvAfterSigning_ReturnsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempRoot = CreateIsolatedBundleRoot();

        try
        {
            var instDir = Path.Combine(tempRoot, "Demo_Bank_(Iqubica)");
            var signResult = await BundleManifestWriter.WriteManifestAsync(instDir, HmacKey, ct);
            signResult.IsSuccess.ShouldBeTrue();

            // Tamper after signing.
            await File.AppendAllTextAsync(
                Path.Combine(instDir, "bundle-metadata.csv"), Environment.NewLine, ct);

            var adapter = CreateAdapter(tempRoot, HmacKey);
            var key = new StatementContextKey("Demo Bank (Iqubica)");

            var result = await adapter.GetBundleAsync(key, ct);

            result.IsSuccess.ShouldBeFalse("A tampered CSV must be rejected before parsing (fail-closed).");
            result.Error.ShouldContain("integrity");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GetBundleAsync_KeyConfigured_MissingManifest_ReturnsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempRoot = CreateIsolatedBundleRoot(); // unsigned — no manifest/signature files

        try
        {
            var adapter = CreateAdapter(tempRoot, HmacKey);
            var key = new StatementContextKey("Demo Bank (Iqubica)");

            var result = await adapter.GetBundleAsync(key, ct);

            result.IsSuccess.ShouldBeFalse("A key configured against an unsigned bundle must fail closed.");
            result.Error.ShouldContain("integrity");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GetChecklistTiersAsync_KeyConfigured_SignedBundle_LoadsSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempRoot = CreateIsolatedBundleRoot();

        try
        {
            var instDir = Path.Combine(tempRoot, "Demo_Bank_(Iqubica)");
            var signResult = await BundleManifestWriter.WriteManifestAsync(instDir, HmacKey, ct);
            signResult.IsSuccess.ShouldBeTrue();

            var adapter = CreateAdapter(tempRoot, HmacKey);
            var key = new StatementContextKey("Demo Bank (Iqubica)");

            var result = await adapter.GetChecklistTiersAsync(key, ct);

            result.IsSuccess.ShouldBeTrue($"Expected success but got: {result.Error}");
            result.Value!.Count.ShouldBeGreaterThan(0);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GetChecklistTiersAsync_KeyConfigured_UnsignedBundle_ReturnsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempRoot = CreateIsolatedBundleRoot(); // unsigned

        try
        {
            var adapter = CreateAdapter(tempRoot, HmacKey);
            var key = new StatementContextKey("Demo Bank (Iqubica)");

            var result = await adapter.GetChecklistTiersAsync(key, ct);

            result.IsSuccess.ShouldBeFalse("Fail-closed: an unsigned bundle must not be read when a key is configured.");
            result.Error.ShouldContain("integrity");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GetChecklistTiersAsync_NoKeyConfigured_LoadsAsBeforeUnaffected()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempRoot = CreateIsolatedBundleRoot();

        try
        {
            var adapter = CreateAdapter(tempRoot); // no key — behavior unchanged from pre-RC6-3.5
            var key = new StatementContextKey("Demo Bank (Iqubica)");

            var result = await adapter.GetChecklistTiersAsync(key, ct);

            result.IsSuccess.ShouldBeTrue($"Expected success but got: {result.Error}");
            result.Value!.Count.ShouldBeGreaterThan(0);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    /// <summary>
    /// Minimal <see cref="ILogger{TCategoryName}"/> capturing level + formatted message per
    /// call, used to assert the "verification disabled" warning is logged exactly once.
    /// </summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
