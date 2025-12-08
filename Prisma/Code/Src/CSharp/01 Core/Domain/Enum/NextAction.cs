// <copyright file="NextAction.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Represents the recommended next action after data fusion and classification.
/// Determines whether the Expediente can be auto-processed or requires human review.
/// </summary>
/// <remarks>
/// Decision thresholds (configurable via FusionCoefficients):
/// - AutoProcess: Overall confidence > 0.85 AND no conflicting fields AND all required fields present
/// - ReviewRecommended: Overall confidence 0.70-0.85 OR some optional field conflicts
/// - ManualReviewRequired: Overall confidence &lt; 0.70 OR missing required fields OR critical field conflicts
///
/// Legal compliance: R29 A-2911 requires 42 mandatory fields with NO NULLS PERMITTED.
/// Any Expediente missing required fields MUST be flagged for manual review.
/// </remarks>
public class NextAction : EnumModel
{
    /// <summary>
    /// High confidence fusion result, safe for automated processing.
    /// Criteria: Confidence > 0.85, all required fields present, no conflicts.
    /// Action: Proceed to automated compliance validation and R29 report generation.
    /// </summary>
    public static readonly NextAction AutoProcess
        = new(1, "AutoProcess", "Procesar automáticamente");

    /// <summary>
    /// Moderate confidence fusion result, recommended for human review before processing.
    /// Criteria: Confidence 0.70-0.85, all required fields present, minor conflicts in optional fields.
    /// Action: Queue for review, highlight low-confidence and conflicting fields.
    /// </summary>
    public static readonly NextAction ReviewRecommended
        = new(2, "ReviewRecommended", "Revisar recomendado");

    /// <summary>
    /// Low confidence or incomplete data, mandatory manual review required.
    /// Criteria: Confidence &lt; 0.70 OR missing required fields OR critical field conflicts.
    /// Action: Block automated processing, assign to human operator for data entry/correction.
    /// </summary>
    public static readonly NextAction ManualReviewRequired
        = new(3, "ManualReviewRequired", "Revisión manual requerida");

    /// <summary>
    /// Initializes a new instance of the <see cref="NextAction"/> class.
    /// Parameterless constructor required by EF Core for entity materialization.
    /// </summary>
    public NextAction()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NextAction"/> class with specified values.
    /// </summary>
    /// <param name="value">The action code.</param>
    /// <param name="name">The internal name.</param>
    /// <param name="displayName">The display name.</param>
    private NextAction(int value, string name, string displayName = "")
        : base(value, name, displayName)
    {
    }

    /// <summary>
    /// Creates a NextAction instance from an integer value.
    /// </summary>
    /// <param name="value">The action code.</param>
    /// <returns>Matching NextAction instance.</returns>
    public static NextAction FromValue(int value) => FromValue<NextAction>(value);

    /// <summary>
    /// Creates a NextAction instance from a name.
    /// </summary>
    /// <param name="name">The internal name.</param>
    /// <returns>Matching NextAction instance.</returns>
    public static NextAction FromName(string name) => FromName<NextAction>(name);

    /// <summary>
    /// Creates a NextAction instance from a display name.
    /// </summary>
    /// <param name="displayName">The display name.</param>
    /// <returns>Matching NextAction instance.</returns>
    public static NextAction FromDisplayName(string displayName) => FromDisplayName<NextAction>(displayName);

    /// <summary>
    /// Implicit conversion to int for database storage and comparisons.
    /// </summary>
    /// <param name="type">The NextAction to convert.</param>
    public static implicit operator int(NextAction type) => type.Value;

    /// <summary>
    /// Implicit conversion from int to NextAction.
    /// </summary>
    /// <param name="value">The action code.</param>
    public static implicit operator NextAction(int value) => FromValue(value);
}
