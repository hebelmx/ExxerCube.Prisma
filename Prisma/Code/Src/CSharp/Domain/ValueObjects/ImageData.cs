namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents a document image with metadata for OCR processing.
/// </summary>
public class ImageData
{
    /// <summary>
    /// Gets or sets the raw image data as a byte array.
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets or sets the source file path of the image.
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the page number of the image.
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets the total number of pages in the document.
    /// </summary>
    public int TotalPages { get; set; } = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageData"/> class.
    /// </summary>
    public ImageData()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageData"/> class with specified values.
    /// </summary>
    /// <param name="data">The raw image data.</param>
    /// <param name="sourcePath">The source file path.</param>
    /// <param name="pageNumber">The page number.</param>
    /// <param name="totalPages">The total number of pages.</param>
    public ImageData(byte[] data, string sourcePath, int pageNumber = 1, int totalPages = 1)
    {
        Data = data;
        SourcePath = sourcePath;
        PageNumber = pageNumber;
        TotalPages = totalPages;
    }
}
