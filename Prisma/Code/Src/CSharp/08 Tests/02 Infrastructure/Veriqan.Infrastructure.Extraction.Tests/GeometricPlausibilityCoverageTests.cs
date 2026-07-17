using System.Text.Json;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Confidence;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Story <b>C1.5</b> (extended by <b>C2.5</b>) — architecture-enforcement / drift-guard test for the
/// geometric-plausibility lever (<see cref="GeometricPlausibilityScorer"/> /
/// <see cref="FieldCalibrationTable"/>). Three independent guards, all self-maintaining (no
/// hand-updated fixture lists):
/// <list type="number">
/// <item>Guard #1 — every field the scorer scores has a calibration-table entry, a PeriodSummary
/// accessor, and &gt;=1 corpus specimen.</item>
/// <item>Guard #2 — every scored field that extracts on a CLEAN specimen clears the 0.8 guard floor
/// (false-abstain drift).</item>
/// <item>Guard #3 (C2.5) — the four recompute operands proven VACUOUS/UNSAFE to score on the real
/// demo (<see cref="DeliberatelyUnscoredRecomputeOperands"/>) must STAY out of every calibration
/// table, so a future dev can't silently re-introduce the C1 Tasa/Cat trap.</item>
/// </list>
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
/// adversarial iff its manifest carries a <c>geometryDefect</c> block (the C1.0a/C1.4/C1.6/C2.1b
/// contract — today exactly <c>s-c1-swap</c>, <c>s-c1-swap-displaced</c>,
/// <c>missing-order-marker</c>, <c>decoy-percent</c>, <c>decoy-resumen-amount</c>,
/// <c>decoy-resumen-amount-realbanamex</c>, <c>decoy-total-amount</c>,
/// <c>decoy-total-amount-realbanamex</c>, <c>decoy-nivel-uso-amount</c> (+ <c>-realbanamex</c>,
/// <c>-moneyfmt</c>, <c>-moneyfmt-realbanamex</c>), <c>decoy-pago-sin-intereses-amount</c> (+
/// <c>-realbanamex</c>); those are SUPPOSED to score low and are covered instead by
/// <see cref="GeometricPlausibilityCalibrationTests"/> /
/// <see cref="GeometricPlausibilityResumenCalibrationTests"/> /
/// <c>GeometricPlausibilityTotalRowCalibrationTests</c> /
/// <c>GeometricPlausibilityHeaderMoneyCalibrationTests</c>.
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
    /// means a future entry added there grows this set automatically) plus whatever
    /// <see cref="FieldCalibrationTable.TotalRow"/> currently holds (C1.6 — <c>TryParseTotalRow</c>
    /// looks a field's calibration up the same way).
    /// </summary>
    private static IReadOnlyList<FieldKind> ScoredFieldKinds() =>
        new[] { FieldKind.Tasa, FieldKind.Cat }
            .Concat(FieldCalibrationTable.Resumen.Keys)
            .Concat(FieldCalibrationTable.TotalRow.Keys)
            .Concat(FieldCalibrationTable.HeaderMoney.Keys)
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
            [FieldKind.TotalCargos] = ps => ps.TotalCargos,
            [FieldKind.TotalAbonos] = ps => ps.TotalAbonos,
            [FieldKind.SaldoCargosRegulares] = ps => ps.SaldoCargosRegulares,
            [FieldKind.PagoParaNoGenerarIntereses] = ps => ps.PagoParaNoGenerarIntereses,
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
            else if (field is FieldKind.TotalCargos or FieldKind.TotalAbonos)
            {
                FieldCalibrationTable.TotalRow.Keys.ShouldContain(field,
                    $"{field}: missing a FieldCalibrationTable.TotalRow entry.");
            }
            else if (field is FieldKind.SaldoCargosRegulares or FieldKind.PagoParaNoGenerarIntereses)
            {
                FieldCalibrationTable.HeaderMoney.Keys.ShouldContain(field,
                    $"{field}: missing a FieldCalibrationTable.HeaderMoney entry.");
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

    // -----------------------------------------------------------------------
    // Guard #3 (C2.5) — the deliberately-unscored recompute operands must STAY unscored
    // -----------------------------------------------------------------------

    /// <summary>
    /// The four confidence-guarded recompute-operand money fields that were investigated for a
    /// geometric-confidence slice (C2) and deliberately LEFT UNSCORED — the inverse of Guard #1's
    /// "every scored field is covered" contract. Each is confidence-guarded by a rule (so today it
    /// asserts confidence 1.0), but arming it would be vacuous or actively unsafe on the real demo
    /// <c>good.pdf</c>, per the C2.3 non-vacuity + false-RED scouts (2026-07-17). If a future dev
    /// adds any of these to a <see cref="FieldCalibrationTable"/> dictionary, Guard #3 trips and
    /// points them back at that analysis — silently arming one re-introduces exactly the C1
    /// Tasa/Cat "vacuous / confident-wrong" trap this epic exists to refuse.
    /// <list type="bullet">
    /// <item><b>SaldoDeudorTotal</b> — a DERIVED total the document never prints in its text layer
    /// (correct value = SaldoCargosRegulares + SaldoCargosAMeses, absent as a token). Force-
    /// extracting it grabs a foreign page-1 amount → false Critical RED on the compliant reference
    /// via CL-24/CL-25. Same hazard class as the Tasa/CAT code strip.</item>
    /// <item><b>CreditoDisponible</b> — DOES extract (26,791.00) but its only live consumer CL-25
    /// already abstains on SaldoDeudorTotal's <c>NotExtracted</c> status BEFORE the 0.8 guard runs;
    /// its other consumer CL-26 is a dead <c>InsufficientData</c> stub → arming cannot flip any real
    /// verdict (vacuous).</item>
    /// <item><b>SaldoCargosAMeses</b> — DOES extract (38,604.69) and already runs the HeaderMoney
    /// scorer path (<c>ExtractNivelDeUsoField</c>), but is intentionally kept out of
    /// <c>FieldCalibrationTable.HeaderMoney</c> via a TryGetValue-miss: its only live consumer CL-24
    /// abstains on SaldoDeudorTotal; CL-23 is a dead stub → currently vacuous. (Scoreable in
    /// principle if SaldoDeudorTotal ever becomes safely extractable — which the scout showed it is
    /// not on this bank.)</item>
    /// <item><b>PagoMinimo</b> — its only guard consumer §6 gates on <c>Tasa</c>, which is
    /// <c>NotExtracted</c> on <c>good.pdf</c>, so §6 abstains unconditionally → vacuous on the real
    /// demo (C2 mini-party).</item>
    /// </list>
    /// </summary>
    private static readonly IReadOnlyList<FieldKind> DeliberatelyUnscoredRecomputeOperands =
        new[]
        {
            FieldKind.SaldoDeudorTotal,
            FieldKind.CreditoDisponible,
            FieldKind.SaldoCargosAMeses,
            FieldKind.PagoMinimo,
        };

    [Fact]
    public void DeliberatelyUnscoredRecomputeOperand_StaysOutOfEveryCalibrationTable()
    {
        var armed = new List<string>();

        foreach (var field in DeliberatelyUnscoredRecomputeOperands)
        {
            if (FieldCalibrationTable.Resumen.ContainsKey(field))
                armed.Add($"{field}: found in FieldCalibrationTable.Resumen");
            if (FieldCalibrationTable.TotalRow.ContainsKey(field))
                armed.Add($"{field}: found in FieldCalibrationTable.TotalRow");
            if (FieldCalibrationTable.HeaderMoney.ContainsKey(field))
                armed.Add($"{field}: found in FieldCalibrationTable.HeaderMoney");
        }

        armed.ShouldBeEmpty(
            "C2.5 Guard #3: a recompute operand that the C2.3 non-vacuity / false-RED scouts "
            + "(2026-07-17) proved is VACUOUS or UNSAFE to score on the real demo good.pdf has "
            + "been added to a FieldCalibrationTable. Arming it re-introduces the C1 Tasa/Cat "
            + "vacuous/confident-wrong trap this epic refuses. Before scoring any of these, redo "
            + "the per-field non-vacuity scout (does it extract on good.pdf, and does its guard "
            + "actually gate a real verdict?) and a false-RED check — see the "
            + "GeometricPlausibilityCoverageTests.DeliberatelyUnscoredRecomputeOperands remarks "
            + "and TRACKER-veriqan-c2-recompute-operand-confidence.md (C2.3/C2.4). Offenders:\n"
            + string.Join("\n", armed));
    }
}
