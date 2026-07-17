using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Calibration;

// ---------------------------------------------------------------------------
// C2.0b — HeaderMoney band locator. Reproduces ExtractNivelDeUsoField's and
// ExtractPagoParaNoGenerarIntereses's label-anchored value-band search
// (PdfPigStatementFieldExtractor.cs ~1110-1147 / ~1646-1692) over REAL PdfPig
// tokenization, so the spike's candidates are measured against the same
// tokens the production extractor actually sees — not author-invented
// BoundingBox fixtures. Deliberately duplicated rather than calling into the
// extractor, same constraint as C1.0b's SpikeBandLocator: the spike must not
// depend on, or risk perturbing, production internals (task brief: "READ-
// ONLY-of-production spike").
// ---------------------------------------------------------------------------

/// <summary>
/// One field's located value row: the full ordered token band (label tokens +
/// any bare "$" sign token(s) + amount-pattern candidates), already filtered
/// to the field's own X-window (open for SaldoCargosRegulares, maxX=300 for
/// PagoParaNoGenerarIntereses — mirroring each extractor's own call to
/// <c>FindAmountInBand</c>/<c>FindAmountInBandSplitDollar</c>), plus how many
/// leading tokens in <see cref="Band"/> are the label (needed by the scorer's
/// rank-distance signal).
/// </summary>
/// <param name="Band">All tokens in the row, ordered left-to-right, already X-windowed.</param>
/// <param name="LabelTokenCount">Number of leading tokens in <see cref="Band"/> that are the label.</param>
internal readonly record struct HeaderMoneyBandContext(IReadOnlyList<SpikeToken> Band, int LabelTokenCount);

internal static class HeaderMoneyBandLocator
{
    private const double YBandTolerance = 5.0;
    private const double PagoMaxX = 300.0;

    /// <summary>
    /// Mirrors <c>ExtractNivelDeUsoField</c>: label row is in the right column
    /// (X &gt;= 280); value search is OPEN-BOUNDS (no maxX) — the root cause of
    /// the SaldoCargosRegulares decoy-leak defect this spike measures.
    /// </summary>
    public static HeaderMoneyBandContext LocateSaldoCargosRegulares(string pdfPath) =>
        LocateNivelDeUsoBand(pdfPath, "Saldo", "cargos", "regulares:", maxX: double.MaxValue);

    /// <summary>
    /// Mirrors <c>ExtractPagoParaNoGenerarIntereses</c>: label row is in the
    /// left column (X &lt;= 200); value search is capped at maxX=300 to stay in
    /// the left column — the decoy specimens in this corpus were built to sit
    /// just inside that ceiling.
    /// </summary>
    public static HeaderMoneyBandContext LocatePagoParaNoGenerarIntereses(string pdfPath)
    {
        using var doc = PdfDocument.Open(pdfPath);
        var sorted = ReadSortedWords(doc);

        for (var i = 0; i + 4 < sorted.Count; i++)
        {
            if (!string.Equals(sorted[i].Text, "Pago", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(sorted[i + 1].Text, "para", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(sorted[i + 2].Text, "no", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(sorted[i + 3].Text, "generar", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(sorted[i + 4].Text, "intereses", StringComparison.OrdinalIgnoreCase)) continue;
            if (sorted[i].BoundingBox.Left > 200) continue;

            var bandY = sorted[i].BoundingBox.Bottom;
            var band = GetBand(sorted, bandY)
                .Where(w => w.BoundingBox.Left <= PagoMaxX)
                .OrderBy(w => w.BoundingBox.Left)
                .Select(w => new SpikeToken(w.Text, w.BoundingBox.Left, w.BoundingBox.Right))
                .ToList();

            return new HeaderMoneyBandContext(band, LabelTokenCount: 5);
        }

        throw new InvalidOperationException(
            $"{pdfPath}: 'Pago para no generar intereses' label not found in the left column — fixture malformed for this spike.");
    }

    private static HeaderMoneyBandContext LocateNivelDeUsoBand(
        string pdfPath, string label0, string label1, string label2, double maxX)
    {
        using var doc = PdfDocument.Open(pdfPath);
        var sorted = ReadSortedWords(doc);

        for (var i = 0; i < sorted.Count; i++)
        {
            if (sorted[i].BoundingBox.Left < 280) continue;
            if (!string.Equals(sorted[i].Text, label0, StringComparison.OrdinalIgnoreCase)) continue;

            var bandY = sorted[i].BoundingBox.Bottom;
            var bandSorted = GetBand(sorted, bandY);

            var labelStart = bandSorted.FindIndex(w =>
                string.Equals(w.Text, label0, StringComparison.OrdinalIgnoreCase) && w.BoundingBox.Left >= 280);
            if (labelStart < 0) continue;
            if (labelStart + 2 >= bandSorted.Count) continue;
            if (!string.Equals(bandSorted[labelStart + 1].Text, label1, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(bandSorted[labelStart + 2].Text, label2, StringComparison.OrdinalIgnoreCase)) continue;

            var band = bandSorted
                .Where(w => w.BoundingBox.Left <= maxX)
                .OrderBy(w => w.BoundingBox.Left)
                .Select(w => new SpikeToken(w.Text, w.BoundingBox.Left, w.BoundingBox.Right))
                .ToList();

            return new HeaderMoneyBandContext(band, LabelTokenCount: 3);
        }

        throw new InvalidOperationException(
            $"{pdfPath}: '{label0} {label1} {label2}' label not found in the right column — fixture malformed for this spike.");
    }

    private static List<Word> ReadSortedWords(PdfDocument doc) =>
        doc.GetPage(1).GetWords()
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();

    private static List<Word> GetBand(List<Word> sorted, double y) =>
        sorted.Where(w => Math.Abs(w.BoundingBox.Bottom - y) <= YBandTolerance)
              .OrderBy(w => w.BoundingBox.Left)
              .ToList();
}
