using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Ocr;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;
using PDFtoImage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// Higher stage for <see cref="FieldKind.Product"/>: renders page 1, crops the top fractional
/// header band, OCRs it via <see cref="IHeaderProductOcrEngine"/>, and regex-matches a
/// product-heading shape (design doc <c>veriqan-e7-s72-header-ocr-design-2026-07-08.md</c> §3.2,
/// §5 step 3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Honesty discipline (mirrors <see cref="FuzzyLabelStage{TValue}"/>, G18):</b> this stage
/// returns <see cref="FieldCandidate{TValue}.None"/> — never a fabricated <c>Found</c> — whenever
/// the page cannot be rendered, the OCR engine fails, or the recognized text does not contain the
/// product-heading shape. It never guesses.
/// </para>
/// <para>
/// The crop rectangle is a <em>fraction</em> of the rendered page height (top 30%), not a fixed
/// pixel count, so it stays correct regardless of render DPI or page size (design doc §3.2).
/// </para>
/// </remarks>
public sealed class HeaderImageOcrStage : IFieldResolutionStage<string>
{
    /// <summary>Fraction of the rendered page height cropped as the header band (top 30%).</summary>
    public const double HeaderCropFraction = 0.30;

    /// <summary>DPI used to render page 1 before cropping — matches the codebase's one existing
    /// render precedent (<c>PdfPigStatementFieldExtractor.FiscalPageRenderDpi</c>). Internal
    /// (not private) so a T1 gated geometry test can compute the expected render-pixel dimensions
    /// independently and compare against <see cref="RenderHeaderCrop"/>'s actual output without
    /// duplicating this constant.</summary>
    internal const int RenderDpi = 150;

    /// <summary>Confidence assigned to a genuine OCR recognition (below positional's 1.0 — this
    /// is an inferred, not positionally-verified, read).</summary>
    private const double OcrConfidence = 0.9;

    // NOTE: deliberately NOT RegexOptions.IgnoreCase and NOT a bare `\s+` capture. Two real-fixture
    // findings drove this shape (see the header-band OCR text observed on good.pdf):
    //   1. `\s+` (which matches \n) let the match jump straight through the blank line separating
    //      a standalone "Tarjeta de Crédito" heading from an unrelated line below it (the client's
    //      name, in this layout) — `[^\S\r\n]+` (horizontal whitespace only) keeps the label match
    //      on a single line.
    //   2. The genuine product-name occurrence sits on a line Tesseract merged with an adjacent,
    //      unrelated column ("Tarjeta de Crédito COSTCO BANAMEX Número de días en el periodo: ...").
    //      A greedy "rest of line" capture would swallow that trailing noise, which then dilutes
    //      the fuzzy-match score at resolution time. The brand name itself is reliably ALL-CAPS
    //      ("COSTCO BANAMEX") while the trailing noise starts with a Title-Case Spanish word
    //      ("Número de días…") — capturing only a run of ALL-CAPS tokens after "Crédito" stops
    //      exactly at the brand boundary without depending on any per-fixture coordinate.
    private static readonly Regex ProductHeadingPattern = new(
        @"Tarjeta[^\S\r\n]+de[^\S\r\n]+Cr[ée]dito(?:[^\S\r\n]+[A-ZÁÉÍÓÚÑÜ0-9]{2,})+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    private readonly IHeaderProductOcrEngine _ocrEngine;
    private readonly ILogger<HeaderImageOcrStage> _logger;

    /// <summary>Initializes a <see cref="HeaderImageOcrStage"/>.</summary>
    public HeaderImageOcrStage(IHeaderProductOcrEngine ocrEngine, ILogger<HeaderImageOcrStage> logger)
    {
        _ocrEngine = ocrEngine ?? throw new ArgumentNullException(nameof(ocrEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public StageId Stage => StageId.HeaderImageOcr;

    /// <inheritdoc/>
    public async Task<Result<FieldCandidate<string>>> TryResolveAsync(
        FieldResolutionContext<string> context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<FieldCandidate<string>>();

        try
        {
            var cropBytes = RenderHeaderCrop(context.PdfBytes);
            if (cropBytes is null)
            {
                _logger.LogDebug("HeaderImageOcrStage: page-1 render failed for {FieldKind}; abstaining.", context.FieldKind);
                return Result<FieldCandidate<string>>.WithSuccess(FieldCandidate<string>.None(Stage));
            }

            var ocrResult = await _ocrEngine.RecognizeAsync(cropBytes, cancellationToken).ConfigureAwait(false);
            if (ocrResult.IsCancelled())
                return ResultExtensions.Cancelled<FieldCandidate<string>>();

            if (ocrResult.IsFailure)
            {
                _logger.LogWarning(
                    "HeaderImageOcrStage: OCR failed while resolving {FieldKind}: {Error} — abstaining.",
                    context.FieldKind,
                    ocrResult.Error);
                return Result<FieldCandidate<string>>.WithSuccess(FieldCandidate<string>.None(Stage));
            }

            var text = ocrResult.Value ?? string.Empty;
            var recognized = TryMatchProductHeading(text);
            if (recognized is null)
                return Result<FieldCandidate<string>>.WithSuccess(FieldCandidate<string>.None(Stage));

            return Result<FieldCandidate<string>>.WithSuccess(
                FieldCandidate<string>.Found(recognized, OcrConfidence, Stage, FieldLocator.PageHint(1)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<FieldCandidate<string>>.WithFailure(
                $"HeaderImageOcrStage failed while resolving {context.FieldKind}: {ex.Message}");
        }
    }

    /// <summary>
    /// Renders page 1 at <see cref="RenderDpi"/>, crops the top <see cref="HeaderCropFraction"/>
    /// of the page height, and returns PNG-encoded crop bytes — or <see langword="null"/> when
    /// the page could not be rendered.
    /// </summary>
    /// <remarks>
    /// Internal (not private) so a T1 gated geometry test can drive this exact production
    /// render/crop path directly and assert on the resulting crop's pixel dimensions — without
    /// requiring native Tesseract (this method never touches OCR) — design doc
    /// <c>veriqan-e7-s72-header-ocr-design-2026-07-08.md</c> §3.3 T1.
    /// </remarks>
    internal static byte[]? RenderHeaderCrop(byte[] pdfBytes)
    {
        using var pdfStream = new MemoryStream(pdfBytes);
#pragma warning disable CA1416 // PDFtoImage is cross-platform (Windows, Linux, macOS)
        using var skBitmap = Conversion.ToImage(
            pdfStream,
            leaveOpen: false,
            page: 0,
            options: new RenderOptions(Dpi: RenderDpi));
#pragma warning restore CA1416

        if (skBitmap is null || skBitmap.Width <= 0 || skBitmap.Height <= 0)
            return null;

        // Guarantee Bgra8888 color layout before reading bytes (PDFium returns Bgra8888 on
        // Windows but RGBA8888 on some Linux builds — mirrors PdfPigStatementFieldExtractor's
        // perceptual-hash render path).
        using var bitmapCopy = skBitmap.ColorType == SkiaSharp.SKColorType.Bgra8888
            ? null
            : skBitmap.Copy(SkiaSharp.SKColorType.Bgra8888);
        var normalizedBitmap = bitmapCopy ?? skBitmap;

        var bgraBytes = normalizedBitmap.Bytes;
        var expectedLength = normalizedBitmap.Width * normalizedBitmap.Height * 4;
        if (bgraBytes is null || bgraBytes.Length != expectedLength)
            return null;

        using var bgra32Image = Image.LoadPixelData<Bgra32>(bgraBytes, normalizedBitmap.Width, normalizedBitmap.Height);
        using var rgba32Image = bgra32Image.CloneAs<Rgba32>();

        var cropHeight = Math.Clamp(
            (int)Math.Round(rgba32Image.Height * HeaderCropFraction),
            1,
            rgba32Image.Height);
        var cropRect = new Rectangle(0, 0, rgba32Image.Width, cropHeight);

        using var cropped = rgba32Image.Clone(ctx => ctx.Crop(cropRect));
        using var outputMs = new MemoryStream();
        cropped.SaveAsPng(outputMs);
        return outputMs.ToArray();
    }

    private static string NormalizeOcrLine(string raw) =>
        WhitespaceRun.Replace(raw, " ").Trim();

    /// <summary>
    /// Regex-matches the product-heading shape against raw OCR text and normalizes the result —
    /// the pure text→product-heading step of <see cref="TryResolveAsync"/>, factored out so it can
    /// run without an OCR engine at all.
    /// </summary>
    /// <remarks>
    /// Internal (not private) so a T1 gated snapshot test can feed a committed real-OCR text
    /// snapshot through this exact production regex/normalize logic and assert the result resolves
    /// to the expected catalog product — without invoking native Tesseract (design doc
    /// <c>veriqan-e7-s72-header-ocr-design-2026-07-08.md</c> §3.3 T1).
    /// </remarks>
    /// <param name="ocrText">Raw OCR text already recognized by an <see cref="IHeaderProductOcrEngine"/>.</param>
    /// <returns>The normalized product-heading text, or <see langword="null"/> when no match.</returns>
    internal static string? TryMatchProductHeading(string ocrText)
    {
        var match = ProductHeadingPattern.Match(ocrText ?? string.Empty);
        if (!match.Success)
            return null;

        var recognized = NormalizeOcrLine(match.Value);
        return string.IsNullOrWhiteSpace(recognized) ? null : recognized;
    }
}
