namespace GotOcr2Sample.Domain.Models;

/// <summary>
/// Represents configuration for OCR execution.
/// </summary>
public class OCRConfig
{
    /// <summary>
    /// Gets or sets the primary language for OCR processing (e.g., "spa", "eng").
    /// </summary>
    public string Language { get; set; } = "spa";

    /// <summary>
    /// Gets or sets the OCR Engine Mode (0-3).
    /// </summary>
    public int OEM { get; set; } = 1;

    /// <summary>
    /// Gets or sets the Page Segmentation Mode (0-13).
    /// </summary>
    public int PSM { get; set; } = 6;

    /// <summary>
    /// Gets or sets the fallback language for OCR processing.
    /// </summary>
    public string FallbackLanguage { get; set; } = "eng";

    /// <summary>
    /// Gets or sets the confidence threshold for OCR results (0.0 to 1.0).
    /// </summary>
    public float ConfidenceThreshold { get; set; } = 0.7f;

    /// <summary>
    /// Initializes a new instance of the <see cref="OCRConfig"/> class.
    /// </summary>
    public OCRConfig()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OCRConfig"/> class with specified values.
    /// </summary>
    /// <param name="language">The primary language.</param>
    /// <param name="oem">The OCR Engine Mode.</param>
    /// <param name="psm">The Page Segmentation Mode.</param>
    /// <param name="fallbackLanguage">The fallback language.</param>
    /// <param name="confidenceThreshold">The confidence threshold.</param>
    public OCRConfig(string language, int oem, int psm, string fallbackLanguage, float confidenceThreshold = 0.7f)
    {
        Language = language;
        OEM = oem;
        PSM = psm;
        FallbackLanguage = fallbackLanguage;
        ConfidenceThreshold = confidenceThreshold;
    }
}
