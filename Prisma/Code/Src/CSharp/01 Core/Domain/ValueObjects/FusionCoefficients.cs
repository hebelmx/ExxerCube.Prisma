// <copyright file="FusionCoefficients.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Configurable coefficients for the data fusion algorithm.
/// These values are optimized via Genetic Algorithm (GA) + Polynomial Regression
/// using a labeled dataset of Expedientes with known ground truth.
/// </summary>
/// <remarks>
/// Optimization methodology (adapted from OCR_FILTER_OPTIMIZATION_JOURNAL.md):
/// Phase 1: Generate labeled dataset (100+ Expedientes with ground truth)
/// Phase 2: Cluster by input properties (K-Means, K=5-10 clusters)
/// Phase 3: GA optimization per cluster (population=50, generations=100)
/// Phase 4: Polynomial regression (2nd/3rd degree) to interpolate between clusters
/// Phase 5: Validation on held-out test set (target: >95% field accuracy, &lt;2% false negatives)
///
/// Initial values below are educated guesses based on domain knowledge.
/// Production values will be GA-optimized.
/// </remarks>
public class FusionCoefficients
{
    // Source reliability base values (before metadata adjustments)

    /// <summary>
    /// Gets or sets the base reliability for XML hand-filled data (0.0-1.0).
    /// Default: 0.60 (prone to typos and catalog mismatches).
    /// </summary>
    public double XML_BaseReliability { get; set; } = 0.60;

    /// <summary>
    /// Gets or sets the base reliability for PDF OCR from CNBV (0.0-1.0).
    /// Default: 0.85 (official source, good OCR quality).
    /// </summary>
    public double PDF_BaseReliability { get; set; } = 0.85;

    /// <summary>
    /// Gets or sets the base reliability for DOCX OCR from authorities (0.0-1.0).
    /// Default: 0.70 (variable quality by authority).
    /// </summary>
    public double DOCX_BaseReliability { get; set; } = 0.70;

    // Metadata weight distribution for source reliability calculation

    /// <summary>
    /// Gets or sets the weight of OCR confidence in source reliability calculation (0.0-1.0).
    /// Default: 0.50 (50% weight on OCR confidence metrics).
    /// </summary>
    public double OCR_ConfidenceWeight { get; set; } = 0.50;

    /// <summary>
    /// Gets or sets the weight of image quality in source reliability calculation (0.0-1.0).
    /// Default: 0.30 (30% weight on image quality metrics).
    /// </summary>
    public double ImageQualityWeight { get; set; } = 0.30;

    /// <summary>
    /// Gets or sets the weight of extraction success in source reliability calculation (0.0-1.0).
    /// Default: 0.20 (20% weight on extraction success metrics).
    /// </summary>
    public double ExtractionSuccessWeight { get; set; } = 0.20;

    // OCR confidence multiplier formula coefficients

    /// <summary>
    /// Gets or sets the exponent for mean OCR confidence in multiplier formula.
    /// Default: 1.5 (amplifies high confidence, penalizes low confidence).
    /// Formula: Math.Pow(MeanConfidence, MeanConfidenceExponent)
    /// </summary>
    public double MeanConfidenceExponent { get; set; } = 1.5;

    /// <summary>
    /// Gets or sets the penalty weight for low-confidence words.
    /// Default: -0.8 (negative to penalize high ratio of low-confidence words).
    /// Formula: LowConfidencePenaltyWeight * (LowConfidenceWords / TotalWords)
    /// </summary>
    public double LowConfidencePenaltyWeight { get; set; } = -0.8;

    // Field score calculation boosts

    /// <summary>
    /// Gets or sets the boost multiplier for fields matching expected patterns (RFC, CURP, dates, etc.).
    /// Default: 1.10 (10% boost for pattern match).
    /// </summary>
    public double PatternMatchBoost { get; set; } = 1.10;

    /// <summary>
    /// Gets or sets the boost multiplier for fields validated against CNBV catalogs.
    /// Default: 1.15 (15% boost for catalog validation).
    /// </summary>
    public double CatalogValidationBoost { get; set; } = 1.15;

    // Fuzzy matching thresholds

    /// <summary>
    /// Gets or sets the minimum similarity threshold for fuzzy string matching (0.0-1.0).
    /// Default: 0.85 (85% similarity required to consider a match).
    /// </summary>
    public double FuzzyMatchThreshold { get; set; } = 0.85;

    /// <summary>
    /// Gets or sets the confidence penalty multiplier for fuzzy matches vs exact matches.
    /// Default: 0.90 (fuzzy match confidence = similarity * 0.90).
    /// </summary>
    public double FuzzyMatchConfidencePenalty { get; set; } = 0.90;

    // Overall confidence calculation weights

    /// <summary>
    /// Gets or sets the weight of required fields in overall Expediente confidence (0.0-1.0).
    /// Default: 0.70 (required fields contribute 70% to overall confidence).
    /// </summary>
    public double RequiredFieldsWeight { get; set; } = 0.70;

    /// <summary>
    /// Gets or sets the weight of optional fields in overall Expediente confidence (0.0-1.0).
    /// Default: 0.30 (optional fields contribute 30% to overall confidence).
    /// </summary>
    public double OptionalFieldsWeight { get; set; } = 0.30;

    // Decision thresholds for NextAction

    /// <summary>
    /// Gets or sets the minimum overall confidence threshold for AutoProcess (0.0-1.0).
    /// Default: 0.85 (85% confidence required to auto-process without review).
    /// </summary>
    public double AutoProcessThreshold { get; set; } = 0.85;

    /// <summary>
    /// Gets or sets the minimum overall confidence threshold for ReviewRecommended (0.0-1.0).
    /// Default: 0.70 (70-85% confidence range triggers review recommendation).
    /// Below 0.70 = ManualReviewRequired.
    /// </summary>
    public double ManualReviewThreshold { get; set; } = 0.70;

    /// <summary>
    /// Gets the validation state for these coefficients.
    /// </summary>
    public ValidationState Validation { get; } = new();
}
