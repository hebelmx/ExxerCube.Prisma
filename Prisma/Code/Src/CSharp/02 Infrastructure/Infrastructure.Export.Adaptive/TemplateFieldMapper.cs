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
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Export.Adaptive;

/// <summary>
/// Implementation of dynamic field mapping using reflection and transformation expressions.
/// </summary>
public class TemplateFieldMapper : ITemplateFieldMapper
{
    private readonly ILogger<TemplateFieldMapper> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateFieldMapper"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public TemplateFieldMapper(ILogger<TemplateFieldMapper> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<string>> MapFieldAsync(
        object sourceObject,
        FieldMapping mapping,
        CancellationToken cancellationToken = default)
    {
        if (sourceObject == null)
        {
            return Result<string>.Failure("Source object cannot be null");
        }

        if (mapping == null)
        {
            return Result<string>.Failure("Mapping cannot be null");
        }

        try
        {
            // Extract field value using reflection
            var extractionResult = ExtractFieldValue(sourceObject, mapping.SourceFieldPath);

            if (extractionResult.IsFailure)
            {
                // Field not found
                if (mapping.IsRequired)
                {
                    return Result<string>.Failure($"Required field '{mapping.SourceFieldPath}' not found");
                }

                // Return default value for optional fields
                if (!string.IsNullOrEmpty(mapping.DefaultValue))
                {
                    return Result<string>.Success(mapping.DefaultValue);
                }

                return Result<string>.Success(string.Empty);
            }

            var value = extractionResult.Value;

            // Handle null values
            if (value == null)
            {
                if (mapping.IsRequired && string.IsNullOrEmpty(mapping.DefaultValue))
                {
                    return Result<string>.Failure($"Required field '{mapping.SourceFieldPath}' is null");
                }

                return Result<string>.Success(mapping.DefaultValue ?? string.Empty);
            }

            // Format value according to data type and format
            var formattedValue = FormatValue(value, mapping.DataType, mapping.Format);

            // Apply transformation if specified
            if (!string.IsNullOrWhiteSpace(mapping.TransformExpression))
            {
                var transformResult = await ApplyTransformationAsync(
                    formattedValue,
                    mapping.TransformExpression,
                    cancellationToken);

                if (transformResult.IsFailure)
                {
                    return transformResult;
                }

                formattedValue = transformResult.Value;
            }

            // Validate if validation rules specified
            if (mapping.ValidationRules != null && mapping.ValidationRules.Count > 0)
            {
                var validationResult = await ValidateFieldValueAsync(
                    formattedValue ?? string.Empty,
                    mapping,
                    cancellationToken);

                if (validationResult.IsFailure)
                {
                    return Result<string>.Failure(validationResult.Error ?? "Validation failed");
                }
            }

            return Result<string>.Success(formattedValue ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mapping field {FieldPath}", mapping.SourceFieldPath);
            return Result<string>.Failure($"Error mapping field '{mapping.SourceFieldPath}': {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, string>>> MapAllFieldsAsync(
        object sourceObject,
        TemplateDefinition template,
        CancellationToken cancellationToken = default)
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

        // Sort by DisplayOrder
        var sortedMappings = template.FieldMappings
            .OrderBy(m => m.DisplayOrder)
            .ToList();

        foreach (var mapping in sortedMappings)
        {
            var mapResult = await MapFieldAsync(sourceObject, mapping, cancellationToken);

            if (mapResult.IsFailure)
            {
                if (mapping.IsRequired)
                {
                    // Required field failed - abort entire mapping
                    return Result<Dictionary<string, string>>.Failure(mapResult.Error ?? "Required field mapping failed");
                }

                // Optional field failed - log and continue
                _logger.LogWarning("Optional field '{FieldPath}' failed to map: {Error}",
                    mapping.SourceFieldPath, mapResult.Error);
                continue;
            }

            result[mapping.TargetField] = mapResult.Value ?? string.Empty;
        }

        return Result<Dictionary<string, string>>.Success(result);
    }

    /// <inheritdoc />
    public Task<Result> ValidateMappingAsync(
        Type sourceType,
        FieldMapping mapping,
        CancellationToken cancellationToken = default)
    {
        if (sourceType == null)
        {
            return Task.FromResult(Result.Failure("Source type cannot be null"));
        }

        if (mapping == null)
        {
            return Task.FromResult(Result.Failure("Mapping cannot be null"));
        }

        try
        {
            // Validate field path exists
            var pathParts = mapping.SourceFieldPath.Split('.');
            var currentType = sourceType;

            foreach (var part in pathParts)
            {
                var property = currentType.GetProperty(part, BindingFlags.Public | BindingFlags.Instance);

                if (property == null)
                {
                    return Task.FromResult(Result.Failure(
                        $"Field path '{mapping.SourceFieldPath}' not found on type '{sourceType.Name}' (missing '{part}')"));
                }

                currentType = property.PropertyType;
            }

            // Validate transformation expression if specified
            if (!string.IsNullOrWhiteSpace(mapping.TransformExpression))
            {
                var transformParts = mapping.TransformExpression.Split('|');
                foreach (var transform in transformParts)
                {
                    var trimmedTransform = transform.Trim();
                    if (!IsValidTransformation(trimmedTransform))
                    {
                        return Task.FromResult(Result.Failure(
                            $"Invalid transformation expression: '{trimmedTransform}' is not supported"));
                    }
                }
            }

            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating mapping for {FieldPath}", mapping.SourceFieldPath);
            return Task.FromResult(Result.Failure($"Validation error: {ex.Message}"));
        }
    }

    /// <inheritdoc />
    public Task<Result<string>> ApplyTransformationAsync(
        string value,
        string transformExpression,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Task.FromResult(Result<string>.Success(value ?? string.Empty));
        }

        if (string.IsNullOrWhiteSpace(transformExpression))
        {
            return Task.FromResult(Result<string>.Success(value));
        }

        try
        {
            var currentValue = value;

            // Split by pipe for chained transformations
            var transformations = transformExpression.Split('|')
                .Select(t => t.Trim())
                .ToList();

            foreach (var transform in transformations)
            {
                currentValue = ApplySingleTransformation(currentValue, transform);
            }

            return Task.FromResult(Result<string>.Success(currentValue));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying transformation '{Transform}' to value '{Value}'",
                transformExpression, value);
            return Task.FromResult(Result<string>.Failure($"Transformation error: {ex.Message}"));
        }
    }

    /// <inheritdoc />
    public Task<Result> ValidateFieldValueAsync(
        string value,
        FieldMapping mapping,
        CancellationToken cancellationToken = default)
    {
        if (mapping.ValidationRules == null || mapping.ValidationRules.Count == 0)
        {
            return Task.FromResult(Result.Success());
        }

        foreach (var rule in mapping.ValidationRules)
        {
            var validationResult = ValidateRule(value, rule);
            if (validationResult.IsFailure)
            {
                return Task.FromResult(validationResult);
            }
        }

        return Task.FromResult(Result.Success());
    }

    //
    // Private helper methods
    //

    private Result<object?> ExtractFieldValue(object sourceObject, string fieldPath)
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

        // Apply format if specified
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

        // Default formatting based on data type
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

    private Result ValidateRule(string value, string rule)
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
            else if (rule.Equals("EmailAddress", StringComparison.OrdinalIgnoreCase))
            {
                var emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
                if (!Regex.IsMatch(value, emailRegex))
                {
                    return Result.Failure($"Value '{value}' is not a valid email address");
                }
            }
            else if (rule.Equals("Required", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return Result.Failure("Value is required and cannot be empty");
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
