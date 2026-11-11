using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Mutation-killing tests for <see cref="FileTypeIdentifierService"/>. Pins the content-signature
/// boundaries (<c>length &lt; 4</c>, <c>length &gt;= 5</c>), the per-byte magic-number comparisons
/// for PDF and ZIP, the XML <c>StartsWith("&lt;?xml")</c> vs <c>TrimStart().StartsWith("&lt;")</c>
/// alternatives, the DOCX <c>word/</c> vs generic-ZIP <c>else</c> vs <c>xl/</c>-only fall-through,
/// every extension-switch arm, the content-wins-over-extension ordering, and the empty-fileName guard.
/// </summary>
public class FileTypeIdentifierServiceMutationTests
{
    private readonly FileTypeIdentifierService _service =
        new(Substitute.For<ILogger<FileTypeIdentifierService>>());

    private Task<Result<FileFormat>> IdentifyAsync(byte[] content, string? fileName = null) =>
        _service.IdentifyFileTypeAsync(content, fileName, TestContext.Current.CancellationToken);

    private static byte[] Pad(byte[] head, int length)
    {
        var buffer = new byte[length];
        Array.Copy(head, buffer, Math.Min(head.Length, length));
        return buffer;
    }

    private static byte[] ZipWithMarker(string? marker, int markerOffset = 100)
    {
        var buffer = Pad(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, 200);
        if (marker is not null)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(marker);
            Array.Copy(bytes, 0, buffer, markerOffset, bytes.Length);
        }

        return buffer;
    }

    // ---- length < 4 boundary ----

    [Fact]
    public async Task ExactlyFourBytePdfSignature_NoFileName_IdentifiedByContent()
    {
        // Exactly 4 bytes (the MinLength boundary) must still be inspected. If `< 4` is mutated to
        // `< 5`, this 4-byte PDF would fall through to null and fail (no fileName fallback).
        var result = await IdentifyAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(FileFormat.Pdf);
    }

    [Fact]
    public async Task ThreeBytes_TooShortForContentId_FallsBackToExtension()
    {
        // 3 bytes (< 4) -> content id returns null without indexing -> extension fallback succeeds.
        var result = await IdentifyAsync(new byte[] { 0x25, 0x50, 0x44 }, "doc.pdf");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(FileFormat.Pdf);
    }

    // ---- PDF per-byte magic-number discrimination ----

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task PdfSignatureWithOneByteWrong_NoFileName_NotIdentified(int corruptIndex)
    {
        var content = Pad(new byte[] { 0x25, 0x50, 0x44, 0x46 }, 8);
        content[corruptIndex] = 0xFF; // not a sig byte, not '<', not 'PK'

        var result = await IdentifyAsync(content);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Unable to identify");
    }

    // ---- XML: length >= 5 boundary + the two StartsWith alternatives ----

    [Fact]
    public async Task ExactlyFiveByteXmlPrefix_IdentifiedAsXml()
    {
        // "<?xml" is exactly 5 bytes -> hits the `length >= 5` boundary.
        var result = await IdentifyAsync(System.Text.Encoding.UTF8.GetBytes("<?xml"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(FileFormat.Xml);
    }

    [Fact]
    public async Task FourByteXmlPrefix_BelowBoundary_NoFileName_NotIdentified()
    {
        // "<?xm" is 4 bytes: passes `length < 4` but fails `length >= 5` so the XML block is skipped.
        // If `>= 5` is mutated to `>= 4`, this would wrongly become Xml.
        var result = await IdentifyAsync(System.Text.Encoding.UTF8.GetBytes("<?xm"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Unable to identify");
    }

    [Fact]
    public async Task XmlPrefixedDeclaration_IdentifiedAsXml()
    {
        // Hits the first StartsWith("<?xml") operand.
        var result = await IdentifyAsync(System.Text.Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?>"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(FileFormat.Xml);
    }

    [Fact]
    public async Task LeadingWhitespaceThenAngleBracket_IdentifiedViaTrimStart()
    {
        // No "<?xml": only the TrimStart().StartsWith("<") second operand can match.
        var result = await IdentifyAsync(System.Text.Encoding.UTF8.GetBytes("   <root></root>"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(FileFormat.Xml);
    }

    [Fact]
    public async Task FiveBytesNoAngleBracket_NoFileName_NotIdentified()
    {
        // length >= 5 but neither XML operand matches -> not Xml.
        var result = await IdentifyAsync(System.Text.Encoding.UTF8.GetBytes("hello"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Unable to identify");
    }

    // ---- ZIP per-byte discrimination + generic-ZIP else branch ----

    [Fact]
    public async Task ZipSignatureNoOfficeMarker_IdentifiedAsGenericZip()
    {
        // PK sig, no "word/" or "xl/" -> the else branch returns Zip.
        var result = await IdentifyAsync(ZipWithMarker(marker: null));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(FileFormat.Zip);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ZipSignatureWithOneByteWrong_NoFileName_NotIdentified(int corruptIndex)
    {
        var content = ZipWithMarker(marker: null);
        content[corruptIndex] = 0xFF;

        var result = await IdentifyAsync(content);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Unable to identify");
    }

    // ---- DOCX word/ vs xl/-only fall-through ----

    [Fact]
    public async Task ZipWithWordMarker_IdentifiedAsDocx()
    {
        var result = await IdentifyAsync(ZipWithMarker("word/document.xml"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(FileFormat.Docx);
    }

    [Fact]
    public async Task ZipWithXlMarkerOnly_NoFileName_NotIdentified()
    {
        // Outer `Contains("word/") || Contains("xl/")` is true (xl/) but the inner `Contains("word/")`
        // is false: no branch returns, so it falls through to null (NOT Docx, NOT generic Zip).
        var result = await IdentifyAsync(ZipWithMarker("xl/worksheets/"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Unable to identify");
    }

    [Fact]
    public async Task ZipWithBothWordAndXlMarkers_PrefersDocx()
    {
        var buffer = ZipWithMarker("xl/", markerOffset: 100);
        var word = System.Text.Encoding.UTF8.GetBytes("word/");
        Array.Copy(word, 0, buffer, 120, word.Length);

        var result = await IdentifyAsync(buffer);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(FileFormat.Docx);
    }

    // ---- extension fallback: every switch arm + ToLowerInvariant ----

    [Theory]
    [InlineData("file.pdf", true)]
    [InlineData("file.xml", true)]
    [InlineData("file.docx", true)]
    [InlineData("file.zip", true)]
    [InlineData("file.PDF", true)]   // ToLowerInvariant must normalize
    [InlineData("file.txt", false)]  // unrecognized -> default arm -> null
    [InlineData("noextension", false)]
    public async Task UnknownContent_ExtensionFallback(string fileName, bool expectSuccess)
    {
        var unknown = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        var result = await IdentifyAsync(unknown, fileName);

        result.IsSuccess.ShouldBe(expectSuccess);
    }

    [Theory]
    [InlineData("file.pdf", "Pdf")]
    [InlineData("file.xml", "Xml")]
    [InlineData("file.docx", "Docx")]
    [InlineData("file.zip", "Zip")]
    public async Task UnknownContent_ExtensionFallback_MapsToExpectedFormat(string fileName, string expectedName)
    {
        var unknown = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        var result = await IdentifyAsync(unknown, fileName);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Name.ShouldBe(expectedName);
    }

    // ---- ordering: content wins over a conflicting extension ----

    [Fact]
    public async Task PdfContentWithXmlExtension_ContentWins()
    {
        // Content id runs first; extension fallback only runs when content id returns null.
        var result = await IdentifyAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 }, "actually.xml");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(FileFormat.Pdf);
    }

    // ---- guards: unknown content + null / empty fileName ----

    [Fact]
    public async Task UnknownContent_NullFileName_Fails()
    {
        var result = await IdentifyAsync(new byte[] { 0x00, 0x01, 0x02, 0x03 });

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Unable to identify");
    }

    [Fact]
    public async Task UnknownContent_EmptyFileName_SkipsExtensionAndFails()
    {
        // `!string.IsNullOrEmpty(fileName)` must short-circuit the extension fallback for "".
        var result = await IdentifyAsync(new byte[] { 0x00, 0x01, 0x02, 0x03 }, string.Empty);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Unable to identify");
    }
}
