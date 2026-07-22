using System.Security.Cryptography;
using System.Text;
using UglyToad.PdfPig;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Calibration.RealCorpus;

/// <summary>
/// RC1.S1 — corpus gate: for every entry in the out-of-repo real-corpus index, extract the full
/// PdfPig text layer and assert that NONE of the real PII tokens from the anonymization map
/// appear, and that the file's sha256 still matches the index (integrity — nobody re-anonymized
/// or swapped the staged file since the index was built).
/// </summary>
/// <remarks>
/// <para>
/// <b>Graceful skip (CI has no corpus):</b> when <see cref="RealCorpusFixtureLocator.TryLoad"/>
/// returns <see langword="null"/> (root/index absent) or the anonymization map file is missing,
/// <see cref="CorpusEntries"/> yields a single sentinel row (<see langword="null"/> entry) so the
/// <see cref="Theory"/> always has at least one case — a zero-row <c>[Theory]</c> silently passing
/// vacuously is a known trap in this repo (see the synthetic-corpus S6.2.6 lesson). The theory body
/// turns that sentinel into an explicit <c>Assert.Skip</c>, which is visible in test output as
/// Skipped rather than merely absent. This class is ALSO marked
/// <c>[Trait("Category", "RealCorpus")]</c> so CI can additionally exclude it up front via
/// <c>-- --filter-not-trait "Category=RealCorpus"</c>, mirroring the existing
/// <c>Category=LiveOcr</c> pattern — the in-body skip is defense-in-depth for anyone who runs the
/// suite without that filter.
/// </para>
/// <para>
/// <b>scanned.pdf</b> is rasterized by design (no text layer) — <c>PdfPig</c>'s per-page
/// <c>Text</c> is empty for it, so the token scan passes trivially (nothing to leak from an image
/// page). This is NOT an OCR test; it is intentionally not OCR'd here.
/// </para>
/// <para>
/// If this gate ever finds a real token in a staged PDF, that is a blocking anonymization defect —
/// do NOT weaken this test to make it pass. Fix the anonymization script/corpus and re-stage.
/// </para>
/// </remarks>
[Trait("Category", "RealCorpus")]
public sealed class RealCorpusPiiLeakGateTests
{
    /// <summary>
    /// MemberData source. Never throws: absence of the corpus is expressed as a single
    /// sentinel <see langword="null"/> row, not an exception and not zero rows.
    /// </summary>
    public static IEnumerable<object?[]> CorpusEntries()
    {
        var fixture = RealCorpusFixtureLocator.TryLoad();
        if (fixture is null || !File.Exists(fixture.MapPath))
        {
            yield return new object?[] { null };
            yield break;
        }

        foreach (var entry in fixture.Entries)
            yield return new object?[] { entry };
    }

    [Theory]
    [MemberData(nameof(CorpusEntries))]
    public async Task RealCorpus_Specimen_NoPiiTokenLeak_AndIntegrityMatches(RealCorpusEntry? entry)
    {
        var ct = TestContext.Current.CancellationToken;

        var fixture = RealCorpusFixtureLocator.TryLoad();
        if (fixture is null || entry is null || !File.Exists(fixture.MapPath))
        {
            Assert.Skip(
                $"Real corpus not staged locally — set {RealCorpusFixtureLocator.RootEnvVar} to the " +
                "staging root and ensure corpus-index.json + the anonymization map exist. " +
                "Expected in CI (no corpus shipped).");
            return;
        }

        var pdfPath = Path.Combine(fixture.RootDir, entry.RelativePath);
        File.Exists(pdfPath).ShouldBeTrue($"Indexed file not found on disk: {entry.RelativePath}");

        var pdfBytes = await File.ReadAllBytesAsync(pdfPath, ct);

        // Integrity: the staged file must still match the sha256 recorded in the index.
        var actualSha256 = Convert.ToHexStringLower(SHA256.HashData(pdfBytes));
        actualSha256.ShouldBe(
            entry.Sha256.ToLowerInvariant(),
            $"sha256 mismatch for '{entry.Id}' ({entry.RelativePath}) — staged file changed since " +
            "corpus-index.json was built. Re-run build_corpus_index.py.");

        // PII scan: none of the real tokens from the anonymization map may appear anywhere in the
        // full extracted text layer.
        var realTokens = RealCorpusPiiTokenLoader.LoadAllRealTokens(fixture.MapPath);
        realTokens.Count.ShouldBeGreaterThan(0,
            $"Anonymization map at '{fixture.MapPath}' produced zero real tokens — loader or map " +
            "is broken; this would make the PII gate vacuously pass.");

        var fullText = ExtractFullText(pdfBytes);

        var leaked = realTokens
            .Where(token => fullText.Contains(token, StringComparison.Ordinal))
            .ToList();

        leaked.ShouldBeEmpty(
            $"PII LEAK in '{entry.Id}' ({entry.RelativePath}): real token(s) found in the text " +
            $"layer: [{string.Join(", ", leaked)}]. This is a blocking anonymization defect.");
    }

    private static string ExtractFullText(byte[] pdfBytes)
    {
        // scanned.pdf has no text layer by design (rasterized) — PdfDocument.Open still opens
        // fine; each page's Text is simply empty, so the leak scan below passes trivially. We do
        // NOT OCR here — that is an explicit non-goal of this gate.
        using var document = PdfDocument.Open(pdfBytes);

        var sb = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            sb.Append(page.Text);
            sb.Append('\n');
        }

        return sb.ToString();
    }
}
