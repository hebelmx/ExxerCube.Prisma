using System.Collections.Generic;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Reporting;

/// <summary>
/// Produces a colour-marked copy of a statement PDF with each <see cref="Domain.Enums.FindingVerdict.Fail"/>
/// finding highlighted at the position identified by its <see cref="Domain.Extraction.FieldLocator"/>.
/// </summary>
/// <remarks>
/// FR-16 / CL-55 — the original PDF bytes are never mutated; a new byte array is returned.
/// </remarks>
public interface IMarkedPdfGenerator
{
    /// <summary>
    /// Draws colour highlights on the pages of <paramref name="originalPdf"/> for every
    /// <see cref="Domain.Enums.FindingVerdict.Fail"/> finding in <paramref name="findings"/>
    /// that carries a bounding-box locator, then returns the annotated PDF as a byte array.
    /// Each drawn highlight is labelled with a sequential callout number (1, 2, 3 …) so
    /// it can be cross-referenced with the UI compliance-report list.
    /// </summary>
    /// <param name="originalPdf">
    /// Raw bytes of the source PDF. Must be a valid, non-empty PDF.
    /// </param>
    /// <param name="findings">
    /// The complete list of rule findings for this verification run.
    /// Only <see cref="Domain.Enums.FindingVerdict.Fail"/> entries with a
    /// <see cref="Domain.Extraction.FieldLocator.HasBoundingBox"/> locator receive a
    /// coloured box; others are silently skipped.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to observe cancellation.
    /// </param>
    /// <param name="checklistTiers">
    /// Optional map from <c>CheckId</c> to <see cref="ChecklistTier"/>.
    /// When a FAIL finding's <c>CheckId</c> maps to <see cref="ChecklistTier.Bank"/> the
    /// highlight is drawn in <b>amber</b> (bank improvement-opportunity colour).
    /// Findings mapped to <see cref="ChecklistTier.Condusef"/> or
    /// <see cref="ChecklistTier.Both"/>, findings whose <c>CheckId</c> is absent from the
    /// map, and any call where this parameter is <see langword="null"/> all receive the
    /// default <b>red</b> highlight — this is the conservative, abstain-safe default so
    /// regulatory findings are never silently downgraded.
    /// When <see langword="null"/>, behaviour is identical to prior versions (all red).
    /// </param>
    /// <returns>
    /// <list type="bullet">
    ///   <item><description>Success — the annotated PDF bytes (may equal the input if there are no FAIL findings with a bounding box).</description></item>
    ///   <item><description>Failure — a typed error message when the input is invalid or the PDF cannot be parsed.</description></item>
    ///   <item><description>Cancelled — when <paramref name="cancellationToken"/> is signalled before work completes.</description></item>
    /// </list>
    /// </returns>
    Result<byte[]> Generate(
        byte[] originalPdf,
        IReadOnlyList<RuleFinding> findings,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, ChecklistTier>? checklistTiers = null);
}
