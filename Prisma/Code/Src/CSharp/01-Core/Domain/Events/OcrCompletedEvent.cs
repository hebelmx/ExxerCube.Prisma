namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// OCR processing completed (Tesseract or GOT-OCR2).
/// </summary>
public record OcrCompletedEvent : DomainEvent
{
    /// <summary>
    /// Gets the unique identifier for the processed file.
    /// </summary>
    public Guid FileId { get; init; }

    /// <summary>
    /// Gets the OCR engine used (Tesseract, GOT-OCR2).
    /// </summary>
    public string OcrEngine { get; init; } = string.Empty;

    /// <summary>
    /// Gets the confidence score of OCR results (0-100).
    /// </summary>
    public decimal Confidence { get; init; }

    /// <summary>
    /// Gets the length of extracted text in characters.
    /// </summary>
    public int ExtractedTextLength { get; init; }

    /// <summary>
    /// Gets the total processing time for OCR operation.
    /// </summary>
    public TimeSpan ProcessingTime { get; init; }

    /// <summary>
    /// Gets a value indicating whether fallback OCR engine was triggered.
    /// </summary>
    public bool FallbackTriggered { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OcrCompletedEvent"/> class.
    /// </summary>
    public OcrCompletedEvent()
    {
        EventType = nameof(OcrCompletedEvent);
    }
}