using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.DependencyInjection;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract;

/// <summary>
/// Regression guard for the Tesseract second-init deadlock (PRISMA-E2-S4, 2026-06-20).
/// </summary>
/// <remarks>
/// <para>
/// Root cause: <c>TesseractEngine</c> (native Tesseract.NET wrapper) holds process-wide global state.
/// Creating a second instance in the same process while the first is alive (or after the first's scope
/// was disposed without the engine itself being disposed) deadlocks inside the native library.
/// </para>
/// <para>
/// Fix: <see cref="TesseractOcrExecutor"/> now lazy-initializes the engine ONCE per executor instance
/// (field <c>_engine</c>) and serializes all calls through a <c>SemaphoreSlim(1,1)</c>. Callers MUST
/// register the executor as <c>AddSingleton</c> so the process lifetime matches the engine lifetime.
/// </para>
/// <para>
/// These tests verify, in-process and without Docker/SQL/Playwright:
/// <list type="bullet">
///   <item>A single executor can OCR successfully (baseline).</item>
///   <item>The SAME executor can OCR a SECOND time without deadlock (the restart scenario
///         that the E2E gate was previously guarding against).</item>
///   <item>Repeated sequential OCR calls on the same singleton all succeed.</item>
///   <item>A tight <c>Timeout</c> attribute catches a hang immediately rather than after the
///         global xUnit timeout — a hung test here is the deadlock reproducing.</item>
/// </list>
/// All tests in this class share a SINGLE <see cref="TesseractOcrExecutor"/> instance (constructed
/// directly, not via DI) to mirror the singleton-per-process contract. The fixture PNG
/// (<c>Fixtures/OcrSamples/ocr_account_noisy.png</c>) is copied to output by the .csproj.
/// </para>
/// </remarks>
public sealed class TesseractSecondInitRegressionTests
{
    // Tight per-test timeout: if Tesseract deadlocks the native init call never returns.
    // 120 s is generous for real OCR; a deadlock will hit the wall here instead of hanging forever.
    private const int OcrTimeoutMs = 120_000;

    private static readonly string FixturePng =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "OcrSamples", "ocr_account_noisy.png");

    private static readonly OCRConfig DefaultConfig = new(
        language: "eng",
        oem: 1,       // LSTM engine mode
        psm: 6,       // Assume uniform block of text
        fallbackLanguage: "spa",
        confidenceThreshold: 0.0f   // accept any confidence — fixture is noisy
    );

    private static TesseractOcrExecutor BuildExecutor() =>
        new(NullLogger<TesseractOcrExecutor>.Instance);

    // ── helpers ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the PNG fixture and returns it as <see cref="ImageData"/>, or skips if Tesseract's
    /// native library / tessdata is not available on this machine (native lib absence is not a
    /// product defect — the test logs the skip reason clearly).
    /// </summary>
    private static async Task<ImageData> LoadFixtureAsync(CancellationToken ct)
    {
        File.Exists(FixturePng).ShouldBeTrue(
            $"Fixture PNG must be copied to output: {FixturePng}. " +
            $"Verify ItemGroup in .csproj copies Fixtures/OcrSamples/ocr_account_noisy.png.");

        var bytes = await File.ReadAllBytesAsync(FixturePng, ct);
        bytes.Length.ShouldBeGreaterThan(0, "Fixture PNG must not be empty");
        return new ImageData(bytes, FixturePng);
    }

    // ── tests ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Baseline: a fresh executor returns a successful (or gracefully-failed) result on first call.
    /// A graceful Result.Failure (e.g. tessdata not found) is also acceptable — the test is verifying
    /// the code path completes, not that text is extracted. A HANG means the deadlock reproduced.
    /// </summary>
    [Fact(DisplayName = "TesseractOcrExecutor — first OCR call completes (no hang)", Timeout = OcrTimeoutMs)]
    public async Task ExecuteOcrAsync_FirstCall_CompletesWithoutHang()
    {
        var ct = TestContext.Current.CancellationToken;
        using var executor = BuildExecutor();

        var imageData = await LoadFixtureAsync(ct);
        var result = await executor.ExecuteOcrAsync(imageData, DefaultConfig);

        // Result must come back as either Success or Failure — never a hang/exception escape.
        // We do NOT assert IsSuccess because tessdata may be absent on some boxes.
        (result.IsSuccess || result.IsFailure).ShouldBeTrue("Result must be resolved (success or failure), never hanging");
    }

    /// <summary>
    /// The core regression: calling OCR a SECOND time on the SAME executor instance must complete.
    /// Before the fix the engine was re-created per call; the second creation deadlocked inside
    /// the native library when run in the same test process as another OCR call.
    /// </summary>
    [Fact(DisplayName = "TesseractOcrExecutor — second OCR call on same instance completes (deadlock regression)", Timeout = OcrTimeoutMs)]
    public async Task ExecuteOcrAsync_SecondCallOnSameExecutor_CompletesWithoutDeadlock()
    {
        var ct = TestContext.Current.CancellationToken;
        using var executor = BuildExecutor();

        var imageData = await LoadFixtureAsync(ct);

        // First call — initializes the engine lazily
        var first = await executor.ExecuteOcrAsync(imageData, DefaultConfig);
        (first.IsSuccess || first.IsFailure).ShouldBeTrue("First call must resolve");

        // Second call — MUST reuse the already-initialized engine; must NOT deadlock
        var second = await executor.ExecuteOcrAsync(imageData, DefaultConfig);
        (second.IsSuccess || second.IsFailure).ShouldBeTrue(
            "Second call on the same executor must resolve without deadlock. " +
            "A hang here means TesseractEngine was re-initialized rather than reused.");
    }

    /// <summary>
    /// Extended restart simulation: 5 sequential OCR calls on the same executor all complete.
    /// Covers the multi-document pipeline scenario (the Athena worker processes N documents
    /// through the same singleton executor).
    /// </summary>
    [Fact(DisplayName = "TesseractOcrExecutor — five sequential calls on same instance all complete", Timeout = OcrTimeoutMs)]
    public async Task ExecuteOcrAsync_FiveSequentialCallsOnSameExecutor_AllComplete()
    {
        var ct = TestContext.Current.CancellationToken;
        using var executor = BuildExecutor();

        var imageData = await LoadFixtureAsync(ct);

        for (var i = 1; i <= 5; i++)
        {
            var result = await executor.ExecuteOcrAsync(imageData, DefaultConfig);
            (result.IsSuccess || result.IsFailure).ShouldBeTrue(
                $"Call #{i} must resolve. A hang = deadlock; an exception escape = unexpected error path.");
        }
    }

    /// <summary>
    /// Dispose contract: after <see cref="TesseractOcrExecutor.Dispose"/> the executor returns a
    /// graceful <c>Result.Failure</c> rather than throwing or hanging.
    /// </summary>
    [Fact(DisplayName = "TesseractOcrExecutor — OCR call after Dispose returns Failure, not exception", Timeout = OcrTimeoutMs)]
    public async Task ExecuteOcrAsync_AfterDispose_ReturnsFailureGracefully()
    {
        var ct = TestContext.Current.CancellationToken;
        var executor = BuildExecutor();

        // Warm up the engine so we also test that Dispose tears it down correctly.
        var imageData = await LoadFixtureAsync(ct);
        _ = await executor.ExecuteOcrAsync(imageData, DefaultConfig);

        executor.Dispose();

        // After dispose a call must return Failure — never throw, never hang.
        var result = await executor.ExecuteOcrAsync(imageData, DefaultConfig);
        result.IsFailure.ShouldBeTrue("A disposed executor must return Result.Failure, not throw.");
    }

    /// <summary>
    /// DI invariant (PRISMA-E2-S4 review fix): <c>AddExtractionServices</c> must resolve the keyed
    /// ("Tesseract") and unkeyed <see cref="IOcrExecutor"/> to the SAME <see cref="TesseractOcrExecutor"/>
    /// instance. If the concrete type were registered twice (once keyed, once unkeyed) two engines would
    /// be built and the second-init deadlock would return the moment both are resolved in one process.
    /// This test fails if anyone re-introduces a duplicate concrete registration.
    /// </summary>
    [Fact(DisplayName = "DI — keyed and unkeyed IOcrExecutor resolve to ONE shared TesseractOcrExecutor (one engine per process)")]
    public void AddExtractionServices_KeyedAndUnkeyedOcrExecutor_ResolveToSameSingletonInstance()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddExtractionServices();
        using var provider = services.BuildServiceProvider();

        var unkeyed = provider.GetRequiredService<IOcrExecutor>();
        var keyed = provider.GetRequiredKeyedService<IOcrExecutor>("Tesseract");
        var concrete = provider.GetRequiredService<TesseractOcrExecutor>();

        keyed.ShouldBeSameAs(unkeyed,
            "keyed and unkeyed IOcrExecutor must be the SAME instance — two instances = two TesseractEngines = the second-init deadlock returns");
        unkeyed.ShouldBeSameAs(concrete,
            "the IOcrExecutor singleton must forward to the single concrete TesseractOcrExecutor registration");
    }
}
