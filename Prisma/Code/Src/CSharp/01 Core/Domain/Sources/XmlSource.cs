namespace ExxerCube.Prisma.Domain.Sources;

/// <summary>
/// Represents an XML document source for field extraction.
/// </summary>
public class XmlSource
{
    /// <summary>
    /// Gets or sets the file path to the XML document.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw XML content as string (optional, for in-memory processing).
    /// </summary>
    public string? XmlContent { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlSource"/> class.
    /// </summary>
    public XmlSource()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlSource"/> class with a file path.
    /// </summary>
    /// <param name="filePath">The file path to the XML document.</param>
    public XmlSource(string filePath) : this()
    {
        FilePath = filePath;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlSource"/> class with XML content.
    /// </summary>
    /// <param name="xmlContent">The raw XML content as string.</param>
    /// <param name="isContent">Flag indicating this is content, not a file path (to differentiate from file path constructor).</param>
    public XmlSource(string xmlContent, bool isContent) : this()
    {
        if (isContent)
        {
            XmlContent = xmlContent;
        }
        else
        {
            FilePath = xmlContent;
        }
    }
}

