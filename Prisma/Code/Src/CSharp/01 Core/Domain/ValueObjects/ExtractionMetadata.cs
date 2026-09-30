// <copyright file="ExtractionMetadata.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.ValueObjects;

using ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Captures OCR quality metrics, image quality metrics, and extraction success metrics
/// for a single source (XML, PDF, or DOCX).
/// Used to dynamically calculate source reliability during data fusion.
/// </summary>
/// <remarks>
/// This metadata feeds into the fusion algorithm's source reliability calculation:
/// - Base reliability (PDF=0.85, DOCX=0.70, XML=0.60)
/// - Adjusted by OCR confidence multiplier (mean/min confidence, low confidence word ratio)
/// - Adjusted by image quality multiplier (quality index, blur, contrast, noise, edge density)
/// - Adjusted by extraction success multiplier (regex matches, catalog validations, pattern violations)
///
/// See IFusionExpediente.CalculateSourceReliability for weighting formula.
/// </remarks>
public class ExtractionMetadata
{
    // OCR Quality Metrics (from Tesseract)

    /// <summary>
    /// Gets or sets the mean OCR confidence across all words (0.0-1.0).
    /// Higher values indicate better OCR quality. Null for XML sources.
    /// </summary>
    public double? MeanConfidence { get; set; }

    /// <summary>
    /// Gets or sets the minimum OCR confidence across all words (0.0-1.0).
    /// Low values indicate potential OCR errors. Null for XML sources.
    /// </summary>
    public double? MinConfidence { get; set; }

    /// <summary>
    /// Gets or sets the total number of words extracted via OCR.
    /// Null for XML sources.
    /// </summary>
    public int? TotalWords { get; set; }

    /// <summary>
    /// Gets or sets the number of words with confidence below threshold (e.g., &lt; 0.60).
    /// High ratio indicates poor OCR quality. Null for XML sources.
    /// </summary>
    public int? LowConfidenceWords { get; set; }

    // Image Quality Metrics (from optimized preprocessing pipeline)

    /// <summary>
    /// Gets or sets the overall quality index from GA-optimized image preprocessing (0.0-1.0).
    /// Combines blur, contrast, noise, and edge density metrics. Null for XML sources.
    /// </summary>
    public double? QualityIndex { get; set; }

    /// <summary>
    /// Gets or sets the blur score (0.0-1.0, higher = sharper image).
    /// Null for XML sources.
    /// </summary>
    public double? BlurScore { get; set; }

    /// <summary>
    /// Gets or sets the contrast score (0.0-1.0, higher = better contrast).
    /// Null for XML sources.
    /// </summary>
    public double? ContrastScore { get; set; }

    /// <summary>
    /// Gets or sets the noise estimate (0.0-1.0, lower = cleaner image).
    /// Null for XML sources.
    /// </summary>
    public double? NoiseEstimate { get; set; }

    /// <summary>
    /// Gets or sets the edge density (0.0-1.0, indicates text sharpness).
    /// Null for XML sources.
    /// </summary>
    public double? EdgeDensity { get; set; }

    // Extraction Success Metrics

    /// <summary>
    /// Gets or sets the number of fields that matched expected regex patterns
    /// (RFC, CURP, NumeroExpediente, CLABE, dates, etc.).
    /// High count indicates clean, well-formatted extraction.
    /// </summary>
    public int RegexMatches { get; set; }

    /// <summary>
    /// Gets or sets the total number of fields successfully extracted (non-null).
    /// Used to calculate extraction success rate.
    /// </summary>
    public int TotalFieldsExtracted { get; set; }

    /// <summary>
    /// Gets or sets the number of fields validated against CNBV catalogs
    /// (AreaDescripcion, AutoridadNombre, Caracter, etc.).
    /// High count indicates high-quality, catalog-compliant extraction.
    /// </summary>
    public int CatalogValidations { get; set; }

    /// <summary>
    /// Gets or sets the number of fields that violated expected patterns or catalogs.
    /// High count indicates poor extraction quality (typos, OCR errors, invalid data).
    /// </summary>
    public int PatternViolations { get; set; }

    /// <summary>
    /// Gets or sets the source type (XML, PDF, DOCX).
    /// Determines base reliability before metric adjustments.
    /// </summary>
    public SourceType Source { get; set; } = SourceType.XML_HandFilled;

    /// <summary>
    /// Gets the validation state for this metadata.
    /// </summary>
    public ValidationState Validation { get; } = new();
}
