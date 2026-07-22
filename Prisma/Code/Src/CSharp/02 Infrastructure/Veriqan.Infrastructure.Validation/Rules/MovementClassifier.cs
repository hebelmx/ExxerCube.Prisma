using System.Text.RegularExpressions;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// Static helper for classifying DESGLOSE movements by description content.
/// Used by CL-18, CL-19, and CL-20 to distinguish MSI installment charges
/// from regular charges.
/// </summary>
internal static class MovementClassifier
{
    /// <summary>
    /// Pattern that identifies an MSI ("meses sin intereses") installment row.
    /// Matches the "NNN de NNN" fragment that Banamex prints in the description,
    /// e.g. "DON COLCHON CUMBRES 005 de 012".
    /// </summary>
    private static readonly Regex MsiPattern = new(
        @"\b\d{1,3}\s+de\s+\d{1,3}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Pattern that identifies a DESGLOSE row whose own charge is interest, a commission/fee,
    /// or the VAT on either (e.g. "MONTO DE INTERESES", "COMISION ANUALIDAD",
    /// "IVA POR INTERESES Y/O COMISIONES") — RC1.S4.a (CL-18 real-corpus triage, class c).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The printed RESUMEN line "Cargos regulares (no a meses)" excludes interest, commission,
    /// and IVA-on-interest/commission rows — they are reported on their own dedicated RESUMEN
    /// lines (MontoIntereses / MontoComisiones / IvaInteresesYComisiones). CL-18's DESGLOSE sum
    /// must mirror that exclusion; evidence: Observed−Expected == MontoIntereses+MontoComisiones+
    /// IvaInteresesYComisiones to the cent on 4 independent real Banamex months
    /// (<c>docs/qa/calibration/real-corpus-triage-2026-07.md</c>).
    /// </para>
    /// <para>
    /// Deliberately conservative: matches only the three keyword families
    /// (INTERES/INTERESES, COMISION/COMISIONES with or without the accent, IVA) as whole words.
    /// A row whose description does not contain one of these keywords is NOT excluded — per the
    /// "misread digit ≠ false non-compliant" program rule, an ambiguous row stays IN the sum
    /// (fail-honest) rather than being silently dropped.
    /// </para>
    /// </remarks>
    private static readonly Regex InterestCommissionIvaPattern = new(
        @"\b(INTERES(?:ES)?|COMISI[OÓ]N(?:ES)?|IVA)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Returns <see langword="true"/> when the movement description contains the MSI
    /// installment marker pattern "NNN de NNN" (e.g. "DON COLCHON CUMBRES 005 de 012").
    /// </summary>
    /// <param name="description">Movement description text from the DESGLOSE table.</param>
    public static bool IsMsi(string description) => MsiPattern.IsMatch(description);

    /// <summary>
    /// Returns <see langword="true"/> when the movement description identifies the row as an
    /// interest, commission, or VAT-on-interest/commission charge (own RESUMEN line — excluded
    /// from CL-18's "Cargos regulares" DESGLOSE sum). See <see cref="InterestCommissionIvaPattern"/>.
    /// </summary>
    /// <param name="description">Movement description text from the DESGLOSE table.</param>
    public static bool IsInterestCommissionOrIva(string description) =>
        InterestCommissionIvaPattern.IsMatch(description);
}
