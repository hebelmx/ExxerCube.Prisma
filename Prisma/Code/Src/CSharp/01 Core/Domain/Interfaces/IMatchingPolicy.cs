namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the matching policy service for configurable field matching rules.
/// </summary>
public interface IMatchingPolicy
{
    /// <summary>
    /// Determines the best value from multiple field values based on matching rules.
    /// </summary>
    /// <param name="fieldName">The field name being matched.</param>
    /// <param name="values">The list of field values from different sources.</param>
    /// <returns>A result containing the best matched value with confidence score or an error.</returns>
    Task<Result<FieldMatchResult>> SelectBestValueAsync(string fieldName, List<FieldValue> values);

    /// <summary>
    /// Calculates the agreement level between multiple field values.
    /// </summary>
    /// <param name="values">The list of field values to compare.</param>
    /// <returns>A result containing the agreement level (0.0-1.0) or an error.</returns>
    Task<Result<float>> CalculateAgreementLevelAsync(List<FieldValue> values);

    /// <summary>
    /// Determines if field values are in conflict (disagree significantly).
    /// </summary>
    /// <param name="values">The list of field values to check.</param>
    /// <param name="threshold">The conflict threshold (default: 0.5, meaning values must agree at least 50%).</param>
    /// <returns>A result indicating whether there is a conflict or an error.</returns>
    Task<Result<bool>> HasConflictAsync(List<FieldValue> values, float threshold = 0.5f);
}

