using System;

namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// A single dispute row extracted from the §23 <i>Cargos no reconocidos</i> section of
/// a VEC (Estado de Cuenta) credit-card statement (Story E10.C4′).
/// </summary>
/// <remarks>
/// <para>
/// <b>Extraction fidelity (best-effort):</b> the §23 section text is stored as a
/// normalized, whitespace-collapsed string after the extraction pipeline runs
/// <see cref="VecTextNormalizer.Normalize"/>.  Per-row bounding-box coordinates and dates
/// are not reliably recoverable from the normalized text.  As a result:
/// <list type="bullet">
///   <item>
///     <see cref="OperationDate"/> is <see langword="null"/> when the date cannot be
///     unambiguously parsed from the surrounding text.
///   </item>
///   <item>
///     <see cref="Description"/> is <see langword="null"/> when the description cannot
///     be extracted.
///   </item>
///   <item>
///     <see cref="Locator"/> carries a section-level page hint, not a row-level bounding box.
///   </item>
/// </list>
/// Consumers (particularly validation rules) MUST NOT use <see cref="OperationDate"/> or
/// <see cref="Description"/> for correctness checks — only <see cref="Amount"/> and
/// <see cref="Status"/> are reliable.
/// </para>
/// </remarks>
/// <param name="Amount">
/// The absolute monetary amount of the disputed charge (always ≥ 0).
/// Used by the linkage rule to find the matching entry in the DESGLOSE DE MOVIMIENTOS table.
/// </param>
/// <param name="Status">
/// The CONDUSEF-mandated status of this dispute row (<see cref="DisputeStatus"/>).
/// </param>
/// <param name="OperationDate">
/// Best-effort date of the original disputed charge, or <see langword="null"/> when the
/// date cannot be reliably parsed from the normalized section text.
/// </param>
/// <param name="Description">
/// Best-effort transaction description extracted from the section text, or
/// <see langword="null"/> when extraction is not reliable.
/// </param>
/// <param name="Locator">
/// Section-level page hint locator.  Per-row bounding-box coordinates are not available
/// from normalized text — this locator identifies which page §23 was found on.
/// </param>
public sealed record DisputeRow(
    decimal Amount,
    DisputeStatus Status,
    DateOnly? OperationDate,
    string? Description,
    FieldLocator Locator)
{
    /// <summary>
    /// Returns <see langword="true"/> when this row's status is one of the two
    /// <i>concluida</i> values — i.e. the dispute has been resolved.
    /// </summary>
    /// <remarks>
    /// Only concluded disputes (<see cref="DisputeStatus.ConcluidaProcedente"/> or
    /// <see cref="DisputeStatus.ConcluidaImprocedente"/>) are required by the Acuerdo
    /// to appear as a line item in the DESGLOSE DE MOVIMIENTOS.
    /// </remarks>
    public bool IsConcluida =>
        Status is DisputeStatus.ConcluidaProcedente or DisputeStatus.ConcluidaImprocedente;
}
