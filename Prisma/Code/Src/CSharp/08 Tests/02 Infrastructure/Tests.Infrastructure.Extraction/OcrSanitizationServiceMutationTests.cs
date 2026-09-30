using ExxerCube.Prisma.Infrastructure.Extraction.Ocr;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Mutation-killing tests for <see cref="OcrSanitizationService"/> — line discovery (first
/// CUENTA / SWIFT line, case-insensitive), delegation to the real <see cref="TextSanitizer"/>,
/// and the cross-field "soft normalization" merge (a normalized SWIFT bumps a clean account).
/// </summary>
public class OcrSanitizationServiceMutationTests
{
    private readonly OcrSanitizationService _service = new(new TextSanitizer());

    [Fact]
    public void SanitizeAccountAndSwift_BothCleanLines_NoMergedWarning()
    {
        var r = _service.SanitizeAccountAndSwift("CUENTA: 123456789\nSWIFT: BOFAUS3N");

        r.Account.Cleaned.ShouldBe("123456789");
        r.Swift.Cleaned.ShouldBe("BOFAUS3N");
        r.Account.Warnings.ShouldBeEmpty();
        r.Swift.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void SanitizeAccountAndSwift_NormalizedSwiftWithCleanAccount_AddsAccountNormalized()
    {
        // SWIFT line has noise -> SwiftNormalized; account is clean (empty warnings) and non-blank
        // -> the merge adds AccountNormalized to the account result.
        var r = _service.SanitizeAccountAndSwift("CUENTA: 123456789\nSWIFT: bofa.us.3n");

        r.Swift.Warnings.ShouldContain("SwiftNormalized");
        r.Account.Warnings.ShouldContain("AccountNormalized");
    }

    [Fact]
    public void SanitizeAccountAndSwift_AccountAlreadyWarned_DoesNotMerge()
    {
        // account itself normalized (dashes removed) -> account.Warnings.Count != 0 -> merge guard
        // (Count == 0) blocks adding a second AccountNormalized; it stays a single warning.
        var r = _service.SanitizeAccountAndSwift("CUENTA: 12-34-56-78\nSWIFT: bofa.us.3n");

        r.Account.Warnings.Count(w => w == "AccountNormalized").ShouldBe(1);
    }

    [Fact]
    public void SanitizeAccountAndSwift_NoMatchingLines_ReportsMissing()
    {
        var r = _service.SanitizeAccountAndSwift("encabezado\notra linea");

        r.Account.Warnings.ShouldContain("AccountMissing");
        r.Swift.Warnings.ShouldContain("SwiftMissing");
    }

    [Fact]
    public void SanitizeAccountAndSwift_PicksFirstCuentaLine()
    {
        var r = _service.SanitizeAccountAndSwift("CUENTA: 111111\nCUENTA: 222222");

        r.Account.Cleaned.ShouldBe("111111"); // first match, not the second
    }

    [Fact]
    public void SanitizeAccountAndSwift_LineMatchIsCaseInsensitive()
    {
        var r = _service.SanitizeAccountAndSwift("cuenta: 333333\nswift: BOFAUS3N");

        r.Account.Cleaned.ShouldBe("333333");
        r.Swift.Cleaned.ShouldBe("BOFAUS3N");
    }

    [Fact]
    public void SanitizeAccountAndSwift_NullText_RawTextEmptyAndBothMissing()
    {
        var r = _service.SanitizeAccountAndSwift(null!);

        r.RawText.ShouldBe(string.Empty);
        r.Account.Warnings.ShouldContain("AccountMissing");
        r.Swift.Warnings.ShouldContain("SwiftMissing");
    }

    [Fact]
    public void SanitizeAccountAndSwift_PreservesRawText()
    {
        const string text = "CUENTA: 123456789\nSWIFT: BOFAUS3N";
        _service.SanitizeAccountAndSwift(text).RawText.ShouldBe(text);
    }

    [Fact]
    public void SanitizeAccountAndSwift_CrlfSeparatedLines_AreSplit()
    {
        var r = _service.SanitizeAccountAndSwift("CUENTA: 654321\r\nSWIFT: BOFAUS3N");

        r.Account.Cleaned.ShouldBe("654321");
        r.Swift.Cleaned.ShouldBe("BOFAUS3N");
    }
}
