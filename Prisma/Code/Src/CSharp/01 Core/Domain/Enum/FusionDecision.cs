// <copyright file="FusionDecision.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Represents the decision type made during field-level data fusion.
/// Indicates how a particular field value was selected from multiple sources.
/// </summary>
/// <remarks>
/// Fusion decision flow:
/// 1. AllSourcesNull - No valid data from any source
/// 2. AllAgree - Exact match across all sources (highest confidence)
/// 3. FuzzyAgreement - Sources agree after fuzzy matching (e.g., name variations)
/// 4. WeightedVoting - Sources disagree, selected by reliability weighting
/// 5. Conflict - Irreconcilable conflict requiring manual review
/// 6. BestEffort - Low confidence selection, recommend review
/// </remarks>
public class FusionDecision : EnumModel
{
    /// <summary>
    /// All sources returned null or invalid values for this field.
    /// Confidence: 0.0
    /// Action: Flag for manual entry if field is required.
    /// </summary>
    public static readonly FusionDecision AllSourcesNull
        = new(1, "AllSourcesNull", "Todas las fuentes nulas");

    /// <summary>
    /// All sources agree on the exact same value (after sanitization).
    /// Confidence: 0.95 (3 sources) or 0.85 (2 sources)
    /// Action: Auto-accept, highest confidence.
    /// </summary>
    public static readonly FusionDecision AllAgree
        = new(2, "AllAgree", "Todas coinciden");

    /// <summary>
    /// Sources agree after fuzzy string matching (e.g., name fields).
    /// Confidence: Similarity score * 0.90 (typically 0.85-0.95)
    /// Action: Use canonical form, generally safe to auto-accept.
    /// </summary>
    public static readonly FusionDecision FuzzyAgreement
        = new(3, "FuzzyAgreement", "Coincidencia aproximada");

    /// <summary>
    /// Sources disagree, value selected by weighted voting using source reliability.
    /// Confidence: Winner's reliability score (typically 0.70-0.85)
    /// Action: Auto-accept if confidence > threshold, otherwise flag for review.
    /// </summary>
    public static readonly FusionDecision WeightedVoting
        = new(4, "WeightedVoting", "Votación ponderada");

    /// <summary>
    /// Irreconcilable conflict between sources for a critical or required field.
    /// Confidence: 0.0
    /// Action: Mandatory manual review, cannot auto-process.
    /// </summary>
    public static readonly FusionDecision Conflict
        = new(5, "Conflict", "Conflicto");

    /// <summary>
    /// Low-confidence selection when sources disagree and confidence margin is narrow.
    /// Confidence: Winner's score * 0.70 (typically 0.50-0.70)
    /// Action: Suggest manual review, may auto-process for non-critical fields.
    /// </summary>
    public static readonly FusionDecision BestEffort
        = new(6, "BestEffort", "Mejor esfuerzo");

    /// <summary>
    /// Initializes a new instance of the <see cref="FusionDecision"/> class.
    /// Parameterless constructor required by EF Core for entity materialization.
    /// </summary>
    public FusionDecision()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FusionDecision"/> class with specified values.
    /// </summary>
    /// <param name="value">The decision type code.</param>
    /// <param name="name">The internal name.</param>
    /// <param name="displayName">The display name.</param>
    private FusionDecision(int value, string name, string displayName = "")
        : base(value, name, displayName)
    {
    }

    /// <summary>
    /// Creates a FusionDecision instance from an integer value.
    /// </summary>
    /// <param name="value">The decision type code.</param>
    /// <returns>Matching FusionDecision instance.</returns>
    public static FusionDecision FromValue(int value) => FromValue<FusionDecision>(value);

    /// <summary>
    /// Creates a FusionDecision instance from a name.
    /// </summary>
    /// <param name="name">The internal name.</param>
    /// <returns>Matching FusionDecision instance.</returns>
    public static FusionDecision FromName(string name) => FromName<FusionDecision>(name);

    /// <summary>
    /// Creates a FusionDecision instance from a display name.
    /// </summary>
    /// <param name="displayName">The display name.</param>
    /// <returns>Matching FusionDecision instance.</returns>
    public static FusionDecision FromDisplayName(string displayName) => FromDisplayName<FusionDecision>(displayName);

    /// <summary>
    /// Implicit conversion to int for database storage and comparisons.
    /// </summary>
    /// <param name="type">The FusionDecision to convert.</param>
    public static implicit operator int(FusionDecision type) => type.Value;

    /// <summary>
    /// Implicit conversion from int to FusionDecision.
    /// </summary>
    /// <param name="value">The decision type code.</param>
    public static implicit operator FusionDecision(int value) => FromValue(value);
}
