using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Application.Ports;

/// <summary>
/// Port for extracting structured fields from a VEC (Estado de Cuenta) statement PDF.
/// </summary>
/// <remarks>
/// <para>
/// Story 3.1 — <see cref="ExtractHeaderAsync"/>: extracts the identity fields from the
/// statement header (client name, address, branch number, card number, CLABE, client number, RFC).
/// </para>
/// <para>
/// Story 3.2 will extend this interface with period/summary extraction, either by adding
/// a sibling method (<c>ExtractPeriodSummaryAsync</c>) or by enriching the returned
/// <see cref="StatementModel"/> with a populated <c>PeriodSummary</c>.
/// </para>
/// </remarks>
public interface IStatementFieldExtractor
{
    /// <summary>
    /// Extracts header identity fields from a VEC statement PDF and returns a
    /// partially-populated <see cref="StatementModel"/> (header section only).
    /// </summary>
    /// <param name="pdf">Raw bytes of the statement PDF.</param>
    /// <param name="cancellationToken">Cancellation token propagated to all async operations.</param>
    /// <returns>
    /// <see cref="Result{T}.IsSuccess"/> with a <see cref="StatementModel"/> on success;
    /// a cancelled result when <paramref name="cancellationToken"/> is already triggered;
    /// a failure result when the PDF cannot be opened or is structurally unrecognisable.
    /// </returns>
    /// <remarks>
    /// Extraction never throws for missing or format-invalid fields — those are represented
    /// as <see cref="Domain.Extraction.ExtractionStatus.NotExtracted"/> or
    /// <see cref="Domain.Extraction.ExtractionStatus.ExtractedInvalidFormat"/> in the returned model.
    /// A failure result is reserved for infrastructure errors (unreadable PDF, etc.).
    /// </remarks>
    Task<Result<StatementModel>> ExtractHeaderAsync(
        byte[] pdf,
        CancellationToken cancellationToken = default);
}
