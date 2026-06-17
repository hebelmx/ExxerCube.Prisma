using System.IO.Compression;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
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
    // Test 1b: Real-PDF QR chain — PDFtoImage render → ZXing decode on a
    //           PdfPig-built PDF that contains an embedded QR image.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds an in-memory PDF using <see cref="PdfDocumentBuilder"/> that contains:
    /// <list type="bullet">
    ///   <item>The CFDI legend text (so the extractor's fast-path fast-passes the
    ///         <c>normalizedFullText.Contains(FiscalLegendNormalized)</c> guard).</item>
    ///   <item>A scannable QR code image (PNG) embedded via <c>page.AddPng</c>.</item>
    /// </list>
    /// Then runs the real extractor pipeline (<see cref="PdfPigStatementFieldExtractor.ExtractFullAsync"/>)
    /// over those bytes and asserts that <see cref="FiscalBlock.QrDecoded"/> is <see langword="true"/>
    /// and <see cref="FiscalBlock.QrPayload"/> matches the original payload.
    /// This exercises the full PDFtoImage render → ZXing decode chain on an actual PDF,
    /// not just an in-memory bitmap.
    /// </summary>
    [Fact]
#pragma warning disable CA1416
    public async Task ExtractFullAsync_PdfWithEmbeddedQr_QrDecodedIsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        const string fiscalPayload =
            "https://verificacfdi.facturaelectronica.sat.gob.mx/default.aspx" +
            "?id=CAFEBABE-DEAD-BEEF-0000-123456789ABC" +
            "&re=CFDI010101AAA&rr=RCPT020202BBB&tt=500.00&fe=ZZZZ99999==";

        // ---- Step 1: encode QR as BGRA pixel data via ZXing ------------------
        // Use a large size (400×400) so rendering the PDF at 150 DPI still gives
        // ZXing enough pixels to decode reliably.
        var qrWriter = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new ZXing.Common.EncodingOptions
            {
                Width = 400,
                Height = 400,
                Margin = 4,
            },
        };
        var qrPixels = qrWriter.Write(fiscalPayload);
        qrPixels.ShouldNotBeNull("ZXing must produce pixel data");

        // ---- Step 2: encode BGRA → grayscale PNG bytes -----------------------
        // PdfPig page.AddPng() requires a valid PNG byte stream.
        // We convert BGRA → grayscale (luminance) and write a minimal PNG.
        var grayBytes = ConvertBgraToGrayscale(qrPixels.Pixels, qrPixels.Width, qrPixels.Height);
        var pngBytes = EncodeGrayscalePng(grayBytes, qrPixels.Width, qrPixels.Height);

        // ---- Step 3: build a minimal PDF with the CFDI legend text and the QR image ------
        // The extractor scans normalizedFullText for "REPRESENTACION IMPRESA SIN VALIDEZ FISCAL"
        // (accent-stripped form) as its fast-path guard. We embed that exact text so the
        // guard passes and ExtractFiscalBlock proceeds to render + scan.
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842); // A4 PDF points
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        // The text must survive NormalizeText (uppercase/accent-strip); write the normalized form directly.
        page.AddText("REPRESENTACION IMPRESA SIN VALIDEZ FISCAL", 10, new PdfPoint(50, 780), font);

        // Embed the QR image in the lower half of the page.
        // PdfPoint origin is bottom-left in PdfPig; place the image in a 300×300 region.
        var imageRect = new PdfRectangle(50, 100, 350, 400);
        page.AddPng(pngBytes, imageRect);

        var pdfDocBytes = builder.Build();
        pdfDocBytes.ShouldNotBeEmpty("PdfDocumentBuilder must produce non-empty bytes");

        // ---- Step 4: run the real extractor over the in-memory PDF --------
        var extractor = CreateExtractor();
        var result = await extractor.ExtractFullAsync(pdfDocBytes, ct);

        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var model = result.Value!;

        var fb = model.FiscalBlock;
        fb.ShouldNotBeNull("FiscalBlock must always be populated by ExtractFullAsync");

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Real-PDF QR test results:\n" +
            $"  BlockPresent : {fb.BlockPresent}\n" +
            $"  QrDecoded    : {fb.QrDecoded}\n" +
            $"  QrPayload    : {fb.QrPayload ?? "(null)"}\n" +
            $"  Locator.Page : {fb.Locator.PageNumber}");

        // ---- Step 5: assert the full render→decode chain succeeded ----------
        fb.BlockPresent.ShouldBeTrue(
            "The CFDI legend text must be found — BlockPresent must be true.");
        fb.QrDecoded.ShouldBeTrue(
            "The QR code embedded in the PDF page must be decodable via PDFtoImage + ZXing.");
        fb.QrPayload.ShouldNotBeNullOrWhiteSpace(
            "A decoded QR must have a non-empty payload.");
        fb.QrPayload.ShouldBe(fiscalPayload,
            "The decoded payload must exactly match the one encoded into the QR image.");
        fb.Locator.PageNumber.ShouldBe(1,
            "The fiscal block locator must point to page 1 (the only page in this test PDF).");
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
        // When BlockPresent==false the locator is FieldLocator.NoPage() (PageNumber==0, the
        // "no-page" sentinel).  When BlockPresent==true the locator is a real page hint (>=1).
        if (fb.BlockPresent)
            fb.Locator.PageNumber.ShouldBeGreaterThan(0, "a found fiscal block must have a valid 1-based page number");
        else
            fb.Locator.PageNumber.ShouldBe(0, "absent fiscal block must use the NoPage sentinel (page 0)");

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
        // PageNumber == 0 is the "no page" sentinel — distinguishes a genuine absent-block
        // locator from a real PageHint(1) that would misleadingly cite "page 1" in findings.
        fb.Locator.PageNumber.ShouldBe(0);
        fb.Locator.HasBoundingBox.ShouldBeFalse();
    }

    // -----------------------------------------------------------------------
    // Helper: BGRA → grayscale luminance conversion
    // -----------------------------------------------------------------------

    /// <summary>
    /// Converts a BGRA byte array (4 bytes per pixel) to a grayscale byte array
    /// (1 byte per pixel) using the standard luminance formula.
    /// </summary>
    private static byte[] ConvertBgraToGrayscale(byte[] bgra, int width, int height)
    {
        var gray = new byte[width * height];
        for (var i = 0; i < width * height; i++)
        {
            var b = bgra[i * 4];
            var g = bgra[i * 4 + 1];
            var r = bgra[i * 4 + 2];
            // Standard luminance: 0.299 R + 0.587 G + 0.114 B (integer arithmetic).
            gray[i] = (byte)((r * 299 + g * 587 + b * 114 + 500) / 1000);
        }
        return gray;
    }

    // -----------------------------------------------------------------------
    // Helper: minimal PNG encoder (grayscale, no dependencies beyond BCL)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Encodes a grayscale byte array (1 byte per pixel, row-major) into a valid PNG file.
    /// Uses only BCL types: <see cref="ZLibStream"/> for IDAT compression and a
    /// hand-rolled CRC32 for chunk integrity.
    /// </summary>
    private static byte[] EncodeGrayscalePng(byte[] gray, int width, int height)
    {
        using var output = new MemoryStream();
        using var bw = new BinaryWriter(output);

        // PNG signature
        bw.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        // IHDR chunk
        WriteChunk(bw, "IHDR", w =>
        {
            WriteUInt32BigEndian(w, (uint)width);
            WriteUInt32BigEndian(w, (uint)height);
            w.Write((byte)8);   // bit depth
            w.Write((byte)0);   // color type 0 = grayscale
            w.Write((byte)0);   // compression method
            w.Write((byte)0);   // filter method
            w.Write((byte)0);   // interlace method = none
        });

        // IDAT chunk: zlib-wrapped deflate of filter-type-0 scanlines
        var filteredScanlines = new byte[height * (width + 1)];
        for (var row = 0; row < height; row++)
        {
            filteredScanlines[row * (width + 1)] = 0; // filter type: None
            Array.Copy(gray, row * width, filteredScanlines, row * (width + 1) + 1, width);
        }

        WriteChunk(bw, "IDAT", w =>
        {
            using var zlibMs = new MemoryStream();
            using (var zlib = new ZLibStream(zlibMs, CompressionLevel.Fastest, leaveOpen: true))
                zlib.Write(filteredScanlines, 0, filteredScanlines.Length);
            w.Write(zlibMs.ToArray());
        });

        // IEND chunk
        WriteChunk(bw, "IEND", _ => { });

        return output.ToArray();
    }

    private static void WriteChunk(BinaryWriter outer, string type, Action<BinaryWriter> writeData)
    {
        using var chunkData = new MemoryStream();
        using var inner = new BinaryWriter(chunkData, System.Text.Encoding.ASCII, leaveOpen: true);
        writeData(inner);
        inner.Flush();
        var dataBytes = chunkData.ToArray();

        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);

        WriteUInt32BigEndian(outer, (uint)dataBytes.Length);
        outer.Write(typeBytes);
        outer.Write(dataBytes);

        // CRC32 over type + data
        var crcInput = new byte[typeBytes.Length + dataBytes.Length];
        typeBytes.CopyTo(crcInput, 0);
        dataBytes.CopyTo(crcInput, typeBytes.Length);
        WriteUInt32BigEndian(outer, ComputeCrc32(crcInput));
    }

    private static void WriteUInt32BigEndian(BinaryWriter bw, uint value)
    {
        bw.Write((byte)(value >> 24));
        bw.Write((byte)(value >> 16));
        bw.Write((byte)(value >> 8));
        bw.Write((byte)(value));
    }

    /// <summary>
    /// Standard IEEE 802.3 CRC32 used by the PNG specification.
    /// </summary>
    private static uint ComputeCrc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return crc ^ 0xFFFFFFFF;
    }
}
