using System.Collections.Generic;
using System.Threading;
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
        CancellationToken cancellationToken = default);
}
