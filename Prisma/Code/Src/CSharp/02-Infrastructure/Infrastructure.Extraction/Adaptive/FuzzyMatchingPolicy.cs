// <copyright file="FuzzyMatchingPolicy.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;

/// <summary>
/// Defines policy for when to use fuzzy matching vs exact matching.
/// Fuzzy matching is selective - only for fields where typos are expected.
/// </summary>
public class FuzzyMatchingPolicy
{
    private static readonly HashSet<string> FuzzyMatchFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Nombre",
        "NombreCompleto",
        "Direccion",
        "Ciudad",
        "Estado",
        "Municipio",
    };

    private static readonly HashSet<string> ExactMatchFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cuenta",
        "CLABE",
        "RFC",
        "CURP",
        "Expediente",
        "Oficio",
        "Monto",
        "Fecha",
    };

    /// <summary>
    /// Determines if fuzzy matching should be used for a given field.
    /// </summary>
    /// <param name="fieldName">The field name to check.</param>
    /// <returns>True if fuzzy matching should be used, false for exact matching.</returns>
    public bool ShouldUseFuzzyMatching(string fieldName)
    {
        // Exact match fields always use exact matching
        if (ExactMatchFields.Contains(fieldName))
            return false;

        // Fuzzy match fields use fuzzy matching
        if (FuzzyMatchFields.Contains(fieldName))
            return true;

        // Default: exact matching for unknown fields
        return false;
    }

    /// <summary>
    /// Gets the similarity threshold for a field (0-100).
    /// </summary>
    /// <param name="fieldName">The field name.</param>
    /// <returns>Similarity threshold percentage.</returns>
    public int GetSimilarityThreshold(string fieldName)
    {
        // High threshold (90%) for names to avoid false positives
        if (fieldName.Contains("Nombre", StringComparison.OrdinalIgnoreCase))
            return 90;

        // Medium threshold (85%) for addresses
        if (fieldName.Contains("Direccion", StringComparison.OrdinalIgnoreCase) ||
            fieldName.Contains("Ciudad", StringComparison.OrdinalIgnoreCase))
            return 85;

        // Default high threshold
        return 90;
    }
}
