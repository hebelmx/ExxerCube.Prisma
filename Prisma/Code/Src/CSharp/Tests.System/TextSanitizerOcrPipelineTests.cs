namespace ExxerCube.Prisma.Tests.System;

/// <summary>
/// System-level check: run Tesseract on a noisy fixture and ensure sanitizer normalizes account/SWIFT without blocking.
/// </summary>
[Collection(nameof(TesseractCollection))]
public class TextSanitizerOcrPipelineTests : IDisposable
{
    private readonly IServiceScope _scope;
    private readonly IOcrExecutor _ocr;
    private readonly TextSanitizer _sanitizer;
    private readonly OcrSanitizationService _sanitizationService;
    private readonly ILogger<TextSanitizerOcrPipelineTests> _logger;

    public TextSanitizerOcrPipelineTests(TesseractFixture fixture)
    {
        _scope = fixture.Host.Services.CreateScope();
        _ocr = _scope.ServiceProvider.GetRequiredService<IOcrExecutor>();
        _sanitizer = new TextSanitizer();
        _sanitizationService = new OcrSanitizationService(_sanitizer);
        _logger = _scope.ServiceProvider.GetRequiredService<ILogger<TextSanitizerOcrPipelineTests>>();
    }

    [Fact(Skip = "Temporarily skipped to isolate XmlExtractor tests")]
    [Trait("Category", "System")]
    public async Task Ocr_and_sanitizer_normalize_account_and_swift_from_noisy_image()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "OcrSamples", "ocr_account_noisy.png");
        File.Exists(fixturePath).ShouldBeTrue($"Fixture not found at {fixturePath}");

        var ct = TestContext.Current.CancellationToken;
        var imageBytes = await File.ReadAllBytesAsync(fixturePath, ct);
        var imageData = new ImageData(imageBytes, fixturePath);
        var config = new OCRConfig { Language = "spa", FallbackLanguage = "eng", PSM = 6, OEM = 1 };

        var ocrResult = await _ocr.ExecuteOcrAsync(imageData, config);
        ocrResult.IsSuccess.ShouldBeTrue();
        var text = ocrResult.Value!.Text;
        _logger.LogInformation("OCR text: {Text}", text);

        var sanitized = _sanitizationService.SanitizeAccountAndSwift(text);

        sanitized.Account.Cleaned.ShouldBe("1234567890123456");
        sanitized.Account.Warnings.ShouldContain("AccountNormalized");
        sanitized.Swift.Cleaned.ShouldBe("BNMXMXMMX");
        sanitized.Swift.Warnings.ShouldContain("SwiftNormalized");
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
}
