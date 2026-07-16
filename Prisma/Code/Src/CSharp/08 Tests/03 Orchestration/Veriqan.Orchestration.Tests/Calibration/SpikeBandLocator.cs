using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Calibration;

// ---------------------------------------------------------------------------
// C1.0b — separation-spike band locator. Reproduces ExtractTasaAndCat's
// label-anchored value-band search (PdfPigStatementFieldExtractor.cs
// ~1208-1293) over REAL PdfPig tokenization, so the spike's scores are
// measured against the same tokens the production extractor actually sees —
// not author-invented BoundingBox fixtures. Deliberately duplicated rather
// than calling into the extractor: the spike must not depend on, or risk
// perturbing, production internals (constraint from the C1.0b task brief).
// ---------------------------------------------------------------------------

/// <summary>
/// Locates the TASA/CAT percent-value band in a real PDF and returns it as
/// PdfPig-free <see cref="SpikeToken"/>s for <see cref="GeometricPlausibilityScorerPrototype"/>.
/// </summary>
internal static class SpikeBandLocator
{
    private const double YBandTolerance = 5.0;
    private static readonly Regex PercentPattern = new(@"^\d+(\.\d+)?%$", RegexOptions.Compiled);

    /// <summary>
    /// Opens <paramref name="pdfPath"/>, finds the CAT/TASA label row (Bottom &lt; 350, mirrors
    /// the production extractor's lower-section restriction), and returns the first percent-
    /// bearing band within 60pt below it — the exact band <c>ExtractTasaAndCat</c> would select.
    /// Throws if no such band exists (a spike test asserting on a malformed fixture should fail
    /// loudly, not silently score 0).
    /// </summary>
    public static IReadOnlyList<SpikeToken> LocateTasaCatValueBand(string pdfPath)
    {
        using var doc = PdfDocument.Open(pdfPath);
        var page1 = doc.GetPage(1);
        var words = page1.GetWords().ToList();

        double? tasaLabelY = null;
        double? catLabelY = null;

        foreach (var w in words)
        {
            if (w.BoundingBox.Bottom > 350)
                continue;

            if (string.Equals(w.Text, "TASA", StringComparison.OrdinalIgnoreCase) && tasaLabelY is null)
                tasaLabelY = w.BoundingBox.Bottom;

            if (string.Equals(w.Text, "CAT", StringComparison.OrdinalIgnoreCase) && catLabelY is null)
                catLabelY = w.BoundingBox.Bottom;

            if (tasaLabelY.HasValue && catLabelY.HasValue)
                break;
        }

        if (tasaLabelY is null && catLabelY is null)
            throw new InvalidOperationException($"{pdfPath}: no TASA/CAT label found — fixture malformed for this spike.");

        var refY = (tasaLabelY ?? catLabelY)!.Value;

        var bands = GroupIntoBands(words, YBandTolerance);

        var percentBands = bands
            .Where(kv => kv.Key < refY && kv.Key > refY - 60)
            .OrderByDescending(kv => kv.Key)
            .ToList();

        foreach (var (_, bandWords) in percentBands)
        {
            if (bandWords.Any(w => PercentPattern.IsMatch(w.Text)))
            {
                return bandWords
                    .Select(w => new SpikeToken(w.Text, w.BoundingBox.Left, w.BoundingBox.Right))
                    .ToList();
            }
        }

        throw new InvalidOperationException($"{pdfPath}: no percent-bearing band found within 60pt below the TASA/CAT label.");
    }

    private static Dictionary<double, List<UglyToad.PdfPig.Content.Word>> GroupIntoBands(
        IReadOnlyList<UglyToad.PdfPig.Content.Word> words,
        double tolerance)
    {
        var bands = new Dictionary<double, List<UglyToad.PdfPig.Content.Word>>();

        foreach (var w in words)
        {
            var y = w.BoundingBox.Bottom;
            var key = bands.Keys.FirstOrDefault(k => Math.Abs(k - y) <= tolerance, double.NaN);

            if (double.IsNaN(key))
            {
                bands[y] = [w];
            }
            else
            {
                bands[key].Add(w);
            }
        }

        return bands;
    }
}
