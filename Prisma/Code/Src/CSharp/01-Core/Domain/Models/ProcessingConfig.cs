namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Represents configuration for the entire processing pipeline.
/// </summary>
public class ProcessingConfig
{
    /// <summary>
    /// Gets or sets a value indicating whether to remove watermarks from images.
    /// </summary>
    public bool RemoveWatermark { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to deskew images.
    /// </summary>
    public bool Deskew { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to binarize images.
    /// </summary>
    public bool Binarize { get; set; } = true;

    /// <summary>
    /// Gets or sets the OCR configuration.
    /// </summary>
    public OCRConfig OCRConfig { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to extract sections from the document.
    /// </summary>
    public bool ExtractSections { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to normalize text.
    /// </summary>
    public bool NormalizeText { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout in seconds for processing operations.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for failed operations.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay in seconds between retry attempts.
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets the output format for results (json, xml, csv, txt, pdf).
    /// </summary>
    public string OutputFormat { get; set; } = "json";

    /// <summary>
    /// Gets or sets the maximum file size in MB for processing.
    /// </summary>
    public int MaxFileSizeMB { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum number of concurrent processing operations.
    /// </summary>
    public int MaxConcurrency { get; set; } = 5;

    /// <summary>
    /// Gets or sets the batch size for processing multiple documents.
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum memory usage in MB for processing.
    /// </summary>
    public int MaxMemoryUsageMB { get; set; } = 1024;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingConfig"/> class.
    /// </summary>
    public ProcessingConfig()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingConfig"/> class with specified values.
    /// </summary>
    /// <param name="removeWatermark">Whether to remove watermarks.</param>
    /// <param name="deskew">Whether to deskew images.</param>
    /// <param name="binarize">Whether to binarize images.</param>
    /// <param name="ocrConfig">The OCR configuration.</param>
    /// <param name="extractSections">Whether to extract sections.</param>
    /// <param name="normalizeText">Whether to normalize text.</param>
    /// <param name="timeoutSeconds">The timeout in seconds.</param>
    /// <param name="maxRetries">The maximum number of retries.</param>
    /// <param name="retryDelaySeconds">The retry delay in seconds.</param>
    /// <param name="outputFormat">The output format.</param>
    /// <param name="maxFileSizeMB">The maximum file size in MB.</param>
    /// <param name="maxConcurrency">The maximum concurrency.</param>
    /// <param name="batchSize">The batch size.</param>
    /// <param name="maxMemoryUsageMB">The maximum memory usage in MB.</param>
    public ProcessingConfig(
        bool removeWatermark, 
        bool deskew, 
        bool binarize, 
        OCRConfig ocrConfig, 
        bool extractSections, 
        bool normalizeText,
        int timeoutSeconds = 300,
        int maxRetries = 3,
        int retryDelaySeconds = 5,
        string outputFormat = "json",
        int maxFileSizeMB = 50,
        int maxConcurrency = 5,
        int batchSize = 10,
        int maxMemoryUsageMB = 1024)
    {
        RemoveWatermark = removeWatermark;
        Deskew = deskew;
        Binarize = binarize;
        OCRConfig = ocrConfig;
        ExtractSections = extractSections;
        NormalizeText = normalizeText;
        TimeoutSeconds = timeoutSeconds;
        MaxRetries = maxRetries;
        RetryDelaySeconds = retryDelaySeconds;
        OutputFormat = outputFormat;
        MaxFileSizeMB = maxFileSizeMB;
        MaxConcurrency = maxConcurrency;
        BatchSize = batchSize;
        MaxMemoryUsageMB = maxMemoryUsageMB;
    }
}
