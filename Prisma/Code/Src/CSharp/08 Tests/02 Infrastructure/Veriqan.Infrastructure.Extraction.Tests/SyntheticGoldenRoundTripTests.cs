using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Golden round-trip tests: drive the REAL, unmodified <see cref="PdfPigStatementFieldExtractor"/>
/// over reproducibly code-generated synthetic PDFs (<c>scripts/veriqan-corpus/synth_gen.py</c>) and
/// assert every field named in the sibling god's-eye manifest against its expected (status, value).
/// </summary>
/// <remarks>
/// <para>
/// <b>E6.S6.2.6</b> — index-driven: every specimen listed in the standing corpus index
/// (<c>Prisma/Fixtures/PRP2/synthetic/corpus-manifest.json</c>) is round-tripped by a single
/// <see cref="SyntheticGolden_Specimen_AllManifestFieldsMatchExpectedOutcome"/> theory case, and
/// <see cref="CorpusIndex_AgreesWithFixturesDirectory_NoOrphansEitherDirection"/> proves the
/// index and the on-disk corpus stay in sync. Adding a fixture to the standing corpus is an
/// index-row addition (<c>synth_gen.py --write-index</c>), not a new test method.
/// </para>
/// <para>
/// The 12 indexed specimens (see the design doc for full provenance) span: <b>E6.S6.2.1</b>
/// baseline (Dummie-VEC 540x780 right-column); <b>E6.S6.2.2</b> real-Banamex baseline
/// (612x792, left-column fallback path — <c>ExtractResumenField</c>'s pass-2 left-column scan,
/// <c>ExtractNivelDeUsoField</c>'s hard right-column gate, <c>ExtractTasaAndCat</c>'s
/// label-anchored scan); <b>E6.S6.2.3</b> defect variants (math/font/scanned/abstain — printed
/// or availability defects that leave extraction-fidelity unaffected or trigger honest
/// abstention; each manifest's optional <c>arithmeticChecks</c> is for a later verdict-level
/// test, not asserted here); <b>E6.S6.2.4</b> in-tolerance value/position variance (personas
/// a/b/c) plus two tolerance-edge cases (displaced amount band → <c>ExtractedInvalidFormat</c>,
/// accent-dropped label → <c>NotExtracted</c>); and <b>E6.S6.2.5</b> a populated DESGLOSE
/// movements table closing the <c>TotalCargos</c>/<c>TotalAbonos</c> gap.
/// </para>
/// <para>
/// These fields double as a regression canary for TASA/CAT/RESUMEN/NIVEL-DE-USO positional
/// resolution — the exact fields the E2.3 real-corpus investigation proved fragile.
/// </para>
/// <para>
/// The manifest is the single source of truth for expected values; these tests never hand-copy
/// a number — see <see cref="SyntheticGoldManifestLoader"/> and
/// <see cref="StatementModelFieldAccessors"/>.
/// </para>
/// </remarks>
public sealed class SyntheticGoldenRoundTripTests
{
    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "synthetic");

    private static PdfPigStatementFieldExtractor CreateExtractor() =>
        new(
            XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>(),
            Microsoft.Extensions.Options.Options.Create(new PdfExtractionOptions()),
            new NullPasswordProvider());

    /// <summary>
    /// Loads the manifest at <paramref name="manifestPath"/>, runs the real extractor over
    /// <paramref name="pdfPath"/>, and asserts every manifest-declared field matches its
    /// expected (status, value). Shared by every synthetic-fixture golden test so each profile
    /// is a one-line <c>[Fact]</c> addition, not a new test method body.
    /// </summary>
    private static async Task AssertGoldenRoundTripAsync(
        string pdfPath,
        string manifestPath,
        CancellationToken ct)
    {
        File.Exists(pdfPath).ShouldBeTrue($"Synthetic fixture not found: {pdfPath}");
        File.Exists(manifestPath).ShouldBeTrue($"Synthetic manifest not found: {manifestPath}");

        var manifestResult = await SyntheticGoldManifestLoader.LoadAsync(manifestPath, ct);
        manifestResult.IsSuccess.ShouldBeTrue($"Manifest must load. Error: {manifestResult.Error}");
        var manifest = manifestResult.Value!;
        manifest.Fields.Count.ShouldBeGreaterThan(0, "Manifest must declare at least one field expectation");

        var pdfBytes = await File.ReadAllBytesAsync(pdfPath, ct);
        var extractor = CreateExtractor();
        var extractResult = await extractor.ExtractFullAsync(pdfBytes, ct);
        extractResult.IsSuccess.ShouldBeTrue($"ExtractFullAsync must succeed. Error: {extractResult.Error}");
        var model = extractResult.Value!;

        var mismatches = new List<string>();

        foreach (var (fieldName, expectation) in manifest.Fields.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!StatementModelFieldAccessors.Map.TryGetValue(fieldName, out var accessor))
            {
                mismatches.Add($"{fieldName}: no accessor registered in StatementModelFieldAccessors.Map");
                continue;
            }

            if (!Enum.TryParse<ExtractionStatus>(expectation.ExpectedStatus, out var expectedStatus))
            {
                mismatches.Add($"{fieldName}: manifest expectedStatus '{expectation.ExpectedStatus}' is not a valid ExtractionStatus");
                continue;
            }

            var (actualValue, actualStatus) = accessor(model);

            if (actualStatus != expectedStatus)
            {
                mismatches.Add(
                    $"{fieldName}: status expected {expectedStatus} but was {actualStatus} (actual value={actualValue ?? "<null>"})");
                continue;
            }

            if (expectedStatus != ExtractionStatus.Extracted)
            {
                // Legitimate abstention (e.g. TotalCargos) — status match is sufficient,
                // there is no expected value to compare.
                continue;
            }

            var expectedValue = expectation.ClrType switch
            {
                "decimal" => (object?)expectation.GetValue<decimal>(),
                "string" => expectation.GetValue<string>(),
                "int" => (object?)expectation.GetValue<int>(),
                "DateOnly" => (object?)expectation.GetValue<DateOnly>(),
                _ => throw new NotSupportedException(
                    $"Unsupported manifest clrType '{expectation.ClrType}' for field '{fieldName}'"),
            };

            if (!Equals(actualValue, expectedValue))
            {
                mismatches.Add($"{fieldName}: value expected {expectedValue} but was {actualValue}");
            }
        }

        mismatches.ShouldBeEmpty(
            $"Synthetic golden round-trip regressed on {mismatches.Count} field(s):\n" +
            string.Join("\n", mismatches));
    }

    /// <summary>
    /// One theory case per specimen listed in <c>corpus-manifest.json</c> — currently 12
    /// (S6.2.1 baseline, S6.2.2 real-Banamex baseline, S6.2.3 math/font/scanned/abstain
    /// defects, S6.2.4 variance a/b/c + edge-band/edge-label, S6.2.5 desglose). Adding a
    /// fixture to the standing corpus means adding an index row, not a new test method; the
    /// case name embeds the specimen <c>id</c> so a failure names the culprit directly.
    /// </summary>
    [Theory]
    [MemberData(nameof(CorpusSpecimens))]
    public async Task SyntheticGolden_Specimen_AllManifestFieldsMatchExpectedOutcome(
        string id, string pdfFileName, string manifestFileName)
    {
        _ = id; // surfaced via the theory display name only; assertions key off the file names.
        var ct = TestContext.Current.CancellationToken;

        await AssertGoldenRoundTripAsync(
            Path.Combine(FixturesDir, pdfFileName),
            Path.Combine(FixturesDir, manifestFileName),
            ct);
    }

    public static IEnumerable<object[]> CorpusSpecimens() =>
        SyntheticCorpusIndexLoader.Load(FixturesDir)
            .Select(specimen => new object[] { specimen.Id, specimen.Pdf, specimen.Manifest });

    /// <summary>
    /// Corpus-completeness gate (E6.S6.2.6): proves <c>corpus-manifest.json</c> and the
    /// on-disk <c>Fixtures\synthetic</c> contents agree in both directions — every indexed
    /// pdf/manifest file exists on disk, and every on-disk <c>*.pdf</c>/<c>*.manifest.json</c>
    /// file has a corresponding index row. Catches a fixture committed without an index entry
    /// (silently untested) and a stale index row pointing at a deleted fixture, either of
    /// which would let the standing corpus silently drift out of sync with the theory above.
    /// </summary>
    [Fact]
    public void CorpusIndex_AgreesWithFixturesDirectory_NoOrphansEitherDirection()
    {
        var specimens = SyntheticCorpusIndexLoader.Load(FixturesDir);

        var problems = new List<string>();

        foreach (var specimen in specimens)
        {
            var pdfPath = Path.Combine(FixturesDir, specimen.Pdf);
            if (!File.Exists(pdfPath))
                problems.Add($"specimen '{specimen.Id}': indexed pdf not found on disk: {specimen.Pdf}");

            var manifestPath = Path.Combine(FixturesDir, specimen.Manifest);
            if (!File.Exists(manifestPath))
                problems.Add($"specimen '{specimen.Id}': indexed manifest not found on disk: {specimen.Manifest}");
        }

        var indexedPdfs = specimens.Select(s => s.Pdf).ToHashSet(StringComparer.Ordinal);
        var indexedManifests = specimens.Select(s => s.Manifest).ToHashSet(StringComparer.Ordinal);

        foreach (var filePath in Directory.EnumerateFiles(FixturesDir))
        {
            var fileName = Path.GetFileName(filePath);
            if (fileName == "corpus-manifest.json")
                continue;

            if (fileName.EndsWith(".manifest.json", StringComparison.Ordinal))
            {
                if (!indexedManifests.Contains(fileName))
                    problems.Add($"orphan manifest on disk (no index row): {fileName}");
            }
            else if (fileName.EndsWith(".pdf", StringComparison.Ordinal))
            {
                if (!indexedPdfs.Contains(fileName))
                    problems.Add($"orphan fixture on disk (no index row): {fileName}");
            }
        }

        problems.ShouldBeEmpty(
            $"corpus-manifest.json and Fixtures\\synthetic disagree on {problems.Count} item(s):\n" +
            string.Join("\n", problems));
    }
}
