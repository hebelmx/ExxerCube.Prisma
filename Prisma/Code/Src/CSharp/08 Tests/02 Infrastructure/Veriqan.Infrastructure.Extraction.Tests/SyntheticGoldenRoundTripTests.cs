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
/// Covers two layout profiles, both extraction-fidelity only (owner ruling 2026-07-06) — no
/// verdict-level assertion, no <c>arithmeticChecks</c>:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>E6.S6.2.1</b> — <c>s6211-baseline.pdf</c>, cloning the proven "Dummie-VEC" layout
/// (540x780, right-column). See
/// <c>docs/planning-artifacts/E6-S6.2-synthetic-generator-design.md</c>.
/// </description></item>
/// <item><description>
/// <b>E6.S6.2.2</b> — <c>s622-realbanamex-baseline.pdf</c>, cloning the real Banamex
/// <c>good.pdf</c> layout (612x792, left-column fallback path: <c>ExtractResumenField</c>'s
/// pass-2 left-column scan, <c>ExtractNivelDeUsoField</c>'s hard right-column gate,
/// <c>ExtractTasaAndCat</c>'s label-anchored scan supplying the TASA/CAT real good.pdf lacks).
/// </description></item>
/// </list>
/// <para>
/// Both this slice's fields double as a regression canary for TASA/CAT/RESUMEN/NIVEL-DE-USO
/// positional resolution — the exact fields the E2.3 real-corpus investigation proved fragile.
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

    [Fact]
    public async Task SyntheticGolden_S6211Baseline_AllManifestFieldsMatchExpectedOutcome()
    {
        var ct = TestContext.Current.CancellationToken;

        await AssertGoldenRoundTripAsync(
            Path.Combine(FixturesDir, "s6211-baseline.pdf"),
            Path.Combine(FixturesDir, "s6211-baseline.manifest.json"),
            ct);
    }

    [Fact]
    public async Task SyntheticGolden_S622RealBanamexBaseline_AllManifestFieldsMatchExpectedOutcome()
    {
        var ct = TestContext.Current.CancellationToken;

        await AssertGoldenRoundTripAsync(
            Path.Combine(FixturesDir, "s622-realbanamex-baseline.pdf"),
            Path.Combine(FixturesDir, "s622-realbanamex-baseline.manifest.json"),
            ct);
    }
}
