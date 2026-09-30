namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents an RFC variant with an optional source tag (e.g., XML, OCR).
/// </summary>
public readonly record struct RfcVariant(string Value, string SourceTag);
