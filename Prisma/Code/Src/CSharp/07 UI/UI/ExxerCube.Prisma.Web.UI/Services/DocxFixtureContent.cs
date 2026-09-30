namespace ExxerCube.Prisma.Web.UI.Services;

/// <summary>
/// Represents the loaded content of a DOCX fixture, including the fixture metadata,
/// extracted plain text, and the full file system path.
/// </summary>
/// <param name="Fixture">The fixture metadata describing the document type and characteristics.</param>
/// <param name="Text">The plain text content extracted from the DOCX file, with normalized spacing.</param>
/// <param name="FullPath">The absolute file system path to the source DOCX file.</param>
public sealed record DocxFixtureContent(
    AdaptiveDocxFixture Fixture,
    string Text,
    string FullPath);