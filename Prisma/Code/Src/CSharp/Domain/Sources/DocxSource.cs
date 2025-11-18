namespace ExxerCube.Prisma.Domain.Sources;

/// <summary>
/// Represents a DOCX document source for field extraction.
/// </summary>
public class DocxSource
{
    /// <summary>
    /// Gets or sets the file path to the DOCX document.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw file content as bytes (optional, for in-memory processing).
    /// </summary>
    public byte[]? FileContent { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DocxSource"/> class.
    /// </summary>
    public DocxSource()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DocxSource"/> class with a file path.
    /// </summary>
    /// <param name="filePath">The file path to the DOCX document.</param>
    public DocxSource(string filePath)
    {
        FilePath = filePath;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DocxSource"/> class with file content.
    /// </summary>
    /// <param name="fileContent">The raw file content as bytes.</param>
    public DocxSource(byte[] fileContent)
    {
        FileContent = fileContent;
    }
}

