namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents a field that's required in template but missing from source.
/// </summary>
public sealed class MissingFieldInfo
{
    /// <summary>
    /// Gets the field path that's required by the template.
    /// </summary>
    public string FieldPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target field name in the template.
    /// </summary>
    public string TargetField { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether this field is required.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Gets the expected data type.
    /// </summary>
    public string ExpectedType { get; init; } = string.Empty;
}