// <copyright file="DocxStructureAnalyzer.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;

/// <summary>
/// Analyzes DOCX document structure to determine best extraction strategy.
/// </summary>
public class DocxStructureAnalyzer
{
    /// <summary>
    /// Analyzes document structure and returns indicators.
    /// </summary>
    /// <param name="text">The full DOCX text content.</param>
    /// <returns>Structure analysis result.</returns>
    public DocxStructureInfo Analyze(string text)
    {
        var upperText = text.ToUpperInvariant();

        return new DocxStructureInfo
        {
            HasStandardLabels = DetectStandardLabels(upperText),
            HasTableStructure = DetectTableStructure(text),
            HasContextualLabels = DetectContextualLabels(upperText),
            HasPoorFormatting = DetectPoorFormatting(text),
            HasCrossReferences = DetectCrossReferences(upperText),
            LabelCount = CountLabels(upperText),
            LineCount = text.Split('\n').Length,
        };
    }

    private static bool DetectStandardLabels(string upperText)
    {
        // Standard CNBV format indicators
        var standardLabels = new[]
        {
            "EXPEDIENTE:",
            "OFICIO:",
            "NOMBRE:",
            "CUENTA:",
            "MONTO:",
        };

        return standardLabels.Count(label => upperText.Contains(label)) >= 3;
    }

    private static bool DetectTableStructure(string text)
    {
        // Simple table detection: multiple tab characters on consecutive lines
        var lines = text.Split('\n');
        var tabLines = lines.Count(line => line.Count(c => c == '\t') >= 2);
        return tabLines >= 3;
    }

    private static bool DetectContextualLabels(string upperText)
    {
        // Contextual patterns like "Expediente: VALUE" or "El expediente VALUE"
        var contextualPatterns = new[]
        {
            "EXPEDIENTE NO.",
            "NÚMERO DE EXPEDIENTE",
            "OFICIO NÚMERO",
            "NOMBRE COMPLETO",
        };

        return contextualPatterns.Any(upperText.Contains);
    }

    private static bool DetectPoorFormatting(string text)
    {
        // Indicators of poor formatting:
        // - Very long lines without breaks
        // - Missing whitespace
        // - Excessive consecutive spaces
        var lines = text.Split('\n');
        var hasLongLines = lines.Any(line => line.Length > 200);
        var hasExcessiveSpaces = text.Contains("  "); // Multiple consecutive spaces
        var hasMissingSpaces = text.Contains(":") && !text.Contains(": ");

        return hasLongLines || hasExcessiveSpaces || hasMissingSpaces;
    }

    private static bool DetectCrossReferences(string upperText)
    {
        // Mexican legal document cross-reference patterns
        var crossRefPatterns = new[]
        {
            "ARRIBA MENCIONADA",
            "ANTERIORMENTE INDICADO",
            "PREVIAMENTE SEÑALADO",
            "REFERIDO EN EL",
            "MENCIONADO EN EL",
        };

        return crossRefPatterns.Any(upperText.Contains);
    }

    private static int CountLabels(string upperText)
    {
        var commonLabels = new[]
        {
            "EXPEDIENTE",
            "OFICIO",
            "NOMBRE",
            "CUENTA",
            "MONTO",
            "CLABE",
            "BANCO",
            "FECHA",
        };

        return commonLabels.Count(upperText.Contains);
    }
}

/// <summary>
/// Contains structure analysis information for a DOCX document.
/// </summary>
public class DocxStructureInfo
{
    /// <summary>
    /// Gets or sets a value indicating whether document has standard CNBV labels.
    /// </summary>
    public bool HasStandardLabels { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether document has table structure.
    /// </summary>
    public bool HasTableStructure { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether document has contextual label-value pairs.
    /// </summary>
    public bool HasContextualLabels { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether document has poor formatting.
    /// </summary>
    public bool HasPoorFormatting { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether document has cross-references.
    /// </summary>
    public bool HasCrossReferences { get; set; }

    /// <summary>
    /// Gets or sets the count of recognized labels in the document.
    /// </summary>
    public int LabelCount { get; set; }

    /// <summary>
    /// Gets or sets the total line count in the document.
    /// </summary>
    public int LineCount { get; set; }
}
