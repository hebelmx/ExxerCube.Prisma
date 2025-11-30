using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Export.Adaptive;

/// <summary>
/// Detects schema evolution and drift between source data and template definitions.
/// Uses reflection to analyze source object structure and compare against templates.
/// </summary>
public sealed class SchemaEvolutionDetector : ISchemaEvolutionDetector
{
    private readonly ITemplateRepository _templateRepository;
    private readonly ILogger<SchemaEvolutionDetector> _logger;

    /// <summary>
    /// Similarity threshold for fuzzy matching (0.7 = 70% similar).
    /// Fields with similarity >= 0.7 are considered potential renames.
    /// </summary>
    private const double SimilarityThreshold = 0.7;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaEvolutionDetector"/> class.
    /// </summary>
    /// <param name="templateRepository">The template repository.</param>
    /// <param name="logger">The logger.</param>
    public SchemaEvolutionDetector(
        ITemplateRepository templateRepository,
        ILogger<SchemaEvolutionDetector> logger)
    {
        _templateRepository = templateRepository ?? throw new ArgumentNullException(nameof(templateRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<SchemaDriftReport>> DetectDriftAsync(
        object sourceObject,
        TemplateDefinition template,
        CancellationToken cancellationToken = default)
    {
        if (sourceObject == null)
            return Result<SchemaDriftReport>.Failure("Source object cannot be null");

        if (template == null)
            return Result<SchemaDriftReport>.Failure("Template cannot be null");

        await Task.CompletedTask; // Async operation placeholder

        try
        {
            // Extract all field paths from source object
            var sourceFields = ExtractFieldPaths(sourceObject);

            // Get template field paths
            var templateFields = template.FieldMappings
                .Select(fm => fm.SourceFieldPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Detect new fields (in source but not in template)
            var newFields = DetectNewFields(sourceObject, sourceFields, templateFields);

            // Detect missing fields (in template but not in source)
            var missingFields = DetectMissingFields(sourceFields, template.FieldMappings);

            // Detect renamed fields using fuzzy matching
            var renamedFields = DetectRenamedFields(sourceFields, templateFields, template.FieldMappings);

            // Calculate severity
            var severity = CalculateSeverity(newFields, missingFields, renamedFields);

            var report = new SchemaDriftReport
            {
                TemplateId = template.TemplateId,
                TemplateType = template.TemplateType,
                TemplateVersion = template.Version,
                DetectedAt = DateTime.UtcNow,
                Severity = severity,
                NewFields = newFields,
                MissingFields = missingFields,
                RenamedFields = renamedFields
            };

            _logger.LogInformation(
                "Schema drift detection complete for template {TemplateId}: {Summary}",
                template.TemplateId,
                report.Summary);

            return Result<SchemaDriftReport>.Success(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting schema drift for template {TemplateId}", template.TemplateId);
            return Result<SchemaDriftReport>.Failure($"Error detecting schema drift: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<SchemaDriftReport>> DetectDriftForActiveTemplateAsync(
        object sourceObject,
        string templateType,
        CancellationToken cancellationToken = default)
    {
        if (sourceObject == null)
            return Result<SchemaDriftReport>.Failure("Source object cannot be null");

        // Load active (latest) template
        var template = await _templateRepository.GetLatestTemplateAsync(templateType, cancellationToken);

        if (template == null)
            return Result<SchemaDriftReport>.Failure($"No active template found for type '{templateType}'");

        // Delegate to DetectDriftAsync
        return await DetectDriftAsync(sourceObject, template, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<FieldMapping[]>> SuggestFieldMappingsAsync(
        object sourceObject,
        string templateType,
        CancellationToken cancellationToken = default)
    {
        if (sourceObject == null)
            return Result<FieldMapping[]>.Failure("Source object cannot be null");

        await Task.CompletedTask; // Async placeholder

        try
        {
            var suggestions = new List<FieldMapping>();
            var sourceFields = ExtractFieldPaths(sourceObject);

            foreach (var fieldPath in sourceFields)
            {
                var fieldInfo = GetFieldInfo(sourceObject, fieldPath);

                if (fieldInfo == null)
                    continue;

                var dataType = GetDataTypeName(fieldInfo.PropertyType);
                var isRequired = !IsNullableType(fieldInfo.PropertyType);
                var targetField = HumanizeFieldName(fieldPath);

                var mapping = new FieldMapping(
                    sourceFieldPath: fieldPath,
                    targetField: targetField,
                    isRequired: isRequired,
                    dataType: dataType);

                suggestions.Add(mapping);
            }

            _logger.LogInformation(
                "Generated {Count} field mapping suggestions for template type {TemplateType}",
                suggestions.Count,
                templateType);

            return Result<FieldMapping[]>.Success(suggestions.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suggesting field mappings");
            return Result<FieldMapping[]>.Failure($"Error suggesting field mappings: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public double CalculateSimilarity(string fieldName1, string fieldName2)
    {
        if (string.IsNullOrEmpty(fieldName1) || string.IsNullOrEmpty(fieldName2))
            return 0.0;

        // Normalize: lowercase and remove common prefixes/suffixes
        var normalized1 = NormalizeFieldName(fieldName1);
        var normalized2 = NormalizeFieldName(fieldName2);

        // Check for substring containment (e.g., "Name" in "FullName")
        // This handles common field naming patterns
        if (normalized1.Contains(normalized2) || normalized2.Contains(normalized1))
        {
            var shorter = Math.Min(normalized1.Length, normalized2.Length);
            var longer = Math.Max(normalized1.Length, normalized2.Length);

            // High similarity if one contains the other
            // Score based on ratio of lengths, but boost to minimum 0.7
            // since substring containment is a strong indicator of field rename
            var containmentSimilarity = (double)shorter / longer;
            var boostedSimilarity = Math.Max(containmentSimilarity, 0.7);
            return Math.Round(boostedSimilarity, 2);
        }

        // Calculate Levenshtein distance
        var distance = ComputeLevenshteinDistance(normalized1, normalized2);
        var maxLength = Math.Max(normalized1.Length, normalized2.Length);

        if (maxLength == 0)
            return 1.0;

        // Convert distance to similarity score (0.0 to 1.0)
        var similarity = 1.0 - ((double)distance / maxLength);

        return Math.Round(similarity, 2);
    }

    /// <inheritdoc />
    public async Task<Result> ValidateTemplateCompatibilityAsync(
        object sourceObject,
        TemplateDefinition template,
        CancellationToken cancellationToken = default)
    {
        var driftResult = await DetectDriftAsync(sourceObject, template, cancellationToken);

        if (driftResult.IsFailure)
            return Result.Failure(driftResult.Error ?? "Unknown error");

        var report = driftResult.Value!;

        // Check if any required fields are missing
        var requiredMissing = report.MissingFields.Where(f => f.IsRequired).ToList();

        if (requiredMissing.Count > 0)
        {
            var missingFieldNames = string.Join(", ", requiredMissing.Select(f => $"'{f.FieldPath}'"));
            return Result.Failure($"Template incompatible: Required field(s) {missingFieldNames} not found in source");
        }

        return Result.Success();
    }

    #region Private Helper Methods

    private HashSet<string> ExtractFieldPaths(object obj, string prefix = "")
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (obj == null)
            return paths;

        var type = obj.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            var fieldPath = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
            paths.Add(fieldPath);

            // For complex types, recursively extract nested paths
            if (IsComplexType(prop.PropertyType) && !IsCollection(prop.PropertyType))
            {
                var value = prop.GetValue(obj);
                if (value != null)
                {
                    var nestedPaths = ExtractFieldPaths(value, fieldPath);
                    foreach (var nested in nestedPaths)
                        paths.Add(nested);
                }
            }
        }

        return paths;
    }

    private List<NewFieldInfo> DetectNewFields(
        object sourceObject,
        HashSet<string> sourceFields,
        HashSet<string> templateFields)
    {
        var newFields = new List<NewFieldInfo>();

        foreach (var fieldPath in sourceFields)
        {
            if (!templateFields.Contains(fieldPath))
            {
                var fieldInfo = GetFieldInfo(sourceObject, fieldPath);
                var sampleValue = GetFieldValue(sourceObject, fieldPath);

                newFields.Add(new NewFieldInfo
                {
                    FieldPath = fieldPath,
                    DetectedType = fieldInfo?.PropertyType.Name ?? "unknown",
                    SampleValue = sampleValue?.ToString()
                });
            }
        }

        return newFields;
    }

    private List<MissingFieldInfo> DetectMissingFields(
        HashSet<string> sourceFields,
        List<FieldMapping> templateMappings)
    {
        var missingFields = new List<MissingFieldInfo>();

        foreach (var mapping in templateMappings)
        {
            if (!sourceFields.Contains(mapping.SourceFieldPath))
            {
                missingFields.Add(new MissingFieldInfo
                {
                    FieldPath = mapping.SourceFieldPath,
                    TargetField = mapping.TargetField,
                    IsRequired = mapping.IsRequired,
                    ExpectedType = mapping.DataType
                });
            }
        }

        return missingFields;
    }

    private List<RenamedFieldInfo> DetectRenamedFields(
        HashSet<string> sourceFields,
        HashSet<string> templateFields,
        List<FieldMapping> templateMappings)
    {
        var renamedFields = new List<RenamedFieldInfo>();

        // For each missing template field, find best matching source field
        var missingTemplateFields = templateFields.Except(sourceFields, StringComparer.OrdinalIgnoreCase).ToList();
        var unmatchedSourceFields = sourceFields.Except(templateFields, StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var templateField in missingTemplateFields)
        {
            double bestScore = 0;
            string? bestMatch = null;

            foreach (var sourceField in unmatchedSourceFields)
            {
                var score = CalculateSimilarity(templateField, sourceField);

                if (score >= SimilarityThreshold && score > bestScore)
                {
                    bestScore = score;
                    bestMatch = sourceField;
                }
            }

            if (bestMatch != null)
            {
                var mapping = templateMappings.FirstOrDefault(m =>
                    m.SourceFieldPath.Equals(templateField, StringComparison.OrdinalIgnoreCase));

                renamedFields.Add(new RenamedFieldInfo
                {
                    OldFieldPath = templateField,
                    SuggestedNewFieldPath = bestMatch,
                    SimilarityScore = bestScore,
                    TargetField = mapping?.TargetField ?? templateField
                });
            }
        }

        return renamedFields;
    }

    private DriftSeverity CalculateSeverity(
        List<NewFieldInfo> newFields,
        List<MissingFieldInfo> missingFields,
        List<RenamedFieldInfo> renamedFields)
    {
        // Get the field paths that have renamed candidates
        var renamedFieldPaths = renamedFields.Select(r => r.OldFieldPath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // High severity: Required fields missing WITHOUT renamed candidates
        var missingRequiredWithoutRename = missingFields
            .Where(f => f.IsRequired && !renamedFieldPaths.Contains(f.FieldPath))
            .ToList();

        if (missingRequiredWithoutRename.Count > 0)
            return DriftSeverity.High;

        // Medium severity: Renamed fields or missing optional fields
        if (renamedFields.Count > 0 || missingFields.Count > 0)
            return DriftSeverity.Medium;

        // Low severity: Only new fields
        if (newFields.Count > 0)
            return DriftSeverity.Low;

        return DriftSeverity.None;
    }

    private PropertyInfo? GetFieldInfo(object obj, string fieldPath)
    {
        var parts = fieldPath.Split('.');
        var currentType = obj.GetType();
        PropertyInfo? lastProp = null;

        foreach (var part in parts)
        {
            var prop = currentType.GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null)
                return null;

            lastProp = prop;
            currentType = prop.PropertyType;
        }

        return lastProp;
    }

    private object? GetFieldValue(object obj, string fieldPath)
    {
        var parts = fieldPath.Split('.');
        object? current = obj;

        foreach (var part in parts)
        {
            if (current == null)
                return null;

            var prop = current.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null)
                return null;

            current = prop.GetValue(current);
        }

        return current;
    }

    private bool IsComplexType(Type type)
    {
        return !type.IsPrimitive
               && type != typeof(string)
               && type != typeof(decimal)
               && type != typeof(DateTime)
               && type != typeof(DateTimeOffset)
               && type != typeof(Guid);
    }

    private bool IsCollection(Type type)
    {
        return type != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
    }

    private bool IsNullableType(Type type)
    {
        return Nullable.GetUnderlyingType(type) != null || !type.IsValueType;
    }

    private string GetDataTypeName(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        return underlyingType.Name switch
        {
            "String" => "string",
            "Int32" => "int",
            "Int64" => "long",
            "Decimal" => "decimal",
            "Double" => "double",
            "Boolean" => "bool",
            "DateTime" => "datetime",
            "Guid" => "guid",
            _ => underlyingType.Name.ToLowerInvariant()
        };
    }

    private string HumanizeFieldName(string fieldPath)
    {
        // Convert "Expediente.NumeroExpediente" → "Numero Expediente"
        var parts = fieldPath.Split('.');
        var lastPart = parts.Last();

        // Insert spaces before capital letters
        var humanized = System.Text.RegularExpressions.Regex.Replace(lastPart, "([A-Z])", " $1").Trim();

        return humanized;
    }

    private string NormalizeFieldName(string fieldName)
    {
        // Lowercase and remove common prefixes/suffixes
        var normalized = fieldName.ToLowerInvariant();

        // Remove common prefixes
        var prefixes = new[] { "get", "set", "is", "has", "the" };
        foreach (var prefix in prefixes)
        {
            if (normalized.StartsWith(prefix))
                normalized = normalized.Substring(prefix.Length);
        }

        // Remove common suffixes
        var suffixes = new[] { "field", "property", "value" };
        foreach (var suffix in suffixes)
        {
            if (normalized.EndsWith(suffix))
                normalized = normalized.Substring(0, normalized.Length - suffix.Length);
        }

        return normalized.Trim();
    }

    private int ComputeLevenshteinDistance(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1))
            return string.IsNullOrEmpty(s2) ? 0 : s2.Length;

        if (string.IsNullOrEmpty(s2))
            return s1.Length;

        var len1 = s1.Length;
        var len2 = s2.Length;
        var matrix = new int[len1 + 1, len2 + 1];

        for (var i = 0; i <= len1; i++)
            matrix[i, 0] = i;

        for (var j = 0; j <= len2; j++)
            matrix[0, j] = j;

        for (var i = 1; i <= len1; i++)
        {
            for (var j = 1; j <= len2; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;

                matrix[i, j] = Math.Min(
                    Math.Min(
                        matrix[i - 1, j] + 1,      // Deletion
                        matrix[i, j - 1] + 1),     // Insertion
                    matrix[i - 1, j - 1] + cost);  // Substitution
            }
        }

        return matrix[len1, len2];
    }

    #endregion
}
