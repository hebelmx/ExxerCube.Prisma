using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// Parses the text found to the right of a matched label band into a field value.
/// </summary>
/// <typeparam name="TValue">The type of the field value.</typeparam>
/// <param name="bandText">
/// The concatenated (or space-joined) text of the words following the matched label.
/// </param>
/// <param name="value">The parsed value when this delegate returns <see langword="true"/>.</param>
/// <returns><see langword="true"/> when <paramref name="bandText"/> was successfully parsed.</returns>
public delegate bool BandValueParser<TValue>(string bandText, out TValue value);

/// <summary>
/// Stage 2 of the progressive fallback-extraction chain: locates a label phrase the positional
/// (stage-1) extractor missed by fuzzy-matching a configured set of label aliases against sliding
/// windows of the document's words, then reads the value band to its right (see
/// <c>docs/planning-artifacts/veriqan-fallback-chain-design-2026-07-05.md</c>, "PaymentDueDate:
/// Positional→fuzzy/Levenshtein, STOP").
/// </summary>
/// <remarks>
/// <para>
/// <b>Honesty discipline (cardinal rule — abstain, never fabricate):</b> this stage returns
/// <see cref="FieldCandidate{TValue}.None"/> — never a Found candidate — whenever: no label alias
/// clears the configured score threshold on any page; the words to the right of the best-matching
/// label do not parse via the configured <see cref="BandValueParser{TValue}"/>; or the parsed
/// value fails the optional plausibility gate. It does not rely on a downstream
/// validator/disagreement gate to catch a bad guess — the ladder-exhaustion abstention policy is
/// a known-open item (design doc forward item, "E3/E7 ladder-exhaustion honesty"), so
/// self-abstention here is how this stage stays honest independent of that policy.
/// </para>
/// <para>
/// When several label windows clear the score threshold (across all pages), candidates are tried
/// in descending score order; the first one whose value band parses and is plausible wins. This
/// lets the stage recover from a coincidental high-scoring match inside unrelated prose (e.g. a
/// CONDUSEF glossary sentence that happens to reuse the label's wording) as long as a genuine
/// labeled value exists elsewhere in the document.
/// </para>
/// <para>
/// The actual matching/parsing algorithm is implemented as a pure, PdfPig-independent method
/// (<see cref="LabelWindowMatcher"/>) operating on <see cref="WordSpan"/> — a minimal projection
/// of <c>UglyToad.PdfPig.Content.Word</c> — so it is unit-testable without constructing real PDF
/// fixtures. This class owns only the PdfPig/<see cref="LazyPdfCorpus"/> integration.
/// </para>
/// </remarks>
/// <typeparam name="TValue">The type of the field value this stage attempts to resolve.</typeparam>
public sealed class FuzzyLabelStage<TValue> : IFieldResolutionStage<TValue>
{
    /// <summary>
    /// Default FuzzySharp partial-ratio score (0-100) a label window must clear to be considered
    /// a candidate match.
    /// </summary>
    public const int DefaultScoreThreshold = 80;

    private readonly IReadOnlyList<string> _labelAliases;
    private readonly BandValueParser<TValue> _parser;
    private readonly Func<TValue, bool>? _isPlausible;
    private readonly int _scoreThreshold;

    /// <summary>
    /// Initializes a <see cref="FuzzyLabelStage{TValue}"/>.
    /// </summary>
    /// <param name="labelAliases">
    /// Label-phrasing aliases to fuzzy-match against, already accent-folded and lowercased (see
    /// <see cref="AccentFolding.FoldAndLower"/>). Must contain at least one alias.
    /// </param>
    /// <param name="parser">Parses the joined value-band text into <typeparamref name="TValue"/>.</param>
    /// <param name="isPlausible">
    /// Optional stage-level sanity gate. When supplied, a value that parses successfully but fails
    /// this predicate is treated as "not found" — the stage never emits an implausible value.
    /// </param>
    /// <param name="scoreThreshold">
    /// Minimum FuzzySharp partial-ratio score (0-100) a label window must clear to be attempted.
    /// Defaults to <see cref="DefaultScoreThreshold"/>.
    /// </param>
    public FuzzyLabelStage(
        IReadOnlyList<string> labelAliases,
        BandValueParser<TValue> parser,
        Func<TValue, bool>? isPlausible = null,
        int scoreThreshold = DefaultScoreThreshold)
    {
        ArgumentNullException.ThrowIfNull(labelAliases);
        if (labelAliases.Count == 0)
            throw new ArgumentException("At least one label alias is required.", nameof(labelAliases));
        ArgumentNullException.ThrowIfNull(parser);
        if (scoreThreshold is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(scoreThreshold), scoreThreshold, "Score threshold must be in [0, 100].");

        _labelAliases = labelAliases;
        _parser = parser;
        _isPlausible = isPlausible;
        _scoreThreshold = scoreThreshold;
    }

    /// <inheritdoc/>
    public StageId Stage => StageId.FuzzyLabel;

    /// <inheritdoc/>
    public Task<Result<FieldCandidate<TValue>>> TryResolveAsync(
        FieldResolutionContext<TValue> context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<FieldCandidate<TValue>>());

        try
        {
            var document = context.Corpus.Value;
            var pageWordsByPage = new Dictionary<int, IReadOnlyList<WordSpan>>();
            var matches = new List<LabelMatch>();

            for (var pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
            {
                var pageWords = document.GetPage(pageNumber).GetWords()
                    .OrderByDescending(w => w.BoundingBox.Bottom)
                    .ThenBy(w => w.BoundingBox.Left)
                    .Select(w => new WordSpan(w.Text, w.BoundingBox.Left, w.BoundingBox.Bottom, w.BoundingBox.Right, w.BoundingBox.Top))
                    .ToList();

                pageWordsByPage[pageNumber] = pageWords;
                matches.AddRange(LabelWindowMatcher.FindMatches(pageNumber, pageWords, _labelAliases, _scoreThreshold));
            }

            foreach (var match in matches
                .OrderByDescending(m => m.Score)
                .ThenBy(m => m.PageNumber)
                .ThenBy(m => m.WindowStart))
            {
                var candidate = LabelWindowMatcher.TryResolveBand(
                    pageWordsByPage[match.PageNumber], match, _parser, _isPlausible);

                if (candidate.HasValue)
                    return Task.FromResult(Result<FieldCandidate<TValue>>.WithSuccess(candidate));
            }

            return Task.FromResult(Result<FieldCandidate<TValue>>.WithSuccess(FieldCandidate<TValue>.None(Stage)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult(Result<FieldCandidate<TValue>>.WithFailure(
                $"FuzzyLabelStage<{typeof(TValue).Name}> failed while resolving {context.FieldKind}: {ex.Message}"));
        }
    }
}
