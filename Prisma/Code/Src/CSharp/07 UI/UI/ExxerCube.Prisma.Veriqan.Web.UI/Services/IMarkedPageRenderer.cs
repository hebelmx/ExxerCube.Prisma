using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Services;

/// <summary>
/// Rasterizes the pages of an already colour-marked PDF (produced by
/// <c>IMarkedPdfGenerator.Generate</c>) into PNG images — one per page that carries at
/// least one <see cref="Domain.Enums.FindingVerdict.Fail"/> finding — so a Blazor page can
/// render them inline (e.g. as data-URI <c>&lt;img&gt;</c> sources) without writing anything
/// to <c>wwwroot</c>.
/// </summary>
/// <remarks>
/// VLD-S3 (see <c>docs/planning-artifacts/epics-veriqan-live-demo-ui-2026-07-04.md</c>).
/// A small, stateless, independently-testable wrapper around
/// <c>PDFtoImage.Conversion.ToImage</c> — it does not draw anything itself (that is
/// <c>IMarkedPdfGenerator</c>'s job), only decides which pages to render and encodes them.
/// </remarks>
public interface IMarkedPageRenderer
{
    /// <summary>
    /// Renders one PNG per page referenced by a <see cref="Domain.Enums.FindingVerdict.Fail"/>
    /// finding in <paramref name="findings"/>. When there are no Fail findings at all (e.g. a
    /// GREEN verdict), falls back to rendering page 1 only, so every verdict still has at
    /// least one hero image.
    /// </summary>
    /// <param name="markedPdf">
    /// Raw bytes of the already marked-up PDF (the output of
    /// <c>IMarkedPdfGenerator.Generate</c>). Must be a valid, non-empty PDF.
    /// </param>
    /// <param name="findings">
    /// The complete list of rule findings for this verification run. Only
    /// <see cref="Domain.Enums.FindingVerdict.Fail"/> entries with a locator whose
    /// <c>PageNumber</c> is in range contribute a page; findings without a usable page
    /// number are silently skipped (matches <c>IMarkedPdfGenerator</c>'s own behaviour).
    /// </param>
    /// <param name="cancellationToken">Token used to observe cancellation.</param>
    /// <returns>
    /// <list type="bullet">
    ///   <item><description>Success — a dictionary keyed by 1-based page number, each value the page's PNG bytes.</description></item>
    ///   <item><description>Failure — a typed error message when the input is invalid or the PDF cannot be parsed/rendered.</description></item>
    ///   <item><description>Cancelled — when <paramref name="cancellationToken"/> is signalled before work completes.</description></item>
    /// </list>
    /// </returns>
    Result<IReadOnlyDictionary<int, byte[]>> RenderFindingPages(
        byte[] markedPdf,
        IReadOnlyList<RuleFinding> findings,
        CancellationToken cancellationToken = default);
}
