using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;
using PDFtoImage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using ZXing;
using ZXing.Rendering;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Tests.Smoke;

/// <summary>
/// Pure-C# stack smoke tests (Veriqan Story 1.4).
/// Validates ZXing.Net (QR encode/decode), CoenM.ImageSharp.ImageHash (perceptual hash),
/// PdfPig (PDF write/read), and PDFtoImage (PDF rasterisation) — no Python, no GPU.
/// </summary>
public sealed class PureCSharpStackSmokeTests
{
    // -----------------------------------------------------------------------
    // Test 1: ZXing.Net QR-code round-trip
    // -----------------------------------------------------------------------

    /// <summary>
    /// Encodes a known string as a QR-code pixel bitmap then decodes it back;
    /// asserts the decoded value equals the original.
    /// </summary>
    [Fact]
    public void QrCode_RoundTrip_DecodesCorrectly()
    {
        const string payload = "ExxerCube-Veriqan-Story-1.4";

        // --- Encode --------------------------------------------------------
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new ZXing.Common.EncodingOptions
            {
                Width = 256,
                Height = 256,
                Margin = 1
            }
        };

        PixelData pixelData = writer.Write(payload);

        pixelData.ShouldNotBeNull();
        pixelData.Width.ShouldBe(256);
        pixelData.Height.ShouldBe(256);
        pixelData.Pixels.ShouldNotBeNull();
        pixelData.Pixels.Length.ShouldBeGreaterThan(0);

        // --- Decode --------------------------------------------------------
        // BarcodeWriterPixelData returns BGRA32 bytes; RGBLuminanceSource accepts BGR24.
        // Convert each 4-byte BGRA pixel to 3-byte BGR for the reader.
        var bgrBytes = ConvertBgraToRgb(pixelData.Pixels, pixelData.Width, pixelData.Height);

        var luminanceSource = new RGBLuminanceSource(
            bgrBytes,
            pixelData.Width,
            pixelData.Height,
            RGBLuminanceSource.BitmapFormat.BGR24);

        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new ZXing.Common.DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = [BarcodeFormat.QR_CODE]
            }
        };

        ZXing.Result decoded = reader.Decode(luminanceSource);

        decoded.ShouldNotBeNull("ZXing failed to decode the QR-code it just encoded.");
        decoded.Text.ShouldBe(payload);
    }

    // -----------------------------------------------------------------------
    // Test 2: CoenM.ImageSharp.ImageHash — deterministic, non-zero, sensitive
    // -----------------------------------------------------------------------

    /// <summary>
    /// Computes a perceptual hash of an in-memory image.
    /// Asserts: (a) hash is non-zero, (b) same image produces same hash,
    /// (c) visually different image produces a different hash.
    /// </summary>
    [Fact]
    public void PerceptualHash_SameImage_ProducesDeterministicNonZeroHash()
    {
        // Build a simple 64×64 white image with a black rectangle in it.
        using var image1 = BuildTestImage(64, 64, fillBlackRect: true);
        using var image1Copy = BuildTestImage(64, 64, fillBlackRect: true);
        // Blank all-white image for the "different" control.
        using var image2 = BuildTestImage(64, 64, fillBlackRect: false);

        var hashAlgorithm = new PerceptualHash();

        ulong hash1 = hashAlgorithm.Hash(image1);
        ulong hash1Again = hashAlgorithm.Hash(image1Copy);
        ulong hash2 = hashAlgorithm.Hash(image2);

        hash1.ShouldNotBe(0UL, "Hash of a non-trivial image must be non-zero.");
        hash1.ShouldBe(hash1Again, "Same pixel content must produce the same hash.");
        hash1.ShouldNotBe(hash2, "Visually different images must not produce the same hash.");
    }

    // -----------------------------------------------------------------------
    // Test 3: PdfPig — build a searchable PDF and read font name from it
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a single-page searchable PDF in memory using PdfPig, then reads it back
    /// and asserts that at least one font name is present on the first page.
    /// Fixture: generated in-memory (Standard14Font.Helvetica).
    /// Font name reported: "Helvetica" (from the Standard 14 subset).
    /// </summary>
    [Fact]
    public void PdfPig_OpenBuiltPdf_ReadsFontName()
    {
        const string embeddedText = "Veriqan smoke test — PdfPig round-trip";

        // Build
        var pdfBytes = BuildSearchablePdf(embeddedText);
        pdfBytes.ShouldNotBeNull();
        pdfBytes.Length.ShouldBeGreaterThan(0);

        // Read
        using var doc = PdfDocument.Open(pdfBytes);
        doc.NumberOfPages.ShouldBeGreaterThan(0);

        var page = doc.GetPage(1);
        page.ShouldNotBeNull();

        // Letters carry per-glyph font info in PdfPig 0.1.x.
        var fontNames = page.Letters
            .Select(l => l.FontName ?? string.Empty)
            .Where(name => name.Length > 0)
            .Distinct()
            .ToList();

        fontNames.ShouldNotBeEmpty("Expected at least one font name on the built page.");
        fontNames.ShouldContain(
            n => n.Contains("Helvetica", StringComparison.OrdinalIgnoreCase),
            "Expected the Helvetica Standard14 font name on the page.");
    }

    // -----------------------------------------------------------------------
    // Test 4: PDFtoImage — render first page, assert non-empty bitmap
    // -----------------------------------------------------------------------

    /// <summary>
    /// Renders the first page of a PdfPig-built PDF via PDFtoImage and asserts that
    /// the resulting SKBitmap has positive width and height and a non-empty encoded buffer.
    /// </summary>
    [Fact]
#pragma warning disable CA1416 // PDFtoImage is cross-platform (Windows, Linux, macOS)
    public void PDFtoImage_RenderFirstPage_ProducesNonEmptyBitmap()
    {
        const string text = "PDFtoImage rasterisation smoke test";
        var pdfBytes = BuildSearchablePdf(text);

        using var pdfStream = new MemoryStream(pdfBytes);
        using var bitmap = Conversion.ToImage(pdfStream, leaveOpen: false, page: 0, options: new RenderOptions(Dpi: 72));

        bitmap.ShouldNotBeNull("Conversion.ToImage must return a non-null SKBitmap.");
        bitmap.Width.ShouldBeGreaterThan(0);
        bitmap.Height.ShouldBeGreaterThan(0);

        // Encode to PNG bytes and assert the buffer is non-trivially sized.
        using var encoded = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
        using var ms = new MemoryStream();
        encoded.SaveTo(ms);

        ms.Length.ShouldBeGreaterThan(100L, "PNG of a rendered page must be non-trivially sized.");
    }
#pragma warning restore CA1416

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>Builds a single-page A4 PDF with the given text using PdfPig's PdfDocumentBuilder.</summary>
    private static byte[] BuildSearchablePdf(string text)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842); // A4 in PDF points
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText(text, 12, new PdfPoint(50, 750), font);
        return builder.Build();
    }

    /// <summary>
    /// Creates a 64×64 <see cref="Image{Rgba32}"/> optionally containing a 20×20 black rectangle.
    /// The caller is responsible for disposal.
    /// </summary>
    private static Image<Rgba32> BuildTestImage(int width, int height, bool fillBlackRect)
    {
        var image = new Image<Rgba32>(width, height, new Rgba32(255, 255, 255, 255));

        if (fillBlackRect)
        {
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 10; y < 30; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 10; x < 30; x++)
                    {
                        row[x] = new Rgba32(0, 0, 0, 255);
                    }
                }
            });
        }

        return image;
    }

    /// <summary>
    /// Converts a BGRA byte array (as returned by <see cref="BarcodeWriterPixelData"/>) into
    /// a BGR byte array suitable for <see cref="RGBLuminanceSource"/> with <c>BitmapFormat.BGR24</c>.
    /// </summary>
    private static byte[] ConvertBgraToRgb(byte[] bgra, int width, int height)
    {
        var bgr = new byte[width * height * 3];
        for (int i = 0; i < width * height; i++)
        {
            int srcBase = i * 4;
            int dstBase = i * 3;
            bgr[dstBase + 0] = bgra[srcBase + 0]; // B
            bgr[dstBase + 1] = bgra[srcBase + 1]; // G
            bgr[dstBase + 2] = bgra[srcBase + 2]; // R
        }

        return bgr;
    }
}
