using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Veriqan.Application.Tests.Extraction;

/// <summary>
/// Unit tests for <see cref="VecTextMatcher"/> (Story 10.3).
/// </summary>
/// <remarks>
/// Coverage matrix:
/// <list type="bullet">
///   <item>Normalization: accent/case/whitespace insensitivity.</item>
///   <item>Normalization: soft-hyphen and invisible-separator removal.</item>
///   <item>Normalization: Unicode ligature folding (ﬁ, ﬂ, ﬀ, ﬃ, ﬄ, æ, œ).</item>
///   <item>Similarity: identical strings → 1.0.</item>
///   <item>Similarity: token reordering still matches.</item>
///   <item>Similarity: near-miss below threshold flagged.</item>
///   <item>Edge cases: null, empty, whitespace-only.</item>
///   <item>Realistic CONDUSEF legend: comparador.banxico.org.mx with stray spacing/case.</item>
/// </list>
/// </remarks>
public sealed class VecTextMatcherTests
{
    // -----------------------------------------------------------------------
    // Normalize — accent / case / whitespace
    // -----------------------------------------------------------------------

    [Fact]
    public void Normalize_AccentedInput_StripsAccents()
    {
        var result = VecTextMatcher.Normalize("Adeúdo en Período");

        result.ShouldBe("ADEUDO EN PERIODO");
    }

    [Fact]
    public void Normalize_MixedCase_UpperCasesResult()
    {
        var result = VecTextMatcher.Normalize("compara tu tarjeta");

        result.ShouldBe("COMPARA TU TARJETA");
    }

    [Fact]
    public void Normalize_ExtraWhitespace_CollapsesToSingleSpace()
    {
        var result = VecTextMatcher.Normalize("consulta  tu   saldo");

        result.ShouldBe("CONSULTA TU SALDO");
    }

    [Fact]
    public void Normalize_LeadingTrailingWhitespace_Trimmed()
    {
        var result = VecTextMatcher.Normalize("  LEYENDA  ");

        result.ShouldBe("LEYENDA");
    }

    // -----------------------------------------------------------------------
    // Normalize — soft hyphen and invisible separators
    // -----------------------------------------------------------------------

    [Fact]
    public void Normalize_SoftHyphen_IsRemoved()
    {
        // Embed a SOFT HYPHEN (U+00AD) between characters.
        var withSoftHyphen = "con­sulta";

        var result = VecTextMatcher.Normalize(withSoftHyphen);

        result.ShouldBe("CONSULTA");
    }

    [Fact]
    public void Normalize_ZeroWidthSpace_IsRemoved()
    {
        var withZwsp = "ban​co";

        var result = VecTextMatcher.Normalize(withZwsp);

        result.ShouldBe("BANCO");
    }

    [Fact]
    public void Normalize_WordJoiner_IsRemoved()
    {
        var withWordJoiner = "ban⁠co";

        var result = VecTextMatcher.Normalize(withWordJoiner);

        result.ShouldBe("BANCO");
    }

    // -----------------------------------------------------------------------
    // Normalize — ligature folding
    // -----------------------------------------------------------------------

    [Fact]
    public void Normalize_LigatureFi_FoldedToFi()
    {
        // ﬁ U+FB01 LATIN SMALL LIGATURE FI
        var result = VecTextMatcher.Normalize("ﬁnanciero");

        result.ShouldBe("FINANCIERO");
    }

    [Fact]
    public void Normalize_LigatureFl_FoldedToFl()
    {
        var result = VecTextMatcher.Normalize("ﬂujo");

        result.ShouldBe("FLUJO");
    }

    [Fact]
    public void Normalize_LigatureFf_FoldedToFf()
    {
        var result = VecTextMatcher.Normalize("ﬀicial");

        result.ShouldBe("FFICIAL");
    }

    [Fact]
    public void Normalize_LigatureFfi_FoldedToFfi()
    {
        var result = VecTextMatcher.Normalize("ﬃcio");

        result.ShouldBe("FFICIO");
    }

    [Fact]
    public void Normalize_LigatureFfl_FoldedToFfl()
    {
        var result = VecTextMatcher.Normalize("ﬄuente");

        result.ShouldBe("FFLUENTE");
    }

    [Fact]
    public void Normalize_LigatureAe_FoldedToAe()
    {
        var result = VecTextMatcher.Normalize("æquitas");

        result.ShouldBe("AEQUITAS");
    }

    [Fact]
    public void Normalize_LigatureOe_FoldedToOe()
    {
        var result = VecTextMatcher.Normalize("œuvre");

        result.ShouldBe("OEUVRE");
    }

    // -----------------------------------------------------------------------
    // Normalize — null / empty / whitespace
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Normalize_NullOrWhitespace_ReturnsEmpty(string? input)
    {
        VecTextMatcher.Normalize(input).ShouldBe(string.Empty);
    }

    // -----------------------------------------------------------------------
    // Similarity — identical strings
    // -----------------------------------------------------------------------

    [Fact]
    public void Similarity_IdenticalStrings_ReturnsOne()
    {
        VecTextMatcher.Similarity("HOLA MUNDO", "HOLA MUNDO").ShouldBe(1.0);
    }

    [Fact]
    public void Similarity_IdenticalAfterNormalization_ReturnsOne()
    {
        VecTextMatcher.Similarity("hólá múndó", "HOLA MUNDO").ShouldBe(1.0);
    }

    // -----------------------------------------------------------------------
    // Similarity — both / one empty
    // -----------------------------------------------------------------------

    [Fact]
    public void Similarity_BothNull_ReturnsOne()
    {
        VecTextMatcher.Similarity(null, null).ShouldBe(1.0);
    }

    [Fact]
    public void Similarity_BothEmpty_ReturnsOne()
    {
        VecTextMatcher.Similarity(string.Empty, string.Empty).ShouldBe(1.0);
    }

    [Fact]
    public void Similarity_BothWhitespace_ReturnsOne()
    {
        VecTextMatcher.Similarity("   ", "\t\n").ShouldBe(1.0);
    }

    [Fact]
    public void Similarity_ActualNullExpectedNotEmpty_ReturnsZero()
    {
        VecTextMatcher.Similarity(null, "LEYENDA").ShouldBe(0.0);
    }

    [Fact]
    public void Similarity_ActualNotEmptyExpectedNull_ReturnsZero()
    {
        VecTextMatcher.Similarity("LEYENDA", null).ShouldBe(0.0);
    }

    // -----------------------------------------------------------------------
    // Similarity — token reordering
    // -----------------------------------------------------------------------

    [Fact]
    public void Similarity_TokensReordered_StillHighScore()
    {
        // Jaccard over the token sets should be 1.0 when same tokens, different order.
        var score = VecTextMatcher.Similarity(
            "CONSULTA TU SALDO EN LINEA",
            "EN LINEA CONSULTA TU SALDO");

        score.ShouldBe(1.0, tolerance: 0.001);
    }

    [Fact]
    public void Similarity_PartialTokenOverlap_ScoreInRange()
    {
        // 3 out of 4 tokens match → Jaccard = 3/4 = 0.75 (or higher via Levenshtein).
        var score = VecTextMatcher.Similarity(
            "COMPARA TU TARJETA AQUI",
            "COMPARA TU TARJETA ONLINE");

        score.ShouldBeGreaterThanOrEqualTo(0.70);
        score.ShouldBeLessThan(1.0);
    }

    // -----------------------------------------------------------------------
    // Similarity — near-miss below threshold
    // -----------------------------------------------------------------------

    [Fact]
    public void Similarity_NearMiss_ScoreBelowStrictThreshold()
    {
        // Completely different strings should score low.
        var score = VecTextMatcher.Similarity("SALDO TOTAL", "NUMERO DE CUENTA");

        score.ShouldBeLessThan(0.5);
    }

    // -----------------------------------------------------------------------
    // Matches — convenience wrapper
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_IdenticalStrings_AtThreshold1_0_ReturnsTrue()
    {
        VecTextMatcher.Matches("CONDUSEF", "CONDUSEF", 1.0).ShouldBeTrue();
    }

    [Fact]
    public void Matches_NearMatch_AtHighThreshold_ReturnsFalse()
    {
        // One character different in a 3-char string → ratio = 1 - 1/8 = 0.875.
        // "CONDUSEF" vs "CONDUSEV" — edit distance 1, max len 8 → Levenshtein 0.875.
        // At threshold 0.99 this should fail.
        VecTextMatcher.Matches("CONDUSEF", "CONDUSEV", 0.99).ShouldBeFalse();
    }

    [Fact]
    public void Matches_SameTokensDifferentOrder_AtThreshold0_9_ReturnsTrue()
    {
        VecTextMatcher.Matches(
            "TARJETA DE CREDITO CONDUSEF",
            "CONDUSEF TARJETA DE CREDITO",
            0.9).ShouldBeTrue();
    }

    [Fact]
    public void Matches_ThresholdOutOfRange_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            VecTextMatcher.Matches("a", "b", 1.1));
    }

    [Fact]
    public void Matches_ThresholdNegative_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            VecTextMatcher.Matches("a", "b", -0.1));
    }

    // -----------------------------------------------------------------------
    // Realistic CONDUSEF legend — §11 URL comparador.banxico.org.mx
    // -----------------------------------------------------------------------

    [Fact]
    public void Similarity_BanxioUrlWithStraySpacingAndCase_HighScore()
    {
        // Extracted from PDF with extra space and mixed case; canonical form is clean.
        const string extracted = "comparador .banxico.org .mx";
        const string canonical = "comparador.banxico.org.mx";

        // After normalization both become "COMPARADOR .BANXICO.ORG .MX" vs
        // "COMPARADOR.BANXICO.ORG.MX". Levenshtein distance = 2 (two spaces removed),
        // max len = 27 → ratio = 1 - 2/27 ≈ 0.926.
        var score = VecTextMatcher.Similarity(extracted, canonical);

        score.ShouldBeGreaterThanOrEqualTo(0.85);
    }

    [Fact]
    public void Matches_BanxioUrlWithSoftHyphen_AtThreshold0_85_ReturnsTrue()
    {
        // PDF inserts a soft hyphen mid-word; canonical has none.
        var extracted = "comparador.ban­xico.org.mx";
        const string canonical = "comparador.banxico.org.mx";

        VecTextMatcher.Matches(extracted, canonical, 0.85).ShouldBeTrue();
    }

    [Fact]
    public void Matches_BanxioUrlWithLigatureFi_AtThreshold0_85_ReturnsTrue()
    {
        // Edge case: ligature fi in a URL extracted from an unusual PDF font.
        var extracted = "comparador.banﬁco.org.mx";   // banﬁco
        const string canonical = "comparador.banfico.org.mx";

        VecTextMatcher.Matches(extracted, canonical, 0.85).ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // Accent + ligature combined
    // -----------------------------------------------------------------------

    [Fact]
    public void Similarity_AccentAndLigatureCombined_FullMatch()
    {
        // ﬁ + accent: "ﬁnancïero" should fully match "financiero".
        var score = VecTextMatcher.Similarity("ﬁnancïero", "financiero");

        score.ShouldBe(1.0, tolerance: 0.001);
    }

    // -----------------------------------------------------------------------
    // R2 (Finding 5): punctuation folding — curly quotes → straight,
    // N/A → NA normalization.
    // These tests verify the PunctuationFoldMap in VecTextMatcher.Normalize().
    // -----------------------------------------------------------------------

    [Fact]
    public void Normalize_LeftDoubleQuote_FoldedToStraight()
    {
        // U+201C " LEFT DOUBLE QUOTATION MARK — common in Word-generated PDFs.
        var result = VecTextMatcher.Normalize("“GLOSARIO”");

        result.ShouldBe("\"GLOSARIO\"",
            "U+201C/U+201D must fold to straight ASCII double-quote.");
    }

    [Fact]
    public void Normalize_RightDoubleQuote_FoldedToStraight()
    {
        // U+201D " RIGHT DOUBLE QUOTATION MARK.
        var result = VecTextMatcher.Normalize("texto ” fin");

        result.ShouldBe("TEXTO \" FIN");
    }

    [Fact]
    public void Normalize_LeftSingleQuote_FoldedToStraight()
    {
        // U+2018 ' LEFT SINGLE QUOTATION MARK.
        var result = VecTextMatcher.Normalize("l’amore");

        result.ShouldBe("L'AMORE");
    }

    [Fact]
    public void Normalize_RightSingleQuote_FoldedToStraight()
    {
        // U+2019 ' RIGHT SINGLE QUOTATION MARK — apostrophe substitute.
        var result = VecTextMatcher.Normalize("don’t");

        result.ShouldBe("DON'T");
    }

    [Fact]
    public void Normalize_NSlashA_FoldsToNA()
    {
        // "N/A" in the catalog should normalize the same way as "NA" in a PDF
        // that omits the slash.
        var withSlash = VecTextMatcher.Normalize("N/A");
        var withoutSlash = VecTextMatcher.Normalize("NA");

        withSlash.ShouldBe(withoutSlash,
            "N/A and NA must normalize to the same string so §27 term-g matching works.");
    }

    [Fact]
    public void Normalize_LowercaseNSlashA_FoldsToNA()
    {
        // Lower-case "n/a" should also fold.
        var result = VecTextMatcher.Normalize("n/a");

        result.ShouldBe("NA");
    }

    [Fact]
    public void Similarity_CurlyQuoteInDocument_MatchesStraightQuoteInCatalog()
    {
        // §17-legend text in catalog uses straight quotes; PDF has curly.
        // Both must normalize to the same and Similarity → 1.0.
        const string catalog = "Consulta la sección \"SALDO SOBRE EL QUE SE CALCULARON LOS INTERESES\"";
        var extracted = "Consulta la sección “SALDO SOBRE EL QUE SE CALCULARON LOS INTERESES”";

        var score = VecTextMatcher.Similarity(extracted, catalog);

        score.ShouldBe(1.0, tolerance: 0.001,
            "Curly-quote PDF vs straight-quote catalog must fully match after folding.");
    }

    [Fact]
    public void Normalize_CurlyQuoteMixedWithLigature_BothFolded()
    {
        // Both ligature fi (U+FB01) and curly quote (U+201C) present.
        var result = VecTextMatcher.Normalize("“ﬁnanciero”");

        result.ShouldBe("\"FINANCIERO\"",
            "Ligature fold + punctuation fold must both apply in sequence.");
    }
}
