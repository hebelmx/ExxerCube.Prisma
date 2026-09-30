using ExxerCube.Prisma.Infrastructure.Extraction.Ocr;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract;

public class TextSanitizerTests
{
    private readonly TextSanitizer _sut = new();

    [Fact]
    public void CleanAccount_removes_noise_and_flags_normalization()
    {
        var result = _sut.CleanAccount("a c o u n t 1 2-34 56");

        result.Raw.ShouldBe("a c o u n t 1 2-34 56");
        result.Cleaned.ShouldBe("123456");
        result.Warnings.ShouldContain("AccountNormalized");
        result.Warnings.ShouldNotContain("AccountLengthSuspect");
    }

    [Fact]
    public void CleanAccount_flags_missing_when_empty()
    {
        var result = _sut.CleanAccount("   ");

        result.Cleaned.ShouldBeEmpty();
        result.Warnings.ShouldContain("AccountMissing");
    }

    [Fact]
    public void CleanSwift_normalizes_and_checks_length()
    {
        var result = _sut.CleanSwift(" abcd efgh ij ");

        result.Raw.ShouldBe(" abcd efgh ij ");
        result.Cleaned.ShouldBe("ABCDEFGHIJ");
        result.Warnings.ShouldContain("SwiftNormalized");
        result.Warnings.ShouldContain("SwiftLengthSuspect");
    }

    [Fact]
    public void CleanSwift_accepts_valid_length()
    {
        var result = _sut.CleanSwift("abcDefGh");

        result.Cleaned.ShouldBe("ABCDEFGH");
        result.Warnings.ShouldContain("SwiftNormalized");
        result.Warnings.ShouldNotContain("SwiftLengthSuspect");
    }

    [Fact]
    public void CleanGeneric_collapses_whitespace()
    {
        var result = _sut.CleanGeneric(" a   b\tc  d ");

        result.Raw.ShouldBe(" a   b\tc  d ");
        result.Cleaned.ShouldBe("a b c d");
        result.Warnings.ShouldContain("GenericNormalized");
    }

    [Fact]
    public void CleanAccount_and_Swift_from_fixture_are_normalized()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "OcrSamples", "noisy_account.txt");
        var text = File.ReadAllText(fixturePath);

        // Split by any line ending style (\r\n, \n, \r) and remove empty entries
        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        lines.Length.ShouldBeGreaterThanOrEqualTo(2, "Fixture should have at least 2 lines (account and swift)");

        var accountLine = lines[0];
        var swiftLine = lines[1];

        var accountResult = _sut.CleanAccount(accountLine);
        var swiftResult = _sut.CleanSwift(swiftLine);

        accountResult.Cleaned.ShouldBe("123456789");
        accountResult.Warnings.ShouldContain("AccountNormalized");

        swiftResult.Cleaned.ShouldBe("BNMXMXMMXXX"); // TextSanitizer strips "SWIFT" label prefix
        swiftResult.Warnings.ShouldContain("SwiftNormalized");
    }

    [Fact]
    public void CleanSwift_handles_OCR_noise_with_accents_and_special_characters()
    {
        // Real-world OCR example: Accents and special characters appear in place of letters
        var noisySwift = "SWIFT: B N M X M X M M´X¨X";

        var result = _sut.CleanSwift(noisySwift);

        result.Raw.ShouldBe(noisySwift);
        result.Cleaned.ShouldBe("BNMXMXMMXX"); // Strips "SWIFT" label, spaces, accents (´, ¨)
        result.Warnings.ShouldContain("SwiftNormalized");
        result.Warnings.ShouldContain("SwiftLengthSuspect"); // 10 chars instead of 8 or 11
    }
}
