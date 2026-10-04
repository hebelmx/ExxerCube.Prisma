// <copyright file="FusionMetadataRecordBuilder.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using System.Linq;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Domain.Services;

/// <summary>
/// Builds the <see cref="UnifiedMetadataRecord"/> the review page reads, from the Extractor's
/// <see cref="FusionResult"/>. The per-field fusion detail (value, sources, confidence, agreement, each
/// source's own value) only exists in the Extractor; only the fused <see cref="Expediente"/> crosses into
/// the Reconciliator, so this record is how that detail reaches a reviewer.
/// </summary>
/// <remarks>Pure and deterministic, like <see cref="FieldConflictAlertBuilder"/>.</remarks>
public static class FusionMetadataRecordBuilder
{
    /// <summary>Builds the record for one fused case.</summary>
    /// <param name="fusionResult">The Extractor's fusion result; <see langword="null"/> yields an empty record.</param>
    /// <returns>The record with the fused expediente, per-field matches and conflict alerts.</returns>
    public static UnifiedMetadataRecord From(FusionResult? fusionResult)
    {
        if (fusionResult is null)
        {
            return new UnifiedMetadataRecord();
        }

        var matches = new Dictionary<string, FieldMatchResult>(StringComparer.Ordinal);
        foreach (var (fieldName, field) in fusionResult.FieldResults.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            // A field every source left empty is not an agreement; listing it would show a reviewer an
            // "Agreed" row at 0%.
            if (field.Decision == FusionDecision.AllSourcesNull)
            {
                continue;
            }

            var hasConflict = field.Decision == FusionDecision.Conflict
                || fusionResult.ConflictingFields.Contains(fieldName);

            matches[fieldName] = new FieldMatchResult
            {
                FieldName = fieldName,
                MatchedValue = field.Value,
                Confidence = (float)field.Confidence,
                SourceType = DescribeSources(field),
                HasConflict = hasConflict,
                AgreementLevel = AgreementOf(field),
                AllValues = SourceValuesOf(fieldName, field),
            };
        }

        return new UnifiedMetadataRecord
        {
            Expediente = fusionResult.FusedExpediente,
            MatchedFields = new MatchedFields
            {
                FieldMatches = matches,
                OverallAgreement = matches.Count == 0 ? 0f : matches.Values.Average(m => m.AgreementLevel),
                ConflictingFields = fusionResult.ConflictingFields.ToList(),
                MissingFields = fusionResult.MissingRequiredFields.ToList(),
            },
            FieldConflictAlerts = FieldConflictAlertBuilder.From(fusionResult),
        };
    }

    /// <summary>The winning source when one was picked, otherwise every contributing source.</summary>
    private static string DescribeSources(FieldFusionResult field)
    {
        if (field.WinningSource is { } winner)
        {
            return winner.Name;
        }

        return field.ContributingSources.Count == 0
            ? string.Empty
            : string.Join(", ", field.ContributingSources.Select(s => s.Name));
    }

    /// <summary>
    /// Full agreement is 1; a fuzzy match is its similarity; otherwise the field's fusion confidence — the
    /// same agreement figure <see cref="FieldConflictAlertBuilder"/> puts on conflict alerts.
    /// </summary>
    private static float AgreementOf(FieldFusionResult field)
    {
        if (field.Decision == FusionDecision.AllAgree)
        {
            return 1f;
        }

        if (field.Decision == FusionDecision.FuzzyAgreement && field.FuzzySimilarity is { } similarity)
        {
            return (float)similarity;
        }

        return (float)field.Confidence;
    }

    /// <summary>
    /// Each source's own value: the recorded per-source values when fusion kept them (conflicts, weighted
    /// votes), the fused value for every contributing source when they all agreed exactly, and nothing for a
    /// fuzzy match, whose per-source spellings were not kept.
    /// </summary>
    private static List<FieldValue> SourceValuesOf(string fieldName, FieldFusionResult field)
    {
        if (field.ConflictingValues.Count > 0)
        {
            return field.ConflictingValues
                .Select(v => new FieldValue(fieldName, v.Value, (float)field.Confidence, v.Source.Name))
                .ToList();
        }

        if (field.Decision == FusionDecision.AllAgree)
        {
            return field.ContributingSources
                .Select(s => new FieldValue(fieldName, field.Value, (float)field.Confidence, s.Name))
                .ToList();
        }

        return new List<FieldValue>();
    }
}
