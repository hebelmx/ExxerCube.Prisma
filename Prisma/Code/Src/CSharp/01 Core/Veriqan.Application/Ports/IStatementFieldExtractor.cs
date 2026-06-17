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
/// Story 3.2 — <see cref="ExtractFullAsync"/>: performs a single-pass extraction of both
/// header identity fields and period/summary fields, returning a <see cref="StatementModel"/>
/// with <see cref="StatementModel.PeriodSummary"/> populated.
/// </para>
/// </remarks>
public interface IStatementFieldExtractor
{
    /// <summary>
    /// Extracts header identity fields from a VEC statement PDF and returns a
    /// partially-populated <see cref="StatementModel"/> (header section only;
    /// <see cref="StatementModel.PeriodSummary"/> is <see langword="null"/>).
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

    /// <summary>
    /// Extracts all fields from a VEC statement PDF — both header identity fields and
    /// period/summary fields — in a single pass, returning a fully-populated
    /// <see cref="StatementModel"/> with <see cref="StatementModel.PeriodSummary"/> set.
    /// </summary>
    /// <param name="pdf">Raw bytes of the statement PDF.</param>
    /// <param name="cancellationToken">Cancellation token propagated to all async operations.</param>
    /// <returns>
    /// <see cref="Result{T}.IsSuccess"/> with a fully-populated <see cref="StatementModel"/> on success;
    /// a cancelled result when <paramref name="cancellationToken"/> is already triggered;
    /// a failure result when the PDF cannot be opened or is structurally unrecognisable.
    /// </returns>
    /// <remarks>
    /// Extraction never throws for missing or format-invalid fields.
    /// All fields in <see cref="StatementModel.PeriodSummary"/> that were not found carry
    /// <see cref="Domain.Extraction.ExtractionStatus.NotExtracted"/> with a page-level locator hint.
    /// </remarks>
    Task<Result<StatementModel>> ExtractFullAsync(
        byte[] pdf,
        CancellationToken cancellationToken = default);
}
