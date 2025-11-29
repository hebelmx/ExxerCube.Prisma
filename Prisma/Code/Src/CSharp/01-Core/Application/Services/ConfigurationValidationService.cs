namespace ExxerCube.Prisma.Application.Services;

/// <summary>
/// Service for validating OCR processing configuration.
/// Ensures configuration changes can be made without code modifications.
/// </summary>
public class ConfigurationValidationService
{
    private readonly ILogger<ConfigurationValidationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationValidationService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ConfigurationValidationService(ILogger<ConfigurationValidationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates a processing configuration and returns validation results.
    /// </summary>
    /// <param name="config">The processing configuration to validate.</param>
    /// <returns>A result containing validation results or errors.</returns>
    public Result<ConfigurationValidationResult> ValidateConfiguration(ProcessingConfig config)
    {
        _logger.LogInformation("Validating processing configuration");

        var validationErrors = new List<string>();
        var warnings = new List<string>();

        try
        {
            // Validate OCR configuration
            var ocrValidation = ValidateOCRConfig(config.OCRConfig);
            validationErrors.AddRange(ocrValidation.Errors);
            warnings.AddRange(ocrValidation.Warnings);

            // Validate processing options
            var processingValidation = ValidateProcessingOptions(config);
            validationErrors.AddRange(processingValidation.Errors);
            warnings.AddRange(processingValidation.Warnings);

            // Validate performance settings
            var performanceValidation = ValidatePerformanceSettings(config);
            validationErrors.AddRange(performanceValidation.Errors);
            warnings.AddRange(performanceValidation.Warnings);

            var result = new ConfigurationValidationResult
            {
                IsValid = !validationErrors.Any(),
                Errors = validationErrors,
                Warnings = warnings,
                ValidatedConfig = config
            };

            if (result.IsValid)
            {
                _logger.LogInformation("Configuration validation completed successfully");
            }
            else
            {
                _logger.LogWarning("Configuration validation completed with {ErrorCount} errors and {WarningCount} warnings", 
                    validationErrors.Count, warnings.Count);
            }

            return Result<ConfigurationValidationResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during configuration validation");
            return Result<ConfigurationValidationResult>.WithFailure($"Configuration validation failed: {ex.Message}", default, ex);
        }
    }

    /// <summary>
    /// Validates OCR configuration settings.
    /// </summary>
    /// <param name="ocrConfig">The OCR configuration to validate.</param>
    /// <returns>Validation results for OCR configuration.</returns>
    private ValidationResult ValidateOCRConfig(OCRConfig ocrConfig)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate language settings
        if (string.IsNullOrWhiteSpace(ocrConfig.Language))
        {
            errors.Add("OCR language is required");
        }
        else if (!IsValidLanguage(ocrConfig.Language))
        {
            errors.Add($"Invalid OCR language: {ocrConfig.Language}");
        }

        if (!string.IsNullOrWhiteSpace(ocrConfig.FallbackLanguage) && !IsValidLanguage(ocrConfig.FallbackLanguage))
        {
            errors.Add($"Invalid fallback language: {ocrConfig.FallbackLanguage}");
        }

        // Validate OEM settings
        if (ocrConfig.OEM < 0 || ocrConfig.OEM > 3)
        {
            errors.Add($"Invalid OCR Engine Mode (OEM): {ocrConfig.OEM}. Must be between 0 and 3");
        }

        // Validate PSM settings
        if (ocrConfig.PSM < 0 || ocrConfig.PSM > 13)
        {
            errors.Add($"Invalid Page Segmentation Mode (PSM): {ocrConfig.PSM}. Must be between 0 and 13");
        }

        // Validate confidence thresholds
        if (ocrConfig.ConfidenceThreshold < 0.0f || ocrConfig.ConfidenceThreshold > 1.0f)
        {
            errors.Add($"Invalid confidence threshold: {ocrConfig.ConfidenceThreshold}. Must be between 0.0 and 1.0");
        }

        // Warnings for potentially problematic configurations
        if (ocrConfig.ConfidenceThreshold > 0.95f)
        {
            warnings.Add("High confidence threshold may result in many rejected documents");
        }

        if (ocrConfig.ConfidenceThreshold < 0.5f)
        {
            warnings.Add("Low confidence threshold may result in poor quality results");
        }

        return new ValidationResult { Errors = errors, Warnings = warnings };
    }

    /// <summary>
    /// Validates processing options.
    /// </summary>
    /// <param name="config">The processing configuration to validate.</param>
    /// <returns>Validation results for processing options.</returns>
    private ValidationResult ValidateProcessingOptions(ProcessingConfig config)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate timeout settings
        if (config.TimeoutSeconds <= 0)
        {
            errors.Add("Timeout must be greater than 0 seconds");
        }
        else if (config.TimeoutSeconds > 3600)
        {
            errors.Add("Timeout cannot exceed 3600 seconds (1 hour)");
        }

        // Validate retry settings
        if (config.MaxRetries < 0)
        {
            errors.Add("Maximum retries cannot be negative");
        }
        else if (config.MaxRetries > 10)
        {
            warnings.Add("High retry count may impact performance");
        }

        // Validate retry delay
        if (config.RetryDelaySeconds < 0)
        {
            errors.Add("Retry delay cannot be negative");
        }
        else if (config.RetryDelaySeconds > 300)
        {
            warnings.Add("Long retry delay may impact responsiveness");
        }

        // Validate output format
        if (!string.IsNullOrWhiteSpace(config.OutputFormat) && !IsValidOutputFormat(config.OutputFormat))
        {
            errors.Add($"Invalid output format: {config.OutputFormat}");
        }

        // Validate file size limits
        if (config.MaxFileSizeMB <= 0)
        {
            errors.Add("Maximum file size must be greater than 0 MB");
        }
        else if (config.MaxFileSizeMB > 100)
        {
            warnings.Add("Large file size limit may impact memory usage");
        }

        return new ValidationResult { Errors = errors, Warnings = warnings };
    }

    /// <summary>
    /// Validates performance-related settings.
    /// </summary>
    /// <param name="config">The processing configuration to validate.</param>
    /// <returns>Validation results for performance settings.</returns>
    private ValidationResult ValidatePerformanceSettings(ProcessingConfig config)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate concurrency settings
        if (config.MaxConcurrency <= 0)
        {
            errors.Add("Maximum concurrency must be greater than 0");
        }
        else if (config.MaxConcurrency > 20)
        {
            warnings.Add("High concurrency may impact system stability");
        }

        // Validate batch size
        if (config.BatchSize <= 0)
        {
            errors.Add("Batch size must be greater than 0");
        }
        else if (config.BatchSize > 100)
        {
            warnings.Add("Large batch size may impact memory usage");
        }

        // Validate memory limits
        if (config.MaxMemoryUsageMB <= 0)
        {
            errors.Add("Maximum memory usage must be greater than 0 MB");
        }
        else if (config.MaxMemoryUsageMB > 2048)
        {
            warnings.Add("High memory limit may impact system stability");
        }

        return new ValidationResult { Errors = errors, Warnings = warnings };
    }

    /// <summary>
    /// Checks if a language code is valid.
    /// </summary>
    /// <param name="language">The language code to validate.</param>
    /// <returns>True if the language is valid, false otherwise.</returns>
    private static bool IsValidLanguage(string language)
    {
        var validLanguages = new[]
        {
            "eng", "spa", "fra", "deu", "ita", "por", "rus", "jpn", "kor", "chi_sim", "chi_tra",
            "ara", "heb", "tha", "vie", "tur", "pol", "ces", "hun", "swe", "nor", "dan", "fin",
            "nld", "ell", "bul", "hrv", "slv", "est", "lav", "lit", "mlt", "ron", "slk", "sqi"
        };

        return validLanguages.Contains(language.ToLowerInvariant());
    }

    /// <summary>
    /// Checks if an output format is valid.
    /// </summary>
    /// <param name="format">The output format to validate.</param>
    /// <returns>True if the format is valid, false otherwise.</returns>
    private static bool IsValidOutputFormat(string format)
    {
        var validFormats = new[] { "json", "xml", "csv", "txt", "pdf" };
        return validFormats.Contains(format.ToLowerInvariant());
    }

    /// <summary>
    /// Creates a default configuration with recommended settings.
    /// </summary>
    /// <returns>A default processing configuration.</returns>
    public static ProcessingConfig CreateDefaultConfiguration()
    {
        return new ProcessingConfig
        {
            RemoveWatermark = true,
            Deskew = true,
            Binarize = true,
            ExtractSections = true,
            NormalizeText = true,
            OCRConfig = new OCRConfig
            {
                Language = "spa",
                OEM = 3,
                PSM = 6,
                FallbackLanguage = "eng",
                ConfidenceThreshold = 0.7f
            },
            TimeoutSeconds = 300,
            MaxRetries = 3,
            RetryDelaySeconds = 5,
            OutputFormat = "json",
            MaxFileSizeMB = 50,
            MaxConcurrency = 5,
            BatchSize = 10,
            MaxMemoryUsageMB = 1024
        };
    }

    /// <summary>
    /// Creates a high-performance configuration for production use.
    /// </summary>
    /// <returns>A high-performance processing configuration.</returns>
    public static ProcessingConfig CreateHighPerformanceConfiguration()
    {
        return new ProcessingConfig
        {
            RemoveWatermark = true,
            Deskew = true,
            Binarize = true,
            ExtractSections = true,
            NormalizeText = true,
            OCRConfig = new OCRConfig
            {
                Language = "spa",
                OEM = 3,
                PSM = 6,
                FallbackLanguage = "eng",
                ConfidenceThreshold = 0.8f
            },
            TimeoutSeconds = 180,
            MaxRetries = 2,
            RetryDelaySeconds = 3,
            OutputFormat = "json",
            MaxFileSizeMB = 25,
            MaxConcurrency = 10,
            BatchSize = 20,
            MaxMemoryUsageMB = 2048
        };
    }

    /// <summary>
    /// Creates a conservative configuration for maximum accuracy.
    /// </summary>
    /// <returns>A conservative processing configuration.</returns>
    public static ProcessingConfig CreateConservativeConfiguration()
    {
        return new ProcessingConfig
        {
            RemoveWatermark = true,
            Deskew = true,
            Binarize = true,
            ExtractSections = true,
            NormalizeText = true,
            OCRConfig = new OCRConfig
            {
                Language = "spa",
                OEM = 3,
                PSM = 6,
                FallbackLanguage = "eng",
                ConfidenceThreshold = 0.9f
            },
            TimeoutSeconds = 600,
            MaxRetries = 5,
            RetryDelaySeconds = 10,
            OutputFormat = "json",
            MaxFileSizeMB = 10,
            MaxConcurrency = 3,
            BatchSize = 5,
            MaxMemoryUsageMB = 512
        };
    }
}