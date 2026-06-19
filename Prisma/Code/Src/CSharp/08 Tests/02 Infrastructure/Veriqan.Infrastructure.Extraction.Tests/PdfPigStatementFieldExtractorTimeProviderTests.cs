using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.Time.Testing;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// VERIQAN-E2-S8 — Verifies that <see cref="PdfPigStatementFieldExtractor"/> consults
/// the injected <see cref="TimeProvider"/> (not the ambient system clock) when repairing
/// truncated charge dates whose year could not be inferred from the operation-date context.
/// </summary>
/// <remarks>
/// <para>
/// The repair path is in <c>TryParseMovementRow</c>: when the charge-date token is a
/// truncated year ("dd-mmm-YYY" — 3 digits) and the operation date is also unparseable,
/// the code falls back to <c>_timeProvider.GetUtcNow().Year</c> and appends its last digit.
/// These tests drive that path with a <see cref="FakeTimeProvider"/> to verify
/// determinism independent of the real clock.
/// </para>
/// <para>
/// <b>Synthetic PDF layout:</b> the extractor requires the word "DESGLOSE" on a page to
/// enter movement-row parsing.  A single data row is placed with:
/// <list type="bullet">
///   <item><description>X ≤ 95: truncated operation date "05-dic-202" (matches
///         DesgloseDatePattern but fails full parse → operationDate = null).</description></item>
///   <item><description>X 96–157: truncated charge date "07-dic-202" (same — triggers
///         the TimeProvider-based year repair).</description></item>
///   <item><description>X ≈ 200: description token.</description></item>
///   <item><description>X ≈ 430: sign token "+" (within the sign column 423–438).</description></item>
///   <item><description>X ≈ 450: amount token "100.00".</description></item>
/// </list>
/// PDF coordinates origin is bottom-left.  We place the DESGLOSE header and data row on
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
    /// and whose operation date is also truncated so that <c>operationDate == null</c>
    /// and the TimeProvider fallback is exercised.
    /// </summary>
    private static byte[] BuildSyntheticDesgloseDesgloseWithTruncatedDates()
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
        // Same truncation — triggers the TimeProvider-based year repair.
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

    // -----------------------------------------------------------------------
    // Test 1 — FakeTimeProvider clock year 2026 → repaired charge date year 2026
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the fake clock is set to 2026-01-01 and the charge-date token contains a
    /// truncated 3-digit year ("07-dic-202"), the extractor must repair the year to 2026
    /// (the last digit of <see cref="TimeProvider.GetUtcNow"/>().Year = 6 → "07-dic-2026").
    /// This proves the extractor consults the injected <see cref="TimeProvider"/> rather
    /// than the ambient system clock.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_TruncatedChargeDate_YearTracksFakeClockYear2026()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange: fake clock fixed at 2026-01-01 00:00:00 UTC.
        var fakeTime = new FakeTimeProvider();
        fakeTime.SetUtcNow(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var extractor = CreateExtractor(fakeTime);
        var pdf = BuildSyntheticDesgloseDesgloseWithTruncatedDates();

        // Act
        var result = await extractor.ExtractFullAsync(pdf, ct);

        // Assert: extraction must succeed (or fail gracefully — not throw)
        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync must succeed. Error: {result.Error}");

        var movements = result.Value!.Movements;
        movements.ShouldNotBeNull("Movements must be populated by ExtractFullAsync");
        movements.Count.ShouldBeGreaterThan(0, "At least one movement row must be parsed from the synthetic DESGLOSE section");

        // The first (only) movement must have a charge date repaired to year 2026.
        var movement = movements[0];
        movement.ChargeDate.ShouldNotBeNull(
            "ChargeDate must be populated after year repair (truncated '07-dic-202' + last digit of 2026 = '07-dic-2026')");
        movement.ChargeDate!.Value.Year.ShouldBe(2026,
            "Year repair must use the FakeTimeProvider year (2026), not the ambient system clock");
        movement.ChargeDate.Value.Month.ShouldBe(12,
            "Month must be December (dic = 12) after repair");
        movement.ChargeDate.Value.Day.ShouldBe(7,
            "Day must be 7 ('07-dic-2026')");
    }

    // -----------------------------------------------------------------------
    // Test 2 — FakeTimeProvider clock year 2025 → repaired charge date year 2025
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the fake clock is fixed in December 2025 and the charge-date token is
    /// "07-dic-202", the extractor must repair the year to 2025 (last digit = 5 → "07-dic-2025").
    /// This is the canonical "December statement processed in December" scenario where the
    /// correct year is 2025 and the test verifies it does NOT bleed into 2026.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_TruncatedChargeDate_DecemberContextYearRepaired2025NotBleedTo2026()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange: fake clock fixed at 2025-12-01 — simulating a December 2025 statement.
        var fakeTime = new FakeTimeProvider();
        fakeTime.SetUtcNow(new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero));

        var extractor = CreateExtractor(fakeTime);
        var pdf = BuildSyntheticDesgloseDesgloseWithTruncatedDates();

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
            "Year repair with clock year 2025 must give 2025, NOT 2026 — same statement processed in December vs January must yield the same Finding");
        movement.ChargeDate.Value.Month.ShouldBe(12, "Month must be December");
        movement.ChargeDate.Value.Day.ShouldBe(7, "Day must be 7");
    }
}
