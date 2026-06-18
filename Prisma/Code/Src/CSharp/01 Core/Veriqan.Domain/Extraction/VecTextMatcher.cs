using System;
using System.Collections.Generic;

namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Shared tolerant-similarity matcher for CONDUSEF-mandated verbatim text blocks.
/// </summary>
/// <remarks>
/// <para>
/// Exact string equality MUST NOT be used for regulatory legend verification because
/// PDF draw-order, soft-hyphens, ligatures, and line-wrap artefacts all produce textually
/// different strings that are legally equivalent. This class provides a configurable
/// similarity gate instead.
/// </para>
/// <para>
/// Normalization pipeline (applied to both operands before scoring):
/// <list type="number">
///   <item>Fold common Unicode ligatures to their ASCII equivalents
///         (ﬁ→fi, ﬂ→fl, ﬀ→ff, ﬃ→ffi, ﬄ→ffl, æ→ae, Æ→AE, œ→oe, Œ→OE).</item>
///   <item>Remove invisible separators: soft hyphen (U+00AD), zero-width space (U+200B),
///         word-joiner (U+2060).</item>
///   <item>Delegate to <see cref="VecTextNormalizer.Normalize"/> which upper-cases, strips
///         diacritics (NFD + remove non-spacing marks), and collapses whitespace.</item>
/// </list>
/// </para>
/// <para>
/// Similarity score is the maximum of:
/// <list type="bullet">
///   <item><em>Normalized Levenshtein ratio</em> — <c>1 - edit_distance / max(|a|, |b|)</c>.
///         Handles minor character-level noise.</item>
///   <item><em>Token-overlap ratio (Jaccard)</em> — <c>|intersection| / |union|</c> on the
///         whitespace-split token sets. Handles token reordering and line-wrapping.</item>
/// </list>
/// The max ensures that both locally-close and structurally-equivalent strings score well.
/// </para>
/// <para>
/// Memory bound: Levenshtein uses the two-row O(min(|a|,|b|)) variant so very long strings
/// (e.g. multi-paragraph legends) do not allocate an m×n matrix.
/// </para>
/// <para>
/// Edge cases:
/// <list type="bullet">
///   <item>Both operands null/empty/whitespace → similarity <c>1.0</c> (vacuously identical).</item>
///   <item>Exactly one operand null/empty/whitespace → similarity <c>0.0</c>.</item>
/// </list>
/// </para>
/// </remarks>
public static class VecTextMatcher
{
    // ------------------------------------------------------------------
    // Ligature / invisible-separator tables
    // ------------------------------------------------------------------

    /// <summary>
    /// Ligature substitution pairs applied before the shared normalizer.
    /// Both lower and upper forms are listed because upper-casing happens
    /// inside <see cref="VecTextNormalizer.Normalize"/>; we fold here first
    /// so the normalizer sees plain ASCII.
    /// </summary>
    private static readonly (string From, string To)[] LigatureMap =
    [
        ("ﬀ", "ff"),   // ﬀ LATIN SMALL LIGATURE FF
        ("ﬃ", "ffi"),  // ﬃ LATIN SMALL LIGATURE FFI
        ("ﬄ", "ffl"),  // ﬄ LATIN SMALL LIGATURE FFL
        ("ﬁ", "fi"),   // ﬁ LATIN SMALL LIGATURE FI
        ("ﬂ", "fl"),   // ﬂ LATIN SMALL LIGATURE FL
        ("æ", "ae"),   // æ LATIN SMALL LETTER AE
        ("Æ", "AE"),   // Æ LATIN CAPITAL LETTER AE
        ("œ", "oe"),   // œ LATIN SMALL LETTER OE
        ("Œ", "OE"),   // Œ LATIN CAPITAL LETTER OE
    ];

    // ------------------------------------------------------------------
    // Punctuation-folding table (matcher-level only — NOT shared with
    // VecTextNormalizer, so CL-32/CL-46 existing behaviour is unchanged).
    // Applied after ligature folding and before invisible-separator removal.
    // ------------------------------------------------------------------

    /// <summary>
    /// Punctuation substitutions applied only inside the matcher's own
    /// <see cref="Normalize"/> pipeline (not in <see cref="VecTextNormalizer"/>).
    /// <list type="bullet">
    ///   <item>Curly/smart double and single quotes → straight ASCII equivalents.
    ///     Handles PDF extraction that preserves typographic quotes.</item>
    ///   <item>"N/A" slash-normalization: the forward slash in "N/A" is stripped so
    ///     that "N/A" and "NA" both produce the token "NA" after collapsing.
    ///     Applied specifically as a targeted replacement to avoid stripping slashes
    ///     in URLs (which are matched separately, pre-normalized as whole strings).
    ///     Note: this ONLY affects the two-character pattern "N/A" (case-insensitive
    ///     after prior ligature folding makes everything pre-uppercase compatible).</item>
    /// </list>
    /// Conservative: only truly matching-irrelevant differences are folded.
    /// </summary>
    private static readonly (string From, string To)[] PunctuationFoldMap =
    [
        ("“", "\""),  // " LEFT DOUBLE QUOTATION MARK → straight "
        ("”", "\""),  // " RIGHT DOUBLE QUOTATION MARK → straight "
        ("‘", "'"),   // ' LEFT SINGLE QUOTATION MARK → straight '
        ("’", "'"),   // ' RIGHT SINGLE QUOTATION MARK → straight '
        // N/A normalisation: fold "N/A" → "NA" so catalog and PDF variants match.
        // Case-insensitive match is handled by doing both forms explicitly.
        ("N/A", "NA"),
        ("n/a", "na"),
    ];

    /// <summary>Invisible separators that are stripped before normalization.</summary>
    private static readonly char[] InvisibleSeparators =
    [
        '­', // SOFT HYPHEN
        '​', // ZERO WIDTH SPACE
        '⁠', // WORD JOINER
    ];

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>
    /// Normalizes <paramref name="text"/> with the extended pipeline required for
    /// tolerant verbatim matching (ligature folding + invisible-separator removal +
    /// shared <see cref="VecTextNormalizer"/> accent/case/whitespace normalization).
    /// </summary>
    /// <param name="text">Raw text, possibly containing PDF artefacts.</param>
    /// <returns>
    /// Fully normalized string ready for similarity scoring, or
    /// <see cref="string.Empty"/> when the input is <see langword="null"/> or whitespace.
    /// </returns>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Step 1: fold ligatures.
        var folded = text;
        foreach (var (from, to) in LigatureMap)
            folded = folded.Replace(from, to, StringComparison.Ordinal);

        // Step 2: fold matching-irrelevant punctuation (curly quotes, N/A slash).
        // This is applied BEFORE upper-casing so case-sensitive replacements are exact.
        foreach (var (from, to) in PunctuationFoldMap)
            folded = folded.Replace(from, to, StringComparison.Ordinal);

        // Step 3: remove invisible separators.
        foreach (var ch in InvisibleSeparators)
            folded = folded.Replace(ch.ToString(), string.Empty, StringComparison.Ordinal);

        // Step 4: delegate to the canonical normalizer (upper + accent-strip + whitespace collapse).
        return VecTextNormalizer.Normalize(folded);
    }

    /// <summary>
    /// Computes a similarity score in <c>[0.0, 1.0]</c> between two strings.
    /// </summary>
    /// <remarks>
    /// Both inputs are normalized via <see cref="Normalize"/> before scoring.
    /// The score is the maximum of the normalized Levenshtein ratio and the token-set
    /// Jaccard overlap so that both minor character noise and token reordering are
    /// tolerated.
    /// </remarks>
    /// <param name="actual">Text extracted from the document.</param>
    /// <param name="expected">Reference / canonical legend text.</param>
    /// <returns>
    /// Similarity score in <c>[0.0, 1.0]</c>.  Returns <c>1.0</c> when both operands
    /// normalize to empty.  Returns <c>0.0</c> when exactly one operand normalizes to empty.
    /// </returns>
    public static double Similarity(string? actual, string? expected)
    {
        var normActual = Normalize(actual);
        var normExpected = Normalize(expected);

        // Both empty → vacuously identical.
        if (normActual.Length == 0 && normExpected.Length == 0)
            return 1.0;

        // Exactly one empty → no similarity.
        if (normActual.Length == 0 || normExpected.Length == 0)
            return 0.0;

        // Exact match fast path.
        if (normActual == normExpected)
            return 1.0;

        var levenshtein = NormalizedLevenshteinRatio(normActual, normExpected);
        var jaccard = TokenSetJaccard(normActual, normExpected);
        return Math.Max(levenshtein, jaccard);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the similarity between <paramref name="actual"/>
    /// and <paramref name="expected"/> meets or exceeds <paramref name="threshold"/>.
    /// </summary>
    /// <param name="actual">Text extracted from the document.</param>
    /// <param name="expected">Reference / canonical legend text.</param>
    /// <param name="threshold">
    /// Caller-supplied minimum acceptable similarity in <c>[0.0, 1.0]</c>.
    /// Callers (e.g. tenant profile, rule configuration) own this value;
    /// it is intentionally not hardcoded here.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when <c>Similarity(actual, expected) &gt;= threshold</c>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="threshold"/> is outside <c>[0.0, 1.0]</c>.
    /// </exception>
    public static bool Matches(string? actual, string? expected, double threshold)
    {
        if (threshold is < 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(threshold),
                threshold, "Threshold must be in [0.0, 1.0].");

        return Similarity(actual, expected) >= threshold;
    }

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Normalized Levenshtein ratio: <c>1 - edit_distance / max(|a|, |b|)</c>.
    /// Uses the memory-efficient two-row variant (O(min(|a|, |b|)) space).
    /// </summary>
    private static double NormalizedLevenshteinRatio(string a, string b)
    {
        // Ensure a is the shorter string to minimise allocated array size.
        if (a.Length > b.Length)
            (a, b) = (b, a);

        var lenA = a.Length;
        var lenB = b.Length;

        // Two-row rolling array.
        var prev = new int[lenA + 1];
        var curr = new int[lenA + 1];

        for (var i = 0; i <= lenA; i++)
            prev[i] = i;

        for (var j = 1; j <= lenB; j++)
        {
            curr[0] = j;
            for (var i = 1; i <= lenA; i++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[i] = Math.Min(
                    Math.Min(prev[i] + 1,       // deletion
                             curr[i - 1] + 1),  // insertion
                    prev[i - 1] + cost);         // substitution
            }
            (prev, curr) = (curr, prev);
        }

        var editDistance = prev[lenA];
        return 1.0 - (double)editDistance / Math.Max(lenA, lenB);
    }

    /// <summary>
    /// Token-set Jaccard overlap: <c>|intersection| / |union|</c> on whitespace-split tokens.
    /// Handles token reordering and line-wrapping artefacts.
    /// </summary>
    private static double TokenSetJaccard(string a, string b)
    {
        var tokensA = new HashSet<string>(
            a.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            StringComparer.Ordinal);

        var tokensB = new HashSet<string>(
            b.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            StringComparer.Ordinal);

        if (tokensA.Count == 0 && tokensB.Count == 0)
            return 1.0;

        var intersection = 0;
        foreach (var token in tokensA)
        {
            if (tokensB.Contains(token))
                intersection++;
        }

        var union = tokensA.Count + tokensB.Count - intersection;
        return union == 0 ? 1.0 : (double)intersection / union;
    }
}
