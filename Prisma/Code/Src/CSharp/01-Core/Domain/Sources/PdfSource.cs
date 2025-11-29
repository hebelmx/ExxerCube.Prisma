namespace ExxerCube.Prisma.Domain.Sources;

/// <summary>
/// Represents a PDF document source for field extraction.
/// </summary>
public class PdfSource
{
    /// <summary>
    /// Gets or sets the file path to the PDF document.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw file content as bytes (optional, for in-memory processing).
    /// </summary>
    public byte[]? FileContent { get; set; }

    /// <summary>
    /// Gets or sets the OCR confidence score if OCR was already performed (optional).
    /// </summary>
    public float? OcrConfidence { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSource"/> class.
    /// </summary>
    public PdfSource()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSource"/> class with a file path.
    /// </summary>
    /// <param name="filePath">The file path to the PDF document.</param>
    public PdfSource(string filePath)
    {
        FilePath = filePath;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSource"/> class with file content.
    /// </summary>
    /// <param name="fileContent">The raw file content as bytes.</param>
    public PdfSource(byte[] fileContent)
    {
        FileContent = fileContent;
    }
}

