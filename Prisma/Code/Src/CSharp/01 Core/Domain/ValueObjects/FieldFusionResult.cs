// <copyright file="FieldFusionResult.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.ValueObjects;

using ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Represents the result of fusing a single field from multiple sources.
/// Contains the selected value, confidence score, decision rationale, and review flags.
/// </summary>
public class FieldFusionResult
{
    /// <summary>
    /// Gets or sets the fused field value selected from the sources (may be null if all sources null or conflict).
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Gets or sets the confidence score for this field fusion (0.0-1.0).
    /// - 1.0 = Perfect agreement across all sources
    /// - 0.85-0.95 = High confidence (all sources agree or fuzzy match)
    /// - 0.70-0.85 = Moderate confidence (weighted voting with clear winner)
    /// - 0.50-0.70 = Low confidence (narrow margin between sources)
    /// - 0.0 = No confidence (all sources null or irreconcilable conflict)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Gets or sets the fusion decision type indicating how this value was selected.
    /// </summary>
    public FusionDecision Decision { get; set; } = FusionDecision.AllSourcesNull;

    /// <summary>
    /// Gets or sets the list of sources that contributed to this fusion decision.
    /// </summary>
    public List<SourceType> ContributingSources { get; set; } = new();

    /// <summary>
    /// Gets or sets the source that provided the winning value (for WeightedVoting/BestEffort decisions).
    /// Null for AllAgree or FuzzyAgreement decisions.
    /// </summary>
    public SourceType? WinningSource { get; set; }

    /// <summary>
    /// Gets or sets the fuzzy string similarity score (0.0-1.0) if FuzzyAgreement was used.
    /// Null for other decision types.
    /// </summary>
    public double? FuzzySimilarity { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this field requires mandatory manual review.
    /// True for: Conflict decisions, missing required fields, AllSourcesNull for required fields.
    /// </summary>
    public bool RequiresManualReview { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether manual review is suggested (but not mandatory).
    /// True for: BestEffort decisions, low-confidence WeightedVoting for non-critical fields.
    /// </summary>
    public bool SuggestReview { get; set; }

    /// <summary>
    /// Gets or sets the list of conflicting values from different sources (for Conflict decision).
    /// Each tuple contains (Source, Value). Empty for non-conflict decisions.
    /// </summary>
    public List<(SourceType Source, string? Value)> ConflictingValues { get; set; } = new();

    /// <summary>
    /// Gets the validation state for this field fusion result.
    /// </summary>
    public ValidationState Validation { get; } = new();
}
