using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;
using PDFtoImage;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Services;

/// <summary>
/// PDFtoImage/SkiaSharp-based implementation of <see cref="IMarkedPageRenderer"/> (VLD-S3).
/// </summary>
/// <remarks>
/// Stateless — safe to register as a singleton. Every call opens its own
/// <see cref="MemoryStream"/> over the supplied bytes; nothing is cached across calls.
/// </remarks>
public sealed class MarkedPageRenderer : IMarkedPageRenderer
{
    /// <summary>
    /// DPI used to rasterize pages, matching <c>FiscalPageRenderDpi</c> at
    /// <c>PdfPigStatementFieldExtractor.cs:3525</c> for visual consistency across the
    /// Veriqan UI.
    /// </summary>
    private const int RenderDpi = 150;

    /// <summary>PNG encode quality passed to <c>SKBitmap.Encode</c> (0-100; PNG is lossless, so this has no visual effect but is required by the API).</summary>
    private const int PngEncodeQuality = 100;

    private readonly ILogger<MarkedPageRenderer> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="MarkedPageRenderer"/>.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public MarkedPageRenderer(ILogger<MarkedPageRenderer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Result<IReadOnlyDictionary<int, byte[]>> RenderFindingPages(
        byte[] markedPdf,
        IReadOnlyList<RuleFinding> findings,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("MarkedPageRenderer.RenderFindingPages cancelled before starting.");
            return ResultExtensions.Cancelled<IReadOnlyDictionary<int, byte[]>>();
        }

        if (markedPdf is null || markedPdf.Length == 0)
        {
            return Result<IReadOnlyDictionary<int, byte[]>>.WithFailure(
                "markedPdf must be a non-empty byte array.");
        }

        if (findings is null)
        {
            return Result<IReadOnlyDictionary<int, byte[]>>.WithFailure(
                "findings must not be null.");
        }

        try
        {
            return RenderCore(markedPdf, findings, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("MarkedPageRenderer.RenderFindingPages cancelled during rendering.");
            return ResultExtensions.Cancelled<IReadOnlyDictionary<int, byte[]>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error rendering marked-page PNGs.");
            return Result<IReadOnlyDictionary<int, byte[]>>.WithFailure(
                $"Failed to render marked pages: {ex.Message}", default(IReadOnlyDictionary<int, byte[]>), ex);
        }
    }

    // -----------------------------------------------------------------------
    // Core implementation
    // -----------------------------------------------------------------------

    private Result<IReadOnlyDictionary<int, byte[]>> RenderCore(
        byte[] markedPdf,
        IReadOnlyList<RuleFinding> findings,
        CancellationToken cancellationToken)
    {
        int pageCount;
        try
        {
#pragma warning disable CA1416 // PDFtoImage is cross-platform
            pageCount = Conversion.GetPageCount(markedPdf);
#pragma warning restore CA1416
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the supplied bytes as a PDF document.");
            return Result<IReadOnlyDictionary<int, byte[]>>.WithFailure(
                $"Input is not a valid PDF: {ex.Message}", default(IReadOnlyDictionary<int, byte[]>), ex);
        }

        if (pageCount <= 0)
        {
            return Result<IReadOnlyDictionary<int, byte[]>>.WithFailure(
                "Input PDF has no pages.");
        }

        var targetPages = DetermineTargetPages(findings, pageCount);

        var rendered = new Dictionary<int, byte[]>(targetPages.Count);

        foreach (var pageNumber in targetPages)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug(
                    "MarkedPageRenderer.RenderFindingPages cancelled while rendering page {Page}.",
                    pageNumber);
                return ResultExtensions.Cancelled<IReadOnlyDictionary<int, byte[]>>();
            }

            rendered[pageNumber] = RenderPageToPng(markedPdf, pageNumber);
        }

        _logger.LogInformation(
            "Rendered {Count} marked page(s) to PNG at {Dpi} DPI: [{Pages}].",
            rendered.Count, RenderDpi, string.Join(", ", rendered.Keys));

        return Result<IReadOnlyDictionary<int, byte[]>>.Success(rendered);
    }

    /// <summary>
    /// Determines the distinct, 1-based page numbers to render: every page referenced by a
    /// <see cref="FindingVerdict.Fail"/> finding whose locator carries an in-range page
    /// number, falling back to page 1 alone when no such page exists (e.g. a GREEN verdict
    /// with zero Fail findings, or every Fail finding lacking a usable page number).
    /// </summary>
    private static IReadOnlyCollection<int> DetermineTargetPages(
        IReadOnlyList<RuleFinding> findings,
        int pageCount)
    {
        var targetPages = new SortedSet<int>();

        foreach (var finding in findings)
        {
            if (finding.Verdict != FindingVerdict.Fail)
                continue;

            var locator = finding.Locator;

            // Locator null, or PageNumber == 0 (NoPage sentinel): no usable page — skip.
            if (locator is null || locator.PageNumber == 0)
                continue;

            // Out of range for the actual document: skip this finding, don't fail the render.
            if (locator.PageNumber < 1 || locator.PageNumber > pageCount)
                continue;

            targetPages.Add(locator.PageNumber);
        }

        if (targetPages.Count == 0)
        {
            targetPages.Add(1);
        }

        return targetPages;
    }

    /// <summary>
    /// Rasterizes a single 1-based page number to PNG bytes via PDFtoImage + SkiaSharp.
    /// </summary>
    private static byte[] RenderPageToPng(byte[] markedPdf, int pageNumber)
    {
        // PDFtoImage page index is 0-based; RuleFinding.Locator.PageNumber is 1-based.
        // Exact pattern already used at PdfPigStatementFieldExtractor.cs:3447 / :3635 —
        // copied, not reinvented.
        using var pdfStream = new MemoryStream(markedPdf);
#pragma warning disable CA1416 // PDFtoImage is cross-platform
        using var bitmap = Conversion.ToImage(
            pdfStream,
            leaveOpen: false,
            page: pageNumber - 1,
            options: new RenderOptions(Dpi: RenderDpi));
#pragma warning restore CA1416

        using var pngData = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, PngEncodeQuality);
        return pngData.ToArray();
    }
}
