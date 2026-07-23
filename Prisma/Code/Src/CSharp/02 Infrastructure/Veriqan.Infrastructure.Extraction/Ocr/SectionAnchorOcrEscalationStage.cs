using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;
using PDFtoImage;
using SkiaSharp;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Ocr;

/// <summary>
/// RC1.S6 — §-anchor OCR escalation ladder: when the PDF text-layer section-detection pass
/// (<c>PdfPigStatementFieldExtractor.ExtractDetectedSections</c>) finds fewer than
/// <see cref="MinPresentSectionsToSkipEscalation"/> present sections, renders every page of the
/// document and re-scans OCR-recognized text for the same §1–28 anchor phrases, upgrading any
/// section the text layer marked absent to present when OCR finds its heading.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ground truth driving this design (RC1.S6 real-corpus probe, 2026-07-23):</b> on the real
/// Banamex/Citibanamex statement family every section heading is raster/image-rendered — the
/// text layer finds 0 of 23 detectable anchors on all 8 sampled statements. Tesseract (spa, 150
/// DPI) recovers 18–20 of 27 anchors per document. On the synthetic Dummie VEC demo fixtures,
/// by contrast, every heading IS real selectable text, so the text-layer pass alone already
/// finds ≥ 2 present sections and this stage never renders a single page — zero cost, zero risk
/// of moving a demo verdict (<see cref="EscalateAsync"/> returns the input list by reference,
/// unchanged, whenever the trigger does not fire).
/// </para>
/// <para>
/// <b>Anchor reuse (never invents new wordings):</b> the same 28-entry anchor table
/// (<see cref="PdfPigStatementFieldExtractor.SectionAnchors"/>) and the same heading-shape gate
/// (<see cref="PdfPigStatementFieldExtractor.IsOcrLineHeadingLikeMatch"/> — the identical
/// <c>IsHeadingLikeMatch</c> the text-layer band scan uses) are applied to each OCR-recognized
/// line. Anchors that are genuinely reworded or absent in the real layout (e.g. §3/§4/§14/§15)
/// stay honestly undetected — this stage does not paper over them.
/// </para>
/// <para>
/// <b>Honest geometry (never fabricates a bounding box):</b> an OCR-upgraded section's
/// <see cref="DetectedSection.Locator"/> is <see cref="FieldLocator.PageHint"/> — page number
/// only. OCR text carries no PDF-point word coordinates, so <see cref="FieldLocator.Bottom"/>
/// stays <see langword="null"/>. Downstream, <c>SectionOrderAndGapRule</c> already requires
/// <c>Locator.Bottom.HasValue</c> before including a section in its order/gap computation — an
/// OCR-sourced section is therefore automatically excluded from both the order sequence and the
/// gap check, which is the correct, honest outcome (no invented coordinate could ever back a
/// "gap ≤ 2 cm" or "reading order" claim). <see cref="DetectedSection.SectionText"/> is left
/// empty for the same reason: reconstructing a heading-to-next-heading text span from a raw OCR
/// text blob (no word bounding boxes to build bands from) would be a fabrication, not a
/// measurement — <c>Section26NotasAclaratoriasRule</c> / <c>Section27GlosarioRule</c> do not need
/// it anyway, since both already scope their verbatim-match search to
/// <see cref="StatementModel.NormalizedFullText"/> (the whole text layer), gated only on section
/// presence — exactly the boolean this stage legitimately flips.
/// </para>
/// <para>
/// <b>Performance:</b> renders and OCRs each page at most once per document per call (a single
/// sequential loop, page 0..N-1) — no page is ever re-rendered or re-OCR'd within one
/// <see cref="EscalateAsync"/> invocation. OCR calls go through the caller-supplied singleton
/// <see cref="IHeaderProductOcrEngine"/>, whose internal semaphore already serializes native
/// Tesseract access and whose content-hash cache dedupes identical page renders across documents.
/// </para>
/// </remarks>
public sealed class SectionAnchorOcrEscalationStage
{
    /// <summary>
    /// The escalation ladder only fires when the text-layer pass found strictly fewer than this
    /// many present sections. Synthetic/demo fixtures (real selectable heading text) comfortably
    /// clear this bar and never trigger a render/OCR pass.
    /// </summary>
    public const int MinPresentSectionsToSkipEscalation = 2;

    /// <summary>
    /// Render DPI — matches the codebase's existing render precedents
    /// (<c>PdfPigStatementFieldExtractor.FiscalPageRenderDpi</c>, <c>HeaderImageOcrStage.RenderDpi</c>).
    /// </summary>
    internal const int RenderDpi = 150;

    private readonly IHeaderProductOcrEngine _ocrEngine;
    private readonly ILogger<SectionAnchorOcrEscalationStage> _logger;

    /// <summary>Initializes a <see cref="SectionAnchorOcrEscalationStage"/>.</summary>
    public SectionAnchorOcrEscalationStage(
        IHeaderProductOcrEngine ocrEngine,
        ILogger<SectionAnchorOcrEscalationStage> logger)
    {
        _ocrEngine = ocrEngine ?? throw new ArgumentNullException(nameof(ocrEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Escalates <paramref name="textLayerSections"/> via page-render + OCR when fewer than
    /// <see cref="MinPresentSectionsToSkipEscalation"/> are present. Returns
    /// <paramref name="textLayerSections"/> unchanged (by reference) when the trigger does not
    /// fire, when <paramref name="pageCount"/> is not positive, or when nothing new is found.
    /// </summary>
    /// <param name="textLayerSections">
    /// The 28-entry list produced by the text-layer pass
    /// (<c>PdfPigStatementFieldExtractor.ExtractDetectedSections</c>).
    /// </param>
    /// <param name="pdfBytes">Raw PDF bytes (rendered independently of the PdfPig text-layer pass).</param>
    /// <param name="pageCount">
    /// Total page count (<c>StatementModel.PageCount</c>) — reused rather than re-derived so this
    /// stage never needs to open the PDF a second time just to learn its page count.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<Result<IReadOnlyList<DetectedSection>>> EscalateAsync(
        IReadOnlyList<DetectedSection> textLayerSections,
        byte[] pdfBytes,
        int pageCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(textLayerSections);
        ArgumentNullException.ThrowIfNull(pdfBytes);

        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<IReadOnlyList<DetectedSection>>();

        var presentCount = textLayerSections.Count(s => s.IsPresent);
        if (presentCount >= MinPresentSectionsToSkipEscalation || pageCount <= 0)
            return Result<IReadOnlyList<DetectedSection>>.WithSuccess(textLayerSections);

        // Which anchors are worth looking for: applicable-anchor entries the text layer did NOT
        // already find, excluding indeterminate (no-anchor) sections (§1 Logo) entirely.
        var unmatchedIndexes = new List<int>();
        for (var i = 0; i < textLayerSections.Count; i++)
        {
            var s = textLayerSections[i];
            if (!s.IsPresent && s.DetectionStatus != SectionDetectionStatus.Indeterminate)
                unmatchedIndexes.Add(i);
        }

        if (unmatchedIndexes.Count == 0)
            return Result<IReadOnlyList<DetectedSection>>.WithSuccess(textLayerSections);

        var anchors = PdfPigStatementFieldExtractor.SectionAnchors;

        // sectionNumber -> 1-based page number of first OCR match, in page order.
        var ocrMatches = new Dictionary<int, int>();

        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            if (cancellationToken.IsCancellationRequested)
                return ResultExtensions.Cancelled<IReadOnlyList<DetectedSection>>();

            // Nothing left to look for — stop rendering/OCR-ing further pages.
            if (ocrMatches.Count >= unmatchedIndexes.Count)
                break;

            byte[]? pageImage;
            try
            {
                pageImage = RenderPageToPng(pdfBytes, pageIndex);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "SectionAnchorOcrEscalationStage: page {PageIndex} render failed; skipping.",
                    pageIndex);
                continue;
            }

            if (pageImage is null)
            {
                _logger.LogDebug(
                    "SectionAnchorOcrEscalationStage: page {PageIndex} render returned no image; skipping.",
                    pageIndex);
                continue;
            }

            var ocrResult = await _ocrEngine.RecognizeAsync(pageImage, cancellationToken).ConfigureAwait(false);
            if (ocrResult.IsCancelled())
                return ResultExtensions.Cancelled<IReadOnlyList<DetectedSection>>();

            if (ocrResult.IsFailure)
            {
                _logger.LogWarning(
                    "SectionAnchorOcrEscalationStage: OCR failed on page {PageIndex}: {Error} — skipping page.",
                    pageIndex,
                    ocrResult.Error);
                continue;
            }

            var pageNumber = pageIndex + 1;
            var lines = (ocrResult.Value ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in lines)
            {
                var normalizedLine = VecTextNormalizer.Normalize(rawLine);
                if (normalizedLine.Length == 0)
                    continue;

                foreach (var sectionIndex in unmatchedIndexes)
                {
                    if (ocrMatches.ContainsKey(sectionIndex))
                        continue;

                    var anchor = anchors[sectionIndex].NormalizedAnchor;
                    if (string.IsNullOrEmpty(anchor))
                        continue;

                    if (PdfPigStatementFieldExtractor.IsOcrLineHeadingLikeMatch(normalizedLine, anchor))
                        ocrMatches[sectionIndex] = pageNumber;
                }
            }
        }

        if (ocrMatches.Count == 0)
            return Result<IReadOnlyList<DetectedSection>>.WithSuccess(textLayerSections);

        var merged = new List<DetectedSection>(textLayerSections.Count);
        foreach (var (i, section) in textLayerSections.Select((s, i) => (i, s)))
        {
            if (!ocrMatches.TryGetValue(i, out var matchedPage))
            {
                merged.Add(section);
                continue;
            }

            merged.Add(section with
            {
                IsPresent = true,
                IsApplicable = true,
                DetectionStatus = SectionDetectionStatus.Present,
                Source = SectionDetectionSource.Ocr,
                Locator = FieldLocator.PageHint(matchedPage),
                SectionText = string.Empty,
            });
        }

        _logger.LogInformation(
            "SectionAnchorOcrEscalationStage: text layer found {TextLayerPresentCount} present section(s); " +
            "OCR escalation upgraded {OcrUpgradedCount} more section(s).",
            presentCount,
            ocrMatches.Count);

        return Result<IReadOnlyList<DetectedSection>>.WithSuccess(merged);
    }

    /// <summary>
    /// Renders one page (0-based <paramref name="pageIndex"/>) at <see cref="RenderDpi"/> and
    /// PNG-encodes it directly (no crop, no pixel-format normalization — the whole rendered
    /// bitmap is passed to OCR as-is). Returns <see langword="null"/> when the page cannot be
    /// rendered.
    /// </summary>
    internal static byte[]? RenderPageToPng(byte[] pdfBytes, int pageIndex)
    {
        using var pdfStream = new MemoryStream(pdfBytes);
#pragma warning disable CA1416 // PDFtoImage is cross-platform (Windows, Linux, macOS)
        using var skBitmap = Conversion.ToImage(
            pdfStream,
            leaveOpen: false,
            page: pageIndex,
            options: new RenderOptions(Dpi: RenderDpi));
#pragma warning restore CA1416

        if (skBitmap is null || skBitmap.Width <= 0 || skBitmap.Height <= 0)
            return null;

        using var image = SKImage.FromBitmap(skBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, quality: 100);
        return data?.ToArray();
    }
}
