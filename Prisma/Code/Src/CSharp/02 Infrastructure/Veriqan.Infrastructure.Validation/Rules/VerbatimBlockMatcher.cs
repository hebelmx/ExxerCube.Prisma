using System;
using System.Collections.Generic;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;

// RC1.S4.a (§26 verbatim matcher, real-corpus triage class c): when a block IS present but
// not a literal substring — multi-column PdfPig word interleaving defeats the substring
// fast-path — the WindowFactor=1.5 window mathematically dilutes VecTextMatcher.Similarity's
// Jaccard/Levenshtein scoring (extra interleaved neighbor tokens inflate the union/edit-distance)
// to a ceiling of ~0.67-0.8, below the 0.82 threshold, even for a near-verbatim block (e.g. §26-b
// differing by one word: "continuarían" vs "continuarán"). The fix does NOT lower the threshold —
// it adds TokenSequenceLcsRatio, an order-preserving longest-common-subsequence ratio over the
// EXPECTED block's own token count (not the union), so interleaved neighbor tokens never dilute
// the score — only genuinely missing/substituted expected tokens do. A single substituted word
// among ~80 tokens still yields ratio ≈ (n-1)/n ≈ 0.99, clearing 0.82. A genuinely absent block
// (e.g. §27 glossary, confirmed absent from the text layer) has almost none of its tokens present
// in any window, so the ratio stays low — LCS length is bounded above by min(|window|, |expected|)
// filtered tokens, and short common Spanish function words (≤2 chars: "DE", "LA", "EL", "EN", …)
// are excluded from the LCS token stream specifically to avoid a spurious high ratio from
// coincidental stop-word alignment in an unrelated window.

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Validation.Rules;

/// <summary>
/// Sliding-window helper that finds the best similarity score for a short expected verbatim
/// block inside a long normalized document text.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="VecTextMatcher.Similarity"/> is designed for two strings of similar length.
/// When comparing a short expected block (e.g. a 50-word legal legend) against a full-document
/// string of thousands of words, the Jaccard denominator is dominated by the document vocabulary
/// and the score collapses to near-zero even when the block is present.
/// </para>
/// <para>
/// This helper slices the (already-normalized) document text into overlapping windows whose
/// character length is proportional to the expected block, scores each window with
/// <see cref="VecTextMatcher.Similarity"/>, and returns the maximum score.  The window
/// boundary is character-aligned to a space so tokens are never split.
/// </para>
/// <para>
/// For blocks up to ~200 chars, the fast path uses a plain <c>Contains</c> check first —
/// if the normalized block is a literal substring of the document the score is immediately
/// returned as 1.0 without computing windows.
/// </para>
/// </remarks>
internal static class VerbatimBlockMatcher
{
    /// <summary>
    /// Window size multiplier: the window spans <c>expected.Length * WindowFactor</c>
    /// characters.  A factor of 1.5 captures line-wrap artefacts and minor insertions
    /// (superscript brackets, footnote numbers) without inflating the Jaccard denominator
    /// too much.
    /// </summary>
    private const double WindowFactor = 1.5;

    /// <summary>
    /// Slide step as a fraction of window size.  0.25 means 75 % overlap between
    /// adjacent windows — denser coverage than 50% so long blocks embedded in the middle
    /// of a section reliably find their best alignment.  Section-scoping (R2) keeps the
    /// search space small, so the extra cost of denser sliding is acceptable.
    /// </summary>
    private const double SlideStep = 0.25;

    /// <summary>
    /// Returns the best similarity score in <c>[0.0, 1.0]</c> for <paramref name="expectedNormalized"/>
    /// found anywhere inside <paramref name="documentNormalized"/>.
    /// </summary>
    /// <param name="documentNormalized">
    /// The full normalized statement text (<see cref="StatementModel.NormalizedFullText"/>
    /// or a section-bounded subset).  Must already be normalized via
    /// <see cref="VecTextMatcher.Normalize"/>.
    /// </param>
    /// <param name="expectedNormalized">
    /// The normalized expected verbatim block.  Must already be normalized via
    /// <see cref="VecTextMatcher.Normalize"/>.
    /// </param>
    /// <returns>
    /// Best window similarity score; <c>0.0</c> when either input is empty;
    /// <c>1.0</c> when <paramref name="expectedNormalized"/> is a literal substring of
    /// <paramref name="documentNormalized"/>.
    /// </returns>
    public static double BestWindowSimilarity(string documentNormalized, string expectedNormalized)
    {
        if (string.IsNullOrEmpty(documentNormalized) || string.IsNullOrEmpty(expectedNormalized))
            return 0.0;

        // Fast path: exact normalized substring match.
        if (documentNormalized.Contains(expectedNormalized, StringComparison.Ordinal))
            return 1.0;

        // If expected is longer than the document, fall back to full-string similarity.
        if (expectedNormalized.Length >= documentNormalized.Length)
            return CombinedScore(documentNormalized, expectedNormalized);

        var windowLen = (int)(expectedNormalized.Length * WindowFactor);
        // Cap window at document length.
        if (windowLen >= documentNormalized.Length)
            return CombinedScore(documentNormalized, expectedNormalized);

        var step = Math.Max(1, (int)(windowLen * SlideStep));
        var best = 0.0;

        for (var start = 0; start + windowLen <= documentNormalized.Length; start += step)
        {
            // Expand to a word boundary: walk right until we hit a space or document end.
            var end = start + windowLen;
            while (end < documentNormalized.Length && documentNormalized[end] != ' ')
                end++;

            var window = documentNormalized.AsSpan(start, end - start).ToString();
            var score = CombinedScore(window, expectedNormalized);
            if (score > best)
                best = score;

            // Early-exit when near-perfect match found.
            if (best >= 0.995)
                return best;
        }

        // Cover the final tail window that the step may have missed.
        var tailStart = documentNormalized.Length - windowLen;
        if (tailStart > 0)
        {
            var tail = documentNormalized[tailStart..];
            var tailScore = CombinedScore(tail, expectedNormalized);
            if (tailScore > best)
                best = tailScore;
        }

        return best;
    }

    /// <summary>
    /// Evaluates a list of (id, expectedNormalized) blocks against the document text and
    /// returns the ids + scores for any block that does not meet <paramref name="threshold"/>.
    /// </summary>
    /// <param name="documentNormalized">Full normalized document text.</param>
    /// <param name="blocks">Blocks to check: (blockId, normalizedExpectedText).</param>
    /// <param name="threshold">Minimum acceptable similarity.</param>
    /// <returns>
    /// List of (blockId, score) for every block whose best score is below <paramref name="threshold"/>.
    /// Empty when all blocks pass.
    /// </returns>
    public static List<(string BlockId, double Score)> FindFailingBlocks(
        string documentNormalized,
        IReadOnlyList<(string Id, string NormalizedText)> blocks,
        double threshold)
    {
        var failing = new List<(string BlockId, double Score)>();
        foreach (var (id, normText) in blocks)
        {
            var score = BestWindowSimilarity(documentNormalized, normText);
            if (score < threshold)
                failing.Add((id, score));
        }

        return failing;
    }

    /// <summary>
    /// Multi-candidate variant of <see cref="FindFailingBlocks"/> (RC1.S4.a, Fix 2b): each block
    /// may declare one or more accepted normalized-text candidates (the DOF original plus any
    /// owner-ruled accepted variants, e.g. §26-i's "compras y cargos regulares" wording). A block
    /// passes when ANY of its candidates clears <paramref name="threshold"/>; the DOF original is
    /// still the canonical/primary candidate — variants only widen acceptance, they never replace it.
    /// </summary>
    /// <param name="documentNormalized">Full normalized document text.</param>
    /// <param name="blocks">Blocks to check: (blockId, list of normalized accepted-text candidates).</param>
    /// <param name="threshold">Minimum acceptable similarity.</param>
    /// <returns>
    /// List of (blockId, bestScore) for every block whose best score across all candidates is
    /// below <paramref name="threshold"/>. Empty when all blocks pass.
    /// </returns>
    public static List<(string BlockId, double Score)> FindFailingBlocksMultiCandidate(
        string documentNormalized,
        IReadOnlyList<(string Id, IReadOnlyList<string> NormalizedCandidates)> blocks,
        double threshold)
    {
        var failing = new List<(string BlockId, double Score)>();
        foreach (var (id, candidates) in blocks)
        {
            var best = 0.0;
            foreach (var candidate in candidates)
            {
                var score = BestWindowSimilarity(documentNormalized, candidate);
                if (score > best)
                    best = score;
                if (best >= threshold)
                    break;
            }

            if (best < threshold)
                failing.Add((id, best));
        }

        return failing;
    }

    // -----------------------------------------------------------------------
    // Combined scoring: VecTextMatcher.Similarity (Levenshtein/Jaccard) + a token-sequence
    // LCS ratio that is not length-penalized by interleaved neighbor tokens (RC1.S4.a Fix 2a).
    // -----------------------------------------------------------------------

    /// <summary>Minimum token length considered by <see cref="TokenSequenceLcsRatio"/> — filters
    /// out short, high-frequency Spanish function words ("DE", "LA", "EL", "EN", "SE", "SU", …)
    /// that could otherwise align coincidentally in an unrelated window and inflate the ratio.</summary>
    private const int MinLcsTokenLength = 3;

    private static double CombinedScore(string window, string expectedNormalized) =>
        Math.Max(
            VecTextMatcher.Similarity(window, expectedNormalized),
            TokenSequenceLcsRatio(window, expectedNormalized));

    /// <summary>
    /// Order-preserving longest-common-subsequence ratio over whitespace-split tokens, normalized
    /// by the EXPECTED block's own (filtered) token count — never by the union with the window's
    /// tokens. Extra tokens interleaved into the window (from neighboring PDF columns) therefore
    /// never dilute the score; only expected tokens that are genuinely missing or substituted do.
    /// </summary>
    private static double TokenSequenceLcsRatio(string window, string expectedNormalized)
    {
        var windowTokens = FilteredTokens(window);
        var expectedTokens = FilteredTokens(expectedNormalized);
        if (expectedTokens.Count == 0)
            return 0.0;

        var lcsLength = LongestCommonSubsequenceLength(windowTokens, expectedTokens);
        return (double)lcsLength / expectedTokens.Count;
    }

    private static List<string> FilteredTokens(string text)
    {
        var raw = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var filtered = new List<string>(raw.Length);
        foreach (var token in raw)
        {
            if (token.Length >= MinLcsTokenLength)
                filtered.Add(token);
        }

        return filtered;
    }

    /// <summary>
    /// Classic dynamic-programming longest-common-subsequence length over two token lists,
    /// space-optimized to two rolling rows (O(min(|a|,|b|)) space, O(|a|·|b|) time). Token lists
    /// here are bounded by a single verbatim block's word count (tens to low hundreds), so this
    /// is cheap even across the many overlapping windows <see cref="BestWindowSimilarity"/> scans.
    /// </summary>
    private static int LongestCommonSubsequenceLength(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (a.Count == 0 || b.Count == 0)
            return 0;

        var prev = new int[b.Count + 1];
        var curr = new int[b.Count + 1];

        for (var i = 1; i <= a.Count; i++)
        {
            curr[0] = 0;
            for (var j = 1; j <= b.Count; j++)
            {
                curr[j] = string.Equals(a[i - 1], b[j - 1], StringComparison.Ordinal)
                    ? prev[j - 1] + 1
                    : Math.Max(prev[j], curr[j - 1]);
            }

            (prev, curr) = (curr, prev);
        }

        return prev[b.Count];
    }
}
