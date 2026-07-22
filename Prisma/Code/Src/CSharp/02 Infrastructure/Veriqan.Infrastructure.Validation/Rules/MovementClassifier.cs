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
    /// Pattern that identifies a DESGLOSE row as a genuine bank-fee row — interest, commission,
    /// or the VAT on either (e.g. "MONTO DE INTERESES", "COMISION ANUALIDAD",
    /// "IVA POR INTERESES Y/O COMISIONES") — by matching known fee-row description PREFIXES,
    /// not bare mid-string keywords. RC1.S4.a (CL-18 real-corpus triage, class c) / RC1.S4.b
    /// (residual fix, chunk B4).
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
    /// <b>RC1.S4.b residual fix:</b> the original pattern matched the bare whole-word keyword
    /// <c>\bCOMISION\b</c> anywhere in the description, which excluded a real $230.00 MERCHANT
    /// purchase row — "COMISION ESTATAL DE AG CEA 800313C95MX" (Comisión Estatal de Aguas, a
    /// state water utility) — from the regular-charges sum (delta exactly −230.00,
    /// B-2026-06). Mexican merchants are commonly named "Comisión …" (e.g. CFE = Comisión
    /// Federal de Electricidad appears on millions of statements), so bare-keyword matching is
    /// unsafe at scale. The pattern now anchors to the specific bank-fee phrase family printed
    /// across all 4 real Banamex "B" months — "IVA POR INTERESES Y/O COMISIONES",
    /// "INTERES GRAVAB. DISPONIBLE BANAM", "INTERES EXENTO DISPONIBLE BANAM" — plus the
    /// synthetic-fixture phrases "MONTO DE INTERESES" / "COMISION ANUALIDAD", all matched as
    /// description PREFIXES. A merchant name will not happen to begin with the bank's own
    /// fee-line wording, so the false positive is eliminated by construction.
    /// </para>
    /// <para>
    /// Deliberately conservative: a row whose description does not start with one of these
    /// known fee-row phrases is NOT excluded — per the "misread digit ≠ false non-compliant"
    /// program rule, an ambiguous row stays IN the sum (fail-honest) rather than being silently
    /// dropped.
    /// </para>
    /// </remarks>
    private static readonly Regex InterestCommissionIvaPattern = new(
        @"^\s*(?:" +
            @"IVA\s+POR\s+INTERES(?:ES)?\s+Y/?O\s+COMISI[OÓ]N(?:ES)?" +
            @"|INTERES\s+GRAVAB" +
            @"|INTERES\s+EXENTO" +
            @"|MONTO\s+DE\s+INTERES(?:ES)?" +
            @"|COMISI[OÓ]N\s+ANUALIDAD" +
        @")",
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
