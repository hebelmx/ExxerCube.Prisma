using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;
using ZXing;
using ZXing.Rendering;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Fiscal-block extraction tests for <see cref="PdfPigStatementFieldExtractor"/> (Story 6.2).
/// </summary>
public sealed class PdfPigStatementFieldExtractorFiscalTests
{
    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static string FixturePath(string fileName) =>
        Path.Combine(FixturesDir, fileName);

    private static readonly string JulAgoFixture =
        FixturePath("01+Dummie+VEC+jul_ago+20252.pdf");

    private static PdfPigStatementFieldExtractor CreateExtractor() =>
        new(XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>());

    // -----------------------------------------------------------------------
    // Test 1: QR round-trip (encode → decode via ZXing)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Encodes a fiscal CFDI payload as a QR bitmap (BGRA), converts to BGR, then decodes
    /// it using the same ZXing path that the extractor uses for fiscal-block scanning.
    /// </summary>
    [Fact]
#pragma warning disable CA1416
    public void FiscalQrDecode_RoundTrip_DecodesOriginalPayload()
    {
        const string fiscalPayload =
            "https://verificacfdi.facturaelectronica.sat.gob.mx/default.aspx" +
            "?id=A1B2C3D4-1111-2222-3333-444455556666" +
            "&re=ABCD010101AAA&rr=XYZ020202BBB&tt=1000.00&fe=ABCDE12345==";

        // --- Encode to BGRA via ZXing BarcodeWriterPixelData ---
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new ZXing.Common.EncodingOptions
            {
                Width = 300,
                Height = 300,
                Margin = 2,
            },
        };

        var pixelData = writer.Write(fiscalPayload);

        pixelData.ShouldNotBeNull();
        pixelData.Width.ShouldBe(300);
        pixelData.Height.ShouldBe(300);

        // --- Convert BGRA → BGR ---
        var bgr = new byte[pixelData.Width * pixelData.Height * 3];
        for (var i = 0; i < pixelData.Width * pixelData.Height; i++)
        {
            var srcBase = i * 4;
            var dstBase = i * 3;
            bgr[dstBase]     = pixelData.Pixels[srcBase];
            bgr[dstBase + 1] = pixelData.Pixels[srcBase + 1];
            bgr[dstBase + 2] = pixelData.Pixels[srcBase + 2];
        }

        // --- Decode ---
        var luminance = new RGBLuminanceSource(
            bgr,
            pixelData.Width,
            pixelData.Height,
            RGBLuminanceSource.BitmapFormat.BGR24);

        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new ZXing.Common.DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = [BarcodeFormat.QR_CODE],
            },
        };

        var decoded = reader.Decode(luminance);

        decoded.ShouldNotBeNull("ZXing failed to decode the fiscal QR it just encoded.");
        decoded.Text.ShouldBe(fiscalPayload, "Decoded payload must exactly match the original.");
    }
#pragma warning restore CA1416

    // -----------------------------------------------------------------------
    // Test 2: jul_ago fixture — fiscal block discovery (reporting, not compliance)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FiscalBlock_JulAgoFixture_ReportsActualFindings()
    {
        var ct = TestContext.Current.CancellationToken;
        var extractor = CreateExtractor();

        File.Exists(JulAgoFixture).ShouldBeTrue($"Fixture not found at: {JulAgoFixture}");
        var pdf = await File.ReadAllBytesAsync(JulAgoFixture, ct);

        var result = await extractor.ExtractFullAsync(pdf, ct);

        result.IsSuccess.ShouldBeTrue($"Extraction failed: {result.Error}");
        var model = result.Value!;

        model.FiscalBlock.ShouldNotBeNull(
            "ExtractFullAsync must always populate FiscalBlock (non-null).");

        var fb = model.FiscalBlock;

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Fiscal block findings for jul_ago fixture:\n" +
            $"  BlockPresent : {fb.BlockPresent}\n" +
            $"  QrDecoded    : {fb.QrDecoded}\n" +
            $"  QrPayload    : {fb.QrPayload ?? "(null)"}\n" +
            $"  FiscalCode   : {fb.FiscalCode ?? "(null)"}\n" +
            $"  IssuerRfc    : {fb.IssuerRfc ?? "(null)"}\n" +
            $"  ReceiverRfc  : {fb.ReceiverRfc ?? "(null)"}\n" +
            $"  Locator.Page : {fb.Locator.PageNumber}");

        fb.Locator.ShouldNotBeNull();
        fb.Locator.PageNumber.ShouldBeGreaterThan(0);

        if (fb.QrDecoded)
            fb.QrPayload.ShouldNotBeNullOrWhiteSpace("QrDecoded=true but QrPayload is empty");

        if (!fb.BlockPresent)
        {
            fb.QrDecoded.ShouldBeFalse("BlockPresent=false implies QrDecoded=false");
            fb.QrPayload.ShouldBeNull("BlockPresent=false implies QrPayload=null");
            fb.FiscalCode.ShouldBeNull("BlockPresent=false implies FiscalCode=null");
            fb.IssuerRfc.ShouldBeNull("BlockPresent=false implies IssuerRfc=null");
            fb.ReceiverRfc.ShouldBeNull("BlockPresent=false implies ReceiverRfc=null");
        }
    }

    // -----------------------------------------------------------------------
    // Test 3: FiscalBlock.NotPresent() sentinel
    // -----------------------------------------------------------------------

    [Fact]
    public void FiscalBlock_NotPresent_HasCorrectDefaults()
    {
        var fb = FiscalBlock.NotPresent();

        fb.BlockPresent.ShouldBeFalse();
        fb.QrDecoded.ShouldBeFalse();
        fb.QrPayload.ShouldBeNull();
        fb.FiscalCode.ShouldBeNull();
        fb.IssuerRfc.ShouldBeNull();
        fb.ReceiverRfc.ShouldBeNull();
        fb.Locator.ShouldNotBeNull();
        fb.Locator.PageNumber.ShouldBe(1);
    }
}
