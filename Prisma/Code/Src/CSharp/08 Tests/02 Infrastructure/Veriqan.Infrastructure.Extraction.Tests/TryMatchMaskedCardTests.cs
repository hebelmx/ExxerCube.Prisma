using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Unit tests for <see cref="PdfPigStatementFieldExtractor.TryMatchMaskedCard"/>.
/// Validates that the masked-card fallback fires only on genuine masked presentations
/// (first three groups are mask glyphs) and NOT on all-digit transaction references
/// that happen to end in the same four digits as the card (MAJOR-2/4 regression guard).
/// </summary>
public sealed class TryMatchMaskedCardTests
{
    // -----------------------------------------------------------------------
    // Positive cases — genuine masked presentations must match
    // -----------------------------------------------------------------------

    [Fact]
    public void TryMatchMaskedCard_XxMaskedSpaceSeparated_ReturnsTrue()
    {
        var result = PdfPigStatementFieldExtractor.TryMatchMaskedCard(
            pageText: "XXXX XXXX XXXX 1234",
            cardLast4: "1234");

        result.ShouldBeTrue();
    }

    [Fact]
    public void TryMatchMaskedCard_AsteriskMaskedSpaceSeparated_ReturnsTrue()
    {
        var result = PdfPigStatementFieldExtractor.TryMatchMaskedCard(
            pageText: "**** **** **** 1234",
            cardLast4: "1234");

        result.ShouldBeTrue();
    }

    [Fact]
    public void TryMatchMaskedCard_XxMaskedHyphenSeparated_ReturnsTrue()
    {
        var result = PdfPigStatementFieldExtractor.TryMatchMaskedCard(
            pageText: "XXXX-XXXX-XXXX-1234",
            cardLast4: "1234");

        result.ShouldBeTrue();
    }

    [Fact]
    public void TryMatchMaskedCard_MaskedCardEmbeddedInSurroundingText_ReturnsTrue()
    {
        // Mask glyphs surrounded by normal sentence text — must still be found.
        var result = PdfPigStatementFieldExtractor.TryMatchMaskedCard(
            pageText: "Tarjeta XXXX XXXX XXXX 1234 vigente",
            cardLast4: "1234");

        result.ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // Negative cases — all-digit references must NOT match (MAJOR-2/4 guard)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Cardinal regression: a transaction reference printed in 4-4-4-4 format
    /// that merely ends in the same four digits as the card must NOT be treated as
    /// a masked-card hit — doing so would produce a false-PASS on CL-34.
    /// </summary>
    [Fact]
    public void TryMatchMaskedCard_AllDigitTransactionRefEndingInLast4_ReturnsFalse()
    {
        // All-digit reference: first three groups are plain digits, not mask glyphs.
        var result = PdfPigStatementFieldExtractor.TryMatchMaskedCard(
            pageText: "1234 5678 9012 1234",
            cardLast4: "1234");

        result.ShouldBeFalse();
    }

    [Fact]
    public void TryMatchMaskedCard_AllDigitReferenceEmbeddedInText_ReturnsFalse()
    {
        // Same scenario, embedded in surrounding text.
        var result = PdfPigStatementFieldExtractor.TryMatchMaskedCard(
            pageText: "alguna referencia 9999 8888 7777 1234 fin",
            cardLast4: "1234");

        result.ShouldBeFalse();
    }

    [Fact]
    public void TryMatchMaskedCard_EmptyText_ReturnsFalse()
    {
        var result = PdfPigStatementFieldExtractor.TryMatchMaskedCard(
            pageText: string.Empty,
            cardLast4: "1234");

        result.ShouldBeFalse();
    }

    [Fact]
    public void TryMatchMaskedCard_Last4Mismatch_ReturnsFalse()
    {
        // Mask glyphs are present but last-4 does not match the card's last-4.
        var result = PdfPigStatementFieldExtractor.TryMatchMaskedCard(
            pageText: "XXXX XXXX XXXX 5678",
            cardLast4: "1234");

        result.ShouldBeFalse();
    }
}
