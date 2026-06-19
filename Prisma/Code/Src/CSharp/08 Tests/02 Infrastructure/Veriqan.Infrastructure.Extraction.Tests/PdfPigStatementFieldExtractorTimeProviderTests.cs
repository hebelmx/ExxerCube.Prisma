using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.Time.Testing;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// VERIQAN-E2-S8 — Verifies that <see cref="PdfPigStatementFieldExtractor"/> correctly
/// anchors truncated-date year-repair to the statement's own period year (NFR-5 / gap #36),
/// falling back to the injected <see cref="TimeProvider"/> only when the period is unavailable.
/// </summary>
/// <remarks>
/// <para>
/// <b>Gap #36 requirement:</b> the same statement processed in December 2025 and January 2026
/// must produce the same repaired movement-date year (determinism across reprocessing).
/// The fix anchors repair to <c>PeriodCutDate.Year</c> extracted from the statement header,
/// so the result is tied to the PDF's declared period rather than the processing clock.
/// </para>
/// <para>
/// <b>Repair anchor precedence (in <c>TryParseMovementRow</c>):</b>
/// <list type="number">
///   <item>Statement period year (<c>PeriodCutDate.Year</c> from the header).</item>
///   <item>Operation-date year (same row — already parsed).</item>
///   <item><c>_timeProvider.GetUtcNow().Year</c> — last-resort / wall-clock fallback.</item>
/// </list>
/// </para>
/// <para>
/// <b>Test 1 + 2 (existing, re-anchored):</b> synthetic PDFs with NO period header.
/// The period anchor is unavailable, so the fallback chain bottoms out at the
/// <see cref="TimeProvider"/>. These tests confirm the TimeProvider path still works
/// as the last-resort fallback.
/// </para>
/// <para>
/// <b>Test 3 (new — the key determinism proof):</b> a synthetic PDF that includes both
/// a valid "Fecha de Corte" header (period year 2025) and a DESGLOSE section with a
/// truncated movement charge date. The test extracts the same PDF TWICE — once with a
/// FakeTimeProvider set to 2025 and once set to 2026. Both runs must yield the same
/// repaired charge-date year (2025), proving that the period anchor — not the clock —
/// drives the repair.
/// </para>
/// <para>
/// <b>Synthetic PDF layout:</b> the extractor requires the word "DESGLOSE" on a page to
/// enter movement-row parsing. A single data row is placed with:
/// <list type="bullet">
///   <item><description>X ≤ 95: truncated operation date "05-dic-202" (matches
///         DesgloseDatePattern but fails full parse → operationDate = null).</description></item>
///   <item><description>X 96–157: truncated charge date "07-dic-202" (same — triggers
///         the year-repair path).</description></item>
///   <item><description>X ≈ 200: description token.</description></item>
///   <item><description>X ≈ 430: sign token "+" (within the sign column 423–438).</description></item>
///   <item><description>X ≈ 450: amount token "100.00".</description></item>
/// </list>
/// PDF coordinates origin is bottom-left. We place the DESGLOSE header and data row on
/// different Y-bands so the header is not misread as a data row.
/// </para>
/// </remarks>
public sealed class PdfPigStatementFieldExtractorTimeProviderTests
{
    // -----------------------------------------------------------------------
    // Factory helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates an extractor backed by <paramref name="timeProvider"/>.
    /// All other dependencies use in-test defaults.
    /// </summary>
    private static PdfPigStatementFieldExtractor CreateExtractor(TimeProvider timeProvider)
    {
        var logger = XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>();
        var options = Microsoft.Extensions.Options.Options.Create(new PdfExtractionOptions());
        return new PdfPigStatementFieldExtractor(logger, options, new NullPasswordProvider(), timeProvider);
    }

    /// <summary>
    /// Builds a minimal PDF that contains exactly one DESGLOSE section with a single
    /// movement row whose charge date is truncated to a 3-digit year ("07-dic-202"),
    /// and whose operation date is also truncated so that <c>operationDate == null</c>.
    /// This PDF has NO period header, so <c>periodYear</c> in the extractor will be null
    /// and the repair falls back to <c>_timeProvider.GetUtcNow().Year</c>.
    /// </summary>
    private static byte[] BuildSyntheticDesgloseWithTruncatedDatesNoPeriodHeader()
    {
        // Page dimensions: use the same A4 approximation as other tests (595 × 842 pt).
        // PDF Y-origin is bottom-left; place items at Y values below the page top.

        var builder = new PdfDocumentBuilder();
        // A4 in PDF points
        var page = builder.AddPage(595, 842);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        const float fontSize = 8f;

        // ---- DESGLOSE section header (Y ≈ 700) -----------------------------
        // The extractor only requires the word "DESGLOSE" to be present on the page.
        page.AddText("DESGLOSE DE MOVIMIENTOS DEL PERIODO", fontSize, new PdfPoint(50, 700), font);

        // ---- Data row (Y ≈ 500) --------------------------------------------
        // Each token is placed at the X-coordinate that maps to the correct column.

        // Column: operation date (X ≤ 95)
        // "05-dic-202" — matches DesgloseDatePattern (\d{1,2}-[alpha]+-\d{2,4}) but
        // fails TryParseSpanishDate (3-digit year does not match \d{4}), leaving operationDate = null.
        page.AddText("05-dic-202", fontSize, new PdfPoint(30, 500), font);

        // Column: charge date (X 96–157)
        // Same truncation — triggers the year-repair path (anchor or fallback).
        page.AddText("07-dic-202", fontSize, new PdfPoint(100, 500), font);

        // Column: description (X 158–422)
        page.AddText("CARGO-PRUEBA-TIMEPROVIDER", fontSize, new PdfPoint(200, 500), font);

        // Column: sign (X 423–438)
        // "+" is a credit token; "-" is a charge token.  Either is fine — we just need a sign.
        page.AddText("+", fontSize, new PdfPoint(428, 500), font);

        // Column: amount (X ≥ 436)
        page.AddText("100.00", fontSize, new PdfPoint(450, 500), font);

        return builder.Build();
    }

    /// <summary>
    /// Builds a synthetic PDF that includes BOTH a valid "Fecha de Corte" header on page 1
    /// (period cut date = 04-ago-2025, year 2025) AND a DESGLOSE section with a truncated
    /// movement charge date ("07-dic-202").
    /// </summary>
    /// <remarks>
    /// <para>
    /// This PDF exercises the FULL anchor hierarchy:
    /// <c>periodYear = 2025</c> (from the header) → the repair uses 2025 regardless
    /// of what clock year the extractor is run under.
    /// </para>
    /// <para>
    /// <b>Period header layout (page 1, Y ≈ 601):</b>
    /// Each word is placed at a different X so PdfPig creates separate Word objects.
    /// The extractor's <c>ExtractFechaDeCorte</c> scans the sorted word list for the
    /// sequence "Fecha" / "de" / "Corte" and reads the value words to the right
    /// of "Corte" on the same Y-band.
    /// Format: "04 de ago 2025" (Spanish long form, 4 tokens).
    /// </para>
    /// </remarks>
    private static byte[] BuildSyntheticDesgloseWithPeriodHeader2025()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        const float fontSize = 8f;

        // ---- Period header: "Fecha de Corte 04 de ago 2025" (Y = 601) -----
        // Each word is a separate AddText call at a different X so PdfPig
        // generates one Word object per token.  The label words ("Fecha", "de",
        // "Corte") must appear consecutively in the sorted (top-to-bottom,
        // left-to-right) word list and on the same Y-band as the value words.
        page.AddText("Fecha", fontSize, new PdfPoint(298, 601), font);
        page.AddText("de", fontSize, new PdfPoint(325, 601), font);
        page.AddText("Corte", fontSize, new PdfPoint(340, 601), font);
        // Value: "04 de ago 2025" (4 tokens, parsed by TryParseSpanishDate Format 2)
        page.AddText("04", fontSize, new PdfPoint(375, 601), font);
        page.AddText("de", fontSize, new PdfPoint(390, 601), font);
        page.AddText("ago", fontSize, new PdfPoint(400, 601), font);
        page.AddText("2025", fontSize, new PdfPoint(415, 601), font);

        // ---- DESGLOSE section header (Y = 700) -----------------------------
        page.AddText("DESGLOSE DE MOVIMIENTOS DEL PERIODO", fontSize, new PdfPoint(50, 700), font);

        // ---- Data row (Y = 500) — truncated dates -------------------------
        // Operation date: truncated, so operationDate stays null.
        page.AddText("05-dic-202", fontSize, new PdfPoint(30, 500), font);
        // Charge date: truncated — repair should use the period year (2025).
        page.AddText("07-dic-202", fontSize, new PdfPoint(100, 500), font);
        // Description
        page.AddText("CARGO-PERIODO-ANCHOR", fontSize, new PdfPoint(200, 500), font);
        // Sign
        page.AddText("+", fontSize, new PdfPoint(428, 500), font);
        // Amount
        page.AddText("100.00", fontSize, new PdfPoint(450, 500), font);

        return builder.Build();
    }

    // -----------------------------------------------------------------------
    // Test 1 — TimeProvider fallback: clock year 2026 → repaired charge date year 2026
    // (no period header; TimeProvider is the last-resort fallback)
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the fake clock is set to 2026-01-01 and no period header is present in the PDF,
    /// the extractor uses the TimeProvider as the last-resort fallback and repairs the
    /// truncated charge-date year to 2026.
    /// This proves the TimeProvider fallback path is still exercised when the period anchor
    /// is unavailable.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_TruncatedChargeDate_NoPeriodHeader_YearTracksFakeClockYear2026()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange: fake clock fixed at 2026-01-01 00:00:00 UTC.
        var fakeTime = new FakeTimeProvider();
        fakeTime.SetUtcNow(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var extractor = CreateExtractor(fakeTime);
        var pdf = BuildSyntheticDesgloseWithTruncatedDatesNoPeriodHeader();

        // Act
        var result = await extractor.ExtractFullAsync(pdf, ct);

        // Assert: extraction must succeed (or fail gracefully — not throw)
        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync must succeed. Error: {result.Error}");

        var movements = result.Value!.Movements;
        movements.ShouldNotBeNull("Movements must be populated by ExtractFullAsync");
        movements.Count.ShouldBeGreaterThan(0, "At least one movement row must be parsed from the synthetic DESGLOSE section");

        // The first (only) movement must have a charge date repaired to year 2026
        // because the TimeProvider fallback is the only available anchor.
        var movement = movements[0];
        movement.ChargeDate.ShouldNotBeNull(
            "ChargeDate must be populated after year repair (truncated '07-dic-202' + last digit of 2026 = '07-dic-2026')");
        movement.ChargeDate!.Value.Year.ShouldBe(2026,
            "Year repair must use the FakeTimeProvider year (2026) when no period header is present — TimeProvider is the last-resort fallback");
        movement.ChargeDate.Value.Month.ShouldBe(12,
            "Month must be December (dic = 12) after repair");
        movement.ChargeDate.Value.Day.ShouldBe(7,
            "Day must be 7 ('07-dic-2026')");
    }

    // -----------------------------------------------------------------------
    // Test 2 — TimeProvider fallback: clock year 2025 → repaired charge date year 2025
    // (no period header; TimeProvider is the last-resort fallback)
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the fake clock is fixed in December 2025 and no period header is present,
    /// the TimeProvider fallback gives year 2025 for the repair.
    /// This is the canonical "December statement processed in December" scenario.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_TruncatedChargeDate_NoPeriodHeader_DecemberFallbackRepairs2025()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange: fake clock fixed at 2025-12-01 — simulating a December 2025 statement.
        var fakeTime = new FakeTimeProvider();
        fakeTime.SetUtcNow(new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero));

        var extractor = CreateExtractor(fakeTime);
        var pdf = BuildSyntheticDesgloseWithTruncatedDatesNoPeriodHeader();

        // Act
        var result = await extractor.ExtractFullAsync(pdf, ct);

        // Assert
        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync must succeed. Error: {result.Error}");

        var movements = result.Value!.Movements;
        movements.ShouldNotBeNull("Movements must be populated by ExtractFullAsync");
        movements.Count.ShouldBeGreaterThan(0, "At least one movement row must be parsed");

        var movement = movements[0];
        movement.ChargeDate.ShouldNotBeNull(
            "ChargeDate must be populated after year repair (truncated '07-dic-202' + last digit of 2025 = '07-dic-2025')");
        movement.ChargeDate!.Value.Year.ShouldBe(2025,
            "Year repair with TimeProvider fallback year 2025 must give 2025, NOT 2026");
        movement.ChargeDate.Value.Month.ShouldBe(12, "Month must be December");
        movement.ChargeDate.Value.Day.ShouldBe(7, "Day must be 7");
    }

    // -----------------------------------------------------------------------
    // Test 3 — KEY determinism proof (gap #36 / NFR-5)
    // The same PDF processed with two different clock years → IDENTICAL result
    // because the period year (2025) anchors the repair, not the clock.
    // -----------------------------------------------------------------------

    /// <summary>
    /// DETERMINISM PROOF (gap #36 / NFR-5): the same statement PDF with period year 2025
    /// produces the SAME repaired charge-date year (2025) regardless of the processing
    /// clock year. Two extractions — one with a FakeTimeProvider set to 2025, one set to
    /// 2026 — must both yield charge date = 2025-12-07.
    /// </summary>
    /// <remarks>
    /// This is the test the prior implementation could not pass in spirit: anchoring on the
    /// TimeProvider alone means that reprocessing the same PDF in a different calendar year
    /// produces a different result (violating NFR-5). The fix anchors to the statement's
    /// declared <c>PeriodCutDate</c> year (2025 from "04 de ago 2025" in the header),
    /// which is PDF-intrinsic and clock-independent.
    /// </remarks>
    [Fact]
    public async Task ExtractFullAsync_TruncatedChargeDate_WithPeriodHeader2025_SameResultRegardlessOfClockYear()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange: ONE synthetic PDF with period cut date 2025-08-04 (year 2025).
        var pdf = BuildSyntheticDesgloseWithPeriodHeader2025();

        // Run #1: clock set to 2025 (same year as period — no discrepancy).
        var fakeTime2025 = new FakeTimeProvider();
        fakeTime2025.SetUtcNow(new DateTimeOffset(2025, 12, 15, 0, 0, 0, TimeSpan.Zero));
        var extractor2025 = CreateExtractor(fakeTime2025);
        var result2025 = await extractor2025.ExtractFullAsync(pdf, ct);

        // Run #2: clock set to 2026 — different calendar year than the statement period.
        var fakeTime2026 = new FakeTimeProvider();
        fakeTime2026.SetUtcNow(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var extractor2026 = CreateExtractor(fakeTime2026);
        var result2026 = await extractor2026.ExtractFullAsync(pdf, ct);

        // Assert both extractions succeeded.
        result2025.IsSuccess.ShouldBeTrue(
            $"Extraction with clock=2025 must succeed. Error: {result2025.Error}");
        result2026.IsSuccess.ShouldBeTrue(
            $"Extraction with clock=2026 must succeed. Error: {result2026.Error}");

        var movements2025 = result2025.Value!.Movements;
        var movements2026 = result2026.Value!.Movements;

        movements2025.ShouldNotBeNull();
        movements2026.ShouldNotBeNull();
        movements2025.Count.ShouldBeGreaterThan(0,
            "At least one movement must be parsed (clock=2025 run)");
        movements2026.Count.ShouldBeGreaterThan(0,
            "At least one movement must be parsed (clock=2026 run)");

        var chargeDate2025 = movements2025[0].ChargeDate;
        var chargeDate2026 = movements2026[0].ChargeDate;

        // THE KEY ASSERTION: both runs must produce the same repaired year (2025),
        // anchored to the statement's PeriodCutDate year — not to the clock year.
        chargeDate2025.ShouldNotBeNull(
            "ChargeDate must be repaired (clock=2025 run): period anchor 2025 → '07-dic-2025'");
        chargeDate2026.ShouldNotBeNull(
            "ChargeDate must be repaired (clock=2026 run): period anchor 2025 → '07-dic-2025'");

        chargeDate2025!.Value.Year.ShouldBe(2025,
            "clock=2025 run: period anchor (2025) must drive the repair, not the clock");
        chargeDate2026!.Value.Year.ShouldBe(2025,
            "clock=2026 run: SAME result — period anchor (2025) overrides the clock year (2026). " +
            "Gap #36 / NFR-5: reprocessing the same statement in a different calendar year must " +
            "yield the same Finding.");

        // Verify month and day are also identical.
        chargeDate2025.Value.Month.ShouldBe(12, "Month must be December (dic = 12) — clock=2025 run");
        chargeDate2025.Value.Day.ShouldBe(7, "Day must be 7 — clock=2025 run");
        chargeDate2026.Value.Month.ShouldBe(12, "Month must be December (dic = 12) — clock=2026 run");
        chargeDate2026.Value.Day.ShouldBe(7, "Day must be 7 — clock=2026 run");

        // Secondary check: both repaired dates are exactly equal.
        chargeDate2025.Value.ShouldBe(chargeDate2026.Value,
            "Both runs on the same PDF must yield bit-for-bit identical charge dates (full determinism).");
    }
}
