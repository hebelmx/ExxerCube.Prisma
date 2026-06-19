using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Reporting;

/// <summary>
/// PdfSharp-based implementation of <see cref="IMarkedPdfGenerator"/> (FR-16, CL-55).
/// </summary>
/// <remarks>
/// <para>
/// <b>Coordinate system translation (PdfPig → PdfSharp):</b>
/// <see cref="FieldLocator"/> boxes originate from PdfPig, which uses a
/// <em>bottom-left</em> origin (Y increases upward, matching the PDF specification).
/// PdfSharp's <see cref="XGraphics"/> uses a <em>top-left</em> origin (Y increases
/// downward). For a page of height <c>H</c> (in PDF points), the transformation is:
/// <code>
///   pdfSharpY = H - pdfPigBottom - boxHeight
/// </code>
/// The X coordinate, width, and height are in the same unit (PDF points) and require no
/// scaling — only the Y origin flips.
/// </para>
/// <para>
/// <b>FAIL findings with no bounding box</b> (page-level hints or <c>NoPage</c>):
/// A small labelled marker is rendered in the top-left margin of the target page (if the
/// page number is in range). When the locator is <c>null</c> or the page number is 0 or
/// out of range, the finding is silently skipped and logged at Debug level.
/// </para>
/// </remarks>
public sealed class MarkedPdfGenerator : IMarkedPdfGenerator
{
    // Semi-transparent highlight colour (red, ~35 % opacity).
    // XColor.FromArgb(alpha, r, g, b): alpha 0 = fully transparent, 255 = opaque.
    private static readonly XColor HighlightFill = XColor.FromArgb(90, 220, 50, 50);
    private static readonly XColor HighlightBorder = XColor.FromArgb(200, 180, 20, 20);

    // Page-margin marker colour (orange, for page-hint-only findings).
    private static readonly XColor MarkerFill = XColor.FromArgb(100, 255, 165, 0);
    private static readonly XColor MarkerBorder = XColor.FromArgb(220, 200, 130, 0);

    // Size of the margin marker dot for findings without a bounding box.
    private const double MarkerSide = 18.0;
    private const double MarkerLeftOffset = 4.0;
    private const double MarkerTopOffset = 4.0;

    // Label font for CheckId annotations.
    // Lazy so that font resolution only happens when actually drawing; a graceful null
    // signals "no font available" and the caller skips the label text.
    private static readonly Lazy<XFont?> LazyLabelFont = new(CreateLabelFont, isThreadSafe: true);

    private static XFont? CreateLabelFont()
    {
        try
        {
            return new XFont("Arial", 7, XFontStyleEx.Regular);
        }
        catch (InvalidOperationException)
        {
            // No appropriate font resolver available in this environment.
            // Labels are cosmetic — silently degrade to no-label mode.
            return null;
        }
    }

    private readonly ILogger<MarkedPdfGenerator> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="MarkedPdfGenerator"/>.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public MarkedPdfGenerator(ILogger<MarkedPdfGenerator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Result<byte[]> Generate(
        byte[] originalPdf,
        IReadOnlyList<RuleFinding> findings,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("MarkedPdfGenerator.Generate cancelled before starting.");
            return ResultExtensions.Cancelled<byte[]>();
        }

        if (originalPdf is null || originalPdf.Length == 0)
        {
            return Result<byte[]>.WithFailure("originalPdf must be a non-empty byte array.");
        }

        if (findings is null)
        {
            return Result<byte[]>.WithFailure("findings must not be null.");
        }

        try
        {
            return GenerateCore(originalPdf, findings, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("MarkedPdfGenerator.Generate cancelled during PDF processing.");
            return ResultExtensions.Cancelled<byte[]>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating colour-marked PDF.");
            return Result<byte[]>.WithFailure($"Failed to generate marked PDF: {ex.Message}", default(byte[]), ex);
        }
    }

    // -----------------------------------------------------------------------
    // Core implementation
    // -----------------------------------------------------------------------

    private Result<byte[]> GenerateCore(
        byte[] originalPdf,
        IReadOnlyList<RuleFinding> findings,
        CancellationToken cancellationToken)
    {
        PdfDocument document;
        try
        {
            using var inputStream = new MemoryStream(originalPdf);
            // Modify mode keeps the original page content intact; we draw on top.
            document = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not open the supplied bytes as a PDF document.");
            return Result<byte[]>.WithFailure($"Input is not a valid PDF: {ex.Message}", default(byte[]), ex);
        }

        using (document)
        {
            var pageCount = document.PageCount;
            _logger.LogDebug("Opened PDF for marking: {PageCount} page(s).", pageCount);

            // Only process FAIL findings.
            var failFindings = findings
                .Where(f => f.Verdict == FindingVerdict.Fail)
                .ToList();

            _logger.LogDebug(
                "{Total} total findings; {Fail} FAIL findings to process.",
                findings.Count, failFindings.Count);

            foreach (var finding in failFindings)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("MarkedPdfGenerator.Generate cancelled while processing findings.");
                    return ResultExtensions.Cancelled<byte[]>();
                }

                DrawFinding(document, finding, pageCount);
            }

            // Serialize to a new byte array — never mutate the caller's input.
            using var outputStream = new MemoryStream();
            document.Save(outputStream);
            var bytes = outputStream.ToArray();

            _logger.LogInformation(
                "Colour-marked PDF generated: {PageCount} page(s), {FailCount} FAIL finding(s) annotated, {Bytes} bytes.",
                pageCount, failFindings.Count, bytes.Length);

            return Result<byte[]>.Success(bytes);
        }
    }

    // -----------------------------------------------------------------------
    // Drawing helpers
    // -----------------------------------------------------------------------

    private void DrawFinding(PdfDocument document, RuleFinding finding, int pageCount)
    {
        var locator = finding.Locator;

        if (locator is null)
        {
            _logger.LogDebug(
                "Finding {CheckId}: no locator — skipping.",
                finding.CheckId);
            return;
        }

        // PageNumber 0 means "NoPage" sentinel — cannot draw anything.
        if (locator.PageNumber == 0)
        {
            _logger.LogDebug(
                "Finding {CheckId}: NoPage sentinel — skipping.",
                finding.CheckId);
            return;
        }

        // Convert 1-based page number to 0-based index.
        var pageIndex = locator.PageNumber - 1;
        if (pageIndex < 0 || pageIndex >= pageCount)
        {
            _logger.LogDebug(
                "Finding {CheckId}: page {Page} out of range (document has {Total} page(s)) — skipping.",
                finding.CheckId, locator.PageNumber, pageCount);
            return;
        }

        var page = document.Pages[pageIndex];

        if (locator.HasBoundingBox)
        {
            DrawBoundingBoxHighlight(page, finding, locator);
        }
        else
        {
            DrawPageMarker(page, finding);
        }
    }

    /// <summary>
    /// Draws a semi-transparent highlight rectangle over the field that failed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Coordinate translation (PdfPig → PdfSharp):</b>
    /// PdfPig gives <c>(left, bottom, width, height)</c> in the <em>visual</em> page space:
    /// bottom-left origin, Y↑, coordinates reflect the page as displayed after any viewer rotation.
    /// PdfSharp <see cref="XGraphics"/> (Append mode) draws in the raw <em>MediaBox</em> space:
    /// top-left origin, Y↓, no implicit rotation applied.
    /// </para>
    /// <para>
    /// The transform therefore has two components:
    /// <list type="number">
    ///   <item>
    ///     <b>CropBox offset:</b> the visual origin (0,0) maps to <c>(CropBox.X1, CropBox.Y1)</c>
    ///     in raw MediaBox coordinates, so that offset is added to every raw coordinate.
    ///   </item>
    ///   <item>
    ///     <b>Page /Rotate:</b> the PDF viewer rotates the raw MediaBox by the /Rotate value
    ///     (degrees clockwise) before display.  We must apply the inverse mapping so that a
    ///     visual point lands on the correct raw PDF coordinate.
    ///   </item>
    /// </list>
    /// </para>
    /// <para>
    /// Let <c>mh</c> = MediaBox.Height, <c>cx1</c>/<c>cy1</c> = CropBox origin,
    /// <c>cw</c>/<c>ch</c> = CropBox dims, <c>vx</c>/<c>vy</c> = PdfPig visual coords:
    /// <code>
    ///   0°:   rawX = cx1+vx,              rawY(top) = mh−cy1−vy−vh,         drawW=vw, drawH=vh
    ///  90°:   rawX = cx1+vy,              rawY(top) = mh−cy1−cw+vx,          drawW=vh, drawH=vw
    /// 180°:   rawX = cx1+cw−vx−vw,       rawY(top) = mh−cy1−ch+vy,          drawW=vw, drawH=vh
    /// 270°:   rawX = cx1+ch−vy−vh,       rawY(top) = mh−cy1−vx−vw,          drawW=vh, drawH=vw
    /// </code>
    /// </para>
    /// </remarks>
    private void DrawBoundingBoxHighlight(PdfPage page, RuleFinding finding, FieldLocator locator)
    {
        // All four components are non-null when HasBoundingBox is true.
        var vx = locator.Left!.Value;      // visual left (PdfPig)
        var vy = locator.Bottom!.Value;    // visual bottom (PdfPig)
        var vw = locator.Width!.Value;     // visual width
        var vh = locator.Height!.Value;    // visual height

        // Raw MediaBox height (never rotation-adjusted in PdfSharp 6.x).
        var mh = page.MediaBox.Height;

        // CropBox offset: visual (0,0) maps to raw (cx1, cy1).
        var cx1 = page.HasCropBox ? page.CropBox.X1 : 0.0;
        var cy1 = page.HasCropBox ? page.CropBox.Y1 : 0.0;
        var cw  = page.HasCropBox ? page.CropBox.Width  : page.MediaBox.Width;
        var ch  = page.HasCropBox ? page.CropBox.Height : page.MediaBox.Height;

        var rect = ComputeHighlightRect(page.Rotate, mh, cx1, cy1, cw, ch, vx, vy, vw, vh);

        using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

        // Semi-transparent fill. XSolidBrush does not implement IDisposable in PdfSharp 6.x.
        var fillBrush = new XSolidBrush(HighlightFill);
        gfx.DrawRectangle(fillBrush, rect);

        // Solid border. XPen does not implement IDisposable in PdfSharp 6.x.
        var borderPen = new XPen(HighlightBorder, 1.0);
        gfx.DrawRectangle(borderPen, rect);

        // CheckId label just above the box (or inside if box is tall enough).
        // LabelFont may be null when no font resolver is configured — skip label gracefully.
        var labelFont = LazyLabelFont.Value;
        if (labelFont is not null)
        {
            var labelY = rect.Top >= 10.0 ? rect.Top - 8.0 : rect.Top + 1.0;
            gfx.DrawString(
                finding.CheckId,
                labelFont,
                XBrushes.DarkRed,
                new XRect(rect.X, labelY, rect.Width, 10.0),
                XStringFormats.TopLeft);
        }

        _logger.LogDebug(
            "Drew bounding-box highlight for {CheckId} on page {Page} (rotate={Rotate}): " +
            "PdfPig(left={L}, bottom={B}, w={W}, h={H}) → PdfSharp(x={X}, y={Y}, w={DW}, h={DH}).",
            finding.CheckId, locator.PageNumber, page.Rotate,
            vx, vy, vw, vh, rect.X, rect.Y, rect.Width, rect.Height);
    }

    /// <summary>
    /// Computes the PdfSharp drawing rectangle (top-left origin, raw MediaBox space) from
    /// a PdfPig visual bounding box (bottom-left origin, post-rotation display space).
    /// </summary>
    /// <param name="rotateDegrees">Page <c>/Rotate</c> value: 0, 90, 180, or 270 (degrees CW for the viewer).</param>
    /// <param name="mediaBoxHeight">Raw MediaBox height in PDF points.</param>
    /// <param name="cx1">CropBox left edge in raw MediaBox coords (0 if no CropBox).</param>
    /// <param name="cy1">CropBox bottom edge in raw MediaBox coords (0 if no CropBox).</param>
    /// <param name="cropWidth">CropBox (or MediaBox) width in PDF points.</param>
    /// <param name="cropHeight">CropBox (or MediaBox) height in PDF points.</param>
    /// <param name="vx">PdfPig visual left coordinate of the bounding box.</param>
    /// <param name="vy">PdfPig visual bottom coordinate of the bounding box.</param>
    /// <param name="vw">Visual box width.</param>
    /// <param name="vh">Visual box height.</param>
    /// <returns>
    /// An <see cref="XRect"/> in PdfSharp's coordinate system (top-left origin, Y↓) that
    /// corresponds to the given visual bounding box.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The coordinate transform for each rotation is derived by inverting the PDF viewer's
    /// rotation (applied CW) to recover raw MediaBox coordinates, then converting
    /// bottom-left → top-left for PdfSharp.  Let cw=cropWidth, ch=cropHeight, mh=mediaBoxHeight.
    /// <code>
    ///   0°:   rawX = cx1+vx,          rawY(top) = mh−cy1−vy−vh,   drawW=vw, drawH=vh
    ///  90°:   rawX = cx1+cw−vy−vh,    rawY(top) = mh−cy1−vx−vw,   drawW=vh, drawH=vw
    /// 180°:   rawX = cx1+cw−vx−vw,    rawY(top) = mh−cy1−ch+vy,   drawW=vw, drawH=vh
    /// 270°:   rawX = cx1+vy,          rawY(top) = mh−cy1−ch+vx,   drawW=vh, drawH=vw
    /// </code>
    /// Invariant: the returned <see cref="XRect"/> must lie fully within the raw MediaBox
    /// (0 ≤ X, X+Width ≤ cx1+cropWidth, 0 ≤ Y, Y+Height ≤ mediaBoxHeight).
    /// </para>
    /// </remarks>
    internal static XRect ComputeHighlightRect(
        int rotateDegrees,
        double mediaBoxHeight,
        double cx1,
        double cy1,
        double cropWidth,
        double cropHeight,
        double vx,
        double vy,
        double vw,
        double vh)
    {
        double rawX, pdfSharpTop, drawW, drawH;

        switch (rotateDegrees)
        {
            case 90:
                // 90° CW viewer rotation: the viewer takes the raw page and rotates it 90° CW.
                // Inverting (90° CCW): rx = cropWidth − vy − vh (bottom), ry = vx.
                // Visual width = cropHeight, visual height = cropWidth; axes swap: drawW=vh, drawH=vw.
                rawX        = cx1 + cropWidth - vy - vh;
                pdfSharpTop = mediaBoxHeight - cy1 - vx - vw;
                drawW       = vh;
                drawH       = vw;
                break;

            case 180:
                // 180° rotation: both axes are inverted.
                rawX        = cx1 + cropWidth  - vx - vw;
                pdfSharpTop = mediaBoxHeight - cy1 - cropHeight + vy;
                drawW       = vw;
                drawH       = vh;
                break;

            case 270:
                // 270° CW (= 90° CCW) viewer rotation: the viewer rotates 270° CW.
                // Inverting (90° CW): rx = vy, ry = cropHeight − vx − vw (bottom).
                // Visual width = cropHeight, visual height = cropWidth; axes swap: drawW=vh, drawH=vw.
                rawX        = cx1 + vy;
                pdfSharpTop = mediaBoxHeight - cy1 - cropHeight + vx;
                drawW       = vh;
                drawH       = vw;
                break;

            default: // 0° — standard Y-flip only, no axis swap
                rawX        = cx1 + vx;
                pdfSharpTop = mediaBoxHeight - cy1 - vy - vh;
                drawW       = vw;
                drawH       = vh;
                break;
        }

        return new XRect(rawX, pdfSharpTop, drawW, drawH);
    }

    /// <summary>
    /// Draws a small coloured square in the top-left margin for findings that have a
    /// page number but no precise bounding box.
    /// </summary>
    private void DrawPageMarker(PdfPage page, RuleFinding finding)
    {
        // Stack markers down the left edge if multiple findings land on the same page.
        // Because we process findings in order, the first marker always draws at the
        // top-left corner. Subsequent ones on the same page will overlap — acceptable
        // since each carries its CheckId label and the use-case (page-hint-only) is rare.
        var rect = new XRect(MarkerLeftOffset, MarkerTopOffset, MarkerSide, MarkerSide);

        using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

        var fillBrush = new XSolidBrush(MarkerFill);
        gfx.DrawRectangle(fillBrush, rect);

        var borderPen = new XPen(MarkerBorder, 1.0);
        gfx.DrawRectangle(borderPen, rect);

        var labelFont = LazyLabelFont.Value;
        if (labelFont is not null)
        {
            gfx.DrawString(
                finding.CheckId,
                labelFont,
                XBrushes.DarkOrange,
                new XRect(MarkerLeftOffset + MarkerSide + 2.0, MarkerTopOffset + 4.0, 80.0, 12.0),
                XStringFormats.TopLeft);
        }

        _logger.LogDebug(
            "Drew page-margin marker for {CheckId} on page {Page} (no bounding box).",
            finding.CheckId, finding.Locator!.PageNumber);
    }
}
