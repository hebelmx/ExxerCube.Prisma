using ExxerCube.Prisma.Infrastructure.Extraction.Ocr;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract;

/// <summary>
/// System-level checks that run Tesseract over synthetic fixtures and validate sanitizer outputs.
/// </summary>
[Collection(nameof(TesseractCollection))]
public class OcrFixturePipelineTests : IDisposable
{
    private readonly IServiceScope _scope;
    private readonly IOcrExecutor _ocr;
    private readonly OcrSanitizationService _sanitization;
    private readonly ILogger<OcrFixturePipelineTests> _logger;

    public OcrFixturePipelineTests(TesseractFixture fixture, ITestOutputHelper output)
    {
        _scope = fixture.Host.Services.CreateScope();
        _ocr = _scope.ServiceProvider.GetRequiredService<IOcrExecutor>();
        _sanitization = new OcrSanitizationService(new TextSanitizer());
        _logger = XUnitLogger.CreateLogger<OcrFixturePipelineTests>(output);
    }

    [Fact]
    [Trait("Category", "System")]
    public async Task Accounts_clean_fixture_sanitizes_without_changes()
    {
        _logger.LogInformation("═══ TEST: Accounts_clean_fixture_sanitizes_without_changes ═══");

        var sanitized = await RunOcrAndSanitizeAsync("Accounts/Clean/account_clean.png");

        _logger.LogInformation("═══ SANITIZATION RESULTS ═══");
        _logger.LogInformation("Account.Raw: '{AccountRaw}'", sanitized.Account.Raw);
        _logger.LogInformation("Account.Cleaned: '{AccountCleaned}' (expected: '1234567890123456')", sanitized.Account.Cleaned);
        _logger.LogInformation("Account.Warnings: [{AccountWarnings}] (expected: no 'AccountNormalized')", string.Join(", ", sanitized.Account.Warnings));
        _logger.LogInformation("Swift.Raw: '{SwiftRaw}'", sanitized.Swift.Raw);
        _logger.LogInformation("Swift.Cleaned: '{SwiftCleaned}' (expected: 'BNMXMXM')", sanitized.Swift.Cleaned);
        _logger.LogInformation("Swift.Warnings: [{SwiftWarnings}] (expected: no 'SwiftNormalized')", string.Join(", ", sanitized.Swift.Warnings));

        sanitized.Account.Cleaned.ShouldBe("1234567890123456");
        sanitized.Swift.Cleaned.ShouldBe("BNMXMXMMX"); // TextSanitizer strips "SWIFT" label prefix (9-char from PNG fixture)
        sanitized.Account.Warnings.ShouldNotContain("AccountNormalized");
        sanitized.Swift.Warnings.ShouldNotContain("SwiftNormalized"); // Clean code, only label stripped (not normalization)

        _logger.LogInformation("✓ TEST PASSED");
    }

    [Fact]
    [Trait("Category", "System")]
    public async Task Accounts_noisy_fixture_normalizes_digits_and_swift()
    {
        _logger.LogInformation("═══ TEST: Accounts_noisy_fixture_normalizes_digits_and_swift ═══");

        var sanitized = await RunOcrAndSanitizeAsync("Accounts/Noisy/account_noisy.png");

        _logger.LogInformation("═══ SANITIZATION RESULTS ═══");
        _logger.LogInformation("Account.Raw: '{AccountRaw}'", sanitized.Account.Raw);
        _logger.LogInformation("Account.Cleaned: '{AccountCleaned}' (expected: '1234567890123456')", sanitized.Account.Cleaned);
        _logger.LogInformation("Account.Warnings: [{AccountWarnings}] (expected: 'AccountNormalized')", string.Join(", ", sanitized.Account.Warnings));
        _logger.LogInformation("Swift.Raw: '{SwiftRaw}'", sanitized.Swift.Raw);
        _logger.LogInformation("Swift.Cleaned: '{SwiftCleaned}' (expected: 'BNMXMXMMXXX')", sanitized.Swift.Cleaned);
        _logger.LogInformation("Swift.Warnings: [{SwiftWarnings}] (expected: 'SwiftNormalized')", string.Join(", ", sanitized.Swift.Warnings));

        sanitized.Account.Cleaned.ShouldBe("1234567890123456");
        sanitized.Swift.Cleaned.ShouldBe("BNMXMXMMXXX"); // TextSanitizer strips "SWIFT" label prefix
        sanitized.Account.Warnings.ShouldContain("AccountNormalized");
        sanitized.Swift.Warnings.ShouldContain("SwiftNormalized");

        _logger.LogInformation("✓ TEST PASSED");
    }

    [Fact]
    [Trait("Category", "System")]
    public async Task Accounts_missing_fixture_flags_missing_account()
    {
        var sanitized = await RunOcrAndSanitizeAsync("Accounts/MissingInOne/account_missing.png");
        sanitized.Account.Cleaned.ShouldBeEmpty();
        sanitized.Account.Warnings.ShouldContain("AccountMissing");
    }

    [Fact]
    [Trait("Category", "System")]
    public async Task Edge_no_xml_fixture_sanitizes_account_and_swift()
    {
        var sanitized = await RunOcrAndSanitizeAsync("Edge/NoXml/no_xml.png");
        sanitized.Account.Cleaned.ShouldBe("9988776655443322");
        sanitized.Swift.Cleaned.ShouldBe("ABCDUS33XXX"); // TextSanitizer strips "SWIFT" label prefix (11 chars)
    }

    [Fact]
    [Trait("Category", "System")]
    public async Task Identity_conflict_fixture_contains_both_rfc_values()
    {
        var text = await RunOcrAsync("Identity/Conflict/identity_conflict.png");
        text.ShouldContain("RFC", Case.Insensitive);
        text.ShouldContain("PELJ800101ABC", Case.Insensitive);
        text.ShouldContain("PELJ800101XYZ", Case.Insensitive);
    }

    [Fact]
    [Trait("Category", "System")]
    public async Task Names_duplicate_different_fixture_contains_both_names()
    {
        var text = await RunOcrAsync("Names/DuplicateDifferent/names_diff.png");
        text.ShouldContain("MARIA GOMEZ GARCIA", Case.Insensitive);
        text.ShouldContain("MARIA GOMEZ HERNANDEZ", Case.Insensitive);
    }

    [Fact]
    [Trait("Category", "System")]
    public async Task Gibberish_fixture_raises_warnings_but_keeps_flow()
    {
        var sanitized = await RunOcrAndSanitizeAsync("Edge/GibberishAccount/gibberish.png");
        sanitized.Account.Cleaned.ShouldNotBeEmpty();
        sanitized.Account.Warnings.ShouldContain("AccountNormalized");
        sanitized.Swift.Cleaned.ShouldBe("O0ORAS"); // TextSanitizer strips "SWIFT" label prefix (gibberish remains)
        sanitized.Swift.Warnings.ShouldContain("SwiftNormalized");
    }

    private async Task<SanitizedOcrValues> RunOcrAndSanitizeAsync(string relativeFixturePath)
    {
        _logger.LogInformation("→ Running OCR and sanitization for: {Fixture}", relativeFixturePath);

        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", relativeFixturePath.Replace('/', Path.DirectorySeparatorChar));
        _logger.LogInformation("→ Fixture path: {FixturePath}", fixturePath);
        _logger.LogInformation("→ File exists: {Exists}", File.Exists(fixturePath));
        File.Exists(fixturePath).ShouldBeTrue($"Fixture not found at {fixturePath}");

        var bytes = await File.ReadAllBytesAsync(fixturePath, TestContext.Current.CancellationToken);
        _logger.LogInformation("→ Image size: {Size} bytes", bytes.Length);

        var imageData = new ImageData(bytes, fixturePath);
        var config = new OCRConfig { Language = "spa", FallbackLanguage = "eng", PSM = 6, OEM = 1 };

        _logger.LogInformation("→ Executing OCR (language: spa, fallback: eng, PSM: 6, OEM: 1)...");
        var ocrResult = await _ocr.ExecuteOcrAsync(imageData, config);
        _logger.LogInformation("→ OCR success: {Success}", ocrResult.IsSuccess);
        ocrResult.IsSuccess.ShouldBeTrue(ocrResult.Error ?? "OCR failed");

        var text = ocrResult.Value!.Text ?? string.Empty;
        _logger.LogInformation("→ OCR text ({Length} chars): {Text}", text.Length, text);

        _logger.LogInformation("→ Running sanitization...");
        var sanitized = _sanitization.SanitizeAccountAndSwift(text);
        _logger.LogInformation("→ Sanitization complete");

        return sanitized;
    }

    private async Task<string> RunOcrAsync(string relativeFixturePath)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", relativeFixturePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(fixturePath).ShouldBeTrue($"Fixture not found at {fixturePath}");

        var bytes = await File.ReadAllBytesAsync(fixturePath, TestContext.Current.CancellationToken);
        var imageData = new ImageData(bytes, fixturePath);
        var config = new OCRConfig { Language = "spa", FallbackLanguage = "eng", PSM = 6, OEM = 1 };

        var ocrResult = await _ocr.ExecuteOcrAsync(imageData, config);
        ocrResult.IsSuccess.ShouldBeTrue(ocrResult.Error ?? "OCR failed");
        return ocrResult.Value!.Text ?? string.Empty;
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
}