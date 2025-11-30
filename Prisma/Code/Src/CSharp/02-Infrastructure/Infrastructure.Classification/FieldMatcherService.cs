using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IndQuestResults;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enums;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Sources;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Classification;

/// <summary>
/// Field matcher service implementation for matching field values across different document formats.
/// Implements <see cref="IFieldMatcher{T}"/> to match fields from XML, DOCX, and PDF sources.
/// </summary>
/// <typeparam name="T">The document source type (e.g., <see cref="DocxSource"/>, <see cref="PdfSource"/>, <see cref="XmlSource"/>).</typeparam>
public class FieldMatcherService<T> : IFieldMatcher<T>
{
    private readonly IFieldExtractor<T> _fieldExtractor;
    private readonly IMatchingPolicy _matchingPolicy;
    private readonly NameMatchingPolicy _nameMatchingPolicy;
    private readonly ILogger<FieldMatcherService<T>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldMatcherService{T}"/> class.
    /// </summary>
    /// <param name="fieldExtractor">The field extractor for extracting fields from sources.</param>
    /// <param name="matchingPolicy">The matching policy for determining best values.</param>
    /// <param name="nameMatchingPolicy">Specialized matching policy for person/legal names.</param>
    /// <param name="logger">The logger instance.</param>
    public FieldMatcherService(
        IFieldExtractor<T> fieldExtractor,
        IMatchingPolicy matchingPolicy,
        NameMatchingPolicy nameMatchingPolicy,
        ILogger<FieldMatcherService<T>> logger)
    {
        _fieldExtractor = fieldExtractor;
        _matchingPolicy = matchingPolicy;
        _nameMatchingPolicy = nameMatchingPolicy;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<MatchedFields>> MatchFieldsAsync(List<T> sources, FieldDefinition[] fieldDefinitions)
    {
        try
        {
            _logger.LogDebug("Matching fields across {SourceCount} sources for {FieldCount} fields", sources.Count, fieldDefinitions.Length);

            if (sources == null || sources.Count == 0)
            {
                return Result<MatchedFields>.WithFailure("No sources provided for field matching");
            }

            if (fieldDefinitions == null || fieldDefinitions.Length == 0)
            {
                return Result<MatchedFields>.WithFailure("No field definitions provided");
            }

            var matchedFields = new MatchedFields();
            var allFieldValues = new Dictionary<string, List<FieldValue>>();
            var additionalFromSources = new List<ExtractedFields>();

            // Extract fields from each source
            foreach (var source in sources)
            {
                var extractResult = await _fieldExtractor.ExtractFieldsAsync(source, fieldDefinitions);
                if (extractResult.IsFailure)
                {
                    _logger.LogWarning("Failed to extract fields from source: {Error}", extractResult.Error);
                    continue;
                }

                var extractedFields = extractResult.Value;
                if (extractedFields == null)
                {
                    continue;
                }
                additionalFromSources.Add(extractedFields);

                // Collect field values from this source
                foreach (var fieldDef in fieldDefinitions)
                {
                    var fieldValue = GetFieldValue(extractedFields, fieldDef.FieldName, GetSourceType(source));
                    if (fieldValue != null)
                    {
                        if (!allFieldValues.ContainsKey(fieldDef.FieldName))
                        {
                            allFieldValues[fieldDef.FieldName] = new List<FieldValue>();
                        }
                        allFieldValues[fieldDef.FieldName].Add(fieldValue);
                    }
                }
            }

            // Match fields using matching policy
            foreach (var fieldDef in fieldDefinitions)
            {
                if (allFieldValues.TryGetValue(fieldDef.FieldName, out var values) && values.Count > 0)
                {
                    var matchResult = await SelectBestWithPolicyAsync(fieldDef.FieldName, values);
                    if (matchResult.IsSuccess && matchResult.Value != null)
                    {
                        matchedFields.FieldMatches[fieldDef.FieldName] = matchResult.Value;

                        // Track conflicts
                        if (matchResult.Value.HasConflict)
                        {
                            matchedFields.ConflictingFields.Add(fieldDef.FieldName);
                        }
                    }
                }
                else
                {
                    // Field missing from all sources
                    matchedFields.MissingFields.Add(fieldDef.FieldName);
                }
            }

            // Calculate overall agreement
            if (matchedFields.FieldMatches.Count > 0)
            {
                var agreementLevels = matchedFields.FieldMatches.Values.Select(m => m.AgreementLevel).ToList();
                matchedFields.OverallAgreement = agreementLevels.Average();
            }

            // Merge additional fields (non-core) across sources
            if (additionalFromSources.Count > 0)
            {
                var xmlFields = CollectAdditional(additionalFromSources, FieldOrigin.Xml);
                var ocrFields = CollectAdditional(additionalFromSources, FieldOrigin.PdfOcr);
                var mergeResult = MergeAdditionalFields(xmlFields, ocrFields);
                matchedFields.AdditionalMerged = mergeResult.Merged;
                matchedFields.AdditionalConflicts = mergeResult.Conflicts;
            }

            _logger.LogDebug("Field matching completed. Matched: {MatchedCount}, Conflicts: {ConflictCount}, Missing: {MissingCount}, Overall Agreement: {Agreement}",
                matchedFields.FieldMatches.Count, matchedFields.ConflictingFields.Count, matchedFields.MissingFields.Count, matchedFields.OverallAgreement);

            return Result<MatchedFields>.Success(matchedFields);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error matching fields across sources");
            return Result<MatchedFields>.WithFailure($"Error matching fields: {ex.Message}", default(MatchedFields), ex);
        }
    }

    /// <inheritdoc />
    public Task<Result<UnifiedMetadataRecord>> GenerateUnifiedRecordAsync(
        MatchedFields matchedFields,
        Expediente? expediente = null,
        ClassificationResult? classification = null)
    {
        try
        {
            _logger.LogDebug("Generating unified metadata record from matched fields");

            if (matchedFields == null)
            {
                return Task.FromResult(Result<UnifiedMetadataRecord>.WithFailure("MatchedFields cannot be null"));
            }

            // Create ExtractedFields from matched fields
            var extractedFields = new ExtractedFields();
            foreach (var match in matchedFields.FieldMatches)
            {
                ApplyMatchedFieldToExtractedFields(extractedFields, match.Key, match.Value.MatchedValue);
            }

            var unifiedRecord = new UnifiedMetadataRecord
            {
                Expediente = expediente,
                ExtractedFields = extractedFields,
                Classification = classification,
                MatchedFields = matchedFields
            };

            _logger.LogDebug("Successfully generated unified metadata record");
            return Task.FromResult(Result<UnifiedMetadataRecord>.Success(unifiedRecord));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating unified metadata record");
            return Task.FromResult(Result<UnifiedMetadataRecord>.WithFailure($"Error generating unified record: {ex.Message}", default(UnifiedMetadataRecord), ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result> ValidateCompletenessAsync(MatchedFields matchedFields, List<string> requiredFields)
    {
        try
        {
            if (matchedFields == null)
            {
                return Result.WithFailure("MatchedFields cannot be null");
            }

            if (requiredFields == null || requiredFields.Count == 0)
            {
                return Result.Success();
            }

            var missingRequired = requiredFields.Where(f => !matchedFields.FieldMatches.ContainsKey(f) || string.IsNullOrWhiteSpace(matchedFields.FieldMatches[f].MatchedValue)).ToList();

            if (missingRequired.Count > 0)
            {
                return Result.WithFailure($"Required fields missing or empty: {string.Join(", ", missingRequired)}");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating completeness");
            return Result.WithFailure($"Error validating completeness: {ex.Message}", ex);
        }
    }

    private static FieldValue? GetFieldValue(ExtractedFields fields, string fieldName, string sourceType)
    {
        var value = fieldName.ToLowerInvariant() switch
        {
            "expediente" => fields.Expediente,
            "causa" => fields.Causa,
            "accionsolicitada" or "accion_solicitada" => fields.AccionSolicitada,
            _ => null
        };

        if (value != null)
        {
            return new FieldValue(fieldName, value, 1.0f, sourceType, ToOrigin(sourceType));
        }

        return null;
    }

    private Task<Result<FieldMatchResult>> SelectBestWithPolicyAsync(string fieldName, List<FieldValue> values)
    {
        if (IsNameField(fieldName))
        {
            return _nameMatchingPolicy.SelectBestValueAsync(fieldName, values);
        }

        return _matchingPolicy.SelectBestValueAsync(fieldName, values);
    }

    private static bool IsNameField(string fieldName) =>
        fieldName.Contains("NOMBRE", StringComparison.OrdinalIgnoreCase);

    private static string GetSourceType(T source)
    {
        return source switch
        {
            DocxSource => "DOCX",
            PdfSource => "PDF",
            XmlSource => "XML",
            _ => "UNKNOWN"
        };
    }

    private static FieldOrigin ToOrigin(string sourceType) =>
        sourceType.ToUpperInvariant() switch
        {
            "XML" => FieldOrigin.Xml,
            "PDF" => FieldOrigin.PdfOcr,
            "DOCX" => FieldOrigin.Docx,
            _ => FieldOrigin.Unknown
        };

    private static void ApplyMatchedFieldToExtractedFields(ExtractedFields fields, string fieldName, string? value)
    {
        switch (fieldName.ToLowerInvariant())
        {
            case "expediente":
                fields.Expediente = value;
                break;
            case "causa":
                fields.Causa = value;
                break;
            case "accionsolicitada":
            case "accion_solicitada":
                fields.AccionSolicitada = value;
                break;
        }
    }

    private static Dictionary<string, string?> CollectAdditional(List<ExtractedFields> sources, FieldOrigin origin)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var fields in sources)
        {
            if (fields == null || fields.AdditionalFields == null) continue;
            foreach (var kvp in fields.AdditionalFields)
            {
                // Skip empty values
                if (string.IsNullOrWhiteSpace(kvp.Value)) continue;

                // Only take those from matching origin if known
                if (origin == FieldOrigin.Xml && !kvp.Key.Equals("Origin", StringComparison.OrdinalIgnoreCase))
                {
                    dict[kvp.Key] = kvp.Value;
                }
                else if (origin == FieldOrigin.PdfOcr && !kvp.Key.Equals("Origin", StringComparison.OrdinalIgnoreCase))
                {
                    dict[kvp.Key] = kvp.Value;
                }
            }
        }
        return dict;
    }

    private static AdditionalFieldMergeResult MergeAdditionalFields(
        IReadOnlyDictionary<string, string?> xmlFields,
        IReadOnlyDictionary<string, string?> ocrFields)
    {
        var result = new AdditionalFieldMergeResult();
        var merged = result.Merged;

        foreach (var kvp in xmlFields)
        {
            merged[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in ocrFields)
        {
            var key = kvp.Key;
            var ocrValue = Normalize(kvp.Value);
            if (!merged.TryGetValue(key, out var existing))
            {
                merged[key] = kvp.Value;
                continue;
            }

            var existingNormalized = Normalize(existing);
            if (!string.IsNullOrWhiteSpace(ocrValue) &&
                !string.IsNullOrWhiteSpace(existingNormalized) &&
                !string.Equals(existingNormalized, ocrValue, StringComparison.OrdinalIgnoreCase))
            {
                result.Conflicts.Add(key);
            }
            else if (string.IsNullOrWhiteSpace(existing) && !string.IsNullOrWhiteSpace(ocrValue))
            {
                merged[key] = kvp.Value;
            }
        }

        return result;
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}

