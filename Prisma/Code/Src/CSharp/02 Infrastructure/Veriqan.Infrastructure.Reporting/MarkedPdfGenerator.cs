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
    /// <b>Y-axis flip:</b> PdfPig gives <c>(left, bottom, width, height)</c> in a
    /// bottom-left coordinate system.  PdfSharp uses top-left.  For a page of height
    /// <c>H</c> the PdfSharp top-left Y is:
    /// <c>pdfSharpY = H - pdfPigBottom - height</c>.
    /// </remarks>
    private void DrawBoundingBoxHighlight(PdfPage page, RuleFinding finding, FieldLocator locator)
    {
        // All four components are non-null when HasBoundingBox is true.
        var left = locator.Left!.Value;
        var pdfPigBottom = locator.Bottom!.Value;
        var width = locator.Width!.Value;
        var height = locator.Height!.Value;

        var pageHeight = page.Height.Point;

        // Y-axis flip: convert PdfPig bottom-left origin to PdfSharp top-left origin.
        var pdfSharpTop = pageHeight - pdfPigBottom - height;

        var rect = new XRect(left, pdfSharpTop, width, height);

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
            var labelY = pdfSharpTop >= 10.0 ? pdfSharpTop - 8.0 : pdfSharpTop + 1.0;
            gfx.DrawString(
                finding.CheckId,
                labelFont,
                XBrushes.DarkRed,
                new XRect(left, labelY, width, 10.0),
                XStringFormats.TopLeft);
        }

        _logger.LogDebug(
            "Drew bounding-box highlight for {CheckId} on page {Page}: PdfPig(left={L}, bottom={B}, w={W}, h={H}) → PdfSharp(x={X}, y={Y}).",
            finding.CheckId, locator.PageNumber, left, pdfPigBottom, width, height, left, pdfSharpTop);
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
