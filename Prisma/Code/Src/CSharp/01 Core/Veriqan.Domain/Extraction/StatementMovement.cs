using System;

namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// A single movement row extracted from the "DESGLOSE DE MOVIMIENTOS DEL PERIODO" table
/// in a VEC (Estado de Cuenta) statement PDF (Story 4.4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Column layout in the PDF</b> (empirically measured, PDF-point coordinates, origin bottom-left):
/// <list type="bullet">
///   <item><description>Fecha de la operación — X ≈ 21–90</description></item>
///   <item><description>Fecha de cargo — X ≈ 101–155 (year digit sometimes truncated in PDF render)</description></item>
///   <item><description>Descripción del movimiento — X ≈ 159–420 (free-form, multi-word)</description></item>
///   <item><description>Monto sign token (+/−) — X ≈ 423–430</description></item>
///   <item><description>Monto amount — X ≈ 440–520</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Row reconstruction:</b> words are grouped into horizontal bands using a Y-tolerance of
/// ±4 pt.  A band is classified as a data row when it contains both a date-like token in the
/// operation-date column (X ≤ 95) and a sign token (+/−) in the Monto column (X ≥ 410).
/// Continuation rows (FX-rate lines with no date/sign) are folded into the description of the
/// preceding row.
/// </para>
/// <para>
/// <b>Truncated charge-date year:</b> the PDF sometimes renders the Fecha de cargo year with
/// the last digit clipped (e.g. "07-jul-202" instead of "07-jul-2025").  The extractor attempts
/// to repair this by appending the missing digit inferred from the period year.  When repair
/// fails, <see cref="ChargeDate"/> is <see langword="null"/>.
/// </para>
/// <para>
/// <b>Abono detection:</b> the sign token "−" or "-" sets <see cref="Sign"/> to
/// <see cref="MovementSign.Credit"/>; "+" sets it to <see cref="MovementSign.Charge"/>.
/// </para>
/// </remarks>
public sealed record StatementMovement
{
    /// <summary>
    /// Initializes a <see cref="StatementMovement"/> with all fields.
    /// </summary>
    /// <param name="operationDate">
    /// Date the transaction occurred ("Fecha de la operación"), or <see langword="null"/> when the
    /// date token cannot be parsed.
    /// </param>
    /// <param name="chargeDate">
    /// Date the amount was posted ("Fecha de cargo"), or <see langword="null"/> when the date token
    /// cannot be parsed (including the PDF year-truncation case that cannot be repaired).
    /// </param>
    /// <param name="description">
    /// Transaction description as printed in the "Descripción del movimiento" column.
    /// Multiple words are joined with a single space.  Never <see langword="null"/> or empty
    /// on a successfully reconstructed row.
    /// </param>
    /// <param name="amount">
    /// Absolute monetary amount (always positive; direction is encoded in <paramref name="sign"/>).
    /// </param>
    /// <param name="sign">
    /// Whether the movement is a charge (cargo, "+") or a credit (abono, "−").
    /// </param>
    /// <param name="locator">
    /// Bounding-box locator for the row within the PDF (page number + coordinates).
    /// </param>
    public StatementMovement(
        DateOnly? operationDate,
        DateOnly? chargeDate,
        string description,
        decimal amount,
        MovementSign sign,
        FieldLocator locator)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(locator);

        OperationDate = operationDate;
        ChargeDate = chargeDate;
        Description = description;
        Amount = amount;
        Sign = sign;
        Locator = locator;
    }

    /// <summary>
    /// Date the transaction occurred ("Fecha de la operación").
    /// <see langword="null"/> when the token cannot be parsed.
    /// </summary>
    public DateOnly? OperationDate { get; }

    /// <summary>
    /// Date the charge was posted to the account ("Fecha de cargo").
    /// <see langword="null"/> when the token cannot be parsed (including PDF year-truncation
    /// cases where the last digit of the year is clipped in the rendered PDF stream).
    /// </summary>
    public DateOnly? ChargeDate { get; }

    /// <summary>
    /// Transaction description as it appears in the "Descripción del movimiento" column.
    /// Multiple PdfPig words on the same band are joined with a single space.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Absolute monetary amount (always non-negative).
    /// The sign/direction is encoded separately in <see cref="Sign"/>.
    /// </summary>
    public decimal Amount { get; }

    /// <summary>
    /// Indicates whether this movement is a charge (cargo) or a credit (abono).
    /// Derived from the "+" / "−" sign token in the Monto column.
    /// </summary>
    public MovementSign Sign { get; }

    /// <summary>
    /// Bounding-box locator identifying the page and approximate coordinates of the row
    /// within the source PDF.
    /// </summary>
    public FieldLocator Locator { get; }
}
