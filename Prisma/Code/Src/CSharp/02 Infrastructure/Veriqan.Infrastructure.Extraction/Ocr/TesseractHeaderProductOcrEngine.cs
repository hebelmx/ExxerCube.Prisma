using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;
using Tesseract;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Ocr;

/// <summary>
/// Tesseract-backed <see cref="IHeaderProductOcrEngine"/>: reads the product-name heading off a
/// rendered page-1 header-band crop (design doc
/// <c>veriqan-e7-s72-header-ocr-design-2026-07-08.md</c> §4.C).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Engine-lifecycle discipline (mirrors Prisma's
/// <c>Infrastructure.Extraction/Teseract/TesseractOcrExecutor.cs</c>, but reimplemented here
/// rather than referenced — Veriqan is a separate bounded context and must not depend on
/// Prisma's OCR infrastructure):</strong> the native <see cref="TesseractEngine"/> must be
/// created AT MOST ONCE per process — a second concurrent instantiation deadlocks — and is not
/// thread-safe, so every call is serialized through <see cref="_engineLock"/> (a
/// <see cref="SemaphoreSlim"/> with max-count 1). The engine is created lazily on first use and
/// reused for the lifetime of this instance.
/// </para>
/// <para>
/// Register this class as a SINGLETON in DI (<c>AddSingleton</c>). Registering it as Transient or
/// Scoped would create a second engine in the same process and trigger the deadlock.
/// </para>
/// <para>
/// Results are memoized in an in-memory <see cref="HeaderOcrResultCache"/> keyed by a
/// content-hash of the crop bytes (design doc §3.3) — the 4 non-scanned demo fixtures share an
/// identical header crop, so the serialized engine is invoked once per distinct crop rather than
/// once per document.
/// </para>
/// </remarks>
public sealed class TesseractHeaderProductOcrEngine : IHeaderProductOcrEngine, IDisposable
{
    private readonly ILogger<TesseractHeaderProductOcrEngine> _logger;
    private readonly HeaderOcrResultCache _cache = new();
    private readonly SemaphoreSlim _engineLock = new(1, 1);

    private TesseractEngine? _engine;
    // volatile: read at RecognizeAsync entry AND inside the locked region on a thread-pool
    // thread, written by Dispose() on a different thread — ensure cross-thread visibility.
    private volatile bool _disposed;

    /// <summary>Initializes a <see cref="TesseractHeaderProductOcrEngine"/>.</summary>
    public TesseractHeaderProductOcrEngine(ILogger<TesseractHeaderProductOcrEngine> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<Result<string>> RecognizeAsync(byte[] cropPngBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cropPngBytes);

        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<string>();

        if (_disposed)
            return Result<string>.WithFailure("TesseractHeaderProductOcrEngine has been disposed.");

        if (_cache.TryGet(cropPngBytes, out var cached))
            return Result<string>.WithSuccess(cached!);

        try
        {
            var text = await Task.Run(
                () => _cache.GetOrCompute(cropPngBytes, () => RecognizeCore(cropPngBytes)),
                cancellationToken).ConfigureAwait(false);

            return Result<string>.WithSuccess(text);
        }
        catch (OperationCanceledException)
        {
            // Task.Run(..., cancellationToken) throws this when cancellation fires after entry.
            // CLAUDE.md: never throw OperationCanceledException — catch and convert to Result.
            return ResultExtensions.Cancelled<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Header-product OCR failed.");
            return Result<string>.WithFailure($"Header-product OCR failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs the actual OCR pass, serialized via <see cref="_engineLock"/> and lazily creating the
    /// singleton engine on first use. Called at most once per distinct crop (the cache wraps this).
    /// </summary>
    private string RecognizeCore(byte[] cropPngBytes)
    {
        _engineLock.Wait();
        try
        {
            // Re-check after acquiring the lock: a concurrent Dispose() may have torn the engine
            // down between the entry guard and here.
            if (_disposed)
                throw new ObjectDisposedException(nameof(TesseractHeaderProductOcrEngine));

            if (_engine is null)
            {
                var tessdataPath = ResolveTessdataPath();
                _logger.LogInformation(
                    "Initializing TesseractEngine for header-product OCR (once per process) — tessdata={TessdataPath}",
                    tessdataPath);
                _engine = new TesseractEngine(tessdataPath, "spa", EngineMode.Default);
            }

            using var pix = Pix.LoadFromMemory(cropPngBytes);
            // PSM.Auto (not SingleBlock): the header-band crop is NOT a single uniform text
            // block — it contains multiple columns (client-identity block on the left, the
            // product-heading text elsewhere) at overlapping Y-coordinates. SingleBlock forces
            // Tesseract to read row-by-row across the whole crop width, interleaving words from
            // both columns onto what looks like one line (observed empirically: "Tarjeta de
            // Crédito CARLOS MENDOZA VARGAS COSTCO BANAMEX" — the client's name from the left
            // column bled into the product heading). Auto performs real layout/column analysis
            // so each column is read as its own block, keeping the heading's own line clean.
            using var page = _engine.Process(pix, PageSegMode.Auto);
            return page.GetText()?.Trim() ?? string.Empty;
        }
        finally
        {
            _engineLock.Release();
        }
    }

    /// <summary>
    /// Cross-platform tessdata directory detection (mirrors Prisma's
    /// <c>TesseractOcrExecutor.GetTessdataPathAsync</c> candidate list). Reimplemented locally —
    /// Veriqan does not reference the Prisma OCR project.
    /// </summary>
    private static string ResolveTessdataPath()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("TESSDATA_PREFIX") ?? string.Empty,
            "/usr/share/tesseract-ocr/5/tessdata",
            "/usr/share/tesseract-ocr/5.00/tessdata",
            "/usr/share/tesseract-ocr/4.00/tessdata",
            "/usr/share/tesseract-ocr/tessdata",
            "/usr/local/share/tessdata",
            "/usr/local/share/tesseract-ocr/tessdata",
            "/opt/homebrew/share/tessdata",
            Path.Combine(AppContext.BaseDirectory, "tessdata"),
            Path.Combine(Directory.GetCurrentDirectory(), "tessdata"),
            Path.Combine(AppContext.BaseDirectory, "x64", "tessdata"),
            Path.Combine(AppContext.BaseDirectory, "x86", "tessdata"),
        };

        foreach (var candidate in candidates.Where(c => !string.IsNullOrEmpty(c)))
        {
            if (Directory.Exists(candidate) && Directory.GetFiles(candidate, "*.traineddata").Length > 0)
                return candidate;
        }

        var fallback = candidates.FirstOrDefault(c => !string.IsNullOrEmpty(c) && Directory.Exists(c));
        if (fallback is not null)
            return fallback;

        throw new InvalidOperationException(
            "Could not locate tessdata directory. Install Tesseract OCR or set TESSDATA_PREFIX.");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _engineLock.Wait();
        try
        {
            _engine?.Dispose();
            _engine = null;
        }
        finally
        {
            _engineLock.Release();
            _engineLock.Dispose();
        }
    }
}
