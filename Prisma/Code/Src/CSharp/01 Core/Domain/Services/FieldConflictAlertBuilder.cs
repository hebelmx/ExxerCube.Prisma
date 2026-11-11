// <copyright file="FieldConflictAlertBuilder.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using System.Linq;

namespace ExxerCube.Prisma.Domain.Services;

using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Pure, side-effect-free mapper that converts a <see cref="FusionResult"/> into a list of
/// <see cref="FieldConflictAlert"/> instances (alertamientos).
/// </summary>
/// <remarks>
/// A field produces an alert when its <see cref="FieldFusionResult.Decision"/> is either
/// <see cref="FusionDecision.Conflict"/> or <see cref="FusionDecision.WeightedVoting"/> — both
/// decisions indicate that sources disagreed on the value. When all fields agree the returned list
/// is empty (no alertamiento).
/// </remarks>
public static class FieldConflictAlertBuilder
{
    /// <summary>
    /// Builds the list of <see cref="FieldConflictAlert"/> instances from the given
    /// <paramref name="fusionResult"/>.
    /// </summary>
    /// <param name="fusionResult">
    /// The fusion result whose <see cref="FusionResult.FieldResults"/> are inspected.
    /// When <see langword="null"/> an empty list is returned.
    /// </param>
    /// <returns>
    /// One <see cref="FieldConflictAlert"/> per conflicting field, ordered by field name.
    /// Returns an empty list when <paramref name="fusionResult"/> is <see langword="null"/>
    /// or no fields disagree.
    /// </returns>
    public static List<FieldConflictAlert> From(FusionResult? fusionResult)
    {
        if (fusionResult is null)
        {
            return new List<FieldConflictAlert>();
        }

        var alerts = new List<FieldConflictAlert>();

        foreach (var (fieldName, fieldResult) in fusionResult.FieldResults)
        {
            if (fieldResult.Decision != FusionDecision.Conflict &&
                fieldResult.Decision != FusionDecision.WeightedVoting)
            {
                continue;
            }

            // Map the per-source tuples from FieldFusionResult to ConflictingSourceValue VOs.
            var sourceValues = fieldResult.ConflictingValues
                .Select(tuple => new ConflictingSourceValue(tuple.Source, tuple.Value))
                .ToList();

            var alert = new FieldConflictAlert(
                fieldName: fieldName,
                conflictingValues: sourceValues,
                agreementLevel: (float)fieldResult.Confidence);

            alerts.Add(alert);
        }

        // Deterministic order for tests and serialisation.
        alerts.Sort((a, b) => string.Compare(a.FieldName, b.FieldName, StringComparison.Ordinal));

        return alerts;
    }
}
