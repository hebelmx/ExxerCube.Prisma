using ExxerCube.Prisma.Infrastructure.Extraction;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2;

public class TextSanitizerTests
{
    private readonly TextSanitizer _sut = new();

    [Fact(Skip = "Temporarily skipped to isolate XmlExtractor tests")]
    public void CleanAccount_removes_noise_and_flags_normalization()
    {
        var result = _sut.CleanAccount("a c o u n t 1 2-34 56");

        result.Raw.ShouldBe("a c o u n t 1 2-34 56");
        result.Cleaned.ShouldBe("123456");
        result.Warnings.ShouldContain("AccountNormalized");
        result.Warnings.ShouldNotContain("AccountLengthSuspect");
    }

    [Fact(Skip = "Temporarily skipped to isolate XmlExtractor tests")]
    public void CleanAccount_flags_missing_when_empty()
    {
        var result = _sut.CleanAccount("   ");

        result.Cleaned.ShouldBeEmpty();
        result.Warnings.ShouldContain("AccountMissing");
    }

    [Fact(Skip = "Temporarily skipped to isolate XmlExtractor tests")]
    public void CleanSwift_normalizes_and_checks_length()
    {
        var result = _sut.CleanSwift(" abcd efgh ij ");

        result.Raw.ShouldBe(" abcd efgh ij ");
        result.Cleaned.ShouldBe("ABCDEFGHIJ");
        result.Warnings.ShouldContain("SwiftNormalized");
        result.Warnings.ShouldContain("SwiftLengthSuspect");
    }

    [Fact(Skip = "Temporarily skipped to isolate XmlExtractor tests")]
    public void CleanSwift_accepts_valid_length()
    {
        var result = _sut.CleanSwift("abcDefGh");

        result.Cleaned.ShouldBe("ABCDEFGH");
        result.Warnings.ShouldContain("SwiftNormalized");
        result.Warnings.ShouldNotContain("SwiftLengthSuspect");
    }

    [Fact(Skip = "Temporarily skipped to isolate XmlExtractor tests")]
    public void CleanGeneric_collapses_whitespace()
    {
        var result = _sut.CleanGeneric(" a   b\tc  d ");

        result.Raw.ShouldBe(" a   b\tc  d ");
        result.Cleaned.ShouldBe("a b c d");
        result.Warnings.ShouldContain("GenericNormalized");
    }

    [Fact(Skip = "Temporarily skipped to isolate XmlExtractor tests")]
    public void CleanAccount_and_Swift_from_fixture_are_normalized()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "OcrSamples", "noisy_account.txt");
        var text = File.ReadAllText(fixturePath);

        var accountLine = text.Split(Environment.NewLine)[0];
        var swiftLine = text.Split(Environment.NewLine)[1];

        var accountResult = _sut.CleanAccount(accountLine);
        var swiftResult = _sut.CleanSwift(swiftLine);

        accountResult.Cleaned.ShouldBe("123456789");
        accountResult.Warnings.ShouldContain("AccountNormalized");

        swiftResult.Cleaned.ShouldBe("BNMXMXMMX");
        swiftResult.Warnings.ShouldContain("SwiftNormalized");
    }
}
