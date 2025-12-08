namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Represents a complete OCR processing session including input image,
/// predicted parameters, OCR results, and quality metrics.
/// Used for continuous learning and model retraining.
/// </summary>
public class OcrSession
{
    /// <summary>
    /// Gets or sets the unique identifier for this session.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets when this session was processed.
    /// </summary>
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    // ════════════════════════════════════════════════════════════════════
    // IMAGE DATA
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets or sets the SHA256 hash of the original image (for deduplication).
    /// </summary>
    public string ImageHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the storage path or blob URL for the original image.
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>
    /// Gets or sets the storage path or blob URL for the enhanced image.
    /// </summary>
    public string? EnhancedImagePath { get; set; }

    /// <summary>
    /// Gets or sets the original image size in bytes.
    /// </summary>
    public long ImageSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the document source type (PDF, Image, etc.).
    /// </summary>
    public string? DocumentType { get; set; }

    /// <summary>
    /// Gets or sets the page number (for multi-page documents).
    /// </summary>
    public int? PageNumber { get; set; }

    // ════════════════════════════════════════════════════════════════════
    // EXTRACTED FEATURES (INPUT TO MODEL)
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets or sets the Laplacian variance (blur score).
    /// Higher = sharper image. Typical range: 0-10000+
    /// </summary>
    public double BlurScore { get; set; }

    /// <summary>
    /// Gets or sets the standard deviation of grayscale intensity.
    /// Typical range: 0-80
    /// </summary>
    public double Contrast { get; set; }

    /// <summary>
    /// Gets or sets the mean absolute Laplacian (noise estimate).
    /// Typical range: 0-100+
    /// </summary>
    public double NoiseEstimate { get; set; }

    /// <summary>
    /// Gets or sets the ratio of Canny edge pixels to total pixels.
    /// Typical range: 0-0.15
    /// </summary>
    public double EdgeDensity { get; set; }

    /// <summary>
    /// Gets or sets the determined image quality level.
    /// </summary>
    public string QualityLevel { get; set; } = string.Empty;

    // ════════════════════════════════════════════════════════════════════
    // PREDICTED PARAMETERS (OUTPUT FROM MODEL)
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets or sets the filter type used (None, PIL, OpenCV, Polynomial).
    /// </summary>
    public string FilterType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the predicted contrast factor (0.5-2.0).
    /// </summary>
    public float? PredictedContrast { get; set; }

    /// <summary>
    /// Gets or sets the predicted brightness factor (0.8-1.3).
    /// </summary>
    public float? PredictedBrightness { get; set; }

    /// <summary>
    /// Gets or sets the predicted sharpness factor (0.5-3.0).
    /// </summary>
    public float? PredictedSharpness { get; set; }

    /// <summary>
    /// Gets or sets the predicted unsharp mask radius (0.0-5.0).
    /// </summary>
    public float? PredictedUnsharpRadius { get; set; }

    /// <summary>
    /// Gets or sets the predicted unsharp mask percentage (0-250).
    /// </summary>
    public float? PredictedUnsharpPercent { get; set; }

    /// <summary>
    /// Gets or sets the model version used for prediction.
    /// </summary>
    public string ModelVersion { get; set; } = "polynomial_v1";

    // ════════════════════════════════════════════════════════════════════
    // OCR RESULTS
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets or sets the OCR engine used (Tesseract, GOT-OCR2, etc.).
    /// </summary>
    public string OcrEngine { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OCR engine version.
    /// </summary>
    public string? OcrEngineVersion { get; set; }

    /// <summary>
    /// Gets or sets the extracted text from baseline (no filter) OCR.
    /// </summary>
    public string? BaselineOcrText { get; set; }

    /// <summary>
    /// Gets or sets the baseline OCR confidence (0.0-1.0).
    /// </summary>
    public float? BaselineOcrConfidence { get; set; }

    /// <summary>
    /// Gets or sets the extracted text from enhanced OCR.
    /// </summary>
    public string? EnhancedOcrText { get; set; }

    /// <summary>
    /// Gets or sets the enhanced OCR confidence (0.0-1.0).
    /// </summary>
    public float? EnhancedOcrConfidence { get; set; }

    /// <summary>
    /// Gets or sets the OCR processing time in milliseconds.
    /// </summary>
    public long OcrProcessingTimeMs { get; set; }

    // ════════════════════════════════════════════════════════════════════
    // QUALITY METRICS (FOR RETRAINING)
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets or sets the ground truth text (from manual review or known source).
    /// NULL if not yet reviewed.
    /// </summary>
    public string? GroundTruth { get; set; }

    /// <summary>
    /// Gets or sets the baseline Levenshtein distance (vs ground truth).
    /// NULL if ground truth not available.
    /// </summary>
    public int? BaselineLevenshteinDistance { get; set; }

    /// <summary>
    /// Gets or sets the enhanced Levenshtein distance (vs ground truth).
    /// NULL if ground truth not available.
    /// </summary>
    public int? EnhancedLevenshteinDistance { get; set; }

    /// <summary>
    /// Gets or sets the improvement percentage ((baseline - enhanced) / baseline * 100).
    /// NULL if ground truth not available.
    /// </summary>
    public double? ImprovementPercent { get; set; }

    /// <summary>
    /// Gets or sets whether this session has been manually reviewed.
    /// </summary>
    public bool IsReviewed { get; set; }

    /// <summary>
    /// Gets or sets the reviewer's name or ID.
    /// </summary>
    public string? ReviewedBy { get; set; }

    /// <summary>
    /// Gets or sets when the review was completed.
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Gets or sets the reviewer's quality rating (1-5).
    /// Used to filter high-quality training data.
    /// </summary>
    public int? QualityRating { get; set; }

    /// <summary>
    /// Gets or sets reviewer notes or corrections.
    /// </summary>
    public string? ReviewNotes { get; set; }

    // ════════════════════════════════════════════════════════════════════
    // OPTIMAL PARAMETERS (FROM MANUAL TUNING OR SEARCH)
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets or sets the optimal contrast factor (determined by review/search).
    /// Used as target for retraining.
    /// </summary>
    public float? OptimalContrast { get; set; }

    /// <summary>
    /// Gets or sets the optimal brightness factor.
    /// </summary>
    public float? OptimalBrightness { get; set; }

    /// <summary>
    /// Gets or sets the optimal sharpness factor.
    /// </summary>
    public float? OptimalSharpness { get; set; }

    /// <summary>
    /// Gets or sets the optimal unsharp radius.
    /// </summary>
    public float? OptimalUnsharpRadius { get; set; }

    /// <summary>
    /// Gets or sets the optimal unsharp percentage.
    /// </summary>
    public float? OptimalUnsharpPercent { get; set; }

    // ════════════════════════════════════════════════════════════════════
    // METADATA
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets or sets whether to include this session in training data.
    /// </summary>
    public bool IncludeInTraining { get; set; } = true;

    /// <summary>
    /// Gets or sets additional metadata as JSON.
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Gets or sets tags for categorization (e.g., "invoice", "contract", "handwritten").
    /// </summary>
    public string? Tags { get; set; }
}
