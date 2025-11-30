// <copyright file="MexicanNameFuzzyMatcher.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using FuzzySharp;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;

/// <summary>
/// Handles fuzzy matching for Mexican names with accent normalization.
/// </summary>
public class MexicanNameFuzzyMatcher
{
    private const int NameSimilarityThreshold = 90; // 90% similarity for names

    /// <summary>
    /// Determines if two names are equivalent using fuzzy matching.
    /// </summary>
    /// <param name="name1">First name to compare.</param>
    /// <param name="name2">Second name to compare.</param>
    /// <returns>True if names are equivalent, false otherwise.</returns>
    public bool AreNamesEquivalent(string? name1, string? name2)
    {
        if (string.IsNullOrWhiteSpace(name1) || string.IsNullOrWhiteSpace(name2))
            return false;

        // Exact match first
        if (string.Equals(name1, name2, StringComparison.OrdinalIgnoreCase))
            return true;

        // Normalize (remove accents) and compare
        var normalized1 = RemoveAccents(name1);
        var normalized2 = RemoveAccents(name2);
        if (string.Equals(normalized1, normalized2, StringComparison.OrdinalIgnoreCase))
            return true;

        // Fuzzy match with high threshold (90%+ similarity)
        var similarity = Fuzz.Ratio(normalized1, normalized2);
        return similarity >= NameSimilarityThreshold;
    }

    /// <summary>
    /// Finds the best matching name from a list of candidates.
    /// </summary>
    /// <param name="targetName">The name to match against.</param>
    /// <param name="candidates">List of candidate names.</param>
    /// <returns>The best matching name, or null if no good match found.</returns>
    public string? FindBestNameMatch(string targetName, IEnumerable<string> candidates)
    {
        if (string.IsNullOrWhiteSpace(targetName))
            return null;

        var normalizedTarget = RemoveAccents(targetName);
        var bestMatch = candidates
            .Select(c => new { Name = c, Similarity = Fuzz.Ratio(normalizedTarget, RemoveAccents(c)) })
            .OrderByDescending(x => x.Similarity)
            .FirstOrDefault();

        return bestMatch?.Similarity >= NameSimilarityThreshold ? bestMatch.Name : null;
    }

    /// <summary>
    /// Removes accent marks from text to normalize Mexican names.
    /// Pérez → Perez, José → Jose, Muñoz → Munoz.
    /// </summary>
    /// <param name="text">Text with potential accents.</param>
    /// <returns>Text without accent marks.</returns>
    public static string RemoveAccents(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }
}
