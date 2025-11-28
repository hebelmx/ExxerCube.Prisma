namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Tracks missing or unresolved required fields to avoid silent null usage.
/// </summary>
public sealed class ValidationState
{
    private readonly HashSet<string> _missing = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Marks a field as missing when the condition is false.
    /// </summary>
    /// <param name="condition">Condition that must be true to consider the field present.</param>
    /// <param name="fieldName">The required field name.</param>
    public void Require(bool condition, string fieldName)
    {
        if (!condition && !string.IsNullOrWhiteSpace(fieldName))
        {
            _missing.Add(fieldName);
        }
    }

    /// <summary>
    /// Gets a value indicating whether all required fields are present.
    /// </summary>
    public bool IsValid => _missing.Count == 0;

    /// <summary>
    /// Gets the collection of missing field names.
    /// </summary>
    public IReadOnlyCollection<string> Missing => _missing;
}
