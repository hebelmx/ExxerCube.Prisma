namespace ExxerCube.Prisma.Web.UI.Services;

/// <summary>
/// Represents a predefined DOCX fixture available for adaptive extraction testing.
/// Each fixture represents a specific document type, structure, and persona scenario.
/// </summary>
/// <param name="Key">The unique identifier key for the fixture, used for lookup operations. Case-insensitive.</param>
/// <param name="DisplayName">The human-readable display name shown in UI components.</param>
/// <param name="FileName">The name of the DOCX file in the fixtures directory.</param>
/// <param name="Description">A detailed description of the fixture's characteristics and use case.</param>
/// <param name="Persona">The persona or document type classification (e.g., "IMSS remit / structured labels").</param>
public sealed record AdaptiveDocxFixture(
    string Key,
    string DisplayName,
    string FileName,
    string Description,
    string Persona);