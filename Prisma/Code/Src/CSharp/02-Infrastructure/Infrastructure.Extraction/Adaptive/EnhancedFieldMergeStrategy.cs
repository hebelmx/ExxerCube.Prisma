// <copyright file="EnhancedFieldMergeStrategy.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;

/// <summary>
/// Enhanced field merge strategy for 3-way merging (XML + OCR + DOCX).
/// </summary>
/// <remarks>
/// Merge priority:
/// 1. XML (highest trust) - structured legal data
/// 2. OCR (medium trust) - extracted from PDF
/// 3. DOCX (complement) - fills gaps when XML/OCR missing
///
/// Special handling:
/// - Names: Use fuzzy matching for equivalence
/// - Accounts: Exact match required (financial data)
/// - Amounts: Exact match required (financial data)
/// - Cross-references: DOCX Search strategy resolves.
/// </remarks>
public class EnhancedFieldMergeStrategy
{
    private readonly MexicanNameFuzzyMatcher _nameMatcher;
    private readonly FuzzyMatchingPolicy _fuzzyPolicy;
    private readonly ILogger<EnhancedFieldMergeStrategy> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnhancedFieldMergeStrategy"/> class.
    /// </summary>
    /// <param name="nameMatcher">Mexican name fuzzy matcher.</param>
    /// <param name="fuzzyPolicy">Fuzzy matching policy.</param>
    /// <param name="logger">Logger instance.</param>
    public EnhancedFieldMergeStrategy(
        MexicanNameFuzzyMatcher nameMatcher,
        FuzzyMatchingPolicy fuzzyPolicy,
        ILogger<EnhancedFieldMergeStrategy> logger)
    {
        _nameMatcher = nameMatcher;
        _fuzzyPolicy = fuzzyPolicy;
        _logger = logger;
    }

    /// <summary>
    /// Merges expediente data from XML, OCR, and DOCX sources.
    /// </summary>
    /// <param name="xmlData">XML extracted data (highest priority).</param>
    /// <param name="ocrData">OCR extracted data (medium priority).</param>
    /// <param name="docxData">DOCX extracted data (complement - fills gaps).</param>
    /// <returns>Merged expediente with conflict warnings.</returns>
    public MergeResult Merge(Expediente? xmlData, Expediente? ocrData, Expediente? docxData)
    {
        var result = new MergeResult
        {
            MergedExpediente = new Expediente(),
            Warnings = new List<string>(),
        };

        // Merge each field with priority and conflict detection
        result.MergedExpediente.NumeroExpediente = MergeField(
            "NumeroExpediente",
            xmlData?.NumeroExpediente,
            ocrData?.NumeroExpediente,
            docxData?.NumeroExpediente,
            exact: true,
            result.Warnings);

        result.MergedExpediente.NumeroOficio = MergeField(
            "NumeroOficio",
            xmlData?.NumeroOficio,
            ocrData?.NumeroOficio,
            docxData?.NumeroOficio,
            exact: true,
            result.Warnings);

        result.MergedExpediente.NombreCompleto = MergeNameField(
            xmlData?.NombreCompleto,
            ocrData?.NombreCompleto,
            docxData?.NombreCompleto,
            result.Warnings);

        result.MergedExpediente.Cuenta = MergeField(
            "Cuenta",
            xmlData?.Cuenta,
            ocrData?.Cuenta,
            docxData?.Cuenta,
            exact: true, // Financial data requires exact match
            result.Warnings);

        result.MergedExpediente.CLABE = MergeField(
            "CLABE",
            xmlData?.CLABE,
            ocrData?.CLABE,
            docxData?.CLABE,
            exact: true, // Financial data requires exact match
            result.Warnings);

        result.MergedExpediente.RFC = MergeField(
            "RFC",
            xmlData?.RFC,
            ocrData?.RFC,
            docxData?.RFC,
            exact: true,
            result.Warnings);

        result.MergedExpediente.Monto = MergeAmountField(
            xmlData?.Monto,
            ocrData?.Monto,
            docxData?.Monto,
            result.Warnings);

        result.MergedExpediente.Banco = MergeField(
            "Banco",
            xmlData?.Banco,
            ocrData?.Banco,
            docxData?.Banco,
            exact: false,
            result.Warnings);

        _logger.LogInformation("Merged expediente with {WarningCount} warnings", result.Warnings.Count);
        return result;
    }

    /// <summary>
    /// Merges a single field with priority: XML > OCR > DOCX.
    /// </summary>
    private string? MergeField(
        string fieldName,
        string? xmlValue,
        string? ocrValue,
        string? docxValue,
        bool exact,
        List<string> warnings)
    {
        // Priority 1: XML
        if (!string.IsNullOrWhiteSpace(xmlValue))
        {
            // Check for conflicts with OCR/DOCX
            if (!string.IsNullOrWhiteSpace(ocrValue) && !ValuesMatch(xmlValue, ocrValue, exact))
            {
                warnings.Add($"{fieldName} conflict: XML='{xmlValue}' vs OCR='{ocrValue}' (using XML)");
            }

            if (!string.IsNullOrWhiteSpace(docxValue) && !ValuesMatch(xmlValue, docxValue, exact))
            {
                warnings.Add($"{fieldName} conflict: XML='{xmlValue}' vs DOCX='{docxValue}' (using XML)");
            }

            return xmlValue;
        }

        // Priority 2: OCR
        if (!string.IsNullOrWhiteSpace(ocrValue))
        {
            // Check for conflict with DOCX
            if (!string.IsNullOrWhiteSpace(docxValue) && !ValuesMatch(ocrValue, docxValue, exact))
            {
                warnings.Add($"{fieldName} conflict: OCR='{ocrValue}' vs DOCX='{docxValue}' (using OCR)");
            }

            return ocrValue;
        }

        // Priority 3: DOCX (complement - fills gaps)
        if (!string.IsNullOrWhiteSpace(docxValue))
        {
            _logger.LogDebug("{FieldName} complemented by DOCX: {Value}", fieldName, docxValue);
            return docxValue;
        }

        return null;
    }

    /// <summary>
    /// Merges name field with fuzzy matching.
    /// </summary>
    private string? MergeNameField(
        string? xmlName,
        string? ocrName,
        string? docxName,
        List<string> warnings)
    {
        // Priority 1: XML
        if (!string.IsNullOrWhiteSpace(xmlName))
        {
            // Check for fuzzy conflicts
            if (!string.IsNullOrWhiteSpace(ocrName) && !_nameMatcher.AreNamesEquivalent(xmlName, ocrName))
            {
                warnings.Add($"NombreCompleto conflict: XML='{xmlName}' vs OCR='{ocrName}' (using XML)");
            }

            if (!string.IsNullOrWhiteSpace(docxName) && !_nameMatcher.AreNamesEquivalent(xmlName, docxName))
            {
                warnings.Add($"NombreCompleto conflict: XML='{xmlName}' vs DOCX='{docxName}' (using XML)");
            }

            return xmlName;
        }

        // Priority 2: OCR
        if (!string.IsNullOrWhiteSpace(ocrName))
        {
            if (!string.IsNullOrWhiteSpace(docxName) && !_nameMatcher.AreNamesEquivalent(ocrName, docxName))
            {
                warnings.Add($"NombreCompleto conflict: OCR='{ocrName}' vs DOCX='{docxName}' (using OCR)");
            }

            return ocrName;
        }

        // Priority 3: DOCX
        return docxName;
    }

    /// <summary>
    /// Merges amount field with conflict detection.
    /// </summary>
    private decimal? MergeAmountField(
        decimal? xmlAmount,
        decimal? ocrAmount,
        decimal? docxAmount,
        List<string> warnings)
    {
        // Priority 1: XML
        if (xmlAmount.HasValue)
        {
            // Check for conflicts (amounts must match exactly)
            if (ocrAmount.HasValue && xmlAmount.Value != ocrAmount.Value)
            {
                warnings.Add($"Monto conflict: XML={xmlAmount:C} vs OCR={ocrAmount:C} (using XML)");
            }

            if (docxAmount.HasValue && xmlAmount.Value != docxAmount.Value)
            {
                warnings.Add($"Monto conflict: XML={xmlAmount:C} vs DOCX={docxAmount:C} (using XML)");
            }

            return xmlAmount;
        }

        // Priority 2: OCR
        if (ocrAmount.HasValue)
        {
            if (docxAmount.HasValue && ocrAmount.Value != docxAmount.Value)
            {
                warnings.Add($"Monto conflict: OCR={ocrAmount:C} vs DOCX={docxAmount:C} (using OCR)");
            }

            return ocrAmount;
        }

        // Priority 3: DOCX
        return docxAmount;
    }

    /// <summary>
    /// Checks if two values match (exact or fuzzy based on policy).
    /// </summary>
    private bool ValuesMatch(string value1, string value2, bool exact)
    {
        if (exact)
            return string.Equals(value1, value2, StringComparison.OrdinalIgnoreCase);

        // Fuzzy match for non-critical fields
        return _nameMatcher.AreNamesEquivalent(value1, value2);
    }
}

/// <summary>
/// Result of merging expediente data from multiple sources.
/// </summary>
public class MergeResult
{
    /// <summary>
    /// Gets or sets the merged expediente data.
    /// </summary>
    public Expediente MergedExpediente { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of warnings from merge conflicts.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Gets a value indicating whether there were any conflicts during merge.
    /// </summary>
    public bool HasConflicts => Warnings.Count > 0;
}
