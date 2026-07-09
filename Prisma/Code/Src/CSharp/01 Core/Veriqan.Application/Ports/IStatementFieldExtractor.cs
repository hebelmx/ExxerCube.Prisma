using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
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
/// <para>
/// Story 4.4 — <see cref="ExtractFullAsync"/> also populates
/// <see cref="StatementModel.Movements"/> with the parsed DESGLOSE DE MOVIMIENTOS DEL PERIODO
/// transaction rows and sets <see cref="StatementModel.MovementsStatus"/> to indicate
/// whether the section was found and how many rows were reconstructed.
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
    /// Extracts all fields from a VEC statement PDF — header identity fields, period/summary
    /// fields, and the DESGLOSE DE MOVIMIENTOS DEL PERIODO transaction table — in a single pass.
    /// </summary>
    /// <param name="pdf">Raw bytes of the statement PDF.</param>
    /// <param name="cancellationToken">Cancellation token propagated to all async operations.</param>
    /// <param name="referenceBundle">
    /// Story 3.3a — optional tenant reference/catalog bundle. When supplied and its
    /// <see cref="VecReferenceBundle.Products"/> catalog is non-empty, an OCR-recovered
    /// <see cref="FieldKind.Product"/> value is gated by catalog membership (the same
    /// <see cref="IProductResolver"/> match the downstream binder uses), so a terminal Product
    /// candidate that does not resolve in the catalog abstains (<c>NotExtracted</c>) rather than
    /// being returned. When <see langword="null"/> (the default — every caller as of this story),
    /// Product resolution is completely unchanged from prior behavior.
    /// </param>
    /// <returns>
    /// <see cref="Result{T}.IsSuccess"/> with a fully-populated <see cref="StatementModel"/> on success;
    /// a cancelled result when <paramref name="cancellationToken"/> is already triggered;
    /// a failure result when the PDF cannot be opened or is structurally unrecognisable.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Extraction never throws for missing or format-invalid fields.
    /// All fields in <see cref="StatementModel.PeriodSummary"/> that were not found carry
    /// <see cref="Domain.Extraction.ExtractionStatus.NotExtracted"/> with a page-level locator hint.
    /// </para>
    /// <para>
    /// When the DESGLOSE section is absent (e.g. minimal test PDFs),
    /// <see cref="StatementModel.Movements"/> is empty and
    /// <see cref="StatementModel.MovementsStatus"/> is
    /// <see cref="Domain.Extraction.MovementsExtractionStatus.SectionNotFound"/>.
    /// Partial parse failures (unreadable date/amount on individual rows) produce a
    /// <see langword="null"/> field on that row rather than a failure result.
    /// </para>
    /// </remarks>
    Task<Result<StatementModel>> ExtractFullAsync(
        byte[] pdf,
        CancellationToken cancellationToken = default,
        VecReferenceBundle? referenceBundle = null);
}
