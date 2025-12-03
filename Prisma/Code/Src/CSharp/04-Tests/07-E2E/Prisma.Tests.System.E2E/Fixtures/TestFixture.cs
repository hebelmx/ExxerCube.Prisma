namespace Prisma.Tests.System.E2E.Fixtures;

/// <summary>
/// Represents a test fixture with PDF document and expected XML output.
/// </summary>
/// <param name="Name">Friendly name of the fixture</param>
/// <param name="PdfPath">Absolute path to the PDF document</param>
/// <param name="XmlPath">Absolute path to the expected XML output</param>
/// <param name="Description">Description of what this fixture tests</param>
/// <param name="ExpectedErrors">Expected errors or edge cases in this fixture</param>
public sealed record TestFixture(
    string Name,
    string PdfPath,
    string XmlPath,
    string Description,
    string[] ExpectedErrors
)
{
    /// <summary>
    /// Reads the PDF file content.
    /// </summary>
    public byte[] ReadPdfBytes()
    {
        if (!File.Exists(PdfPath))
            throw new FileNotFoundException($"Fixture PDF not found: {PdfPath}");

        return File.ReadAllBytes(PdfPath);
    }

    /// <summary>
    /// Reads the expected XML content.
    /// </summary>
    public string ReadExpectedXml()
    {
        if (!File.Exists(XmlPath))
            throw new FileNotFoundException($"Fixture XML not found: {XmlPath}");

        return File.ReadAllText(XmlPath);
    }

    /// <summary>
    /// Gets the file name without extension.
    /// </summary>
    public string FileNameWithoutExtension => Path.GetFileNameWithoutExtension(PdfPath);

    /// <summary>
    /// Validates that fixture files exist.
    /// </summary>
    public void ValidateFilesExist()
    {
        if (!File.Exists(PdfPath))
            throw new FileNotFoundException($"Fixture PDF not found: {PdfPath}");

        if (!File.Exists(XmlPath))
            throw new FileNotFoundException($"Fixture XML not found: {XmlPath}");
    }
}
