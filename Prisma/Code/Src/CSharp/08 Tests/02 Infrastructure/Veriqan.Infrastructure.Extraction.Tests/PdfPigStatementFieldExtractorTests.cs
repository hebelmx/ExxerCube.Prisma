using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using IndQuestResults.Operations;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Integration tests for <see cref="PdfPigStatementFieldExtractor"/> against the three
/// Dummie VEC statement fixtures (jul-ago, ago-sep, sep-oct 2025).
/// </summary>
/// <remarks>
/// <para>
/// These tests run against the real PDF text layer — no mocking.
/// The fixtures are copied to the test output directory as Content items in the csproj.
/// </para>
/// <para>
/// CLABE finding: all three Dummie VEC PDFs contain <c>9876543210123</c> (13 digits),
/// which is deliberately short of the 18-digit CLABE standard. Tests assert
/// <see cref="ExtractionStatus.ExtractedInvalidFormat"/> for CLABE — this is correct
/// behaviour; the extractor surfaces what is actually in the document.
/// </para>
/// </remarks>
public sealed class PdfPigStatementFieldExtractorTests
{
    // -----------------------------------------------------------------------
    // Fixture paths (resolved relative to test output directory)
    // -----------------------------------------------------------------------

    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static string FixturePath(string fileName) =>
        Path.Combine(FixturesDir, fileName);

    private static readonly string JulAgoFixture =
        FixturePath("01+Dummie+VEC+jul_ago+20252.pdf");

    private static readonly string AgoSepFixture =
        FixturePath("02+Dummie+VEC+ago_sep+2025.pdf");

    private static readonly string SepOctFixture =
        FixturePath("03+Dummie+VEC+sep_oct+2025.pdf");

    /// <summary>All three fixture paths (for parameterised tests).</summary>
    public static IEnumerable<object[]> AllFixtures =>
    [
        [JulAgoFixture, "jul_ago"],
        [AgoSepFixture, "ago_sep"],
        [SepOctFixture, "sep_oct"],
    ];

    // -----------------------------------------------------------------------
    // Factory
    // -----------------------------------------------------------------------

    private static PdfPigStatementFieldExtractor CreateExtractor()
    {
        var logger = XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>();
        return new PdfPigStatementFieldExtractor(logger, Microsoft.Extensions.Options.Options.Create(new PdfExtractionOptions()), new NullPasswordProvider());
    }

    private static byte[] ReadFixture(string path)
    {
        File.Exists(path).ShouldBeTrue($"Fixture not found at: {path}");
        return File.ReadAllBytes(path);
    }

    // -----------------------------------------------------------------------
    // Test 1: Card number normalized to 16 digits
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task Extract_DummieVecStatement_CardNumberIs16Digits(string fixturePath, string label)
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(fixturePath);

        var result = await extractor.ExtractHeaderAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue($"[{label}] Extraction must succeed. Error: {result.Error}");
        var model = result.Value!;

        model.CardNumber.Status.ShouldBe(
            ExtractionStatus.Extracted,
            $"[{label}] Card number should be valid (16 digits)");

        var digits = model.CardNumber.Value!;
        digits.ShouldNotBeNullOrWhiteSpace($"[{label}] Card number value must not be blank");
        digits.Length.ShouldBe(16, $"[{label}] Card number must be 16 digits; got '{digits}' ({digits.Length} chars)");

        // Assert digits-only (each char is a digit)
        foreach (var ch in digits)
            char.IsDigit(ch).ShouldBeTrue($"[{label}] Card number char '{ch}' must be a digit");

        // Fixture-specific sanity: the dummy card is 4567 8901 2345 6789
        digits.ShouldBe("4567890123456789", $"[{label}] Expected specific card number from fixture");

        model.CardNumber.Confidence.ShouldBe(1.0, $"[{label}] Card confidence should be 1.0 for valid format");
        model.CardNumber.Locator.ShouldNotBeNull($"[{label}] Locator must not be null");
        model.CardNumber.Locator.PageNumber.ShouldBe(1);
    }

    // -----------------------------------------------------------------------
    // Test 2: CLABE is 13 digits in all three fixtures (fixture is deliberately invalid)
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task Extract_DummieVecStatement_ClabeIsExtractedInvalidFormat_13Digits(string fixturePath, string label)
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(fixturePath);

        var result = await extractor.ExtractHeaderAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue($"[{label}] Extraction must succeed. Error: {result.Error}");
        var model = result.Value!;

        // The fixture CLABE is "9876543210123" — 13 digits, NOT 18.
        // The extractor must report this as ExtractedInvalidFormat and preserve the value.
        model.Clabe.Status.ShouldBe(
            ExtractionStatus.ExtractedInvalidFormat,
            $"[{label}] CLABE '9876543210123' is 13 digits; must be reported as ExtractedInvalidFormat");

        var clabeDigits = model.Clabe.Value!;
        clabeDigits.ShouldNotBeNullOrWhiteSpace($"[{label}] CLABE value must not be blank even when invalid");
        clabeDigits.Length.ShouldBe(13, $"[{label}] Expected 13-digit CLABE in fixture; got '{clabeDigits}' ({clabeDigits.Length} chars)");
        clabeDigits.ShouldBe("9876543210123", $"[{label}] Expected specific CLABE value from fixture");

        // Confidence should be < 1.0 for invalid format but > 0.0 (it was found).
        model.Clabe.Confidence.ShouldBeGreaterThan(0.0, $"[{label}] CLABE was found so confidence > 0");
        model.Clabe.Confidence.ShouldBeLessThan(1.0, $"[{label}] CLABE format is invalid so confidence < 1.0");

        model.Clabe.Locator.ShouldNotBeNull($"[{label}] Locator must not be null");
        model.Clabe.Locator.PageNumber.ShouldBe(1);
    }

    // -----------------------------------------------------------------------
    // Test 3: RFC matches the Mexican RFC pattern
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task Extract_DummieVecStatement_RfcMatchesPattern(string fixturePath, string label)
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(fixturePath);

        var result = await extractor.ExtractHeaderAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue($"[{label}] Extraction must succeed. Error: {result.Error}");
        var model = result.Value!;

        model.Rfc.Status.ShouldBe(
            ExtractionStatus.Extracted,
            $"[{label}] RFC should be valid");

        var rfc = model.Rfc.Value!;
        rfc.ShouldNotBeNullOrWhiteSpace($"[{label}] RFC value must not be blank");
        rfc.ShouldBe("PEPG780620225", $"[{label}] Expected specific RFC from fixture");

        // Pattern check: 4-char alpha prefix + 6 digits + 3 alphanumeric homoclave
        System.Text.RegularExpressions.Regex.IsMatch(
            rfc,
            @"^[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}$")
            .ShouldBeTrue($"[{label}] RFC '{rfc}' does not match Mexican RFC pattern");

        model.Rfc.Confidence.ShouldBe(1.0, $"[{label}] RFC confidence must be 1.0 for valid format");
        model.Rfc.Locator.ShouldNotBeNull();
        model.Rfc.Locator.PageNumber.ShouldBe(1);
    }

    // -----------------------------------------------------------------------
    // Test 4: Every header field has a non-null Locator and meaningful Confidence
    //         (no field is silently blank — missing fields are NotExtracted with a hint)
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task Extract_DummieVecStatement_EveryHeaderFieldHasConfidenceAndLocator(string fixturePath, string label)
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(fixturePath);

        var result = await extractor.ExtractHeaderAsync(pdf, ct);
        result.IsSuccess.ShouldBeTrue($"[{label}] Extraction must succeed. Error: {result.Error}");
        var model = result.Value!;

        AssertFieldHasLocator(model.BranchNumber, "BranchNumber", label);
        AssertFieldHasLocator(model.CardNumber, "CardNumber", label);
        AssertFieldHasLocator(model.Clabe, "Clabe", label);
        AssertFieldHasLocator(model.ClientNumber, "ClientNumber", label);
        AssertFieldHasLocator(model.Rfc, "Rfc", label);

        // Complex fields
        model.ClientName.Locator.ShouldNotBeNull($"[{label}] ClientName.Locator must not be null");
        model.ClientName.Locator.PageNumber.ShouldBe(1, $"[{label}] ClientName.Locator.PageNumber must be 1");
        model.ClientName.Confidence.ShouldBeGreaterThanOrEqualTo(0.0, $"[{label}] ClientName.Confidence >= 0");
        model.ClientName.Confidence.ShouldBeLessThanOrEqualTo(1.0, $"[{label}] ClientName.Confidence <= 1");

        model.Address.Locator.ShouldNotBeNull($"[{label}] Address.Locator must not be null");
        model.Address.Locator.PageNumber.ShouldBe(1, $"[{label}] Address.Locator.PageNumber must be 1");
        model.Address.Confidence.ShouldBeGreaterThanOrEqualTo(0.0, $"[{label}] Address.Confidence >= 0");
        model.Address.Confidence.ShouldBeLessThanOrEqualTo(1.0, $"[{label}] Address.Confidence <= 1");
    }

    private static void AssertFieldHasLocator(ExtractedField<string> field, string name, string label)
    {
        field.ShouldNotBeNull($"[{label}] {name} field object must not be null");
        field.Locator.ShouldNotBeNull($"[{label}] {name}.Locator must not be null (NotExtracted still needs a hint)");
        field.Locator.PageNumber.ShouldBeGreaterThan(0, $"[{label}] {name}.Locator.PageNumber must be >= 1");
        field.Confidence.ShouldBeGreaterThanOrEqualTo(0.0, $"[{label}] {name}.Confidence >= 0");
        field.Confidence.ShouldBeLessThanOrEqualTo(1.0, $"[{label}] {name}.Confidence <= 1");

        if (field.Status == ExtractionStatus.NotExtracted)
            field.Value.ShouldBeNull($"[{label}] {name} NotExtracted value should be null");
    }

    // -----------------------------------------------------------------------
    // Test 5: Client name and address extracted
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task Extract_DummieVecStatement_ClientNameAndAddressExtracted(string fixturePath, string label)
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(fixturePath);

        var result = await extractor.ExtractHeaderAsync(pdf, ct);
        result.IsSuccess.ShouldBeTrue($"[{label}] Extraction must succeed. Error: {result.Error}");
        var model = result.Value!;

        // Client name
        model.ClientName.Status.ShouldBe(
            ExtractionStatus.Extracted,
            $"[{label}] Client name must be extracted");
        var name = model.ClientName.Value!;
        name.FullName.ShouldNotBeNullOrWhiteSpace($"[{label}] Full name must not be blank");
        name.FullName.ShouldContain("GUADALUPE");
        name.FullName.ShouldContain("PEPITA");

        // Best-effort split: with 3 tokens, first is "GUADALUPE", last is "PEPITA PEPITA"
        name.FirstNames.ShouldBe("GUADALUPE", $"[{label}] FirstNames should be 'GUADALUPE'");
        name.LastNames.ShouldNotBeNullOrWhiteSpace($"[{label}] LastNames must not be blank for 3-token name");
        name.LastNames!.ShouldContain("PEPITA");

        // Address
        model.Address.Status.ShouldBe(
            ExtractionStatus.Extracted,
            $"[{label}] Address must be extracted");
        var addr = model.Address.Value!;
        addr.RawAddress.ShouldNotBeNullOrWhiteSpace($"[{label}] RawAddress must not be blank");
        addr.RawAddress.ShouldContain("PARQUE");
        addr.RawAddress.ShouldContain("11850");
        addr.PostalCode.ShouldBe("11850", $"[{label}] PostalCode must be '11850'");
        addr.State.ShouldNotBeNullOrWhiteSpace($"[{label}] State must not be blank");
    }

    // -----------------------------------------------------------------------
    // Test 6: Branch number and client number extracted
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public async Task Extract_DummieVecStatement_BranchAndClientNumberExtracted(string fixturePath, string label)
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();
        var pdf = ReadFixture(fixturePath);

        var result = await extractor.ExtractHeaderAsync(pdf, ct);
        result.IsSuccess.ShouldBeTrue($"[{label}] Extraction must succeed. Error: {result.Error}");
        var model = result.Value!;

        model.BranchNumber.Status.ShouldBe(ExtractionStatus.Extracted,
            $"[{label}] BranchNumber must be extracted");
        model.BranchNumber.Value.ShouldBe("910",
            $"[{label}] BranchNumber should be '910'");

        model.ClientNumber.Status.ShouldBe(ExtractionStatus.Extracted,
            $"[{label}] ClientNumber must be extracted");
        model.ClientNumber.Value.ShouldBe("99887766",
            $"[{label}] ClientNumber should be '99887766'");
    }

    // -----------------------------------------------------------------------
    // Test 7: Pre-cancelled token returns a cancelled result without opening the PDF
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Extract_Cancelled_ReturnsCancelled()
    {
        var extractor = CreateExtractor();
        var pdf = ReadFixture(JulAgoFixture);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await extractor.ExtractHeaderAsync(pdf, cts.Token);

        result.IsFailure.ShouldBeTrue("Pre-cancelled token must produce a failure result");
        result.IsCancelled().ShouldBeTrue("Pre-cancelled token must produce a cancelled result");
        result.Value.ShouldBeNull("No model should be returned on cancellation");
    }

    // -----------------------------------------------------------------------
    // Test 8: Empty PDF bytes returns a failure (not an exception)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Extract_EmptyBytes_ReturnsFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();

        var result = await extractor.ExtractHeaderAsync([], ct);

        result.IsFailure.ShouldBeTrue("Empty PDF bytes must produce a failure result");
        result.IsCancelled().ShouldBeFalse("Empty PDF is not a cancellation");
    }
}
