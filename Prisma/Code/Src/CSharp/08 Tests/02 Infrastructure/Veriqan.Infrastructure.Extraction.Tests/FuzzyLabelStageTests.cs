using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Unit tests for the pure, PdfPig-independent matching/parsing core of
/// <see cref="FuzzyLabelStage{TValue}"/> — <see cref="LabelWindowMatcher"/> — exercised directly
/// against hand-constructed <see cref="WordSpan"/> lists so the honesty/abstention paths can be
/// verified without building real PDF fixtures.
/// </summary>
public sealed class FuzzyLabelStageTests
{
    private static readonly string[] PaymentDueDateAliases =
    [
        "fecha limite de pago",
        "fecha de pago",
        "pagar antes del",
        "fecha limite pago",
    ];

    private static bool ParseDate(string bandText, out DateOnly value) =>
        StatementValueParsers.TryParseSpanishDate(bandText, out value);

    private static WordSpan Word(string text, double left, double bottom, double width = 40, double height = 10) =>
        new(text, left, bottom, left + width, bottom + height);

    // -----------------------------------------------------------------------
    // LabelWindowMatcher.FindMatches
    // -----------------------------------------------------------------------

    [Fact]
    public void FindMatches_AccentedReflownLabel_ScoresAboveThreshold()
    {
        // "Fecha Límite Para Pago" — an accented, reflown variant of "Fecha límite de pago" that
        // the positional extractor's exact 4-token match would miss (no "de", different word).
        var words = new List<WordSpan>
        {
            Word("Fecha", 100, 500),
            Word("Límite", 145, 500),
            Word("Para", 195, 500),
            Word("Pago", 235, 500),
            Word("lunes,", 300, 500),
            Word("25-ago-2025", 350, 500),
        };

        var matches = LabelWindowMatcher.FindMatches(pageNumber: 1, words, PaymentDueDateAliases, scoreThreshold: 80).ToList();

        matches.ShouldNotBeEmpty();
        matches.Any(m => m.Score >= 80).ShouldBeTrue();
    }

    [Fact]
    public void FindMatches_NoAliasClearsThreshold_ReturnsEmpty()
    {
        var words = new List<WordSpan>
        {
            Word("Saldo", 100, 500),
            Word("Deudor", 150, 500),
            Word("Total", 200, 500),
        };

        var matches = LabelWindowMatcher.FindMatches(1, words, PaymentDueDateAliases, scoreThreshold: 80);

        matches.ShouldBeEmpty();
    }

    // -----------------------------------------------------------------------
    // LabelWindowMatcher.TryResolveBand
    // -----------------------------------------------------------------------

    [Fact]
    public void TryResolveBand_LabelFollowedByValidDate_RecoversDate()
    {
        var words = new List<WordSpan>
        {
            Word("Fecha", 100, 500),
            Word("límite", 145, 500),
            Word("de", 195, 500),
            Word("pago", 225, 500),
            Word("lunes,", 300, 500), // day-name token, must be filtered out
            Word("25-ago-2025", 350, 500),
        };

        var match = new LabelMatch(PageNumber: 1, WindowStart: 0, WindowLength: 4, Score: 100);

        var candidate = LabelWindowMatcher.TryResolveBand<DateOnly>(
            words, match, ParseDate, PaymentDueDatePlausibilityValidator.IsPlausible);

        candidate.HasValue.ShouldBeTrue();
        candidate.Value.ShouldBe(new DateOnly(2025, 8, 25));
        candidate.Stage.ShouldBe(StageId.FuzzyLabel);
        candidate.Score.ShouldBe(1.0);
    }

    [Fact]
    public void TryResolveBand_FootnoteMarkerBeforeDate_SkipsFootnoteAndRecovers()
    {
        // "Fecha límite de pago 1 25-ago-2025" — "1" is a superscript footnote marker.
        var words = new List<WordSpan>
        {
            Word("Fecha", 100, 500),
            Word("límite", 145, 500),
            Word("de", 195, 500),
            Word("pago", 225, 500),
            Word("1", 260, 500, width: 6),
            Word("25-ago-2025", 300, 500),
        };

        var match = new LabelMatch(1, 0, 4, 100);

        var candidate = LabelWindowMatcher.TryResolveBand<DateOnly>(
            words, match, ParseDate, PaymentDueDatePlausibilityValidator.IsPlausible);

        candidate.HasValue.ShouldBeTrue();
        candidate.Value.ShouldBe(new DateOnly(2025, 8, 25));
    }

    [Fact]
    public void TryResolveBand_LabelWithNoTrailingValue_ReturnsNone()
    {
        // The matched label is the last thing on the band — nothing to its right to parse.
        var words = new List<WordSpan>
        {
            Word("Fecha", 100, 500),
            Word("límite", 145, 500),
            Word("de", 195, 500),
            Word("pago", 225, 500),
        };

        var match = new LabelMatch(1, 0, 4, 100);

        var candidate = LabelWindowMatcher.TryResolveBand<DateOnly>(
            words, match, ParseDate, PaymentDueDatePlausibilityValidator.IsPlausible);

        candidate.HasValue.ShouldBeFalse();
        candidate.Stage.ShouldBe(StageId.FuzzyLabel);
    }

    [Fact]
    public void TryResolveBand_TrailingProseInsteadOfDate_AbstainsRatherThanFabricate()
    {
        // Mirrors the real good.pdf glossary sentence "Fecha límite para realizar el pago de la
        // tarjeta." — a fuzzy match against "fecha limite de pago" scores high, but the words to
        // the right of the matched window are prose, not a date. Must abstain, not fabricate.
        var words = new List<WordSpan>
        {
            Word("Fecha", 100, 500),
            Word("límite", 145, 500),
            Word("para", 195, 500),
            Word("realizar", 235, 500),
            Word("el", 300, 500),
            Word("pago", 330, 500),
            Word("de", 380, 500),
            Word("la", 410, 500),
        };

        var match = new LabelMatch(1, 0, 4, 85); // "Fecha límite para realizar" window

        var candidate = LabelWindowMatcher.TryResolveBand<DateOnly>(
            words, match, ParseDate, PaymentDueDatePlausibilityValidator.IsPlausible);

        candidate.HasValue.ShouldBeFalse();
    }

    [Fact]
    public void TryResolveBand_ImplausibleDate_Abstains()
    {
        var words = new List<WordSpan>
        {
            Word("Fecha", 100, 500),
            Word("límite", 145, 500),
            Word("de", 195, 500),
            Word("pago", 225, 500),
            Word("01-ene-1999", 300, 500), // outside the plausible sanity window
        };

        var match = new LabelMatch(1, 0, 4, 100);

        var candidate = LabelWindowMatcher.TryResolveBand<DateOnly>(
            words, match, ParseDate, PaymentDueDatePlausibilityValidator.IsPlausible);

        candidate.HasValue.ShouldBeFalse();
    }

    [Fact]
    public void TryResolveBand_NoPlausibilityGate_AcceptsAnyParseableDate()
    {
        // isPlausible: null must mean "no extra gate" — parse success alone is sufficient.
        var words = new List<WordSpan>
        {
            Word("Fecha", 100, 500),
            Word("límite", 145, 500),
            Word("de", 195, 500),
            Word("pago", 225, 500),
            Word("01-ene-1999", 300, 500),
        };

        var match = new LabelMatch(1, 0, 4, 100);

        var candidate = LabelWindowMatcher.TryResolveBand<DateOnly>(words, match, ParseDate, isPlausible: null);

        candidate.HasValue.ShouldBeTrue();
        candidate.Value.ShouldBe(new DateOnly(1999, 1, 1));
    }

    // -----------------------------------------------------------------------
    // AccentFolding
    // -----------------------------------------------------------------------

    [Fact]
    public void AccentFolding_FoldAndLower_StripsAccentsAndLowercases()
    {
        AccentFolding.FoldAndLower("FECHA LÍMITE DE PAGO").ShouldBe("fecha limite de pago");
        AccentFolding.FoldAndLower("Número").ShouldBe("numero");
    }

    // -----------------------------------------------------------------------
    // FuzzyLabelStage<TValue> — construction guards
    // -----------------------------------------------------------------------

    [Fact]
    public void Constructor_EmptyAliasList_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new FuzzyLabelStage<DateOnly>(Array.Empty<string>(), ParseDate));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Constructor_ScoreThresholdOutOfRange_Throws(int threshold)
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new FuzzyLabelStage<DateOnly>(PaymentDueDateAliases, ParseDate, scoreThreshold: threshold));
    }

    [Fact]
    public void Stage_IsAlwaysFuzzyLabel()
    {
        var stage = new FuzzyLabelStage<DateOnly>(PaymentDueDateAliases, ParseDate);

        stage.Stage.ShouldBe(StageId.FuzzyLabel);
    }

    // -----------------------------------------------------------------------
    // FuzzyLabelStage<TValue>.TryResolveAsync — real-PDF integration smoke test
    // -----------------------------------------------------------------------

    /// <summary>
    /// Runs the full PdfPig-integrated stage against the real demo corpus. This is a smoke test,
    /// not a value-pinning test: the demo corpus's page-1 layout does not print a labeled
    /// "Fecha límite de pago" value anywhere (verified by direct PdfPig-coordinate inspection —
    /// only glossary/footnote prose reuses the label's wording), so the honest, correct outcome on
    /// these fixtures is abstention. This test asserts the stage never throws, never fabricates an
    /// implausible value, and surfaces whatever it actually found via a diagnostic message.
    /// </summary>
    [Theory]
    [InlineData("good.pdf")]
    [InlineData("compliant-master.pdf")]
    public async Task TryResolveAsync_RealDemoFixture_NeverFabricatesAndReportsOutcome(string fixtureName)
    {
        var ct = TestContext.Current.CancellationToken;
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "demo", fixtureName);
        if (!File.Exists(path))
            Assert.Skip($"Demo corpus fixture not found at: {path}");

        var pdf = await File.ReadAllBytesAsync(path, ct);
        using var corpus = new LazyPdfCorpus(pdf);
        var stage = new FuzzyLabelStage<DateOnly>(
            PaymentDueDateAliases, ParseDate, PaymentDueDatePlausibilityValidator.IsPlausible);
        var context = new FieldResolutionContext<DateOnly>(
            FieldKind.PaymentDueDate, pdf, corpus, Array.Empty<FieldCandidate<DateOnly>>(), new StageBudget());

        var result = await stage.TryResolveAsync(context, ct);

        result.IsSuccess.ShouldBeTrue();
        var candidate = result.Value!;

        if (candidate.HasValue)
        {
            PaymentDueDatePlausibilityValidator.IsPlausible(candidate.Value).ShouldBeTrue();
            TestContext.Current.SendDiagnosticMessage(
                $"[E2.2] {fixtureName}: FuzzyLabelStage recovered PaymentDueDate={candidate.Value:yyyy-MM-dd} "
                + $"(score {candidate.Score:0.00}).");
        }
        else
        {
            TestContext.Current.SendDiagnosticMessage(
                $"[E2.2] {fixtureName}: FuzzyLabelStage abstained (no plausible labeled value found in the corpus).");
        }
    }
}
