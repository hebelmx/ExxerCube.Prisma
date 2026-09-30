using ExxerCube.Prisma.Infrastructure.Extraction.Ocr;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract;

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

    public TextSanitizerOcrPipelineTests(TesseractFixture fixture, ITestOutputHelper output)
    {
        _scope = fixture.Host.Services.CreateScope();
        _ocr = _scope.ServiceProvider.GetRequiredService<IOcrExecutor>();
        _sanitizer = new TextSanitizer();
        _sanitizationService = new OcrSanitizationService(_sanitizer);
        _logger = XUnitLogger.CreateLogger<TextSanitizerOcrPipelineTests>(output);
    }

    [Fact]
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

        _logger.LogInformation("Sanitized Result: {@Sanitized}", sanitized);
        sanitized.Account.Cleaned.ShouldBe("1234567890123456");
        _logger.LogInformation("Sanitized Account: {Account}", sanitized.Account.Cleaned);
        sanitized.Account.Warnings.ShouldContain("AccountNormalized");
        _logger.LogInformation("Sanitized Account: {Account}", sanitized.Account.Cleaned);
        sanitized.Swift.Cleaned.ShouldBe("BNMXMXMMX"); // TextSanitizer strips "SWIFT" label prefix (PNG fixture has 9-char code)
        _logger.LogInformation("Sanitized SWIFT: {Swift}", sanitized.Swift.Cleaned);
        sanitized.Swift.Warnings.ShouldContain("SwiftNormalized");
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
}