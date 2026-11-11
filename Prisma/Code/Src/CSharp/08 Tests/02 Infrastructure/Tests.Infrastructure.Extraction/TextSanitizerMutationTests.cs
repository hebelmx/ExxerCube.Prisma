using ExxerCube.Prisma.Infrastructure.Extraction.Ocr;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Mutation-killing tests for <see cref="TextSanitizer"/> (pure OCR text cleanup). Pins the
/// account / SWIFT / generic cleaners: label stripping, non-digit / non-alphanumeric removal,
/// the length-suspect boundaries, the "only normalized if the value itself changed" rule, and
/// the SWIFT 9->11 padding conditions.
/// </summary>
public class TextSanitizerMutationTests
{
    private readonly TextSanitizer _sanitizer = new();

    // ---- CleanAccount ----

    [Fact]
    public void CleanAccount_Null_ReportsMissing()
    {
        var r = _sanitizer.CleanAccount(null);
        r.Raw.ShouldBe(string.Empty);
        r.Cleaned.ShouldBe(string.Empty);
        r.Warnings.ShouldContain("AccountMissing");
    }

    [Fact]
    public void CleanAccount_PlainDigitsInRange_NoWarnings()
    {
        var r = _sanitizer.CleanAccount("123456789");
        r.Cleaned.ShouldBe("123456789");
        r.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void CleanAccount_LabelStrippedButDigitsUnchanged_NoNormalizedWarning()
    {
        // label removal alone must NOT flag AccountNormalized (cleaned == withoutLabel).
        var r = _sanitizer.CleanAccount("CUENTA: 123456789");
        r.Cleaned.ShouldBe("123456789");
        r.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void CleanAccount_NonDigitsRemoved_FlagsNormalized()
    {
        var r = _sanitizer.CleanAccount("12-34-56-78");
        r.Cleaned.ShouldBe("12345678");
        r.Warnings.ShouldContain("AccountNormalized");
        r.Warnings.ShouldNotContain("AccountLengthSuspect"); // 8 in [6,20]
    }

    [Theory]
    [InlineData("123456", false)]              // exactly MinAccountLength (6) -> not suspect
    [InlineData("12345", true)]                // 5 -> suspect
    [InlineData("12345678901234567890", false)] // exactly MaxAccountLength (20) -> not suspect
    [InlineData("123456789012345678901", true)] // 21 -> suspect
    public void CleanAccount_LengthBoundaries(string digits, bool suspect)
    {
        var r = _sanitizer.CleanAccount(digits);
        if (suspect)
            r.Warnings.ShouldContain("AccountLengthSuspect");
        else
            r.Warnings.ShouldNotContain("AccountLengthSuspect");
    }

    [Fact]
    public void CleanAccount_LabelOnly_ReportsMissing()
    {
        var r = _sanitizer.CleanAccount("CUENTA:");
        r.Cleaned.ShouldBe(string.Empty);
        r.Warnings.ShouldContain("AccountMissing");
    }

    // ---- CleanSwift ----

    [Fact]
    public void CleanSwift_Null_ReportsMissing()
    {
        var r = _sanitizer.CleanSwift(null);
        r.Cleaned.ShouldBe(string.Empty);
        r.Warnings.ShouldContain("SwiftMissing");
    }

    [Fact]
    public void CleanSwift_CleanEightChar_NoWarnings()
    {
        var r = _sanitizer.CleanSwift("SWIFT: BOFAUS3N");
        r.Cleaned.ShouldBe("BOFAUS3N");
        r.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void CleanSwift_LowercaseNoise_UppercasesAndFlagsNormalized()
    {
        var r = _sanitizer.CleanSwift("bofa.us.3n");
        r.Cleaned.ShouldBe("BOFAUS3N");
        r.Warnings.ShouldContain("SwiftNormalized");
        r.Warnings.ShouldNotContain("SwiftLengthSuspect"); // 8 is standard
    }

    [Fact]
    public void CleanSwift_NineCharsNoWhitespaceWithNoise_PadsToEleven()
    {
        // 9 alphanumerics after stripping a non-alnum, no whitespace -> normalized + pad to 11 'X'.
        var r = _sanitizer.CleanSwift("BANKUS33X.");
        r.Cleaned.ShouldBe("BANKUS33XXX"); // 9 -> PadRight(11,'X')
        r.Warnings.ShouldContain("SwiftNormalized");
        r.Warnings.ShouldNotContain("SwiftLengthSuspect"); // padded to 11 -> standard
    }

    [Fact]
    public void CleanSwift_NineCharsClean_NotPaddedAndStandard()
    {
        // 9 alphanumerics, no noise -> not normalized -> shouldPad false -> stays 9, still standard.
        var r = _sanitizer.CleanSwift("BOFAUS3NX");
        r.Cleaned.ShouldBe("BOFAUS3NX");
        r.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void CleanSwift_NineCharsWithWhitespace_NotPadded()
    {
        // 9 alnum but had whitespace -> shouldPad false (the !hadWhitespace clause) -> stays 9.
        var r = _sanitizer.CleanSwift("BOF AUS3NX");
        r.Cleaned.ShouldBe("BOFAUS3NX");
        r.Cleaned.Length.ShouldBe(9);
        r.Warnings.ShouldContain("SwiftNormalized");
    }

    [Fact]
    public void CleanSwift_SevenChars_LengthSuspect()
    {
        var r = _sanitizer.CleanSwift("BOFAU3N");
        r.Cleaned.ShouldBe("BOFAU3N");
        r.Warnings.ShouldContain("SwiftLengthSuspect");
    }

    // ---- CleanGeneric ----

    [Fact]
    public void CleanGeneric_CollapsesWhitespaceAndTrims()
    {
        var r = _sanitizer.CleanGeneric("  hello   world  ");
        r.Cleaned.ShouldBe("hello world");
        r.Warnings.ShouldContain("GenericNormalized");
    }

    [Fact]
    public void CleanGeneric_Null_ProducesEmptyResult()
    {
        // exercises the `raw ?? string.Empty` null fallback (otherwise NoCoverage).
        var r = _sanitizer.CleanGeneric(null);
        r.Cleaned.ShouldBe(string.Empty);
        r.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void CleanGeneric_AlreadyClean_NoWarning()
    {
        var r = _sanitizer.CleanGeneric("hello world");
        r.Cleaned.ShouldBe("hello world");
        r.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void CleanGeneric_TabsAndNewlines_CollapseToSingleSpace()
    {
        var r = _sanitizer.CleanGeneric("a\t\tb\nc");
        r.Cleaned.ShouldBe("a b c");
        r.Warnings.ShouldContain("GenericNormalized");
    }
}
