namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents the final result of processing a single image.
/// </summary>
public class ProcessingResult
{
    /// <summary>
    /// Gets or sets the source path of the processed image.
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the page number of the processed image.
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Gets or sets the OCR result from processing.
    /// </summary>
    public OCRResult OCRResult { get; set; } = new();

    /// <summary>
    /// Gets or sets the extracted fields from the document.
    /// </summary>
    public ExtractedFields ExtractedFields { get; set; } = new();

    /// <summary>
    /// Gets or sets the output path where results were saved.
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Gets or sets the list of processing errors that occurred.
    /// </summary>
    public List<string> ProcessingErrors { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingResult"/> class.
    /// </summary>
    public ProcessingResult()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingResult"/> class with specified values.
    /// </summary>
    /// <param name="sourcePath">The source path.</param>
    /// <param name="pageNumber">The page number.</param>
    /// <param name="ocrResult">The OCR result.</param>
    /// <param name="extractedFields">The extracted fields.</param>
    /// <param name="outputPath">The output path.</param>
    /// <param name="processingErrors">The processing errors.</param>
    public ProcessingResult(string sourcePath, int pageNumber, OCRResult ocrResult, ExtractedFields extractedFields, string? outputPath = null, List<string>? processingErrors = null)
    {
        SourcePath = sourcePath;
        PageNumber = pageNumber;
        OCRResult = ocrResult;
        ExtractedFields = extractedFields;
        OutputPath = outputPath;
        ProcessingErrors = processingErrors ?? new List<string>();
    }
}
