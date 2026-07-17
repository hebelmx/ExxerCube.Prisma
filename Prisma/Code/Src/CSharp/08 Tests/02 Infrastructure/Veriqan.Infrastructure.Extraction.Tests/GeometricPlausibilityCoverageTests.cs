using System.Text.Json;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Confidence;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Story <b>C1.5</b> — architecture-enforcement / drift-guard test for the C1 geometric-
/// plausibility lever (<see cref="GeometricPlausibilityScorer"/> / <see cref="FieldCalibrationTable"/>).
/// Two independent guards, both self-maintaining (no hand-updated fixture lists):
/// </summary>
/// <remarks>
/// <para>
/// <b>Guard #1</b> (<see cref="EveryScoredField_HasCalibrationTableEntry_AndAtLeastOneCalibrationSpecimen"/>):
/// every <see cref="FieldKind"/> the scorer actually scores today (Tasa/Cat plus whatever
/// <see cref="FieldCalibrationTable.Resumen"/> currently holds) must have a calibration-table
/// entry AND be exercised by at least one real corpus specimen. It walks
/// <c>FieldCalibrationTable.Resumen.Keys</c> and the on-disk corpus directly — a future dev who
/// adds a new scored <see cref="FieldKind"/> to the table without also adding a calibration
/// specimen (or a <c>confidenceExpectations</c> entry to an existing one) trips this test with
/// zero test-file edits required to detect the gap.
/// </para>
/// <para>
/// <b>Guard #2</b> (<see cref="CleanSpecimen_EveryScoredFieldThatExtracts_MeetsConfidenceGuardFloor"/>):
/// the false-abstain drift guard the C1 design doc's "cardinal risk" section demands — for every
/// CLEAN (non-adversarial) specimen in the standing corpus, every scored field that actually
/// extracts must score &gt;= 0.8 when the C1 flag is armed. It fails loudly if a future
/// signal/constant change in the scorer drags a legitimately-clean field below the 0.8 guard
/// floor. "Clean" is determined structurally, not by a hand-maintained id list: a specimen is
/// adversarial iff its manifest carries a <c>geometryDefect</c> block (the C1.0a/C1.4 contract —
/// today exactly <c>s-c1-swap</c>, <c>s-c1-swap-displaced</c>, <c>missing-order-marker</c>,
/// <c>decoy-percent</c>, <c>decoy-resumen-amount</c>, <c>decoy-resumen-amount-realbanamex</c>);
/// those 6 are SUPPOSED to score low and are covered instead by
/// <see cref="GeometricPlausibilityCalibrationTests"/> / <see cref="GeometricPlausibilityResumenCalibrationTests"/>.
/// </para>
/// <para>
/// No production code is touched by this story; the C1 flag stays DARK
/// (<c>PdfExtractionOptions.EmitGeometricConfidence</c> default <see langword="false"/>) — this
/// suite arms it locally, per-call, exactly like the C1.2/C1.4 calibration suites do.
/// </para>
/// </remarks>
public sealed class GeometricPlausibilityCoverageTests
{
    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "synthetic");

    private static PdfPigStatementFieldExtractor CreateExtractor(bool emitGeometricConfidence) =>
        new(
            XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>(),
            Microsoft.Extensions.Options.Options.Create(new PdfExtractionOptions()),
            new NullPasswordProvider(),
            timeProvider: null,
            enableCatalogImageHashing: false,
            emitGeometricConfidence: emitGeometricConfidence);

    /// <summary>
    /// Every <see cref="FieldKind"/> the C1 scorer actually scores today: Tasa/Cat
    /// (<c>ExtractTasaAndCat</c>, backed by the single non-FieldKind-keyed
    /// <see cref="FieldCalibrationTable.TasaCat"/> record) plus whatever
    /// <see cref="FieldCalibrationTable.Resumen"/> currently holds (<c>ExtractResumenField</c>
    /// looks a field's calibration up BY <see cref="FieldKind"/>, so walking <c>.Keys</c> here
    /// means a future entry added there grows this set automatically).
    /// </summary>
    private static IReadOnlyList<FieldKind> ScoredFieldKinds() =>
        new[] { FieldKind.Tasa, FieldKind.Cat }
            .Concat(FieldCalibrationTable.Resumen.Keys)
            .Distinct()
            .ToList();

    /// <summary>
    /// Explicit accessor from a scored <see cref="FieldKind"/> to the
    /// <see cref="ExtractedField{T}"/> on <see cref="PeriodSummary"/> that carries its C1
    /// geometric-plausibility confidence. A small, explicit, well-commented list (rather than
    /// reflection-by-name) — <see cref="FieldKind"/>'s own XML doc comments already declare a
    /// one-to-one mapping to these properties (see <c>FieldKind.cs</c>), so a mismatch here is
    /// caught by <see cref="EveryScoredField_HasCalibrationTableEntry_AndAtLeastOneCalibrationSpecimen"/>'s
    /// accessor-presence assertion, not silently ignored. Extend this map whenever
    /// <see cref="FieldCalibrationTable.Resumen"/> grows.
    /// </summary>
    private static readonly IReadOnlyDictionary<FieldKind, Func<PeriodSummary, ExtractedField<decimal>>> FieldAccessors =
        new Dictionary<FieldKind, Func<PeriodSummary, ExtractedField<decimal>>>
        {
            [FieldKind.Tasa] = ps => ps.Tasa,
            [FieldKind.Cat] = ps => ps.Cat,
            [FieldKind.AdeudoPeriodoAnterior] = ps => ps.AdeudoPeriodoAnterior,
            [FieldKind.CargosRegularesNoMeses] = ps => ps.CargosRegularesNoMeses,
            [FieldKind.CargosComprasAMesesCapital] = ps => ps.CargosComprasAMesesCapital,
            [FieldKind.MontoIntereses] = ps => ps.MontoIntereses,
            [FieldKind.MontoComisiones] = ps => ps.MontoComisiones,
            [FieldKind.IvaInteresesYComisiones] = ps => ps.IvaInteresesYComisiones,
            [FieldKind.PagosYAbonos] = ps => ps.PagosYAbonos,
        };

    // -----------------------------------------------------------------------
    // Guard #1 — every scored field <-> a FieldCalibrationTable entry <-> >=1 specimen
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EveryScoredField_HasCalibrationTableEntry_AndAtLeastOneCalibrationSpecimen()
    {
        var ct = TestContext.Current.CancellationToken;
        var scoredFields = ScoredFieldKinds();
        scoredFields.ShouldNotBeEmpty();

        // (a) FieldCalibrationTable entry + (b) a PeriodSummary accessor to reach it. Trivially
        // true for anything sourced FROM FieldCalibrationTable.Resumen.Keys itself, but the
        // assertions still document/enforce the contract so a future refactor that drops an
        // entry (e.g. splits TasaCat, or forgets to wire an accessor) fails loudly here rather
        // than silently under-covering Guard #2 below.
        foreach (var field in scoredFields)
        {
            if (field is FieldKind.Tasa or FieldKind.Cat)
            {
                FieldCalibrationTable.TasaCat.ShouldNotBeNull(
                    $"{field}: FieldCalibrationTable.TasaCat must exist to back this scored field.");
            }
            else
            {
                FieldCalibrationTable.Resumen.Keys.ShouldContain(field,
                    $"{field}: missing a FieldCalibrationTable.Resumen entry.");
            }

            FieldAccessors.Keys.ShouldContain(field,
                $"{field}: FieldCalibrationTable carries this FieldKind but "
                + $"{nameof(GeometricPlausibilityCoverageTests)}.{nameof(FieldAccessors)} has no "
                + "matching PeriodSummary accessor — add one so the C1.5 clean-field sweep "
                + "(Guard #2) can reach it.");
        }

        // (c) specimen coverage: every scored field must be exercised by >=1 real corpus
        // specimen's confidenceExpectations — loaded fresh from disk each run, never hardcoded,
        // so adding/removing a specimen from the corpus is picked up with zero test-file edits.
        var referencedFields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var specimen in SyntheticCorpusIndexLoader.Load(FixturesDir))
        {
            var manifestResult = await SyntheticGoldManifestLoader.LoadAsync(
                Path.Combine(FixturesDir, specimen.Manifest), ct);
            manifestResult.IsSuccess.ShouldBeTrue(
                $"{specimen.Id}: manifest must load. Error: {manifestResult.Error}");

            foreach (var fieldName in manifestResult.Value!.ConfidenceExpectations.Keys)
                referencedFields.Add(fieldName);
        }

        foreach (var field in scoredFields)
        {
            referencedFields.ShouldContain(field.ToString(),
                $"{field}: no calibration specimen declares a confidenceExpectations entry for "
                + "this field — a scored field with no specimen means the 0.8-guard behaviour "
                + "for it is unverified by the corpus. Add a specimen (or extend an existing "
                + "one's manifest) that declares a confidenceExpectations band for it.");
        }
    }

    // -----------------------------------------------------------------------
    // Guard #2 — every clean specimen's scored fields that extract must clear the 0.8 floor
    // -----------------------------------------------------------------------

    /// <summary>
    /// One theory case per NON-adversarial specimen in the standing corpus index (filtered at
    /// the <see cref="CleanSpecimens"/> MemberData source, not skipped inside the test body, so
    /// a new adversarial specimen is excluded automatically the moment its manifest gains a
    /// <c>geometryDefect</c> block — and a new clean specimen is picked up automatically too).
    /// </summary>
    [Theory]
    [MemberData(nameof(CleanSpecimens))]
    public async Task CleanSpecimen_EveryScoredFieldThatExtracts_MeetsConfidenceGuardFloor(
        string id, string pdfFileName, string manifestFileName)
    {
        var ct = TestContext.Current.CancellationToken;

        var manifestResult = await SyntheticGoldManifestLoader.LoadAsync(
            Path.Combine(FixturesDir, manifestFileName), ct);
        manifestResult.IsSuccess.ShouldBeTrue($"{id}: manifest must load. Error: {manifestResult.Error}");
        var manifest = manifestResult.Value!;

        var pdfPath = Path.Combine(FixturesDir, pdfFileName);
        File.Exists(pdfPath).ShouldBeTrue($"{id}: synthetic fixture not found: {pdfPath}");
        var pdfBytes = await File.ReadAllBytesAsync(pdfPath, ct);

        var extractor = CreateExtractor(emitGeometricConfidence: true);
        var result = await extractor.ExtractFullAsync(pdfBytes, ct);
        if (!result.IsSuccess || result.Value!.PeriodSummary is null)
        {
            // Extraction-level failure is a different drift guard's job (e.g.
            // SyntheticGoldenRoundTripTests) — nothing to score confidence on here.
            return;
        }

        var periodSummary = result.Value!.PeriodSummary!;
        var mismatches = new List<string>();

        foreach (var (field, accessor) in FieldAccessors)
        {
            // Respect an explicit "low" band even on a nominally clean (non-geometryDefect)
            // specimen — defensive: none exist in the corpus today (every "low" band belongs to
            // a geometryDefect-tagged specimen, already excluded by CleanSpecimens), but a
            // future specimen could legitimately declare one without also being adversarial.
            if (manifest.ConfidenceExpectations.TryGetValue(field.ToString(), out var expectation)
                && string.Equals(expectation.Band, "low", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var extracted = accessor(periodSummary);
            if (extracted.Status != ExtractionStatus.Extracted)
                continue; // nothing to score — a field that legitimately abstains on this fixture.

            if (extracted.Confidence < 0.8)
            {
                mismatches.Add(
                    $"{field}: confidence {extracted.Confidence} < 0.8 on a clean specimen "
                    + "(false-abstain drift — a scorer/constant change dragged a legitimately-"
                    + "clean field below the 0.8 guard floor)");
            }
        }

        mismatches.ShouldBeEmpty(
            $"{id}: {mismatches.Count} scored field(s) regressed below the 0.8 guard floor on a "
            + "clean specimen:\n" + string.Join("\n", mismatches));
    }

    /// <summary>
    /// MemberData source: every corpus specimen whose manifest lacks a <c>geometryDefect</c>
    /// block. xUnit v3 MemberData sources run synchronously at discovery time, so this does a
    /// minimal, self-contained JSON presence check rather than the full async
    /// <see cref="SyntheticGoldManifestLoader"/> parse.
    /// </summary>
    public static IEnumerable<object[]> CleanSpecimens() =>
        SyntheticCorpusIndexLoader.Load(FixturesDir)
            .Where(specimen => !HasGeometryDefect(Path.Combine(FixturesDir, specimen.Manifest)))
            .Select(specimen => new object[] { specimen.Id, specimen.Pdf, specimen.Manifest });

    private static bool HasGeometryDefect(string manifestPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        return doc.RootElement.TryGetProperty("geometryDefect", out var el)
            && el.ValueKind != JsonValueKind.Null;
    }
}
