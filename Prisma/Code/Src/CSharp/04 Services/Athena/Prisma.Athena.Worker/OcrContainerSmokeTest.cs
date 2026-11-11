using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using Microsoft.Extensions.Logging;

namespace Prisma.Athena.Worker;

/// <summary>
/// Container OCR smoke test, run via <c>--ocr-smoke</c>. Proves BOTH native stacks the Athena runtime
/// image needs are co-resident and functional inside the built container, with zero DB/hub/config
/// dependencies: (1) Emgu.CV's <c>libcvextern.so</c> renders a marker bitmap, (2) the same
/// <see cref="TesseractOcrExecutor"/> the production pipeline uses OCRs it back to text. See
/// docs/planning-artifacts/remediation/TRACKER-O1-athena-container-ocr.md (O1, chunk C1).
/// </summary>
/// <remarks>
/// This is a bootstrap-time console diagnostic, not a library API, so it favors clear PASS/FAIL
/// console output over the Result&lt;T&gt; pattern used elsewhere in the codebase and converts every
/// exception into a process exit code rather than letting it escape unhandled.
/// </remarks>
internal static class OcrContainerSmokeTest
{
    private const string MarkerText = "PRISMA OCR SMOKE 12345";

    /// <summary>
    /// Runs the smoke test and returns the process exit code (0 = pass, 1 = fail). Never throws.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token honored before the render and file read;
    /// <see cref="IOcrExecutor.ExecuteOcrAsync"/> takes no token (pre-existing interface-wide gap),
    /// so the native OCR call itself is not interruptible.</param>
    public static async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("=== Athena OCR container smoke test (--ocr-smoke) ===");
        string? tempPngPath = null;

        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine("FAIL: smoke test cancelled before start.");
                return 1;
            }

            // Step 1: Emgu.CV render — proves libcvextern.so loads and executes on this runtime image.
            Console.WriteLine("[1/2] Rendering marker text via Emgu.CV (CvInvoke.PutText)...");
            using (var mat = new Mat(300, 1400, DepthType.Cv8U, 1))
            {
                mat.SetTo(new MCvScalar(255), null); // white background, 1-channel grayscale

                CvInvoke.PutText(
                    mat,
                    MarkerText,
                    new Point(30, 180),
                    FontFace.HersheySimplex,
                    2.2,
                    new MCvScalar(0), // black text
                    thickness: 4,
                    lineType: LineType.EightConnected,
                    bottomLeftOrigin: false);

                tempPngPath = Path.Combine(Path.GetTempPath(), $"ocr-smoke-{Guid.NewGuid():N}.png");
                CvInvoke.Imwrite(tempPngPath, mat);
            }

            Console.WriteLine($"PASS: Emgu.CV rendered the marker and wrote {tempPngPath}");

            // Step 2: Tesseract OCR — proves the tesseract natives + tessdata + co-residency with Emgu.
            Console.WriteLine("[2/2] OCR-ing the rendered PNG via TesseractOcrExecutor...");
            var pngBytes = await File.ReadAllBytesAsync(tempPngPath, cancellationToken).ConfigureAwait(false);

            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            using var executor = new TesseractOcrExecutor(loggerFactory.CreateLogger<TesseractOcrExecutor>());

            // "spa" deliberately: the production pipeline uses OCRConfig's default language (spa), and
            // TesseractOcrExecutor initializes its once-per-process engine with the first language it
            // sees — so the smoke must prove the SAME tessdata production will demand, not just eng.
            // The ASCII marker OCRs fine under the spa model.
            var ocrResult = await executor.ExecuteOcrAsync(
                new ImageData(pngBytes, tempPngPath),
                new OCRConfig(language: "spa", oem: 1, psm: 6, fallbackLanguage: "spa")).ConfigureAwait(false);

            if (!ocrResult.IsSuccess)
            {
                Console.WriteLine($"FAIL: Tesseract OCR execution failed: {ocrResult.Error}");
                Console.WriteLine("=== Athena OCR container smoke test: FAIL ===");
                return 1;
            }

            var recognizedText = ocrResult.Value?.Text ?? string.Empty;
            Console.WriteLine($"Recognized text: \"{recognizedText.Replace('\n', ' ').Replace('\r', ' ')}\"");

            // Loose match: tolerate OCR mangling punctuation/spacing but require the load-bearing tokens.
            var normalized = recognizedText.ToUpperInvariant();
            if (normalized.Contains("PRISMA", StringComparison.Ordinal)
                && normalized.Contains("SMOKE", StringComparison.Ordinal)
                && normalized.Contains("12345", StringComparison.Ordinal))
            {
                Console.WriteLine("PASS: recognized text contains the PRISMA / SMOKE / 12345 marker tokens.");
                Console.WriteLine("=== Athena OCR container smoke test: PASS ===");
                return 0;
            }

            Console.WriteLine("FAIL: recognized text did not contain the expected marker tokens.");
            Console.WriteLine("=== Athena OCR container smoke test: FAIL ===");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: unhandled exception during OCR smoke test: {ex}");
            Console.WriteLine("=== Athena OCR container smoke test: FAIL ===");
            return 1;
        }
        finally
        {
            if (tempPngPath is not null && File.Exists(tempPngPath))
            {
                try
                {
                    File.Delete(tempPngPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Best-effort cleanup; a stray temp file is not a smoke-test failure.
                }
            }
        }
    }
}
