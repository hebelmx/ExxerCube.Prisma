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
using NSubstitute;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Builds the contract-conforming NSubstitute mock that backs the blueprint instance
/// of <see cref="SchemaEvolutionDetectorContract"/>.
/// </summary>
/// <remarks>
/// <para>
/// The contract asserts real schema-drift outcomes (reflection field extraction, new/missing/
/// renamed detection, severity, Levenshtein/containment similarity, mapping suggestion), which
/// canned stubs cannot satisfy. The factory therefore evolved into a <em>reference fake</em>
/// of the documented detector semantics — the same algorithm the production
/// <c>SchemaEvolutionDetector</c> implements, expressed over Domain types only. The active-template
/// lookup reads a backing store the blueprint seeds via the contract's seed hook. This is exactly
/// the evolution ADR-005 §6 expects.
/// </para>
/// </remarks>
public static class SchemaEvolutionDetectorMockFactory
{
    private const double SimilarityThreshold = 0.7;

    /// <summary>
    /// Creates an <see cref="ISchemaEvolutionDetector"/> mock that satisfies every test in
    /// <see cref="SchemaEvolutionDetectorContract"/>, backed by the supplied active-template store.
    /// </summary>
    /// <param name="templateStore">
    /// The shared store the blueprint instance seeds; the fake reads its latest active template
    /// for <see cref="ISchemaEvolutionDetector.DetectDriftForActiveTemplateAsync"/>.
    /// </param>
    /// <returns>The configured mock.</returns>
    public static ISchemaEvolutionDetector CreateContractConformingMock(List<TemplateDefinition> templateStore)
    {
        var mock = Substitute.For<ISchemaEvolutionDetector>();

        mock.DetectDriftAsync(Arg.Any<object>(), Arg.Any<TemplateDefinition>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(DetectDrift(call.ArgAt<object>(0), call.ArgAt<TemplateDefinition>(1))));

        mock.DetectDriftForActiveTemplateAsync(Arg.Any<object>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(DetectDriftForActive(
                templateStore, call.ArgAt<object>(0), call.ArgAt<string>(1))));

        mock.SuggestFieldMappingsAsync(Arg.Any<object>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(SuggestFieldMappings(call.ArgAt<object>(0))));

        mock.CalculateSimilarity(Arg.Any<string>(), Arg.Any<string>())
            .Returns(call => CalculateSimilarity(call.ArgAt<string>(0), call.ArgAt<string>(1)));

        mock.ValidateTemplateCompatibilityAsync(Arg.Any<object>(), Arg.Any<TemplateDefinition>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(ValidateCompatibility(call.ArgAt<object>(0), call.ArgAt<TemplateDefinition>(1))));

        return mock;
    }

    private static Result<SchemaDriftReport> DetectDrift(object sourceObject, TemplateDefinition template)
    {
        if (sourceObject == null)
            return Result<SchemaDriftReport>.Failure("Source object cannot be null");

        if (template == null)
            return Result<SchemaDriftReport>.Failure("Template cannot be null");

        var sourceFields = ExtractFieldPaths(sourceObject);
        var templateFields = template.FieldMappings
            .Select(fm => fm.SourceFieldPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newFields = DetectNewFields(sourceObject, sourceFields, templateFields);
        var missingFields = DetectMissingFields(sourceFields, template.FieldMappings);
        var renamedFields = DetectRenamedFields(sourceFields, templateFields, template.FieldMappings);
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

        return Result<SchemaDriftReport>.Success(report);
    }

    private static Result<SchemaDriftReport> DetectDriftForActive(
        List<TemplateDefinition> templateStore, object sourceObject, string templateType)
    {
        if (sourceObject == null)
            return Result<SchemaDriftReport>.Failure("Source object cannot be null");

        var now = DateTime.UtcNow;
        var template = templateStore
            .Where(t =>
                t.TemplateType == templateType &&
                t.IsActive &&
                t.EffectiveDate <= now &&
                (t.ExpirationDate == null || t.ExpirationDate > now))
            .OrderByDescending(t => t.EffectiveDate)
            .FirstOrDefault();

        if (template == null)
            return Result<SchemaDriftReport>.Failure($"No active template found for type '{templateType}'");

        return DetectDrift(sourceObject, template);
    }

    private static Result<FieldMapping[]> SuggestFieldMappings(object sourceObject)
    {
        if (sourceObject == null)
            return Result<FieldMapping[]>.Failure("Source object cannot be null");

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

            suggestions.Add(new FieldMapping(
                sourceFieldPath: fieldPath,
                targetField: targetField,
                isRequired: isRequired,
                dataType: dataType));
        }

        return Result<FieldMapping[]>.Success(suggestions.ToArray());
    }

    private static double CalculateSimilarity(string fieldName1, string fieldName2)
    {
        if (string.IsNullOrEmpty(fieldName1) || string.IsNullOrEmpty(fieldName2))
            return 0.0;

        var normalized1 = NormalizeFieldName(fieldName1);
        var normalized2 = NormalizeFieldName(fieldName2);

        if (normalized1.Contains(normalized2) || normalized2.Contains(normalized1))
        {
            var shorter = Math.Min(normalized1.Length, normalized2.Length);
            var longer = Math.Max(normalized1.Length, normalized2.Length);

            var containmentSimilarity = (double)shorter / longer;
            var boostedSimilarity = Math.Max(containmentSimilarity, 0.7);
            return Math.Round(boostedSimilarity, 2);
        }

        var distance = ComputeLevenshteinDistance(normalized1, normalized2);
        var maxLength = Math.Max(normalized1.Length, normalized2.Length);

        if (maxLength == 0)
            return 1.0;

        var similarity = 1.0 - ((double)distance / maxLength);

        return Math.Round(similarity, 2);
    }

    private static Result ValidateCompatibility(object sourceObject, TemplateDefinition template)
    {
        var driftResult = DetectDrift(sourceObject, template);

        if (driftResult.IsFailure)
            return Result.Failure(driftResult.Error ?? "Unknown error");

        var report = driftResult.Value!;
        var requiredMissing = report.MissingFields.Where(f => f.IsRequired).ToList();

        if (requiredMissing.Count > 0)
        {
            var missingFieldNames = string.Join(", ", requiredMissing.Select(f => $"'{f.FieldPath}'"));
            return Result.Failure($"Template incompatible: Required field(s) {missingFieldNames} not found in source");
        }

        return Result.Success();
    }

    //
    // Private reflection/analysis helpers (ported from SchemaEvolutionDetector, minus logging)
    //

    private static HashSet<string> ExtractFieldPaths(object obj, string prefix = "")
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

            if (IsComplexType(prop.PropertyType) && !IsCollection(prop.PropertyType))
            {
                var value = prop.GetValue(obj);
                if (value != null)
                {
                    foreach (var nested in ExtractFieldPaths(value, fieldPath))
                        paths.Add(nested);
                }
            }
        }

        return paths;
    }

    private static List<NewFieldInfo> DetectNewFields(
        object sourceObject, HashSet<string> sourceFields, HashSet<string> templateFields)
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

    private static List<MissingFieldInfo> DetectMissingFields(
        HashSet<string> sourceFields, List<FieldMapping> templateMappings)
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

    private static List<RenamedFieldInfo> DetectRenamedFields(
        HashSet<string> sourceFields, HashSet<string> templateFields, List<FieldMapping> templateMappings)
    {
        var renamedFields = new List<RenamedFieldInfo>();

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

    private static DriftSeverity CalculateSeverity(
        List<NewFieldInfo> newFields, List<MissingFieldInfo> missingFields, List<RenamedFieldInfo> renamedFields)
    {
        var renamedFieldPaths = renamedFields.Select(r => r.OldFieldPath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingRequiredWithoutRename = missingFields
            .Where(f => f.IsRequired && !renamedFieldPaths.Contains(f.FieldPath))
            .ToList();

        if (missingRequiredWithoutRename.Count > 0)
            return DriftSeverity.High;

        if (renamedFields.Count > 0 || missingFields.Count > 0)
            return DriftSeverity.Medium;

        if (newFields.Count > 0)
            return DriftSeverity.Low;

        return DriftSeverity.None;
    }

    private static PropertyInfo? GetFieldInfo(object obj, string fieldPath)
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

    private static object? GetFieldValue(object obj, string fieldPath)
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

    private static bool IsComplexType(Type type)
    {
        return !type.IsPrimitive
               && type != typeof(string)
               && type != typeof(decimal)
               && type != typeof(DateTime)
               && type != typeof(DateTimeOffset)
               && type != typeof(Guid);
    }

    private static bool IsCollection(Type type)
    {
        return type != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
    }

    private static bool IsNullableType(Type type)
    {
        return Nullable.GetUnderlyingType(type) != null || !type.IsValueType;
    }

    private static string GetDataTypeName(Type type)
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

    private static string HumanizeFieldName(string fieldPath)
    {
        var parts = fieldPath.Split('.');
        var lastPart = parts.Last();

        return System.Text.RegularExpressions.Regex.Replace(lastPart, "([A-Z])", " $1").Trim();
    }

    private static string NormalizeFieldName(string fieldName)
    {
        var normalized = fieldName.ToLowerInvariant();

        var prefixes = new[] { "get", "set", "is", "has", "the" };
        foreach (var prefix in prefixes)
        {
            if (normalized.StartsWith(prefix))
                normalized = normalized.Substring(prefix.Length);
        }

        var suffixes = new[] { "field", "property", "value" };
        foreach (var suffix in suffixes)
        {
            if (normalized.EndsWith(suffix))
                normalized = normalized.Substring(0, normalized.Length - suffix.Length);
        }

        return normalized.Trim();
    }

    private static int ComputeLevenshteinDistance(string s1, string s2)
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
                        matrix[i - 1, j] + 1,
                        matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[len1, len2];
    }
}
