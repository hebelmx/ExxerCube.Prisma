// <copyright file="FieldConflictAlert.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents a cross-document field mismatch alert (alertamiento) produced by the fusion stage.
/// Captures the field name, the per-source conflicting values, and the agreement level across sources.
/// </summary>
public sealed class FieldConflictAlert
{
    /// <summary>
    /// Gets the name of the field that has conflicting values across document sources.
    /// </summary>
    public string FieldName { get; }

    /// <summary>
    /// Gets the per-source values that disagree for this field.
    /// Each entry carries the <see cref="ConflictingSourceValue.Source"/> and the value it provided.
    /// </summary>
    public IReadOnlyList<ConflictingSourceValue> ConflictingValues { get; }

    /// <summary>
    /// Gets the agreement level (0.0–1.0) across sources for this field, as computed by the
    /// fusion engine. Lower values indicate stronger disagreement.
    /// </summary>
    public float AgreementLevel { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldConflictAlert"/> class.
    /// </summary>
    /// <param name="fieldName">The name of the conflicting field.</param>
    /// <param name="conflictingValues">The per-source values that disagree.</param>
    /// <param name="agreementLevel">The agreement level (0.0–1.0).</param>
    public FieldConflictAlert(
        string fieldName,
        IReadOnlyList<ConflictingSourceValue> conflictingValues,
        float agreementLevel)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("Field name cannot be null or whitespace.", nameof(fieldName));

        FieldName = fieldName;
        ConflictingValues = conflictingValues ?? throw new ArgumentNullException(nameof(conflictingValues));
        AgreementLevel = agreementLevel;
    }
}
