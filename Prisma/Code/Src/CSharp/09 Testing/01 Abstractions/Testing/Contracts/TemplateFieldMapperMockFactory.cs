using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using IndQuestResults.Operations;
using NSubstitute;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Builds the contract-conforming NSubstitute mock that backs the blueprint instance
/// of <see cref="TemplateFieldMapperContract"/>.
/// </summary>
/// <remarks>
/// <para>
/// The contract asserts real mapping outcomes (reflection extraction, formatting, the
/// pipe-chained transformation mini-language, and string-rule validation), which a
/// canned stub cannot satisfy. The factory therefore evolved into a <em>reference fake</em>
/// of the documented mapper semantics — the same algorithm the production
/// <c>TemplateFieldMapper</c> implements, expressed here over Domain types only (no
/// logger, no Infrastructure reference). This is exactly the evolution ADR-005 §6 expects.
/// </para>
/// </remarks>
public static class TemplateFieldMapperMockFactory
{
    /// <summary>
    /// Creates an <see cref="ITemplateFieldMapper"/> mock that satisfies every test in
    /// <see cref="TemplateFieldMapperContract"/>.
    /// </summary>
    /// <returns>The configured mock.</returns>
    public static ITemplateFieldMapper CreateContractConformingMock()
    {
        var mock = Substitute.For<ITemplateFieldMapper>();

        mock.MapFieldAsync(Arg.Any<object>(), Arg.Any<FieldMapping>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.ArgAt<CancellationToken>(2).IsCancellationRequested
                ? ResultExtensions.Cancelled<string>()
                : MapField(call.ArgAt<object>(0), call.ArgAt<FieldMapping>(1))));

        mock.MapAllFieldsAsync(Arg.Any<object>(), Arg.Any<TemplateDefinition>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.ArgAt<CancellationToken>(2).IsCancellationRequested
                ? ResultExtensions.Cancelled<Dictionary<string, string>>()
                : MapAllFields(call.ArgAt<object>(0), call.ArgAt<TemplateDefinition>(1))));

        mock.ValidateMappingAsync(Arg.Any<Type>(), Arg.Any<FieldMapping>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.ArgAt<CancellationToken>(2).IsCancellationRequested
                ? ResultExtensions.Cancelled()
                : ValidateMapping(call.ArgAt<Type>(0), call.ArgAt<FieldMapping>(1))));

        mock.ApplyTransformationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.ArgAt<CancellationToken>(2).IsCancellationRequested
                ? ResultExtensions.Cancelled<string>()
                : ApplyTransformation(call.ArgAt<string>(0), call.ArgAt<string>(1))));

        mock.ValidateFieldValueAsync(Arg.Any<string>(), Arg.Any<FieldMapping>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.ArgAt<CancellationToken>(2).IsCancellationRequested
                ? ResultExtensions.Cancelled()
                : ValidateFieldValue(call.ArgAt<string>(0), call.ArgAt<FieldMapping>(1))));

        return mock;
    }

    private static Result<string> MapField(object sourceObject, FieldMapping mapping)
    {
        if (sourceObject == null)
        {
            return Result<string>.Failure("Source object cannot be null");
        }

        if (mapping == null)
        {
            return Result<string>.Failure("Mapping cannot be null");
        }

        var extractionResult = ExtractFieldValue(sourceObject, mapping.SourceFieldPath);

        if (extractionResult.IsFailure)
        {
            if (mapping.IsRequired)
            {
                return Result<string>.Failure($"Required field '{mapping.SourceFieldPath}' not found");
            }

            if (!string.IsNullOrEmpty(mapping.DefaultValue))
            {
                return Result<string>.Success(mapping.DefaultValue);
            }

            return Result<string>.Success(string.Empty);
        }

        var value = extractionResult.Value;

        if (value == null)
        {
            if (mapping.IsRequired && string.IsNullOrEmpty(mapping.DefaultValue))
            {
                return Result<string>.Failure($"Required field '{mapping.SourceFieldPath}' is null");
            }

            return Result<string>.Success(mapping.DefaultValue ?? string.Empty);
        }

        var formattedValue = FormatValue(value, mapping.DataType, mapping.Format);

        if (!string.IsNullOrWhiteSpace(mapping.TransformExpression))
        {
            var transformResult = ApplyTransformation(formattedValue, mapping.TransformExpression);

            if (transformResult.IsFailure)
            {
                return transformResult;
            }

            formattedValue = transformResult.Value;
        }

        if (mapping.ValidationRules != null && mapping.ValidationRules.Count > 0)
        {
            var validationResult = ValidateFieldValue(formattedValue ?? string.Empty, mapping);

            if (validationResult.IsFailure)
            {
                return Result<string>.Failure(validationResult.Error ?? "Validation failed");
            }
        }

        return Result<string>.Success(formattedValue ?? string.Empty);
    }

    private static Result<Dictionary<string, string>> MapAllFields(object sourceObject, TemplateDefinition template)
    {
        if (sourceObject == null)
        {
            return Result<Dictionary<string, string>>.Failure("Source object cannot be null");
        }

        if (template == null)
        {
            return Result<Dictionary<string, string>>.Failure("Template cannot be null");
        }

        var result = new Dictionary<string, string>();

        var sortedMappings = template.FieldMappings
            .OrderBy(m => m.DisplayOrder)
            .ToList();

        foreach (var mapping in sortedMappings)
        {
            var mapResult = MapField(sourceObject, mapping);

            if (mapResult.IsFailure)
            {
                if (mapping.IsRequired)
                {
                    return Result<Dictionary<string, string>>.Failure(mapResult.Error ?? "Required field mapping failed");
                }

                continue;
            }

            result[mapping.TargetField] = mapResult.Value ?? string.Empty;
        }

        return Result<Dictionary<string, string>>.Success(result);
    }

    private static Result ValidateMapping(Type sourceType, FieldMapping mapping)
    {
        if (sourceType == null)
        {
            return Result.Failure("Source type cannot be null");
        }

        if (mapping == null)
        {
            return Result.Failure("Mapping cannot be null");
        }

        var pathParts = mapping.SourceFieldPath.Split('.');
        var currentType = sourceType;

        foreach (var part in pathParts)
        {
            var property = currentType.GetProperty(part, BindingFlags.Public | BindingFlags.Instance);

            if (property == null)
            {
                return Result.Failure(
                    $"Field path '{mapping.SourceFieldPath}' not found on type '{sourceType.Name}' (missing '{part}')");
            }

            currentType = property.PropertyType;
        }

        if (!string.IsNullOrWhiteSpace(mapping.TransformExpression))
        {
            var transformParts = mapping.TransformExpression.Split('|');
            foreach (var transform in transformParts)
            {
                var trimmedTransform = transform.Trim();
                if (!IsValidTransformation(trimmedTransform))
                {
                    return Result.Failure(
                        $"Invalid transformation expression: '{trimmedTransform}' is not supported");
                }
            }
        }

        return Result.Success();
    }

    private static Result<string> ApplyTransformation(string value, string transformExpression)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Result<string>.Success(value ?? string.Empty);
        }

        if (string.IsNullOrWhiteSpace(transformExpression))
        {
            return Result<string>.Success(value);
        }

        try
        {
            var currentValue = value;

            var transformations = transformExpression.Split('|')
                .Select(t => t.Trim())
                .ToList();

            foreach (var transform in transformations)
            {
                currentValue = ApplySingleTransformation(currentValue, transform);
            }

            return Result<string>.Success(currentValue);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Transformation error: {ex.Message}");
        }
    }

    private static Result ValidateFieldValue(string value, FieldMapping mapping)
    {
        if (mapping.ValidationRules == null || mapping.ValidationRules.Count == 0)
        {
            return Result.Success();
        }

        foreach (var rule in mapping.ValidationRules)
        {
            var validationResult = ValidateRule(value, rule);
            if (validationResult.IsFailure)
            {
                return validationResult;
            }
        }

        return Result.Success();
    }

    private static Result<object?> ExtractFieldValue(object sourceObject, string fieldPath)
    {
        try
        {
            var pathParts = fieldPath.Split('.');
            object? currentObject = sourceObject;

            foreach (var part in pathParts)
            {
                if (currentObject == null)
                {
                    return Result<object?>.Failure($"Null reference encountered in path '{fieldPath}' at '{part}'");
                }

                var property = currentObject.GetType()
                    .GetProperty(part, BindingFlags.Public | BindingFlags.Instance);

                if (property == null)
                {
                    return Result<object?>.Failure($"Property '{part}' not found in path '{fieldPath}'");
                }

                currentObject = property.GetValue(currentObject);
            }

            return Result<object?>.Success(currentObject);
        }
        catch (Exception ex)
        {
            return Result<object?>.Failure($"Error extracting field '{fieldPath}': {ex.Message}");
        }
    }

    private static string FormatValue(object value, string dataType, string? format)
    {
        if (value == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(format))
        {
            if (value is DateTime dateTime)
            {
                return dateTime.ToString(format, CultureInfo.InvariantCulture);
            }

            if (value is IFormattable formattable)
            {
                return formattable.ToString(format, CultureInfo.InvariantCulture);
            }
        }

        return dataType?.ToLowerInvariant() switch
        {
            "datetime" when value is DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "decimal" when value is decimal dec => dec.ToString(CultureInfo.InvariantCulture),
            "int" when value is int i => i.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string ApplySingleTransformation(string value, string transform)
    {
        var match = Regex.Match(transform, @"^(\w+)\((.*)\)$");

        if (!match.Success)
        {
            throw new ArgumentException($"Invalid transformation format: '{transform}'");
        }

        var function = match.Groups[1].Value;
        var arguments = match.Groups[2].Value;

        return function switch
        {
            "ToUpper" => value.ToUpper(CultureInfo.InvariantCulture),
            "ToLower" => value.ToLower(CultureInfo.InvariantCulture),
            "Trim" => value.Trim(),
            "Substring" => ApplySubstring(value, arguments),
            "Replace" => ApplyReplace(value, arguments),
            "PadLeft" => ApplyPadLeft(value, arguments),
            "PadRight" => ApplyPadRight(value, arguments),
            _ => throw new NotSupportedException($"Transformation '{function}' is not supported")
        };
    }

    private static string ApplySubstring(string value, string arguments)
    {
        var parts = arguments.Split(',').Select(p => p.Trim()).ToArray();

        if (parts.Length != 2 || !int.TryParse(parts[0], out var start) || !int.TryParse(parts[1], out var length))
        {
            throw new ArgumentException($"Invalid Substring arguments: '{arguments}'");
        }

        if (start < 0 || start >= value.Length)
        {
            return value;
        }

        if (start + length > value.Length)
        {
            length = value.Length - start;
        }

        return value.Substring(start, length);
    }

    private static string ApplyReplace(string value, string arguments)
    {
        var parts = arguments.Split(',').Select(p => p.Trim()).ToArray();

        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid Replace arguments: '{arguments}'");
        }

        return value.Replace(parts[0], parts[1]);
    }

    private static string ApplyPadLeft(string value, string arguments)
    {
        var parts = arguments.Split(',').Select(p => p.Trim()).ToArray();

        if (parts.Length < 1 || !int.TryParse(parts[0], out var totalWidth))
        {
            throw new ArgumentException($"Invalid PadLeft arguments: '{arguments}'");
        }

        var paddingChar = parts.Length > 1 && parts[1].Length > 0 ? parts[1][0] : ' ';

        return value.PadLeft(totalWidth, paddingChar);
    }

    private static string ApplyPadRight(string value, string arguments)
    {
        var parts = arguments.Split(',').Select(p => p.Trim()).ToArray();

        if (parts.Length < 1 || !int.TryParse(parts[0], out var totalWidth))
        {
            throw new ArgumentException($"Invalid PadRight arguments: '{arguments}'");
        }

        var paddingChar = parts.Length > 1 && parts[1].Length > 0 ? parts[1][0] : ' ';

        return value.PadRight(totalWidth, paddingChar);
    }

    private static bool IsValidTransformation(string transform)
    {
        var validTransforms = new[]
        {
            "ToUpper()", "ToLower()", "Trim()",
            "Substring", "Replace", "PadLeft", "PadRight"
        };

        return validTransforms.Any(t =>
            transform.Equals(t, StringComparison.OrdinalIgnoreCase) ||
            transform.StartsWith(t.TrimEnd(')'), StringComparison.OrdinalIgnoreCase));
    }

    private static Result ValidateRule(string value, string rule)
    {
        try
        {
            if (rule.StartsWith("Regex:", StringComparison.OrdinalIgnoreCase))
            {
                var pattern = rule.Substring(6);
                if (!Regex.IsMatch(value, pattern))
                {
                    return Result.Failure($"Value '{value}' does not match regex pattern '{pattern}'");
                }
            }
            else if (rule.StartsWith("Range:", StringComparison.OrdinalIgnoreCase))
            {
                var rangeParts = rule.Substring(6).Split(',');
                if (rangeParts.Length == 2 &&
                    decimal.TryParse(rangeParts[0], out var min) &&
                    decimal.TryParse(rangeParts[1], out var max) &&
                    decimal.TryParse(value, out var numValue))
                {
                    if (numValue < min || numValue > max)
                    {
                        return Result.Failure($"Value '{value}' is outside the range {min}-{max}");
                    }
                }
            }
            else if (rule.StartsWith("MinLength:", StringComparison.OrdinalIgnoreCase))
            {
                var minLengthStr = rule.Substring(10);
                if (int.TryParse(minLengthStr, out var minLength) && value.Length < minLength)
                {
                    return Result.Failure($"Value length {value.Length} is below minimum length {minLength}");
                }
            }
            else if (rule.StartsWith("MaxLength:", StringComparison.OrdinalIgnoreCase))
            {
                var maxLengthStr = rule.Substring(10);
                if (int.TryParse(maxLengthStr, out var maxLength) && value.Length > maxLength)
                {
                    return Result.Failure($"Value length {value.Length} exceeds maximum length {maxLength}");
                }
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Validation rule '{rule}' error: {ex.Message}");
        }
    }
}
