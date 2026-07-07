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
/// <item><description>
/// <b>E6.S6.2.3</b> — 4 defect variants of the S6.2.1 baseline layout: <c>s6211-math.pdf</c>
/// (printed-value math defect, extraction-fidelity unaffected), <c>s6211-font.pdf</c> (Courier
/// token injected for a later CL-35 check, extraction-fidelity unaffected), <c>s6211-scanned.pdf</c>
/// (image-only, no text layer — every field honestly <c>NotExtracted</c>, extractor still
/// returns success), and <c>s6211-abstain.pdf</c> (TASA/CAT block omitted — honest abstention).
/// Each manifest's optional <c>arithmeticChecks</c> is for a later verdict-level test, not
/// asserted here.
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

    /// <summary>
    /// E6.S6.2.3 defect variant — injected math defect (Pago printed +$11.00 above the
    /// 5-core-sum identity). Extraction-fidelity only: the defect lives in a printed field
    /// value, not in extractability, so every field still resolves <c>Extracted</c> exactly
    /// like the baseline. The manifest's <c>arithmeticChecks</c> (CL-21/CL-22 expected Fail)
    /// are for the later verdict-level test, not asserted here.
    /// </summary>
    [Fact]
    public async Task SyntheticGolden_S6211Math_AllManifestFieldsMatchExpectedOutcome()
    {
        var ct = TestContext.Current.CancellationToken;

        await AssertGoldenRoundTripAsync(
            Path.Combine(FixturesDir, "s6211-math.pdf"),
            Path.Combine(FixturesDir, "s6211-math.manifest.json"),
            ct);
    }

    /// <summary>
    /// E6.S6.2.3 defect variant — injected font defect (a Courier token in the header, for a
    /// later CL-35 typography check). Extraction-fidelity is unaffected: every baseline field
    /// still resolves <c>Extracted</c> with its unchanged value.
    /// </summary>
    [Fact]
    public async Task SyntheticGolden_S6211Font_AllManifestFieldsMatchExpectedOutcome()
    {
        var ct = TestContext.Current.CancellationToken;

        await AssertGoldenRoundTripAsync(
            Path.Combine(FixturesDir, "s6211-font.pdf"),
            Path.Combine(FixturesDir, "s6211-font.manifest.json"),
            ct);
    }

    /// <summary>
    /// E6.S6.2.3 defect variant — image-only (rasterized) PDF, no text layer. Confirms
    /// <see cref="PdfPigStatementFieldExtractor.ExtractFullAsync"/> still returns a
    /// <b>successful</b> <c>Result&lt;T&gt;</c> with every field honestly <c>NotExtracted</c>
    /// (the extractor itself does not gate on a text-layer floor — that is a pipeline/verdict
    /// concern, not an extractor concern).
    /// </summary>
    [Fact]
    public async Task SyntheticGolden_S6211Scanned_AllManifestFieldsMatchExpectedOutcome()
    {
        var ct = TestContext.Current.CancellationToken;

        await AssertGoldenRoundTripAsync(
            Path.Combine(FixturesDir, "s6211-scanned.pdf"),
            Path.Combine(FixturesDir, "s6211-scanned.manifest.json"),
            ct);
    }

    /// <summary>
    /// E6.S6.2.3 defect variant — TASA/CAT block deliberately omitted from the source PDF.
    /// Honest-abstention test: Tasa/Cat must resolve <c>NotExtracted</c> while every other
    /// baseline field still resolves <c>Extracted</c>.
    /// </summary>
    [Fact]
    public async Task SyntheticGolden_S6211Abstain_AllManifestFieldsMatchExpectedOutcome()
    {
        var ct = TestContext.Current.CancellationToken;

        await AssertGoldenRoundTripAsync(
            Path.Combine(FixturesDir, "s6211-abstain.pdf"),
            Path.Combine(FixturesDir, "s6211-abstain.manifest.json"),
            ct);
    }

    /// <summary>
    /// E6.S6.2.4 in-tolerance variance — three seeded personas re-emit the baseline
    /// layout with NOVEL arithmetic-consistent values (product/holder/amounts/percents)
    /// plus a rigid whole-page Y-shift inside <c>YBandTolerance</c> (5pt). Every field
    /// must STILL resolve <c>Extracted</c> to the manifest's (varied) value — proving the
    /// extractor generalizes over value + absolute-position variance and is not overfit to
    /// the one hardcoded s6211 token table. Values differ per fixture, so a memorized
    /// baseline string could not satisfy these.
    /// </summary>
    [Theory]
    [InlineData("a")]
    [InlineData("b")]
    [InlineData("c")]
    public async Task SyntheticGolden_S6211Variance_AllManifestFieldsMatchExpectedOutcome(string label)
    {
        var ct = TestContext.Current.CancellationToken;

        await AssertGoldenRoundTripAsync(
            Path.Combine(FixturesDir, $"s6211-var-{label}.pdf"),
            Path.Combine(FixturesDir, $"s6211-var-{label}.manifest.json"),
            ct);
    }

    /// <summary>
    /// E6.S6.2.4 tolerance-edge — the Adeudo amount token is displaced +7pt off its label
    /// band (beyond <c>YBandTolerance</c> 5pt). The label IS still matched but no amount
    /// parses in its band, so <c>AdeudoPeriodoAnterior</c> honestly resolves
    /// <c>ExtractedInvalidFormat</c> (implied-zero — the Epic-5 F1 matched-label /
    /// missing-amount distinction, NOT <c>NotExtracted</c>) while every other field stays
    /// <c>Extracted</c>. This edge and the label edge exercise the extractor's two distinct
    /// honest failure modes.
    /// </summary>
    [Fact]
    public async Task SyntheticGolden_S6211EdgeBand_AllManifestFieldsMatchExpectedOutcome()
    {
        var ct = TestContext.Current.CancellationToken;

        await AssertGoldenRoundTripAsync(
            Path.Combine(FixturesDir, "s6211-edge-band.pdf"),
            Path.Combine(FixturesDir, "s6211-edge-band.manifest.json"),
            ct);
    }

    /// <summary>
    /// E6.S6.2.4 tolerance-edge — the accent is dropped from the <c>Crédito</c> label token
    /// (<c>Crédito disponible:</c> → <c>Credito disponible:</c>). Positional labels are
    /// matched by exact <c>OrdinalIgnoreCase</c> equality (not accent-folded), so
    /// <c>CreditoDisponible</c> honestly resolves <c>NotExtracted</c> while every other field
    /// stays <c>Extracted</c> — mapping the exact-label-match edge.
    /// </summary>
    [Fact]
    public async Task SyntheticGolden_S6211EdgeLabel_AllManifestFieldsMatchExpectedOutcome()
    {
        var ct = TestContext.Current.CancellationToken;

        await AssertGoldenRoundTripAsync(
            Path.Combine(FixturesDir, "s6211-edge-label.pdf"),
            Path.Combine(FixturesDir, "s6211-edge-label.manifest.json"),
            ct);
    }

    /// <summary>
    /// E6.S6.2.5 — page 1 reuses the S6.2.1 baseline layout verbatim (unchanged canary: all
    /// 14 baseline fields still resolve <c>Extracted</c> to their baseline values), and page 2
    /// adds a populated DESGLOSE DE MOVIMIENTOS DEL PERIODO table (3 rows + a "Total cargos" and
    /// a "Total abonos" summary row). Closes the <c>TotalCargos</c>/<c>TotalAbonos</c> gap every
    /// prior synthetic manifest (S6.2.1/S6.2.2/S6.2.4) explicitly deferred as
    /// <c>NotExtracted</c> — both now resolve <c>Extracted</c>, and both totals tie to the exact
    /// same RESUMEN values the baseline's own CL-21/CL-22 arithmetic checks already assert
    /// (32,446.69 == PagoParaNoGenerarIntereses; 67,796.35 == PagosYAbonos).
    /// </summary>
    [Fact]
    public async Task SyntheticGolden_S6211Desglose_AllManifestFieldsMatchExpectedOutcome()
    {
        var ct = TestContext.Current.CancellationToken;

        await AssertGoldenRoundTripAsync(
            Path.Combine(FixturesDir, "s6211-desglose.pdf"),
            Path.Combine(FixturesDir, "s6211-desglose.manifest.json"),
            ct);
    }
}
