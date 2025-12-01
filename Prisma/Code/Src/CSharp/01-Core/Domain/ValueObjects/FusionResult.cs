// <copyright file="FusionResult.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.ValueObjects;

using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Represents the complete result of multi-source Expediente data fusion.
/// Contains the fused Expediente, overall confidence metrics, conflict analysis, and recommended next action.
/// </summary>
public class FusionResult
{
    /// <summary>
    /// Gets or sets the fused Expediente with reconciled data from all sources.
    /// Null if fusion failed or all sources returned null.
    /// </summary>
    public Expediente? FusedExpediente { get; set; }

    /// <summary>
    /// Gets or sets the overall confidence score for the fused Expediente (0.0-1.0).
    /// Calculated as weighted combination:
    /// - 70% weight on required fields confidence
    /// - 30% weight on optional fields confidence
    /// </summary>
    public double OverallConfidence { get; set; }

    /// <summary>
    /// Gets or sets the confidence score specifically for required fields (0.0-1.0).
    /// Required fields vary by operation type (Bloqueo, Desbloqueo, Transferencia, etc.).
    /// See Article 4 validation for field requirements.
    /// </summary>
    public double RequiredFieldsScore { get; set; }

    /// <summary>
    /// Gets or sets the confidence score for optional fields (0.0-1.0).
    /// Lower weight in overall confidence calculation.
    /// </summary>
    public double OptionalFieldsScore { get; set; }

    /// <summary>
    /// Gets or sets the list of field names with irreconcilable conflicts between sources.
    /// These fields require mandatory manual review.
    /// </summary>
    public List<string> ConflictingFields { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of required field names that are missing (null) after fusion.
    /// Missing required fields trigger ManualReviewRequired next action.
    /// </summary>
    public List<string> MissingRequiredFields { get; set; } = new();

    /// <summary>
    /// Gets or sets the recommended next action based on confidence thresholds and field completeness.
    /// - AutoProcess: Confidence > 0.85, all required fields present, no conflicts
    /// - ReviewRecommended: Confidence 0.70-0.85, all required fields present
    /// - ManualReviewRequired: Confidence &lt; 0.70 OR missing required fields OR critical conflicts
    /// </summary>
    public NextAction NextAction { get; set; } = NextAction.ManualReviewRequired;

    /// <summary>
    /// Gets or sets the detailed fusion results per field.
    /// Key: field name, Value: FieldFusionResult with decision, confidence, and source info.
    /// </summary>
    public Dictionary<string, FieldFusionResult> FieldResults { get; set; } = new();

    /// <summary>
    /// Gets or sets the calculated source reliability scores for each source.
    /// Key: SourceType, Value: reliability score (0.0-1.0) after OCR/quality adjustments.
    /// </summary>
    public Dictionary<SourceType, double> SourceReliabilities { get; set; } = new();

    /// <summary>
    /// Gets the validation state for this fusion result.
    /// </summary>
    public ValidationState Validation { get; } = new();
}
