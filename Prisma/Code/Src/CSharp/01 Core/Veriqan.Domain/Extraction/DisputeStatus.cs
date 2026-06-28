namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Mandated CONDUSEF status for a §23 <i>Cargos no reconocidos</i> dispute row.
/// </summary>
/// <remarks>
/// <para>
/// The three statutory values are drawn directly from the CONDUSEF <i>Acuerdo</i>
/// (DOF 29-Dec-2022) §23 closed-enumeration specification:
/// <list type="bullet">
///   <item><c>pendiente-en-revisión</c> — under review; no decision yet.</item>
///   <item><c>concluida-procedente</c>  — concluded, charge reversed (favour of client).</item>
///   <item><c>concluida-improcedente</c>— concluded, charge upheld (favour of institution).</item>
/// </list>
/// <see cref="Unknown"/> is a defensive sentinel used when the status token in the extracted
/// text does not match any of the three mandated values.
/// </para>
/// </remarks>
public enum DisputeStatus
{
    /// <summary>
    /// The dispute is still under review by the institution (<c>pendiente-en-revisión</c>).
    /// A DESGLOSE linkage check is not required — the charge has not been resolved yet.
    /// </summary>
    Pendiente,

    /// <summary>
    /// The dispute was concluded in the client's favour — the charge was reversed
    /// (<c>concluida-procedente</c>).  The reversed amount MUST appear as a credit
    /// in the DESGLOSE DE MOVIMIENTOS section.
    /// </summary>
    ConcluidaProcedente,

    /// <summary>
    /// The dispute was concluded in the institution's favour — the charge was upheld
    /// (<c>concluida-improcedente</c>).  The contested amount MUST appear as a debit
    /// in the DESGLOSE DE MOVIMIENTOS section.
    /// </summary>
    ConcluidaImprocedente,

    /// <summary>
    /// The status token extracted from the §23 section text does not match any of the
    /// three mandated CONDUSEF values.  Rules should treat <see cref="Unknown"/> rows as
    /// unresolvable and abstain from linkage checks for those rows.
    /// </summary>
    Unknown,
}
