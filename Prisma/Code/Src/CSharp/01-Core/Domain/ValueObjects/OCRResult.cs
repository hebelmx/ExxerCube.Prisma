namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents the result from OCR execution with confidence metrics.
/// </summary>
public class OCRResult
{
    /// <summary>
    /// Gets or sets the extracted text from the OCR process.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the average confidence score (0.0 to 100.0).
    /// </summary>
    public float ConfidenceAvg { get; set; }

    /// <summary>
    /// Gets or sets the median confidence score (0.0 to 100.0).
    /// </summary>
    public float ConfidenceMedian { get; set; }

    /// <summary>
    /// Gets or sets the list of confidence scores for individual words.
    /// </summary>
    public List<float> Confidences { get; set; } = new();

    /// <summary>
    /// Gets or sets the language used for OCR processing.
    /// </summary>
    public string LanguageUsed { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="OCRResult"/> class.
    /// </summary>
    public OCRResult()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OCRResult"/> class with specified values.
    /// </summary>
    /// <param name="text">The extracted text.</param>
    /// <param name="confidenceAvg">The average confidence score.</param>
    /// <param name="confidenceMedian">The median confidence score.</param>
    /// <param name="confidences">The list of confidence scores.</param>
    /// <param name="languageUsed">The language used for OCR.</param>
    public OCRResult(string text, float confidenceAvg, float confidenceMedian, List<float> confidences, string languageUsed)
    {
        Text = text;
        ConfidenceAvg = confidenceAvg;
        ConfidenceMedian = confidenceMedian;
        Confidences = confidences;
        LanguageUsed = languageUsed;
    }
}
