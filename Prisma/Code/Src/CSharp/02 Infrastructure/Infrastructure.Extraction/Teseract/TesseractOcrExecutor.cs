using PDFtoImage;
using SixLabors.ImageSharp.PixelFormats;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

/// <summary>
/// Tesseract OCR implementation of IOcrExecutor.
/// Provides traditional OCR processing using Tesseract engine for fast, reliable text extraction.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Engine-lifecycle design (deadlock fix, 2026-06-20):</strong>
/// <c>TesseractEngine</c> must be created AT MOST ONCE per process: the native Tesseract library holds
/// global state and deadlocks if a second engine is initialized before the first is fully disposed.
/// Additionally <c>TesseractEngine</c> is NOT thread-safe, so concurrent OCR calls on a shared engine
/// would corrupt results.
/// </para>
/// <para>
/// Fix: the engine is created LAZILY on first use and then reused for the lifetime of this executor
/// instance. Concurrent calls are serialized via <see cref="_engineLock"/> (a <see cref="SemaphoreSlim"/>
/// with max-count = 1). Register this class as a SINGLETON in DI (<c>AddSingleton</c>) so only one engine
/// is ever created in the process. This executor implements <see cref="IDisposable"/> so the DI container
/// properly disposes the engine on host shutdown.
/// </para>
/// <para>
/// Do NOT register this class as Transient or Scoped — each new instance would create a second engine
/// in the same process and trigger the deadlock.
/// </para>
/// </remarks>
public sealed class TesseractOcrExecutor : IOcrExecutor, IDisposable
{
    private readonly ILogger<TesseractOcrExecutor> _logger;

    // Cross-instance path cache: finding tessdata is expensive; cache the resolved path once across
    // all executor instances (safe as static because the path is write-once and never changes).
    private static readonly SemaphoreSlim _tessdataLock = new(1, 1);
    private static string? _cachedTessdataPath;

    // Per-instance engine + lock. The engine is created once on first use; all OCR calls are
    // serialized through _engineLock because TesseractEngine is NOT thread-safe.
    private TesseractEngine? _engine;
    private readonly SemaphoreSlim _engineLock = new(1, 1);
    // volatile: read at ExecuteOcrAsync entry AND inside the locked region on a thread-pool thread,
    // written by Dispose() on a different thread — ensure cross-thread visibility.
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TesseractOcrExecutor"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public TesseractOcrExecutor(ILogger<TesseractOcrExecutor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes OCR on an image using Tesseract engine.
    /// </summary>
    /// <param name="imageData">The image data to process.</param>
    /// <param name="config">The OCR configuration.</param>
    /// <returns>A result containing the OCR result or an error.</returns>
    public async Task<Result<OCRResult>> ExecuteOcrAsync(ImageData imageData, OCRConfig config)
    {
        if (_disposed)
        {
            return Result<OCRResult>.Failure("TesseractOcrExecutor has been disposed.");
        }

        try
        {
            // Validate input first (before any logging that accesses imageData properties)
            if (imageData == null)
            {
                _logger.LogWarning("Null image data provided");
                return Result<OCRResult>.Failure("Image data is null");
            }

            if (imageData.Data == null || imageData.Data.Length == 0)
            {
                _logger.LogWarning("Empty image data provided");
                return Result<OCRResult>.Failure("Image data is empty");
            }

            _logger.LogInformation(
                "Executing Tesseract OCR on image: {SourcePath}, Page: {PageNumber}/{TotalPages}",
                imageData.SourcePath,
                imageData.PageNumber,
                imageData.TotalPages);

            // Map language code to Tesseract format
            var tesseractLanguage = MapToTesseractLanguage(config.Language);

            // Get tessdata path (cross-instance cached, one-time I/O scan)
            var tessdataPath = await GetTessdataPathAsync();

            _logger.LogDebug(
                "Tesseract configuration: language={Language}, OEM={OEM}, PSM={PSM}, tessdata={TessdataPath}",
                tesseractLanguage,
                config.OEM,
                config.PSM,
                tessdataPath);

            // Acquire the per-instance engine lock to ensure:
            //   1) Only one TesseractEngine is ever created in this process (deadlock prevention).
            //   2) Concurrent OCR calls are serialized (TesseractEngine is NOT thread-safe).
            // Run the CPU-bound work on a thread-pool thread to avoid blocking the calling async context.
            var result = await Task.Run(() =>
            {
                _engineLock.Wait();
                try
                {
                    // Re-check after acquiring the lock: a concurrent Dispose() may have torn down the
                    // engine between the entry guard and here. Never lazy-create an engine on a disposed
                    // executor — that would be a second-init on a shutting-down host (the very deadlock
                    // this class prevents).
                    if (_disposed)
                    {
                        return Result<OCRResult>.Failure("TesseractOcrExecutor has been disposed.");
                    }

                    // Lazy-init: create the engine once and reuse for the lifetime of this singleton.
                    if (_engine == null)
                    {
                        _logger.LogInformation(
                            "Initializing TesseractEngine (once per process) — tessdata={TessdataPath}, lang={Language}, OEM={OEM}",
                            tessdataPath, tesseractLanguage, config.OEM);
                        _engine = new TesseractEngine(tessdataPath, tesseractLanguage, (EngineMode)config.OEM);
                        _engine.SetVariable("tessedit_char_whitelist",
                            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,;:!?¿¡()[]{}\"'-/$%&ñÑáéíóúÁÉÍÓÚüÜ ");
                    }

                    return ProcessWithEngine(_engine, imageData.Data, config);
                }
                finally
                {
                    _engineLock.Release();
                }
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tesseract OCR execution failed for {SourcePath}", imageData.SourcePath);
            return Result<OCRResult>.Failure($"OCR execution failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

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

    /// <summary>
    /// Processes image bytes using an already-initialized, caller-locked <see cref="TesseractEngine"/>.
    /// The caller is responsible for holding <see cref="_engineLock"/> for the duration of this call
    /// because <see cref="TesseractEngine"/> is not thread-safe.
    /// </summary>
    private Result<OCRResult> ProcessWithEngine(TesseractEngine engine, byte[] imageBytes, OCRConfig config)
    {
        try
        {
            // Convert bytes to image - support both direct images and PDFs
            byte[] processedImageBytes;
            try
            {
                // Try as direct image first
                using var ms = new MemoryStream(imageBytes);
                using var image = Image.Load(ms);
                using var outputMs = new MemoryStream();
                image.SaveAsPng(outputMs);
                processedImageBytes = outputMs.ToArray();
                _logger.LogDebug("Loaded image directly: {Width}x{Height}", image.Width, image.Height);
            }
            catch
            {
                // If direct image load fails, try as PDF
                _logger.LogDebug("Direct image loading failed, attempting PDF conversion");
                processedImageBytes = ConvertPdfToImage(imageBytes);
            }

            // Load image into Tesseract
            using var pix = Pix.LoadFromMemory(processedImageBytes);

            // Set page segmentation mode
            using var page = engine.Process(pix, (PageSegMode)config.PSM);

            // Extract text and confidence
            var extractedText = page.GetText()?.Trim() ?? string.Empty;
            var meanConfidence = page.GetMeanConfidence();

            _logger.LogDebug(
                "Tesseract extraction complete: {TextLength} chars, {Confidence:F2}% confidence",
                extractedText.Length,
                meanConfidence * 100);

            // Get word-level confidences
            var confidences = new List<float>();
            using var iterator = page.GetIterator();
            iterator.Begin();

            do
            {
                if (iterator.TryGetBoundingBox(PageIteratorLevel.Word, out _))
                {
                    var wordConfidence = iterator.GetConfidence(PageIteratorLevel.Word);
                    if (wordConfidence > 0)
                    {
                        confidences.Add(wordConfidence);
                    }
                }
            } while (iterator.Next(PageIteratorLevel.Word));

            // Calculate metrics
            float confidenceAvg = confidences.Any() ? confidences.Average() : meanConfidence * 100f;
            float confidenceMedian = confidences.Any()
                ? CalculateMedian(confidences)
                : meanConfidence * 100f;

            _logger.LogInformation(
                "Tesseract OCR completed. Text length: {TextLength}, Confidence avg: {ConfidenceAvg:F2}%, Median: {ConfidenceMedian:F2}%",
                extractedText.Length,
                confidenceAvg,
                confidenceMedian);

            // Create OCRResult
            var ocrResult = new OCRResult(
                text: extractedText,
                confidenceAvg: confidenceAvg,
                confidenceMedian: confidenceMedian,
                confidences: confidences,
                languageUsed: config.Language
            );

            return Result<OCRResult>.Success(ocrResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tesseract processing failed");
            return Result<OCRResult>.Failure($"Tesseract processing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Converts PDF bytes to image bytes using PDFtoImage for proper rasterization.
    /// </summary>
    private byte[] ConvertPdfToImage(byte[] pdfBytes)
    {
        try
        {
            // Use 300 DPI for OCR (standard for high-quality text recognition)
            const int dpi = 300;

            _logger.LogDebug("Converting PDF to image at {DPI} DPI using PDFtoImage", dpi);

            // Convert bytes to stream for PDFtoImage
            using var pdfStream = new MemoryStream(pdfBytes);

            // Convert first page of PDF to SKBitmap (page index 0, 300 DPI)
            var options = new RenderOptions(Dpi: dpi);
#pragma warning disable CA1416 // PDFtoImage is cross-platform (Windows, Linux, macOS)
            using var skBitmap = Conversion.ToImage(pdfStream, 0, options: options);
#pragma warning restore CA1416

            if (skBitmap == null)
            {
                throw new InvalidOperationException("Failed to convert PDF to image");
            }

            _logger.LogDebug("PDF page rendered: {Width}x{Height} pixels", skBitmap.Width, skBitmap.Height);

            // Convert SKBitmap to ImageSharp Image for Tesseract
            using var image = new Image<Rgba32>(skBitmap.Width, skBitmap.Height);

            // Copy pixels from SKBitmap to ImageSharp Image
            unsafe
            {
                var pixels = skBitmap.GetPixels();
                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < skBitmap.Height; y++)
                    {
                        var pixelRow = accessor.GetRowSpan(y);
                        for (int x = 0; x < skBitmap.Width; x++)
                        {
                            var skColor = skBitmap.GetPixel(x, y);
                            pixelRow[x] = new Rgba32(skColor.Red, skColor.Green, skColor.Blue, skColor.Alpha);
                        }
                    }
                });
            }

            // Convert to PNG bytes
            using var outputMs = new MemoryStream();
            image.SaveAsPng(outputMs);

            _logger.LogInformation(
                "PDF rendered successfully using PDFtoImage at {DPI} DPI ({Width}x{Height} pixels)",
                dpi, skBitmap.Width, skBitmap.Height);

            return outputMs.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF conversion failed");
            throw new InvalidOperationException("Failed to convert PDF to image", ex);
        }
    }

    /// <summary>
    /// Maps language code to Tesseract format.
    /// </summary>
    private string MapToTesseractLanguage(string language)
    {
        return language.ToLower() switch
        {
            "spa" or "spanish" or "es" => "spa",
            "eng" or "english" or "en" => "eng",
            "fra" or "french" or "fr" => "fra",
            "deu" or "german" or "de" => "deu",
            _ => language
        };
    }

    /// <summary>
    /// Gets the tessdata path for Tesseract language data files.
    /// Uses cross-platform detection with caching.
    /// </summary>
    private async Task<string> GetTessdataPathAsync()
    {
        // Return cached path if available
        if (_cachedTessdataPath != null)
        {
            return _cachedTessdataPath;
        }

        await _tessdataLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_cachedTessdataPath != null)
            {
                return _cachedTessdataPath;
            }

            // Detection strategy (based on Helix implementation)
            var candidates = new List<string>
            {
                // 1. Environment variable
                Environment.GetEnvironmentVariable("TESSDATA_PREFIX") ?? "",

                // 2. Common installation paths
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR", "tessdata"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tesseract-OCR", "tessdata"),

                // 3. Linux/Mac common paths (Debian/Ubuntu install tessdata under a versioned
                //    directory, e.g. /usr/share/tesseract-ocr/5/tessdata on Tesseract 5).
                "/usr/share/tesseract-ocr/5/tessdata",
                "/usr/share/tesseract-ocr/5.00/tessdata",
                "/usr/share/tesseract-ocr/4.00/tessdata",
                "/usr/share/tesseract-ocr/tessdata",
                "/usr/local/share/tessdata",
                "/usr/local/share/tesseract-ocr/tessdata",
                "/opt/homebrew/share/tessdata",

                // 4. Relative to application
                Path.Combine(AppContext.BaseDirectory, "tessdata"),
                Path.Combine(Directory.GetCurrentDirectory(), "tessdata"),

                // 5. NuGet package location (if using Tesseract NuGet with embedded data)
                Path.Combine(AppContext.BaseDirectory, "x64", "tessdata"),
                Path.Combine(AppContext.BaseDirectory, "x86", "tessdata"),
            };

            foreach (var candidate in candidates.Where(c => !string.IsNullOrEmpty(c)))
            {
                if (Directory.Exists(candidate))
                {
                    // Verify it contains language data files
                    var hasLanguageData = Directory.GetFiles(candidate, "*.traineddata").Any();
                    if (hasLanguageData)
                    {
                        _cachedTessdataPath = candidate;
                        _logger.LogInformation("Tessdata path found: {TessdataPath}", _cachedTessdataPath);
                        return _cachedTessdataPath;
                    }
                }
            }

            // Fallback: use first existing directory even without validation
            var fallback = candidates.FirstOrDefault(c => !string.IsNullOrEmpty(c) && Directory.Exists(c));
            if (fallback != null)
            {
                _cachedTessdataPath = fallback;
                _logger.LogWarning(
                    "Tessdata path found but no .traineddata files detected: {TessdataPath}. " +
                    "OCR may fail if language data is not available.",
                    _cachedTessdataPath);
                return _cachedTessdataPath;
            }

            // No tessdata found
            throw new InvalidOperationException(
                "Could not locate tessdata directory. Please install Tesseract OCR or set TESSDATA_PREFIX environment variable.");
        }
        finally
        {
            _tessdataLock.Release();
        }
    }

    /// <summary>
    /// Calculates the median value from a list of floats.
    /// </summary>
    private static float CalculateMedian(List<float> values)
    {
        if (values == null || values.Count == 0)
            return 0f;

        var sorted = values.OrderBy(x => x).ToList();
        int count = sorted.Count;

        if (count % 2 == 0)
        {
            // Even number of elements - average the two middle values
            return (sorted[count / 2 - 1] + sorted[count / 2]) / 2f;
        }
        else
        {
            // Odd number of elements - return the middle value
            return sorted[count / 2];
        }
    }
}