namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr;

/// <summary>
/// Reconciles additional fields coming from XML and OCR sources.
/// </summary>
public static class AdditionalFieldsReconciler
{
    /// <summary>
    /// Merges additional fields giving preference to XML values when present, otherwise falling back to OCR.
    /// Conflicts (same key, different normalized value) are recorded.
    /// </summary>
    public static AdditionalFieldMergeResult Merge(
        IReadOnlyDictionary<string, string?> xmlFields,
        IReadOnlyDictionary<string, string?> ocrFields)
    {
        var result = new AdditionalFieldMergeResult();
        var merged = result.Merged;

        foreach (var kvp in xmlFields)
        {
            merged[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in ocrFields)
        {
            var key = kvp.Key;
            var ocrValue = Normalize(kvp.Value);
            if (!merged.TryGetValue(key, out var existing))
            {
                merged[key] = kvp.Value;
                continue;
            }

            var existingNormalized = Normalize(existing);
            if (!string.Equals(existingNormalized, ocrValue, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(ocrValue)
                && !string.IsNullOrWhiteSpace(existingNormalized))
            {
                result.Conflicts.Add(key);
            }
            else if (string.IsNullOrWhiteSpace(existing) && !string.IsNullOrWhiteSpace(ocrValue))
            {
                merged[key] = kvp.Value;
            }
        }

        return result;
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
