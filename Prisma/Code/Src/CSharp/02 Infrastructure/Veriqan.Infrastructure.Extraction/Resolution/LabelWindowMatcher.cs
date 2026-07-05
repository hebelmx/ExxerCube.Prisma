using System;
using System.Collections.Generic;
using System.Linq;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using FuzzySharp;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// Minimal, PdfPig-independent projection of a single word's text and bounding box. Exists so the
/// pure matching/parsing algorithm in <see cref="LabelWindowMatcher"/> — the core of
/// <see cref="FuzzyLabelStage{TValue}"/> — can be unit-tested without constructing a real PDF
/// document.
/// </summary>
/// <param name="Text">The word's raw text, exactly as PdfPig tokenized it.</param>
/// <param name="Left">X coordinate of the left edge, in PDF points.</param>
/// <param name="Bottom">Y coordinate of the bottom edge, in PDF points.</param>
/// <param name="Right">X coordinate of the right edge, in PDF points.</param>
/// <param name="Top">Y coordinate of the top edge, in PDF points.</param>
internal readonly record struct WordSpan(string Text, double Left, double Bottom, double Right, double Top);

/// <summary>
/// A candidate label match: a window of <see cref="WindowLength"/> consecutive words on
/// <see cref="PageNumber"/>, starting at <see cref="WindowStart"/>, that scored <see cref="Score"/>
/// (0-100) against one of the configured label aliases.
/// </summary>
/// <param name="PageNumber">1-based page number the window was found on.</param>
/// <param name="WindowStart">Index, into that page's reading-order word list, of the window's first word.</param>
/// <param name="WindowLength">Number of consecutive words in the window (the matched alias's token count).</param>
/// <param name="Score">FuzzySharp partial-ratio score in [0, 100].</param>
internal readonly record struct LabelMatch(int PageNumber, int WindowStart, int WindowLength, int Score);

/// <summary>
/// Pure, PdfPig-independent core of the fuzzy label-anchor stage (<see cref="FuzzyLabelStage{TValue}"/>):
/// finds sliding-window fuzzy matches against a label-alias dictionary, then extracts and parses
/// the value band to the right of a matched label.
/// </summary>
/// <remarks>
/// Isolated from <see cref="FuzzyLabelStage{TValue}"/> (which owns the PdfPig/<see cref="LazyPdfCorpus"/>
/// integration) purely so this algorithm — including its abstention paths — is testable against
/// hand-constructed <see cref="WordSpan"/> lists instead of real PDF fixtures.
/// </remarks>
internal static class LabelWindowMatcher
{
    /// <summary>
    /// Y-coordinate tolerance (PDF points) used to group words extracted from the same page into
    /// a horizontal "band" (line), mirroring <c>PdfPigStatementFieldExtractor</c>'s own
    /// <c>YBandTolerance</c> convention.
    /// </summary>
    private const double YBandTolerance = 5.0;

    /// <summary>
    /// Slides a window of each alias's own token count across <paramref name="pageWords"/> (which
    /// must already be in reading order — top-to-bottom, then left-to-right) and yields every
    /// window whose FuzzySharp partial-ratio score against that alias clears
    /// <paramref name="scoreThreshold"/>.
    /// </summary>
    /// <param name="pageNumber">1-based page number <paramref name="pageWords"/> was read from.</param>
    /// <param name="pageWords">The page's words, in reading order.</param>
    /// <param name="foldedLabelAliases">
    /// Label aliases to match against, already accent-folded and lowercased (see
    /// <see cref="AccentFolding.FoldAndLower"/>) — every candidate window is folded the same way
    /// before scoring.
    /// </param>
    /// <param name="scoreThreshold">Minimum partial-ratio score (0-100) for a window to be yielded.</param>
    internal static IEnumerable<LabelMatch> FindMatches(
        int pageNumber,
        IReadOnlyList<WordSpan> pageWords,
        IReadOnlyList<string> foldedLabelAliases,
        int scoreThreshold)
    {
        foreach (var alias in foldedLabelAliases)
        {
            var tokenCount = alias.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            if (tokenCount == 0)
                continue;

            for (var i = 0; i + tokenCount <= pageWords.Count; i++)
            {
                var windowText = AccentFolding.FoldAndLower(
                    string.Join(" ", Enumerable.Range(i, tokenCount).Select(j => pageWords[j].Text)));

                var score = Fuzz.PartialRatio(windowText, alias);
                if (score >= scoreThreshold)
                    yield return new LabelMatch(pageNumber, i, tokenCount, score);
            }
        }
    }

    /// <summary>
    /// Attempts to extract and parse the value band to the right of a matched label window.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>PdfPigStatementFieldExtractor.ExtractFechaLimiteDePago</c>'s value-band
    /// heuristic: take the label's Y-band, keep words to the right of the label's right edge,
    /// drop single-digit footnote markers and day-name tokens ending in a comma, then try parsing
    /// both the concatenated and space-joined forms. Returns
    /// <see cref="FieldCandidate{TValue}.None"/> — never a fabricated value — when there is
    /// nothing to the right of the label, when <paramref name="parser"/> cannot parse either
    /// joined form, or when <paramref name="isPlausible"/> rejects the parsed value.
    /// </remarks>
    /// <typeparam name="TValue">The type of the field value.</typeparam>
    /// <param name="pageWords">All words on <see cref="LabelMatch.PageNumber"/>, in reading order.</param>
    /// <param name="match">The label window to resolve a value band for.</param>
    /// <param name="parser">Parses the joined value-band text into <typeparamref name="TValue"/>.</param>
    /// <param name="isPlausible">
    /// Optional stage-level sanity gate; when supplied, a value that parses successfully but fails
    /// this predicate is still treated as "not found" (honesty over recall).
    /// </param>
    internal static FieldCandidate<TValue> TryResolveBand<TValue>(
        IReadOnlyList<WordSpan> pageWords,
        LabelMatch match,
        BandValueParser<TValue> parser,
        Func<TValue, bool>? isPlausible)
    {
        var labelWords = Enumerable.Range(match.WindowStart, match.WindowLength)
            .Select(i => pageWords[i])
            .ToList();

        var bandY = labelWords[0].Bottom;
        var band = pageWords
            .Where(w => Math.Abs(w.Bottom - bandY) <= YBandTolerance)
            .OrderBy(w => w.Left)
            .ToList();

        var labelRight = labelWords[^1].Right;
        var valueWords = band
            .Where(w => w.Left > labelRight)
            .OrderBy(w => w.Left)
            .ToList();

        var locatorHint = BuildLocator(valueWords.Count > 0 ? valueWords : labelWords, match.PageNumber);

        if (valueWords.Count == 0)
            return FieldCandidate<TValue>.None(StageId.FuzzyLabel, locatorHint);

        var filteredTokens = valueWords
            .Select(w => w.Text)
            .Where(t => !IsSingleDigitFootnote(t))
            .Where(t => !t.EndsWith(",", StringComparison.Ordinal))
            .ToList();

        if (TryParseCandidate(filteredTokens, parser, isPlausible, out var value))
            return FieldCandidate<TValue>.Found(value, match.Score / 100.0, StageId.FuzzyLabel, locatorHint);

        return FieldCandidate<TValue>.None(StageId.FuzzyLabel, locatorHint);
    }

    private static bool TryParseCandidate<TValue>(
        List<string> tokens,
        BandValueParser<TValue> parser,
        Func<TValue, bool>? isPlausible,
        out TValue value)
    {
        var concatText = string.Concat(tokens).Trim();
        if (parser(concatText, out value) && IsAcceptable(value, isPlausible))
            return true;

        var spacedText = string.Join(" ", tokens).Trim();
        if (parser(spacedText, out value) && IsAcceptable(value, isPlausible))
            return true;

        value = default!;
        return false;
    }

    private static bool IsAcceptable<TValue>(TValue value, Func<TValue, bool>? isPlausible) =>
        isPlausible is null || isPlausible(value);

    private static bool IsSingleDigitFootnote(string text) =>
        text.Length == 1 && char.IsDigit(text[0]);

    private static FieldLocator BuildLocator(IReadOnlyList<WordSpan> words, int pageNumber)
    {
        if (words.Count == 0)
            return FieldLocator.PageHint(pageNumber);

        var left = words.Min(w => w.Left);
        var bottom = words.Min(w => w.Bottom);
        var right = words.Max(w => w.Right);
        var top = words.Max(w => w.Top);

        return new FieldLocator(pageNumber, left, bottom, right - left, top - bottom);
    }
}
