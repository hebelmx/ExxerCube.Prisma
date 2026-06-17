namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Indicates whether a movement in the DESGLOSE DE MOVIMIENTOS DEL PERIODO table
/// is a charge (cargo) or a credit (abono).
/// </summary>
/// <remarks>
/// <para>
/// The VEC statement uses a leading sign token on the Monto column:
/// <list type="bullet">
///   <item><description>"+" — cargo (charge): increases the outstanding balance.</description></item>
///   <item><description>"−" / "-" — abono (credit): reduces the outstanding balance (payments, refunds).</description></item>
/// </list>
/// </para>
/// </remarks>
public enum MovementSign
{
    /// <summary>
    /// Cargo — a charge that increases the outstanding balance (sign token "+" in the PDF).
    /// </summary>
    Charge,

    /// <summary>
    /// Abono — a credit that reduces the outstanding balance (sign token "−"/"-" in the PDF).
    /// </summary>
    Credit,
}
