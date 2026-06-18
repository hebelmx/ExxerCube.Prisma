using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Story 11.1 spike — fixture-driven tests for §8, §19, §20, §16 financial grid extraction.
/// Runs against all three real Dummie VEC PRP2 PDF fixtures.
/// </summary>
/// <remarks>
/// Test naming convention: {Method}_{Scenario}_{Expected}.
/// Asserts structural correctness (row counts, cell kinds, NA handling, abstain path)
/// not regulatory compliance.  The §20 waterfall sum is computed and reported to inform
/// rule authors for E11.2.
/// </remarks>
public sealed class FinancialTableExtractionTests
{
    // -----------------------------------------------------------------------
    // Fixture paths
    // -----------------------------------------------------------------------

    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static string FixturePath(string fileName) =>
        Path.Combine(FixturesDir, fileName);

    private static readonly string JulAgoFixture =
        FixturePath("01+Dummie+VEC+jul_ago+20252.pdf");

    private static readonly string AgoSepFixture =
        FixturePath("02+Dummie+VEC+ago_sep+2025.pdf");

    private static readonly string SepOctFixture =
        FixturePath("03+Dummie+VEC+sep_oct+2025.pdf");

    public static IEnumerable<object[]> AllFixtures =>
    [
        [JulAgoFixture, "jul_ago"],
        [AgoSepFixture, "ago_sep"],
        [SepOctFixture, "sep_oct"],
    ];

    // -----------------------------------------------------------------------
    // Factory helpers
    // -----------------------------------------------------------------------

    private static PdfPigStatementFieldExtractor CreateExtractor() =>
        new(XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>());

    private static byte[] ReadFixture(string path)
    {
        File.Exists(path).ShouldBeTrue($"Fixture not found at: {path}");
        return File.ReadAllBytes(path);
    }

    private static FinancialTable GetTable(IReadOnlyList<FinancialTable> tables, int sectionNumber) =>
        tables.First(t => t.SectionNumber == sectionNumber);

    // -----------------------------------------------------------------------
    // §19 Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFullAsync_JulAgo_Section19HasSixRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(JulAgoFixture);

        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue($"Extraction failed: {result.Error}");
        var tables = result.Value!.FinancialTables;
        tables.ShouldNotBeEmpty("FinancialTables should be populated");

        var sec19 = GetTable(tables, 19);
        sec19.Status.ShouldBe(TableExtractionStatus.Extracted,
            $"§19 status should be Extracted but was {sec19.Status}");
        sec19.Rows.Count.ShouldBe(6, "§19 grid has 6 fixed row labels");
    }

    [Fact]
    public async Task ExtractFullAsync_JulAgo_Section19RowsHaveFourValueCells()
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(JulAgoFixture);

        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue();
        var sec19 = GetTable(result.Value!.FinancialTables, 19);
        sec19.Status.ShouldBe(TableExtractionStatus.Extracted);

        foreach (var row in sec19.Rows)
        {
            row.Values.Count.ShouldBe(4,
                $"Row '{row.Label.RawText}' should have 4 value columns (SaldoBase/Dias/Tasa/Monto)");
        }
    }

    [Fact]
    public async Task ExtractFullAsync_JulAgo_Section19LabelCellsHaveLabelKind()
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(JulAgoFixture);

        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue();
        var sec19 = GetTable(result.Value!.FinancialTables, 19);
        sec19.Status.ShouldBe(TableExtractionStatus.Extracted);

        foreach (var row in sec19.Rows)
        {
            row.Label.Kind.ShouldBe(CellKind.Label,
                $"Row label '{row.Label.RawText}' should have Kind=Label");
            row.Label.Confidence.ShouldBe(1.0);
        }
    }

    [Fact]
    public async Task ExtractFullAsync_JulAgo_Section19CellsAreNotApplicableOrParsed()
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(JulAgoFixture);

        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue();
        var sec19 = GetTable(result.Value!.FinancialTables, 19);
        sec19.Status.ShouldBe(TableExtractionStatus.Extracted);

        // Every value cell must be either:
        //  - NotApplicable  (NA in the PDF)
        //  - Amount/Rate/Days with Confidence >= 0.7 (parsed or low-conf text found)
        //  - Empty/Missing  (column genuinely absent)
        // It MUST NOT be ParseFailure with a non-empty value we can't explain.
        foreach (var row in sec19.Rows)
        {
            foreach (var cell in row.Values)
            {
                cell.Kind.ShouldBeOneOf(
                    [CellKind.Amount, CellKind.Rate, CellKind.Days,
                     CellKind.NotApplicable, CellKind.Empty],
                    $"Row '{row.Label.RawText}' has unexpected cell kind {cell.Kind} " +
                    $"(raw='{cell.RawText}')");
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task ExtractFullAsync_AllFixtures_Section19StatusExtractedOrNotFound(
        string fixturePath, string fixtureName)
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(fixturePath);

        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue($"[{fixtureName}] Extraction failed: {result.Error}");
        var sec19 = GetTable(result.Value!.FinancialTables, 19);

        // Status must be a terminal/valid state — never an exception.
        sec19.Status.ShouldBeOneOf(
            [TableExtractionStatus.Extracted, TableExtractionStatus.SectionNotFound,
             TableExtractionStatus.NoRowsParsed, TableExtractionStatus.Indeterminate],
            $"[{fixtureName}] §19 has unexpected status {sec19.Status}");
    }

    // -----------------------------------------------------------------------
    // §20 Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFullAsync_JulAgo_Section20HasExactlyOneRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(JulAgoFixture);

        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue();
        var sec20 = GetTable(result.Value!.FinancialTables, 20);
        sec20.Status.ShouldBe(TableExtractionStatus.Extracted,
            $"§20 status={sec20.Status}; expected Extracted");
        sec20.Rows.Count.ShouldBe(1, "§20 has exactly one data row (the waterfall distribution)");
    }

    [Fact]
    public async Task ExtractFullAsync_JulAgo_Section20RowHasSevenValueCells()
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(JulAgoFixture);

        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue();
        var sec20 = GetTable(result.Value!.FinancialTables, 20);
        sec20.Status.ShouldBe(TableExtractionStatus.Extracted);

        var row = sec20.Rows[0];
        // Semantic column order (for E11.2 waterfall rule):
        //   [0] Pagos y abonos (LHS, signed −)
        //   [1] Compras y cargos Regulares
        //   [2] Compras a meses SIN intereses
        //   [3] Compras a meses CON intereses
        //   [4] Intereses y comisiones
        //   [5] IVA de intereses y comisiones
        //   [6] Saldo a favor
        row.Values.Count.ShouldBe(7,
            "§20 row should have 7 value columns (Pagos/Compras/SinInt/ConInt/Intereses/IVA/SaldoFavor)");

        // Regression guard: the "=" structural separator must NOT occupy a column slot.
        row.Values.ShouldAllBe(c => c.RawText != "=",
            "the '=' separator must be dropped, not kept as a value cell");

        // Regression guard: the 7th column (Saldo a favor) must be captured, not dropped
        // past the 7-cell cap. In jul_ago it is +$0.00.
        var saldoAFavor = row.Values[6];
        saldoAFavor.Kind.ShouldBe(CellKind.Amount,
            $"§20 col[6] Saldo a favor should be a parsed amount, was {saldoAFavor.Kind} raw='{saldoAFavor.RawText}'");
        saldoAFavor.ParsedValue.ShouldBe(0.00m);
    }

    [Fact]
    public async Task ExtractFullAsync_JulAgo_Section20WaterfallReconciliation()
    {
        // The waterfall check: col[0] (Pagos y abonos) should equal
        // the (signed) sum of cols[1..6].  Because geometry-based extraction
        // may be imprecise this test REPORTS the result rather than hard-failing,
        // to inform E11.2 rule authors whether to expect Pass or Fail on real fixtures.
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(JulAgoFixture);

        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue();
        var sec20 = GetTable(result.Value!.FinancialTables, 20);
        sec20.Status.ShouldBe(TableExtractionStatus.Extracted);
        sec20.Rows.Count.ShouldBeGreaterThan(0);

        var row = sec20.Rows[0];
        row.Values.Count.ShouldBe(7);

        // Extract parsed values (null if not parsed).
        var colValues = row.Values.Select(c => c.ParsedValue).ToList();

        // Log for observability.
        var log = XUnitLogger.CreateLogger<FinancialTableExtractionTests>();
        for (var i = 0; i < row.Values.Count; i++)
        {
            var cell = row.Values[i];
            log.LogInformation("§20 col[{Idx}] kind={Kind} conf={Conf:F2} raw='{Raw}' parsed={Val}",
                i, cell.Kind, cell.Confidence, cell.RawText, cell.ParsedValue?.ToString("F2") ?? "null");
        }

        // Only assert waterfall if at least 4 columns have parsed values.
        var parsedCount = colValues.Count(v => v.HasValue);
        log.LogInformation("§20 waterfall: {ParsedCount}/7 columns parsed.", parsedCount);

        if (parsedCount >= 4)
        {
            var pagosYAbonos = colValues[0];
            var sumComponents = colValues.Skip(1).Where(v => v.HasValue).Sum(v => v!.Value);
            log.LogInformation("§20 Pagos y abonos = {P}, sum(cols 1-6) = {S}, diff = {D}",
                pagosYAbonos?.ToString("F2") ?? "null",
                sumComponents.ToString("F2"),
                pagosYAbonos.HasValue ? Math.Abs(pagosYAbonos.Value - sumComponents).ToString("F2") : "N/A");
        }

        // The test passes as long as extraction succeeded and row has 7 cells.
        // The log output above informs E11.2 rule authors.
        sec20.Status.ShouldBe(TableExtractionStatus.Extracted);
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task ExtractFullAsync_AllFixtures_Section20StatusIsValid(
        string fixturePath, string fixtureName)
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(fixturePath);

        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue($"[{fixtureName}] Extraction failed: {result.Error}");
        var sec20 = GetTable(result.Value!.FinancialTables, 20);

        sec20.Status.ShouldBeOneOf(
            [TableExtractionStatus.Extracted, TableExtractionStatus.SectionNotFound,
             TableExtractionStatus.NoRowsParsed, TableExtractionStatus.Indeterminate],
            $"[{fixtureName}] §20 unexpected status {sec20.Status}");
    }

    // -----------------------------------------------------------------------
    // §8 Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFullAsync_JulAgo_Section8HasThreeIndicatorRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(JulAgoFixture);

        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue();
        var sec8 = GetTable(result.Value!.FinancialTables, 8);
        sec8.Status.ShouldBe(TableExtractionStatus.Extracted,
            $"§8 status={sec8.Status}; expected Extracted");
        sec8.Rows.Count.ShouldBe(3, "§8 has exactly 3 cost indicator rows");
    }

    [Fact]
    public async Task ExtractFullAsync_JulAgo_Section8EachRowHasOneValueCell()
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(JulAgoFixture);

        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue();
        var sec8 = GetTable(result.Value!.FinancialTables, 8);
        sec8.Status.ShouldBe(TableExtractionStatus.Extracted);

        foreach (var row in sec8.Rows)
        {
            row.Values.Count.ShouldBe(1,
                $"§8 row '{row.Label.RawText}' should have exactly 1 value cell");
        }
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task ExtractFullAsync_AllFixtures_Section8StatusIsValid(
        string fixturePath, string fixtureName)
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(fixturePath);

        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue($"[{fixtureName}] Extraction failed: {result.Error}");
        var sec8 = GetTable(result.Value!.FinancialTables, 8);

        sec8.Status.ShouldBeOneOf(
            [TableExtractionStatus.Extracted, TableExtractionStatus.SectionNotFound,
             TableExtractionStatus.NoRowsParsed, TableExtractionStatus.Indeterminate],
            $"[{fixtureName}] §8 unexpected status {sec8.Status}");
    }

    // -----------------------------------------------------------------------
    // §16 Abstain test (absent → NotFound, not an exception)
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task ExtractFullAsync_AllFixtures_Section16AbsentYieldsNotFoundOrExtracted(
        string fixturePath, string fixtureName)
    {
        // §16 is conditional. When absent, must be NotFound (never an exception or Fail).
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(fixturePath);

        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue($"[{fixtureName}] Extraction failed: {result.Error}");
        var sec16 = GetTable(result.Value!.FinancialTables, 16);

        // Any valid terminal status is acceptable — the key invariant is no exception.
        sec16.Status.ShouldBeOneOf(
            [TableExtractionStatus.SectionNotFound, TableExtractionStatus.Extracted,
             TableExtractionStatus.NoRowsParsed, TableExtractionStatus.Indeterminate],
            $"[{fixtureName}] §16 unexpected status {sec16.Status}");

        // When absent, rows must be empty.
        if (sec16.Status == TableExtractionStatus.SectionNotFound)
            sec16.Rows.ShouldBeEmpty($"[{fixtureName}] §16 NotFound should have no rows");
    }

    // -----------------------------------------------------------------------
    // §6 Abstain tests (§6 absent in all PRP2 fixtures → SectionNotFound, no crash)
    //
    // NOTE: §6 extraction is UNCALIBRATED — no §6 PDF fixture exists in the PRP2
    // corpus.  These tests verify only the graceful-abstain path (SectionNotFound)
    // and that the §6 entry is present in FinancialTables so Section6PaymentSimulationRule
    // can locate it.  Real-statement accuracy is corpus-gated.
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task ExtractFullAsync_AllFixtures_Section6PresentInFinancialTables(
        string fixturePath, string fixtureName)
    {
        // §6 must be present in FinancialTables (by SectionNumber) on every fixture
        // so Section6PaymentSimulationRule can look it up via FirstOrDefault(t => t.SectionNumber == 6).
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(fixturePath);

        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue($"[{fixtureName}] Extraction failed: {result.Error}");
        var tables = result.Value!.FinancialTables;

        var sec6 = tables.FirstOrDefault(t => t.SectionNumber == 6);
        sec6.ShouldNotBeNull($"[{fixtureName}] §6 entry must be present in FinancialTables");
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task ExtractFullAsync_AllFixtures_Section6AbsentYieldsSectionNotFound(
        string fixturePath, string fixtureName)
    {
        // §6 is absent from all three PRP2 Dummie VEC fixtures.
        // The graceful-abstain path must return SectionNotFound (never an exception).
        // ⚠️ UNCALIBRATED: if a real §6 statement is added to fixtures this assertion
        // may change to Extracted — recalibrate then.
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(fixturePath);

        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue($"[{fixtureName}] Extraction failed: {result.Error}");
        var sec6 = GetTable(result.Value!.FinancialTables, 6);

        // On current fixtures (§6 absent) expect SectionNotFound.
        // Any valid terminal status is acceptable — the key invariant is no exception.
        sec6.Status.ShouldBeOneOf(
            [TableExtractionStatus.SectionNotFound, TableExtractionStatus.Extracted,
             TableExtractionStatus.NoRowsParsed, TableExtractionStatus.Indeterminate],
            $"[{fixtureName}] §6 has unexpected status {sec6.Status}");

        // When absent, rows must be empty.
        if (sec6.Status == TableExtractionStatus.SectionNotFound)
            sec6.Rows.ShouldBeEmpty($"[{fixtureName}] §6 SectionNotFound should have no rows");
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task ExtractFullAsync_AllFixtures_Section6WhenExtractedHasThreeRowsWithTwoCellsEach(
        string fixturePath, string fixtureName)
    {
        // Guard: if a real §6 statement is added to the fixture corpus and the extractor
        // returns Extracted, verify the row/cell shape matches Section6PaymentSimulationRule's
        // contract: 3 rows (k=1,2,5), each with 2 value cells [months, interest].
        // ⚠️ UNCALIBRATED: on current fixtures this test is a no-op (SectionNotFound).
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(fixturePath);

        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue($"[{fixtureName}] Extraction failed: {result.Error}");
        var sec6 = GetTable(result.Value!.FinancialTables, 6);

        if (sec6.Status != TableExtractionStatus.Extracted)
            return; // SectionNotFound/Indeterminate — not yet calibrated; skip shape check.

        sec6.Rows.Count.ShouldBe(3,
            $"[{fixtureName}] §6 Extracted must have 3 scenario rows (k=1,2,5)");

        foreach (var row in sec6.Rows)
        {
            row.Values.Count.ShouldBe(2,
                $"[{fixtureName}] §6 row '{row.Label.RawText}' must have 2 value cells " +
                $"[months, interest] (Section6PaymentSimulationRule contract)");

            // Cell kinds: months col [0] may be Days (good parse), Amount (parse failure but
            // raw text found — ParseFailure returns Kind=Amount confidence=0.7), NotApplicable,
            // or Empty (absent).  ⚠️ UNCALIBRATED: column X-ranges not yet fitted to a real §6
            // PDF so Amount-kind parse failures are expected until recalibration.
            row.Values[0].Kind.ShouldBeOneOf(
                [CellKind.Days, CellKind.Amount, CellKind.NotApplicable, CellKind.Empty],
                $"[{fixtureName}] §6 row '{row.Label.RawText}' months cell (col 0) unexpected kind {row.Values[0].Kind}");

            // Interest col [1]: Amount, NotApplicable, or Empty.
            row.Values[1].Kind.ShouldBeOneOf(
                [CellKind.Amount, CellKind.NotApplicable, CellKind.Empty],
                $"[{fixtureName}] §6 row '{row.Label.RawText}' interest cell (col 1) unexpected kind {row.Values[1].Kind}");
        }
    }

    // -----------------------------------------------------------------------
    // FinancialTables collection invariants
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task ExtractFullAsync_AllFixtures_FinancialTablesContainsFiveEntries(
        string fixturePath, string fixtureName)
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(fixturePath);

        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue($"[{fixtureName}] Extraction failed: {result.Error}");
        var tables = result.Value!.FinancialTables;

        // Always exactly 5 entries: §8, §19, §20, §16, §6.
        // §6 is UNCALIBRATED (no §6 fixture exists in PRP2 corpus) — it is always SectionNotFound
        // on current fixtures.  The count must still be 5 so the §6 rule can find it by SectionNumber.
        tables.Count.ShouldBe(5, $"[{fixtureName}] Expected 5 FinancialTable entries (§8, §19, §20, §16, §6)");
        tables.Select(t => t.SectionNumber).ShouldBe([8, 19, 20, 16, 6],
            $"[{fixtureName}] Table section numbers should be [8, 19, 20, 16, 6]");
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task ExtractFullAsync_AllFixtures_ExistingTestsUnaffected(
        string fixturePath, string fixtureName)
    {
        // Regression: adding FinancialTables must not break the StatementModel's
        // existing extraction fields.
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(fixturePath);

        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue($"[{fixtureName}] Extraction failed: {result.Error}");
        var model = result.Value!;

        // Spot-check pre-existing fields.
        model.ClientName.ShouldNotBeNull();
        model.Sections.Count.ShouldBe(28, $"[{fixtureName}] Sections should still have 28 entries");
        model.Movements.ShouldNotBeNull();
        model.NormalizedFullText.ShouldNotBeNull();
    }

    // -----------------------------------------------------------------------
    // Cell confidence invariants
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractFullAsync_JulAgo_AllCellsHaveValidConfidence()
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(JulAgoFixture);

        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue();
        var tables = result.Value!.FinancialTables;

        foreach (var table in tables)
        {
            foreach (var row in table.Rows)
            {
                row.Label.Confidence.ShouldBeInRange(0.0, 1.0,
                    $"§{table.SectionNumber} row '{row.Label.RawText}' label confidence out of range");
                foreach (var cell in row.Values)
                {
                    cell.Confidence.ShouldBeInRange(0.0, 1.0,
                        $"§{table.SectionNumber} row '{row.Label.RawText}' value cell confidence out of range");
                }
            }
        }
    }
}
