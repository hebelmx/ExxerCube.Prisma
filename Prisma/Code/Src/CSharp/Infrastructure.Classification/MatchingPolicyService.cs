using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IndQuestResults;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Infrastructure.Classification;

/// <summary>
/// Matching policy service implementation for configurable field matching rules.
/// Implements <see cref="IMatchingPolicy"/> to determine best values and calculate agreement levels.
/// </summary>
public class MatchingPolicyService : IMatchingPolicy
{
    private readonly MatchingPolicyOptions _options;
    private readonly ILogger<MatchingPolicyService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MatchingPolicyService"/> class.
    /// </summary>
    /// <param name="options">The matching policy configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    public MatchingPolicyService(IOptions<MatchingPolicyOptions> options, ILogger<MatchingPolicyService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<FieldMatchResult>> SelectBestValueAsync(string fieldName, List<FieldValue> values)
    {
        try
        {
            if (values == null || values.Count == 0)
            {
                return Task.FromResult(Result<FieldMatchResult>.WithFailure("No values provided for matching"));
            }

            // Filter out null/empty values
            var validValues = values.Where(v => !string.IsNullOrWhiteSpace(v.Value)).ToList();
            if (validValues.Count == 0)
            {
                return Task.FromResult(Result<FieldMatchResult>.Success(new FieldMatchResult(fieldName, null, 0.0f, "NONE")));
            }

            // If only one value, return it
            if (validValues.Count == 1)
            {
                var singleValue = validValues[0];
                return Task.FromResult(Result<FieldMatchResult>.Success(new FieldMatchResult(
                    fieldName,
                    singleValue.Value,
                    singleValue.Confidence,
                    singleValue.SourceType)
                {
                    AllValues = values,
                    HasConflict = false,
                    AgreementLevel = 1.0f
                }));
            }

            // Apply source priority if configured
            var prioritizedValues = ApplySourcePriority(validValues, fieldName);

            // Group values by their actual value (case-insensitive for string comparison)
            var valueGroups = prioritizedValues
                .GroupBy(v => v.Value?.Trim().ToUpperInvariant() ?? string.Empty)
                .OrderByDescending(g => g.Count())
                .ToList();

            // Get the most common value
            var bestGroup = valueGroups[0];
            var bestValue = bestGroup.First().Value;
            
            // Select best source type based on priority and confidence
            var bestSourceType = SelectBestSourceType(bestGroup, fieldName);

            // Calculate confidence based on agreement and individual confidences
            var agreementLevel = (float)bestGroup.Count() / validValues.Count;
            var avgConfidence = bestGroup.Average(v => v.Confidence);
            var finalConfidence = agreementLevel * avgConfidence;

            // Check for conflicts (if there are multiple different values)
            var hasConflict = valueGroups.Count > 1 && valueGroups[0].Count() < validValues.Count;

            var bestOrigin = bestGroup.First().Origin;
            var bestRaw = bestGroup.First().RawValue;

            var matchResult = new FieldMatchResult(fieldName, bestValue, finalConfidence, bestSourceType, bestOrigin, bestRaw)
            {
                AllValues = values,
                HasConflict = hasConflict,
                AgreementLevel = agreementLevel
            };

            _logger.LogDebug("Selected best value for field {FieldName}: {Value} (confidence: {Confidence}, agreement: {Agreement})",
                fieldName, bestValue, finalConfidence, agreementLevel);

            return Task.FromResult(Result<FieldMatchResult>.Success(matchResult));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting best value for field {FieldName}", fieldName);
            return Task.FromResult(Result<FieldMatchResult>.WithFailure($"Error selecting best value: {ex.Message}", default(FieldMatchResult), ex));
        }
    }

    /// <inheritdoc />
    public Task<Result<float>> CalculateAgreementLevelAsync(List<FieldValue> values)
    {
        try
        {
            if (values == null || values.Count == 0)
            {
                return Task.FromResult(Result<float>.Success(0.0f));
            }

            // Filter out null/empty values
            var validValues = values.Where(v => !string.IsNullOrWhiteSpace(v.Value)).ToList();
            if (validValues.Count == 0)
            {
                return Task.FromResult(Result<float>.Success(0.0f));
            }

            if (validValues.Count == 1)
            {
                return Task.FromResult(Result<float>.Success(1.0f));
            }

            // Group values by their actual value (case-insensitive)
            var valueGroups = validValues
                .GroupBy(v => v.Value?.Trim().ToUpperInvariant() ?? string.Empty)
                .ToList();

            // Agreement level is the proportion of values that match the most common value
            var maxGroupSize = valueGroups.Max(g => g.Count());
            var agreementLevel = (float)maxGroupSize / validValues.Count;

            return Task.FromResult(Result<float>.Success(agreementLevel));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating agreement level");
            return Task.FromResult(Result<float>.WithFailure($"Error calculating agreement level: {ex.Message}", 0.0f, ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> HasConflictAsync(List<FieldValue> values, float threshold = 0.5f)
    {
        // Use configured threshold if no explicit threshold provided (default parameter)
        var effectiveThreshold = threshold == 0.5f ? _options.ConflictThreshold : threshold;
        return await HasConflictAsyncInternal(values, effectiveThreshold);
    }

    private async Task<Result<bool>> HasConflictAsyncInternal(List<FieldValue> values, float threshold)
    {
        try
        {
            if (values == null || values.Count == 0)
            {
                return Result<bool>.Success(false);
            }

            // Filter out null/empty values
            var validValues = values.Where(v => !string.IsNullOrWhiteSpace(v.Value)).ToList();
            if (validValues.Count <= 1)
            {
                return Result<bool>.Success(false);
            }

            // Calculate agreement level
            var agreementResult = await CalculateAgreementLevelAsync(validValues);
            if (agreementResult.IsFailure)
            {
                return Result<bool>.WithFailure(agreementResult.Error ?? "Failed to calculate agreement");
            }

            var agreementLevel = agreementResult.Value;
            // Conflict exists when agreement level is less than or equal to threshold
            // (threshold represents minimum required agreement, so equality means insufficient agreement)
            var hasConflict = agreementLevel <= threshold;

            return Result<bool>.Success(hasConflict);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for conflicts");
            return Result<bool>.WithFailure($"Error checking for conflicts: {ex.Message}", false, ex);
        }
    }

    private List<FieldValue> ApplySourcePriority(List<FieldValue> values, string fieldName)
    {
        // Check if field-specific priority is configured
        var sourcePriority = _options.FieldRules.TryGetValue(fieldName, out var fieldRule) && fieldRule.SourcePriority != null
            ? fieldRule.SourcePriority
            : _options.SourcePriority;

        if (sourcePriority == null || sourcePriority.Count == 0)
        {
            return values;
        }

        // Sort values by source priority
        return values.OrderBy(v =>
        {
            var index = sourcePriority.IndexOf(v.SourceType);
            return index >= 0 ? index : int.MaxValue;
        }).ThenByDescending(v => v.Confidence).ToList();
    }

    private string SelectBestSourceType(IGrouping<string, FieldValue> valueGroup, string fieldName)
    {
        // Check if field-specific priority is configured
        var sourcePriority = _options.FieldRules.TryGetValue(fieldName, out var fieldRule) && fieldRule.SourcePriority != null
            ? fieldRule.SourcePriority
            : _options.SourcePriority;

        if (sourcePriority != null && sourcePriority.Count > 0)
        {
            // Find first value from highest priority source
            foreach (var sourceType in sourcePriority)
            {
                var valueFromSource = valueGroup.FirstOrDefault(v => v.SourceType == sourceType);
                if (valueFromSource != null)
                {
                    return sourceType;
                }
            }
        }

        // Fallback to highest confidence
        return valueGroup.OrderByDescending(v => v.Confidence).First().SourceType;
    }

    private float GetConflictThreshold(string fieldName)
    {
        if (_options.FieldRules.TryGetValue(fieldName, out var fieldRule) && fieldRule.ConflictThreshold.HasValue)
        {
            return fieldRule.ConflictThreshold.Value;
        }
        return _options.ConflictThreshold;
    }
}

