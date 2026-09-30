namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents a new field found in source data.
/// </summary>
public sealed class NewFieldInfo
{
    /// <summary>
    /// Gets the field path in the source object.
    /// </summary>
    public string FieldPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the detected type of the field.
    /// </summary>
    public string DetectedType { get; init; } = string.Empty;

    /// <summary>
    /// Gets a sample value from the source data.
    /// </summary>
    public string? SampleValue { get; init; }
}