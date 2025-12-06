using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;
using FuzzySharp;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Imaging;

/// <summary>
/// Production-grade text comparison implementation using Levenshtein distance algorithm.
/// Provides OCR quality assessment through edit distance and quality score calculations.
/// </summary>
public class LevenshteinTextComparer : ITextComparer
{
    private readonly ILogger<LevenshteinTextComparer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LevenshteinTextComparer"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public LevenshteinTextComparer(ILogger<LevenshteinTextComparer> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public int CalculateEditDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
            return target?.Length ?? 0;

        if (string.IsNullOrEmpty(target))
            return source.Length;

        int m = source.Length;
        int n = target.Length;

        // Create distance matrix
        int[,] distance = new int[m + 1, n + 1];

        // Initialize first column (deletions from source)
        for (int i = 0; i <= m; i++)
            distance[i, 0] = i;

        // Initialize first row (insertions to source)
        for (int j = 0; j <= n; j++)
            distance[0, j] = j;

        // Calculate edit distance using dynamic programming
        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                int cost = (source[i - 1] == target[j - 1]) ? 0 : 1;

                distance[i, j] = Math.Min(
                    Math.Min(
                        distance[i - 1, j] + 1,      // Deletion
                        distance[i, j - 1] + 1),     // Insertion
                    distance[i - 1, j - 1] + cost);  // Substitution
            }
        }

        return distance[m, n];
    }

    /// <inheritdoc />
    public int CalculateFuzzyRatio(string source, string target)
    {
        if (string.IsNullOrEmpty(source) && string.IsNullOrEmpty(target))
            return 100; // Both empty = identical

        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
            return 0; // One empty = completely different

        // Use FuzzySharp.Fuzz.Ratio for fuzzy matching
        // Returns 0-100 where 100 is exact match
        int fuzzyScore = Fuzz.Ratio(source, target);

        _logger.LogDebug(
            "Fuzzy ratio calculated: {Score} for '{Source}' vs '{Target}'",
            fuzzyScore,
            source.Length > 50 ? source[..50] + "..." : source,
            target.Length > 50 ? target[..50] + "..." : target);

        return fuzzyScore;
    }

    /// <inheritdoc />
    public double CalculateSimilarity(string source, string target)
    {
        if (string.IsNullOrEmpty(source) && string.IsNullOrEmpty(target))
            return 1.0; // Both empty = identical

        int editDistance = CalculateEditDistance(source, target);
        int maxLength = Math.Max(source?.Length ?? 0, target?.Length ?? 0);

        if (maxLength == 0)
            return 1.0;

        // Similarity = 1 - (edit distance / max length)
        double similarity = 1.0 - ((double)editDistance / maxLength);
        return Math.Max(0.0, Math.Min(1.0, similarity)); // Clamp to [0, 1]
    }

    /// <inheritdoc />
    public double CalculateQualityScore(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        double score = 0;
        int totalChars = text.Length;

        // 1. Alphanumeric ratio (40% weight) - Good OCR has high alphanumeric content
        int alphanumericCount = text.Count(c => char.IsLetterOrDigit(c));
        double alphanumericRatio = (double)alphanumericCount / totalChars;
        score += alphanumericRatio * 40;

        // 2. Whitespace ratio (20% weight) - Proper spacing indicates good OCR
        int whitespaceCount = text.Count(c => char.IsWhiteSpace(c));
        double whitespaceRatio = (double)whitespaceCount / totalChars;
        // Optimal whitespace is around 15-20% of text
        double whitespaceScore = 1.0 - Math.Abs(whitespaceRatio - 0.175) / 0.175;
        whitespaceScore = Math.Max(0, Math.Min(1, whitespaceScore));
        score += whitespaceScore * 20;

        // 3. Special character penalty (20% weight) - Excessive special chars indicate errors
        int specialCharCount = text.Count(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c));
        double specialCharRatio = (double)specialCharCount / totalChars;
        // Special chars should be < 10% for good OCR
        double specialCharScore = Math.Max(0, 1.0 - (specialCharRatio / 0.1));
        score += specialCharScore * 20;

        // 4. Word validity (20% weight) - Reasonable word lengths and patterns
        var words = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 0)
        {
            // Average word length should be 3-8 characters for typical text
            double avgWordLength = words.Average(w => w.Length);
            double wordLengthScore = 1.0 - Math.Min(1.0, Math.Abs(avgWordLength - 5.5) / 5.5);
            score += wordLengthScore * 10;

            // Percentage of words with reasonable length (2-15 chars)
            int validLengthWords = words.Count(w => w.Length >= 2 && w.Length <= 15);
            double validLengthRatio = (double)validLengthWords / words.Length;
            score += validLengthRatio * 10;
        }

        _logger.LogDebug(
            "Quality score calculated: {Score:F2} (Alphanumeric={AlphaRatio:F2}, Whitespace={WsRatio:F2}, SpecialChar={ScRatio:F2})",
            score,
            alphanumericRatio,
            whitespaceRatio,
            specialCharRatio);

        return score;
    }

    /// <inheritdoc />
    public TextMatchResult? FindBestMatch(string phrase, string text, double threshold = 0.85)
    {
        // Handle edge cases
        if (string.IsNullOrWhiteSpace(phrase) || string.IsNullOrWhiteSpace(text))
        {
            _logger.LogDebug("FindBestMatch: Null or empty input (phrase={PhraseEmpty}, text={TextEmpty})",
                string.IsNullOrWhiteSpace(phrase),
                string.IsNullOrWhiteSpace(text));
            return null;
        }

        // Normalize for comparison (case-insensitive)
        var phraseNormalized = phrase.Trim().ToUpperInvariant();
        var textNormalized = text.ToUpperInvariant();

        // If phrase is longer than text, no match possible
        if (phraseNormalized.Length > textNormalized.Length)
        {
            _logger.LogDebug("FindBestMatch: Phrase longer than text ({PhraseLen} > {TextLen})",
                phraseNormalized.Length, textNormalized.Length);
            return null;
        }

        TextMatchResult? bestMatch = null;
        double bestSimilarity = 0.0;
        int bestStartIndex = -1;

        // Calculate word count for sliding window
        var phraseWords = phraseNormalized.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        int phraseWordCount = phraseWords.Length;

        // Split text into words for window sliding
        var textWords = textNormalized.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        if (textWords.Length == 0)
        {
            return null;
        }

        // Use sliding window of similar word count to phrase
        // Window size: phraseWordCount - 1 to phraseWordCount + 2 (to handle variations)
        for (int windowSize = Math.Max(1, phraseWordCount - 1);
             windowSize <= Math.Min(textWords.Length, phraseWordCount + 2);
             windowSize++)
        {
            for (int i = 0; i <= textWords.Length - windowSize; i++)
            {
                // Extract window
                var windowWords = textWords.Skip(i).Take(windowSize).ToArray();
                var windowText = string.Join(" ", windowWords);

                // Calculate fuzzy similarity using FuzzySharp
                int fuzzyScore = Fuzz.Ratio(phraseNormalized, windowText);
                double similarity = fuzzyScore / 100.0;

                // Track best match
                if (similarity > bestSimilarity)
                {
                    bestSimilarity = similarity;

                    // Find start index in original text (need to account for original casing/spacing)
                    // This is approximate - find first word of window in original text
                    int startIndex = FindSubstringIndex(text, windowWords[0], i);

                    if (startIndex >= 0)
                    {
                        int length = windowText.Length;

                        bestMatch = new TextMatchResult
                        {
                            MatchedText = text.Substring(startIndex, Math.Min(length, text.Length - startIndex)),
                            Similarity = similarity,
                            StartIndex = startIndex,
                            Length = length
                        };
                        bestStartIndex = startIndex;

                        _logger.LogTrace(
                            "FindBestMatch: New best match found - Similarity={Similarity:F2}, Window='{Window}', OriginalText='{Match}'",
                            similarity,
                            windowText,
                            bestMatch.MatchedText);
                    }
                }
            }
        }

        // Return best match only if it meets threshold
        if (bestMatch != null && bestSimilarity >= threshold)
        {
            _logger.LogDebug(
                "FindBestMatch: Match found - Phrase='{Phrase}', Match='{Match}', Similarity={Similarity:F2}",
                phrase,
                bestMatch.MatchedText.Length > 50 ? bestMatch.MatchedText[..50] + "..." : bestMatch.MatchedText,
                bestSimilarity);

            return bestMatch;
        }

        _logger.LogDebug(
            "FindBestMatch: No match above threshold - Phrase='{Phrase}', BestSimilarity={BestSim:F2}, Threshold={Threshold:F2}",
            phrase,
            bestSimilarity,
            threshold);

        return null;
    }

    /// <summary>
    /// Helper method to find the approximate start index of a word in the original (non-normalized) text.
    /// </summary>
    private int FindSubstringIndex(string originalText, string normalizedWord, int wordPosition)
    {
        var words = originalText.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        if (wordPosition >= words.Length)
            return -1;

        // Find cumulative position
        int currentPos = 0;
        for (int i = 0; i < wordPosition && i < words.Length; i++)
        {
            currentPos = originalText.IndexOf(words[i], currentPos, StringComparison.OrdinalIgnoreCase);
            if (currentPos < 0)
                return -1;
            currentPos += words[i].Length;
        }

        // Find the actual word at wordPosition
        if (wordPosition < words.Length)
        {
            currentPos = originalText.IndexOf(words[wordPosition], currentPos, StringComparison.OrdinalIgnoreCase);
            return currentPos;
        }

        return -1;
    }
}
