using System;
using System.Collections.Generic;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;

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
            return VecTextMatcher.Similarity(documentNormalized, expectedNormalized);

        var windowLen = (int)(expectedNormalized.Length * WindowFactor);
        // Cap window at document length.
        if (windowLen >= documentNormalized.Length)
            return VecTextMatcher.Similarity(documentNormalized, expectedNormalized);

        var step = Math.Max(1, (int)(windowLen * SlideStep));
        var best = 0.0;

        for (var start = 0; start + windowLen <= documentNormalized.Length; start += step)
        {
            // Expand to a word boundary: walk right until we hit a space or document end.
            var end = start + windowLen;
            while (end < documentNormalized.Length && documentNormalized[end] != ' ')
                end++;

            var window = documentNormalized.AsSpan(start, end - start).ToString();
            var score = VecTextMatcher.Similarity(window, expectedNormalized);
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
            var tailScore = VecTextMatcher.Similarity(tail, expectedNormalized);
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
}
