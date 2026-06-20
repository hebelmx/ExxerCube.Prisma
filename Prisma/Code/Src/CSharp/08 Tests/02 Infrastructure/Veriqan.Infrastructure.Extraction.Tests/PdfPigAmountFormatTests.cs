using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Unit tests for <see cref="PdfPigStatementFieldExtractor.AmountNumberFormatSession"/> —
/// the per-session number-format detector introduced in VERIQAN-E2-S10.
/// </summary>
/// <remarks>
/// The session class is <c>internal</c>, visible here via the
/// <c>[assembly: InternalsVisibleTo(...)]</c> attribute in the production assembly.
/// Tests exercise the session directly rather than through a synthetic PDF so that
/// the format-detection and parse logic can be verified without PdfPig's extraction
/// pipeline introducing PDF-layout noise.
/// </remarks>
public sealed class PdfPigAmountFormatTests
{
    // Alias for brevity.
    private static PdfPigStatementFieldExtractor.AmountNumberFormatSession NewSession()
        => new();

    // -----------------------------------------------------------------------
    // Test 1 — European format: "1.234,56" → 1234.56
    // -----------------------------------------------------------------------

    [Fact]
    public void TryParse_EuropeanFormat_ParsedAsCorrectDecimal()
    {
        // Arrange
        var session = NewSession();
        // "1.234,56" — dot is thousands separator, comma is decimal separator.
        // The session must detect this as European format from the first unambiguous token.

        // Act
        var succeeded = session.TryParse("1.234,56", out var value);

        // Assert
        succeeded.ShouldBeTrue("European-format token '1.234,56' must parse successfully");
        value.ShouldBe(1234.56m, "European '1.234,56' must be interpreted as 1234.56");
    }

    // -----------------------------------------------------------------------
    // Test 2 — US/MX format: "1,234.56" → 1234.56 (regression guard)
    // -----------------------------------------------------------------------

    [Fact]
    public void TryParse_UsMxFormat_ParsedAsCorrectDecimal()
    {
        // Arrange
        var session = NewSession();
        // "1,234.56" — comma is thousands separator, dot is decimal separator.
        // This is the standard format in Mexican VEC statements; must continue to work.

        // Act
        var succeeded = session.TryParse("1,234.56", out var value);

        // Assert
        succeeded.ShouldBeTrue("US/MX-format token '1,234.56' must parse successfully");
        value.ShouldBe(1234.56m, "US/MX '1,234.56' must be interpreted as 1234.56");
    }

    // -----------------------------------------------------------------------
    // Test 3 — Ambiguous single-separator token: "1.234" → abstain (false)
    // -----------------------------------------------------------------------

    [Fact]
    public void TryParse_AmbiguousSingleSeparator_ReturnsFalse()
    {
        // Arrange — fresh session with no prior unambiguous token seen.
        var session = NewSession();
        // "1.234" is ambiguous: could be 1234 (European thousands separator)
        // or 1.234 (US decimal value).  With no prior context, the session must abstain.

        // Act
        var succeeded = session.TryParse("1.234", out var value);

        // Assert
        succeeded.ShouldBeFalse(
            "Ambiguous token '1.234' must not parse when no format has been detected yet");
        value.ShouldBe(0m, "Output value must be default (0m) on parse failure");
    }

    // -----------------------------------------------------------------------
    // Bonus: once format is set by an unambiguous token, ambiguous tokens follow it
    // -----------------------------------------------------------------------

    [Fact]
    public void TryParse_AmbiguousTokenAfterUsMxContext_UsesUsMxFormat()
    {
        // Arrange — prime the session with a clear US/MX token first.
        var session = NewSession();
        session.TryParse("32,446.69", out _); // sets US/MX format

        // Now an ambiguous token "1.234" should be parsed as US decimal (1.234).
        var succeeded = session.TryParse("1.234", out var value);

        // Assert — once US/MX is known, "1.234" is treated as 1.234 (decimal 1.234).
        succeeded.ShouldBeTrue(
            "After US/MX format is established, '1.234' must parse as a plain decimal");
        value.ShouldBe(1.234m);
    }

    [Fact]
    public void TryParse_AmbiguousTokenAfterEuropeanContext_UsesEuropeanFormat()
    {
        // Arrange — prime the session with a clear European token first.
        var session = NewSession();
        session.TryParse("1.234,56", out _); // sets European format

        // Now an ambiguous token "1.234" should be parsed as European thousands (1234).
        var succeeded = session.TryParse("1.234", out var value);

        // Assert — once European is known, "1.234" is treated as 1234 (dot=thousands).
        succeeded.ShouldBeTrue(
            "After European format is established, '1.234' must parse as 1234");
        value.ShouldBe(1234m);
    }

    // -----------------------------------------------------------------------
    // Concurrent isolation: two sessions must not share state
    // -----------------------------------------------------------------------

    [Fact]
    public void TwoSessions_AreIndependent()
    {
        // Arrange
        var sessionA = NewSession();
        var sessionB = NewSession();

        // Prime sessionA with US/MX.
        sessionA.TryParse("32,446.69", out _);

        // Prime sessionB with European.
        sessionB.TryParse("1.234,56", out _);

        // Act — parse "1.234" in each.
        sessionA.TryParse("1.234", out var valueA);
        sessionB.TryParse("1.234", out var valueB);

        // Assert — each session interprets it according to its own format.
        valueA.ShouldBe(1.234m,  "Session A (US/MX) must treat '1.234' as a plain decimal");
        valueB.ShouldBe(1234m,   "Session B (European) must treat '1.234' as 1234");
    }

    // -----------------------------------------------------------------------
    // Regression: TryParseUsMx — never abstains on format grounds (S10 fix)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Regression guard for the bug where amounts &lt; $1,000 (no thousands comma)
    /// caused total extraction failure because the detecting <see cref="AmountNumberFormatSession.TryParse"/>
    /// abstained on every token (no both-separator anchor ever appeared).
    /// <para>
    /// <c>TryParseUsMx</c> is the fixed entry point for regex-gated call sites where
    /// the token shape is already proven to be US/MX.  It must never abstain.
    /// </para>
    /// </summary>
    [Fact]
    public void TryParseUsMx_LoneTwoDecimalAmount_ParsesWithoutPriorContext()
    {
        // Arrange — fresh session, NO prior token to seed format detection.
        var session = NewSession();

        // Act + Assert: sub-$1,000 amounts with only a decimal separator (no thousands comma).
        // These are the tokens that would have abstained under the old detecting TryParse.
        var ok580 = session.TryParseUsMx("580.00", out var v580);
        ok580.ShouldBeTrue("'580.00' must parse via TryParseUsMx on a fresh session");
        v580.ShouldBe(580.00m);

        var ok45 = session.TryParseUsMx("45.00", out var v45);
        ok45.ShouldBeTrue("'45.00' must parse via TryParseUsMx after seeding");
        v45.ShouldBe(45.00m);

        var ok12 = session.TryParseUsMx("12.50", out var v12);
        ok12.ShouldBeTrue("'12.50' must parse via TryParseUsMx");
        v12.ShouldBe(12.50m);
    }

    [Fact]
    public void TryParseUsMx_ThousandsAmount_ParsesCorrectly()
    {
        // Arrange — fresh session.
        var session = NewSession();

        // "1,234" — comma is thousands separator in US/MX; result is 1234.
        var ok = session.TryParseUsMx("1,234", out var value);

        ok.ShouldBeTrue("'1,234' must parse via TryParseUsMx as US/MX thousands");
        value.ShouldBe(1234m);
    }

    [Fact]
    public void TryParseUsMx_SeedsSessionForSubsequentDetectingTryParse()
    {
        // Arrange — fresh session, seed via TryParseUsMx (simulates regex-gated path).
        var session = NewSession();
        session.TryParseUsMx("580.00", out _);

        // Now the detecting TryParse should know the format is US/MX,
        // so an ambiguous token "1.234" parses as 1.234 (decimal), not 1234 (euro thousands).
        var succeeded = session.TryParse("1.234", out var value);

        succeeded.ShouldBeTrue(
            "After TryParseUsMx seeds the session, detecting TryParse must resolve ambiguous tokens");
        value.ShouldBe(1.234m, "US/MX context: '1.234' is a plain decimal, not European thousands");
    }

    [Fact]
    public void TryParse_AmbiguousSingleSeparator_StillAbstainsWhenDetectingPathUnseeded()
    {
        // Arrange — fresh session, nothing seeded yet (neither TryParse nor TryParseUsMx called).
        var session = NewSession();

        // Act — detecting path with lone-separator ambiguous token.
        var succeeded = session.TryParse("580.00", out _);

        // Assert — the detecting path still abstains on a lone-separator token with no context.
        // (This ensures TryParse's abstain behaviour is preserved for the §20 unfiltered path.)
        succeeded.ShouldBeFalse(
            "The detecting TryParse must still abstain for '580.00' on a fresh session " +
            "(lone separator, ambiguous); only TryParseUsMx should parse it unconditionally");
    }
}
