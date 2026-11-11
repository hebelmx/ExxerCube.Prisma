namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Mutation-killing boundary/exact-value tests for <see cref="ConfigurationValidationService"/>.
/// The pre-existing tests cover one example per rule with far-from-boundary values; these pin the
/// exact comparison boundaries, the lookup tables, the error/warning aggregation, and the factories.
/// </summary>
public class ConfigurationValidationServiceMutationTests
{
    // ── numeric ERROR boundaries (valid AT the boundary, error just past it) ────

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(3, false)]
    [InlineData(4, true)]
    public void Oem_RangeBoundaries(int oem, bool expectError) =>
        HasError(Validate(c => c.OCRConfig.OEM = oem), "Invalid OCR Engine Mode").ShouldBe(expectError);

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(13, false)]
    [InlineData(14, true)]
    public void Psm_RangeBoundaries(int psm, bool expectError) =>
        HasError(Validate(c => c.OCRConfig.PSM = psm), "Invalid Page Segmentation Mode").ShouldBe(expectError);

    [Theory]
    [InlineData(-0.01f, true)]
    [InlineData(0.0f, false)]
    [InlineData(1.0f, false)]
    [InlineData(1.01f, true)]
    public void ConfidenceThreshold_RangeBoundaries(float threshold, bool expectError) =>
        HasError(Validate(c => c.OCRConfig.ConfidenceThreshold = threshold), "Invalid confidence threshold").ShouldBe(expectError);

    [Theory]
    [InlineData(0, true, "Timeout must be greater than 0")]
    [InlineData(1, false, "Timeout must be greater than 0")]
    [InlineData(3600, false, "Timeout cannot exceed")]
    [InlineData(3601, true, "Timeout cannot exceed")]
    public void Timeout_Boundaries(int timeout, bool expectError, string fragment) =>
        HasError(Validate(c => c.TimeoutSeconds = timeout), fragment).ShouldBe(expectError);

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    public void MaxRetries_ErrorBoundary(int retries, bool expectError) =>
        HasError(Validate(c => c.MaxRetries = retries), "Maximum retries cannot be negative").ShouldBe(expectError);

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    public void RetryDelay_ErrorBoundary(int delay, bool expectError) =>
        HasError(Validate(c => c.RetryDelaySeconds = delay), "Retry delay cannot be negative").ShouldBe(expectError);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void MaxFileSize_ErrorBoundary(int sizeMb, bool expectError) =>
        HasError(Validate(c => c.MaxFileSizeMB = sizeMb), "Maximum file size must be greater than 0").ShouldBe(expectError);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void MaxConcurrency_ErrorBoundary(int concurrency, bool expectError) =>
        HasError(Validate(c => c.MaxConcurrency = concurrency), "Maximum concurrency must be greater than 0").ShouldBe(expectError);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void BatchSize_ErrorBoundary(int batch, bool expectError) =>
        HasError(Validate(c => c.BatchSize = batch), "Batch size must be greater than 0").ShouldBe(expectError);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void MaxMemory_ErrorBoundary(int memMb, bool expectError) =>
        HasError(Validate(c => c.MaxMemoryUsageMB = memMb), "Maximum memory usage must be greater than 0").ShouldBe(expectError);

    // ── WARNING boundaries (no warning AT the boundary, warning just past it) ────

    [Theory]
    [InlineData(0.95f, false)]
    [InlineData(0.96f, true)]
    public void ConfidenceThreshold_HighWarningBoundary(float threshold, bool expectWarn) =>
        HasWarning(Validate(c => c.OCRConfig.ConfidenceThreshold = threshold), "High confidence threshold").ShouldBe(expectWarn);

    [Theory]
    [InlineData(0.5f, false)]
    [InlineData(0.49f, true)]
    public void ConfidenceThreshold_LowWarningBoundary(float threshold, bool expectWarn) =>
        HasWarning(Validate(c => c.OCRConfig.ConfidenceThreshold = threshold), "Low confidence threshold").ShouldBe(expectWarn);

    [Theory]
    [InlineData(10, false)]
    [InlineData(11, true)]
    public void MaxRetries_WarningBoundary(int retries, bool expectWarn) =>
        HasWarning(Validate(c => c.MaxRetries = retries), "High retry count").ShouldBe(expectWarn);

    [Theory]
    [InlineData(300, false)]
    [InlineData(301, true)]
    public void RetryDelay_WarningBoundary(int delay, bool expectWarn) =>
        HasWarning(Validate(c => c.RetryDelaySeconds = delay), "Long retry delay").ShouldBe(expectWarn);

    [Theory]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public void MaxFileSize_WarningBoundary(int sizeMb, bool expectWarn) =>
        HasWarning(Validate(c => c.MaxFileSizeMB = sizeMb), "Large file size limit").ShouldBe(expectWarn);

    [Theory]
    [InlineData(20, false)]
    [InlineData(21, true)]
    public void MaxConcurrency_WarningBoundary(int concurrency, bool expectWarn) =>
        HasWarning(Validate(c => c.MaxConcurrency = concurrency), "High concurrency").ShouldBe(expectWarn);

    [Theory]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public void BatchSize_WarningBoundary(int batch, bool expectWarn) =>
        HasWarning(Validate(c => c.BatchSize = batch), "Large batch size").ShouldBe(expectWarn);

    [Theory]
    [InlineData(2048, false)]
    [InlineData(2049, true)]
    public void MaxMemory_WarningBoundary(int memMb, bool expectWarn) =>
        HasWarning(Validate(c => c.MaxMemoryUsageMB = memMb), "High memory limit").ShouldBe(expectWarn);

    // ── language / output-format lookup tables ──────────────────────────────────

    [Theory]
    [InlineData("eng")]
    [InlineData("spa")]
    [InlineData("fra")]
    [InlineData("deu")]
    [InlineData("ita")]
    [InlineData("por")]
    [InlineData("rus")]
    [InlineData("jpn")]
    [InlineData("kor")]
    [InlineData("chi_sim")]
    [InlineData("chi_tra")]
    [InlineData("ara")]
    [InlineData("heb")]
    [InlineData("tha")]
    [InlineData("vie")]
    [InlineData("tur")]
    [InlineData("pol")]
    [InlineData("ces")]
    [InlineData("hun")]
    [InlineData("swe")]
    [InlineData("nor")]
    [InlineData("dan")]
    [InlineData("fin")]
    [InlineData("nld")]
    [InlineData("ell")]
    [InlineData("bul")]
    [InlineData("hrv")]
    [InlineData("slv")]
    [InlineData("est")]
    [InlineData("lav")]
    [InlineData("lit")]
    [InlineData("mlt")]
    [InlineData("ron")]
    [InlineData("slk")]
    [InlineData("sqi")]
    public void Language_EveryCatalogedCode_IsValid(string language) =>
        HasError(Validate(c => c.OCRConfig.Language = language), "Invalid OCR language").ShouldBeFalse();

    [Fact]
    public void Language_UpperCase_IsNormalizedAndValid() =>
        HasError(Validate(c => c.OCRConfig.Language = "SPA"), "Invalid OCR language").ShouldBeFalse();

    [Fact]
    public void Language_Unknown_IsInvalid() =>
        HasError(Validate(c => c.OCRConfig.Language = "zzz"), "Invalid OCR language").ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Language_MissingOrBlank_IsRequiredError(string language)
    {
        var result = Validate(c => c.OCRConfig.Language = language);
        HasError(result, "OCR language is required").ShouldBeTrue();
        HasError(result, "Invalid OCR language").ShouldBeFalse(); // the else-if must NOT also fire
    }

    [Fact]
    public void FallbackLanguage_Unknown_IsInvalid() =>
        HasError(Validate(c => c.OCRConfig.FallbackLanguage = "zzz"), "Invalid fallback language").ShouldBeTrue();

    [Fact]
    public void FallbackLanguage_UpperCase_IsNormalizedAndValid() =>
        HasError(Validate(c => c.OCRConfig.FallbackLanguage = "ENG"), "Invalid fallback language").ShouldBeFalse();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FallbackLanguage_Blank_IsNotValidated(string fallback) =>
        HasError(Validate(c => c.OCRConfig.FallbackLanguage = fallback), "fallback").ShouldBeFalse();

    [Fact]
    public void OutputFormat_UpperCase_IsNormalizedAndValid() =>
        HasError(Validate(c => c.OutputFormat = "JSON"), "Invalid output format").ShouldBeFalse();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void OutputFormat_Blank_IsNotValidated(string format) =>
        HasError(Validate(c => c.OutputFormat = format), "Invalid output format").ShouldBeFalse();

    // ── IsValid + error/warning aggregation across the three sub-validators ──────

    [Fact]
    public void WarningOnlyConfig_IsValidTrue_NoErrors()
    {
        var result = Validate(c => c.MaxRetries = 15); // warning, not error
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.Warnings.ShouldNotBeEmpty();
    }

    [Fact]
    public void ErrorConfig_IsValidFalse()
    {
        var result = Validate(c => c.OCRConfig.OEM = 99);
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void OcrError_SurfacesAsSingleError() => SingleError(c => c.OCRConfig.OEM = 99, "Invalid OCR Engine Mode");

    [Fact]
    public void ProcessingError_SurfacesAsSingleError() => SingleError(c => c.TimeoutSeconds = 0, "Timeout must be greater than 0");

    [Fact]
    public void PerformanceError_SurfacesAsSingleError() => SingleError(c => c.MaxConcurrency = 0, "Maximum concurrency must be greater than 0");

    [Fact]
    public void OcrWarning_SurfacesAsSingleWarning() => SingleWarning(c => c.OCRConfig.ConfidenceThreshold = 0.98f, "High confidence threshold");

    [Fact]
    public void ProcessingWarning_SurfacesAsSingleWarning() => SingleWarning(c => c.MaxRetries = 15, "High retry count");

    [Fact]
    public void PerformanceWarning_SurfacesAsSingleWarning() => SingleWarning(c => c.MaxConcurrency = 25, "High concurrency");

    // ── catch block ─────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateConfiguration_NullOcrConfig_ReturnsFailureFromCatch()
    {
        var config = CreateValidConfiguration();
        config.OCRConfig = null!; // ValidateOCRConfig dereferences it → NRE inside the try

        var result = NewService().ValidateConfiguration(config);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Configuration validation failed");
    }

    // ── factory presets: exact field values + each validates clean ──────────────

    [Fact]
    public void CreateDefaultConfiguration_HasExactValues()
    {
        var c = ConfigurationValidationService.CreateDefaultConfiguration();
        c.RemoveWatermark.ShouldBeTrue();
        c.Deskew.ShouldBeTrue();
        c.Binarize.ShouldBeTrue();
        c.ExtractSections.ShouldBeTrue();
        c.NormalizeText.ShouldBeTrue();
        c.OCRConfig.Language.ShouldBe("spa");
        c.OCRConfig.OEM.ShouldBe(3);
        c.OCRConfig.PSM.ShouldBe(6);
        c.OCRConfig.FallbackLanguage.ShouldBe("eng");
        c.OCRConfig.ConfidenceThreshold.ShouldBe(0.7f);
        c.TimeoutSeconds.ShouldBe(300);
        c.MaxRetries.ShouldBe(3);
        c.RetryDelaySeconds.ShouldBe(5);
        c.OutputFormat.ShouldBe("json");
        c.MaxFileSizeMB.ShouldBe(50);
        c.MaxConcurrency.ShouldBe(5);
        c.BatchSize.ShouldBe(10);
        c.MaxMemoryUsageMB.ShouldBe(1024);
    }

    [Fact]
    public void CreateHighPerformanceConfiguration_HasExactValues()
    {
        var c = ConfigurationValidationService.CreateHighPerformanceConfiguration();
        c.RemoveWatermark.ShouldBeTrue();
        c.Deskew.ShouldBeTrue();
        c.Binarize.ShouldBeTrue();
        c.ExtractSections.ShouldBeTrue();
        c.NormalizeText.ShouldBeTrue();
        c.OCRConfig.Language.ShouldBe("spa");
        c.OCRConfig.OEM.ShouldBe(3);
        c.OCRConfig.PSM.ShouldBe(6);
        c.OCRConfig.FallbackLanguage.ShouldBe("eng");
        c.OCRConfig.ConfidenceThreshold.ShouldBe(0.8f);
        c.TimeoutSeconds.ShouldBe(180);
        c.MaxRetries.ShouldBe(2);
        c.RetryDelaySeconds.ShouldBe(3);
        c.OutputFormat.ShouldBe("json");
        c.MaxFileSizeMB.ShouldBe(25);
        c.MaxConcurrency.ShouldBe(10);
        c.BatchSize.ShouldBe(20);
        c.MaxMemoryUsageMB.ShouldBe(2048);
    }

    [Fact]
    public void CreateConservativeConfiguration_HasExactValues()
    {
        var c = ConfigurationValidationService.CreateConservativeConfiguration();
        c.RemoveWatermark.ShouldBeTrue();
        c.Deskew.ShouldBeTrue();
        c.Binarize.ShouldBeTrue();
        c.ExtractSections.ShouldBeTrue();
        c.NormalizeText.ShouldBeTrue();
        c.OCRConfig.Language.ShouldBe("spa");
        c.OCRConfig.OEM.ShouldBe(3);
        c.OCRConfig.PSM.ShouldBe(6);
        c.OCRConfig.FallbackLanguage.ShouldBe("eng");
        c.OCRConfig.ConfidenceThreshold.ShouldBe(0.9f);
        c.TimeoutSeconds.ShouldBe(600);
        c.MaxRetries.ShouldBe(5);
        c.RetryDelaySeconds.ShouldBe(10);
        c.OutputFormat.ShouldBe("json");
        c.MaxFileSizeMB.ShouldBe(10);
        c.MaxConcurrency.ShouldBe(3);
        c.BatchSize.ShouldBe(5);
        c.MaxMemoryUsageMB.ShouldBe(512);
    }

    [Fact]
    public void AllFactoryPresets_ValidateClean()
    {
        foreach (var config in new[]
        {
            ConfigurationValidationService.CreateDefaultConfiguration(),
            ConfigurationValidationService.CreateHighPerformanceConfiguration(),
            ConfigurationValidationService.CreateConservativeConfiguration(),
        })
        {
            var result = NewService().ValidateConfiguration(config);
            result.IsSuccess.ShouldBeTrue();
            result.Value!.IsValid.ShouldBeTrue();
            result.Value!.Errors.ShouldBeEmpty();
            result.Value!.Warnings.ShouldBeEmpty();
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    private static ConfigurationValidationService NewService() =>
        new(Substitute.For<ILogger<ConfigurationValidationService>>());

    private static ConfigurationValidationResult Validate(Action<ProcessingConfig> mutate)
    {
        var config = CreateValidConfiguration();
        mutate(config);
        var result = NewService().ValidateConfiguration(config);
        result.IsSuccess.ShouldBeTrue();
        return result.Value!;
    }

    private static bool HasError(ConfigurationValidationResult result, string fragment) =>
        result.Errors.Any(e => e.Contains(fragment, StringComparison.Ordinal));

    private static bool HasWarning(ConfigurationValidationResult result, string fragment) =>
        result.Warnings.Any(w => w.Contains(fragment, StringComparison.Ordinal));

    private static void SingleError(Action<ProcessingConfig> mutate, string fragment)
    {
        var result = Validate(mutate);
        result.Errors.Count.ShouldBe(1);
        HasError(result, fragment).ShouldBeTrue();
        result.IsValid.ShouldBeFalse();
    }

    private static void SingleWarning(Action<ProcessingConfig> mutate, string fragment)
    {
        var result = Validate(mutate);
        result.Warnings.Count.ShouldBe(1);
        HasWarning(result, fragment).ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    private static ProcessingConfig CreateValidConfiguration() => new()
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
            ConfidenceThreshold = 0.7f,
        },
        TimeoutSeconds = 300,
        MaxRetries = 3,
        RetryDelaySeconds = 5,
        OutputFormat = "json",
        MaxFileSizeMB = 50,
        MaxConcurrency = 5,
        BatchSize = 10,
        MaxMemoryUsageMB = 1024,
    };
}
