using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Extraction;
using ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

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
    private readonly ILogger<TextSanitizerOcrPipelineTests> _logger;

    public TextSanitizerOcrPipelineTests(TesseractFixture fixture)
    {
        _scope = fixture.Host.Services.CreateScope();
        _ocr = _scope.ServiceProvider.GetRequiredService<IOcrExecutor>();
        _sanitizer = new TextSanitizer();
        _logger = _scope.ServiceProvider.GetRequiredService<ILogger<TextSanitizerOcrPipelineTests>>();
    }

    [Fact]
    [Trait("Category", "System")]
    public async Task Ocr_and_sanitizer_normalize_account_and_swift_from_noisy_image()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "OcrSamples", "ocr_account_noisy.png");
        File.Exists(fixturePath).ShouldBeTrue($"Fixture not found at {fixturePath}");

        var imageBytes = await File.ReadAllBytesAsync(fixturePath);
        var imageData = new ImageData(imageBytes, fixturePath);
        var config = new OCRConfig { Language = "spa", FallbackLanguage = "eng", PSM = 6, OEM = 1 };

        var ocrResult = await _ocr.ExecuteOcrAsync(imageData, config);
        ocrResult.IsSuccess.ShouldBeTrue();
        var text = ocrResult.Value!.Text;
        _logger.LogInformation("OCR text: {Text}", text);

        var accountLine = text.Split('\n').FirstOrDefault(l => l.Contains("CUENTA", StringComparison.OrdinalIgnoreCase));
        var swiftLine = text.Split('\n').FirstOrDefault(l => l.Contains("SWIFT", StringComparison.OrdinalIgnoreCase));

        accountLine.ShouldNotBeNull("Account line should be present in OCR output");
        swiftLine.ShouldNotBeNull("SWIFT line should be present in OCR output");

        var accountResult = _sanitizer.CleanAccount(accountLine);
        var swiftResult = _sanitizer.CleanSwift(swiftLine);

        accountResult.Cleaned.ShouldBe("1234567890123456");
        accountResult.Warnings.ShouldContain("AccountNormalized");
        swiftResult.Cleaned.ShouldBe("BNMXMXMMX");
        swiftResult.Warnings.ShouldContain("SwiftNormalized");
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
}
