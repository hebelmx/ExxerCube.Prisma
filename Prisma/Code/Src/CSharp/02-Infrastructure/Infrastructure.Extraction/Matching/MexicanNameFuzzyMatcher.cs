namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Matching;

/// <summary>
/// Fuzzy matcher specifically for Mexican names with common variations.
/// CRITICAL: Use ONLY for name fields, NOT for:
/// - RFCs (must be exact)
/// - CURPs (must be exact)
/// - Account numbers (must be exact)
/// - Expedientes (must be exact)
/// - Amounts (must be exact)
/// - Dates (must be exact)
///
/// Handles common Mexican name variations:
/// - Accent variations: Pérez/Perez, González/Gonzales, José/Jose
/// - Spelling variations: González/Gonzales/Gonzalez, Christian/Cristian
/// - Case variations: PÉREZ/pérez/Pérez
///
/// Uses Levenshtein distance with 85% similarity threshold (best-effort OCR).
/// </summary>
public sealed class MexicanNameFuzzyMatcher
{
    private const int SimilarityThreshold = 85;

    private static readonly HashSet<string> SpanishGivenNames = new(new[]
    {
        "jose", "maria", "juan", "luis", "carlos", "ana", "miguel", "angel", "pedro",
        "antonio", "fernando", "jesus", "roberto", "ricardo", "francisco", "alejandro",
        "cristian", "christian", "guadalupe", "sofia", "karla"
    }, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> SpanishSurnames = new(new[]
    {
        "perez", "gonzalez", "gonzales", "garcia", "rodriguez", "hernandez", "lopez",
        "martinez", "ramirez", "sanchez", "diaz", "dominguez", "cruz", "gomez", "juarez"
    }, StringComparer.OrdinalIgnoreCase);

    // Patterns that indicate NON-name fields (must match exactly)
    private static readonly Regex RfcPattern = new(@"^[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}$", RegexOptions.Compiled);

    private static readonly Regex CurpPattern = new(@"^[A-Z]{4}\d{6}[HM][A-Z]{5}[0-9A-Z]\d$", RegexOptions.Compiled);
    private static readonly Regex ExpedientePattern = new(@"^[A-Z]/[A-Z]{1,2}\d+-\d+-\d+-[A-Z]+$", RegexOptions.Compiled);
    private static readonly Regex AccountPattern = new(@"^\d{10,18}$", RegexOptions.Compiled);
    private static readonly Regex AmountPattern = new(@"^\$?\s*[\d,]+\.?\d*$", RegexOptions.Compiled);
    private static readonly Regex DatePattern = new(@"^\d{4}-\d{2}-\d{2}$|^\d{2}/\d{2}/\d{4}$", RegexOptions.Compiled);

    /// <summary>
    /// Gets the similarity threshold percentage for fuzzy matching (85%).
    /// </summary>
    public int MatchThreshold => SimilarityThreshold;

    /// <summary>
    /// Determines if two values match using fuzzy logic for names, exact matching for non-names.
    /// </summary>
    /// <param name="value1">First value to compare.</param>
    /// <param name="value2">Second value to compare.</param>
    /// <returns>True if values match (fuzzy for names, exact for others); otherwise, false.</returns>
    public bool IsMatch(string? value1, string? value2)
    {
        if (string.IsNullOrWhiteSpace(value1) || string.IsNullOrWhiteSpace(value2))
        {
            return false;
        }

        // Check if either value is a non-name field (RFC, account, etc.)
        // These MUST match exactly - no fuzzy matching allowed
        if (!IsNameField(value1) || !IsNameField(value2))
        {
            // Exact match required for non-name fields
            return string.Equals(value1, value2, StringComparison.Ordinal);
        }

        // Only fuzzy match when BOTH values look like Mexican/Spanish names.
        if (!IsLikelyMexicanName(value1) || !IsLikelyMexicanName(value2))
        {
            // For non-Mexican names, require normalized exact match to avoid English false positives (e.g., John/Jon).
            return string.Equals(NormalizeForComparison(value1), NormalizeForComparison(value2), StringComparison.Ordinal);
        }

        // For name fields, use fuzzy matching with accent normalization
        var normalized1 = NormalizeForComparison(value1);
        var normalized2 = NormalizeForComparison(value2);

        // Calculate similarity score
        var similarity = Fuzz.Ratio(normalized1, normalized2);

        return similarity >= SimilarityThreshold;
    }

    /// <summary>
    /// Calculates the similarity score between two values (0-100).
    /// </summary>
    /// <param name="value1">First value to compare.</param>
    /// <param name="value2">Second value to compare.</param>
    /// <returns>Similarity score from 0 to 100.</returns>
    public int GetSimilarityScore(string? value1, string? value2)
    {
        if (string.IsNullOrWhiteSpace(value1) || string.IsNullOrWhiteSpace(value2))
        {
            return 0;
        }

        var normalized1 = NormalizeForComparison(value1);
        var normalized2 = NormalizeForComparison(value2);

        return Fuzz.Ratio(normalized1, normalized2);
    }

    /// <summary>
    /// Determines if a value represents a name field (suitable for fuzzy matching).
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value appears to be a name; otherwise, false.</returns>
    public bool IsNameField(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmedValue = value.Trim();

        // Check if value matches patterns for non-name fields
        if (RfcPattern.IsMatch(trimmedValue)) return false;
        if (CurpPattern.IsMatch(trimmedValue)) return false;
        if (ExpedientePattern.IsMatch(trimmedValue)) return false;
        if (AccountPattern.IsMatch(trimmedValue)) return false;
        if (AmountPattern.IsMatch(trimmedValue)) return false;
        if (DatePattern.IsMatch(trimmedValue)) return false;

        // If value contains special characters typical of non-name fields, it's not a name
        if (trimmedValue.Contains('$') || trimmedValue.Contains('/') || trimmedValue.Contains('-'))
        {
            // Exception: hyphens in compound surnames are OK (e.g., "García-López")
            if (trimmedValue.Contains('/') || trimmedValue.Contains('$'))
            {
                return false;
            }
        }

        // If value is all digits, it's not a name
        if (trimmedValue.All(char.IsDigit))
        {
            return false;
        }

        // If value contains mostly letters (allowing spaces, accents, hyphens), it's likely a name
        var letterCount = trimmedValue.Count(c => char.IsLetter(c) || c == ' ' || c == '-' || c == '\'');
        var totalCount = trimmedValue.Length;

        return (double)letterCount / totalCount >= 0.80; // 80% letters = name
    }

    private static bool IsLikelyMexicanName(string value)
    {
        var normalized = NormalizeForComparison(value);
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Accented letters or ñ are a strong Spanish indicator
        if (value.IndexOfAny(new[] { 'á', 'é', 'í', 'ó', 'ú', 'ñ', 'Á', 'É', 'Í', 'Ó', 'Ú', 'Ñ' }) >= 0)
        {
            return true;
        }

        foreach (var token in tokens)
        {
            if (SpanishGivenNames.Contains(token) || SpanishSurnames.Contains(token))
            {
                return true;
            }

            if (token.EndsWith("ez", StringComparison.OrdinalIgnoreCase) || token.EndsWith("es", StringComparison.OrdinalIgnoreCase))
            {
                return true; // Common Spanish surname endings (Perez, Gonzales, Hernandez)
            }
        }

        return false;
    }

    /// <summary>
    /// Normalizes a string for comparison by removing accents and converting to lowercase.
    /// </summary>
    /// <param name="value">The value to normalize.</param>
    /// <returns>Normalized string.</returns>
    private static string NormalizeForComparison(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // Convert to lowercase
        var normalized = value.ToLowerInvariant();

        // Remove accents/diacritics
        normalized = RemoveDiacritics(normalized);

        // Normalize whitespace
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        return normalized;
    }

    /// <summary>
    /// Removes diacritics (accents) from a string.
    /// Example: "Pérez" → "Perez", "González" → "Gonzalez"
    /// </summary>
    /// <param name="text">The text to process.</param>
    /// <returns>Text without diacritics.</returns>
    private static string RemoveDiacritics(string text)
    {
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