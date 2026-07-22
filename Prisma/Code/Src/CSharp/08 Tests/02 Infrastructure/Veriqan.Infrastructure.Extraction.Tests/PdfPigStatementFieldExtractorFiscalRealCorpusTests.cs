using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// RC1.S4.b (2026-07-22) — fiscal-block real-corpus recalibration tests for CL-50..53.
/// </summary>
/// <remarks>
/// <para>
/// Ground truth for these synthetic fixtures comes from direct inspection of the real
/// (anonymized) Banamex/Citibanamex corpus staged at <c>VERIQAN_REAL_CORPUS_ROOT</c> — see
/// <c>docs/qa/calibration/real-corpus-triage-2026-07.md</c> and the RC1 epic doc for the
/// per-check evidence. No PII from that corpus is reproduced here: all values below are
/// synthetic placeholders shaped like the real layout (same label wording, same two-line UUID
/// wrap, same decoy label), not real extracted tokens.
/// </para>
/// <para>
/// Each test builds a minimal in-memory PDF via <see cref="PdfDocumentBuilder"/> and drives the
/// real <see cref="PdfPigStatementFieldExtractor.ExtractFullAsync"/> pipeline — no mocking of the
/// extraction internals.
/// </para>
/// </remarks>
public sealed class PdfPigStatementFieldExtractorFiscalRealCorpusTests
{
    private const float FontSize = 10f;

    private static PdfPigStatementFieldExtractor CreateExtractor() =>
        new(XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>(),
            Microsoft.Extensions.Options.Options.Create(new PdfExtractionOptions()),
            new NullPasswordProvider());

    // -----------------------------------------------------------------------
    // Test 1: real-family SAT legend recognized by the fast-path
    // -----------------------------------------------------------------------

    /// <summary>
    /// The real-corpus legend "ESTE DOCUMENTO ES UNA REPRESENTACIÓN IMPRESA DE UN CFDI" — the
    /// SAME SAT-standard legend RC1.S4.c aligned <c>mandatory-legends.csv</c> to — must trip the
    /// fiscal-block fast-path, additively alongside the retired Epic-3 synthetic legend (which
    /// stays covered by <see cref="PdfPigStatementFieldExtractorFiscalTests"/>).
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_SatLegendPresent_BlockPresentIsTrue()
    {
        var ct = TestContext.Current.CancellationToken;

        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        // The Standard-14 Helvetica font PdfPig's builder embeds cannot render 'Ó' (accented) —
        // write the pre-normalized form directly (NormalizeText upper-cases + strips diacritics
        // before matching, so this exercises the same fast-path comparison the real accented
        // legend would after normalization; see the sibling QR round-trip test in
        // PdfPigStatementFieldExtractorFiscalTests for the same convention).
        page.AddText(
            "ESTE DOCUMENTO ES UNA REPRESENTACION IMPRESA DE UN CFDI",
            FontSize, new PdfPoint(26, 780), font);

        var pdfBytes = builder.Build();

        var extractor = CreateExtractor();
        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var fb = result.Value!.FiscalBlock;
        fb.ShouldNotBeNull();
        fb.BlockPresent.ShouldBeTrue(
            "the real-corpus SAT-standard legend must be recognized by the fast-path additively, "
            + "not just the retired synthetic legend");
    }

    // -----------------------------------------------------------------------
    // Test 2: two-line wrapped UUID reassembled
    // -----------------------------------------------------------------------

    /// <summary>
    /// Real-corpus statements wrap the "UUID" (folio fiscal) value mid hex-group across the
    /// label's row and the row directly beneath it (observed line pitch ≈10pt, same X column).
    /// The extractor must rejoin the two fragments into one valid UUID rather than reporting the
    /// truncated first fragment or missing the field entirely.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_TwoLineWrappedUuid_ReassemblesFiscalCode()
    {
        var ct = TestContext.Current.CancellationToken;

        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        page.AddText(
            "ESTE DOCUMENTO ES UNA REPRESENTACION IMPRESA DE UN CFDI",
            FontSize, new PdfPoint(26, 780), font);

        // Label + first fragment on one row; continuation directly beneath, same X column,
        // 10pt lower — mirrors the real-corpus wrap shape exactly.
        page.AddText("UUID", FontSize, new PdfPoint(300, 700), font);
        page.AddText("AAAABBBB-CCCC-DDDD-EEEE-FFFF", FontSize, new PdfPoint(350, 700), font);
        page.AddText("00001111", FontSize, new PdfPoint(350, 690), font);

        var pdfBytes = builder.Build();

        var extractor = CreateExtractor();
        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var fb = result.Value!.FiscalBlock;
        fb.ShouldNotBeNull();
        fb.BlockPresent.ShouldBeTrue();
        fb.FiscalCode.ShouldBe(
            "AAAABBBB-CCCC-DDDD-EEEE-FFFF00001111",
            "the two wrapped fragments must be rejoined into one valid UUID");
    }

    // -----------------------------------------------------------------------
    // Test 3: issuer RFC extracted by label anchor (decoy present, must be ignored)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Real statements print a THIRD RFC-shaped token labeled "RFC Proveedor Certificado" (the
    /// SAT-authorized certification provider — not the bank) between the Receptor and Emisor
    /// rows. The extractor must anchor strictly to the "RFC del Emisor" label and ignore that
    /// decoy, regardless of which one appears first in reading order.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_IssuerRfcLabelAnchored_IgnoresPacDecoyRfc()
    {
        var ct = TestContext.Current.CancellationToken;

        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        page.AddText(
            "ESTE DOCUMENTO ES UNA REPRESENTACION IMPRESA DE UN CFDI",
            FontSize, new PdfPoint(26, 780), font);

        // Decoy — appears FIRST in reading order (higher Y = earlier/top in PdfPig's bottom-up
        // page coordinates read top-to-bottom by the band scan... placed above the real label).
        page.AddText("RFC Proveedor Certificado", FontSize, new PdfPoint(300, 650), font);
        page.AddText("PAC010101ZZZ", FontSize, new PdfPoint(450, 650), font);

        // The real issuer RFC — label-anchored, same band as its value.
        page.AddText("RFC del Emisor", FontSize, new PdfPoint(26, 600), font);
        page.AddText("BDI000101IDF", FontSize, new PdfPoint(155, 600), font);

        var pdfBytes = builder.Build();

        var extractor = CreateExtractor();
        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var fb = result.Value!.FiscalBlock;
        fb.ShouldNotBeNull();
        fb.BlockPresent.ShouldBeTrue();
        fb.IssuerRfc.ShouldBe(
            "BDI000101IDF",
            "IssuerRfc must come from the 'RFC del Emisor' label, never the "
            + "'RFC Proveedor Certificado' decoy");
    }

    // -----------------------------------------------------------------------
    // Test 4: unanchored RFC never positionally guessed as receiver
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the "RFC del Receptor" label's value cell is empty — the honest, observed real-corpus
    /// case (the customer's RFC is image-rendered elsewhere, never printed in this page's text
    /// layer) — the extractor must NOT fall back to grabbing some other RFC-shaped token
    /// elsewhere on the page. <see cref="Domain.Extraction.FiscalBlock.ReceiverRfc"/> must stay
    /// <see langword="null"/> (NotExtracted) rather than a wrong guess.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_ReceptorLabelValueEmpty_ReceiverRfcStaysNotExtracted()
    {
        var ct = TestContext.Current.CancellationToken;

        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        page.AddText(
            "ESTE DOCUMENTO ES UNA REPRESENTACION IMPRESA DE UN CFDI",
            FontSize, new PdfPoint(26, 780), font);

        // Label present, value cell intentionally left empty (nothing to its right on this band).
        page.AddText("RFC del Receptor", FontSize, new PdfPoint(300, 700), font);

        // A stray RFC-shaped token elsewhere on the page, unconnected to any label — must NOT be
        // positionally picked up as the receiver's RFC.
        page.AddText("ZZZ999999ZZZ", FontSize, new PdfPoint(300, 300), font);

        var pdfBytes = builder.Build();

        var extractor = CreateExtractor();
        var result = await extractor.ExtractFullAsync(pdfBytes, ct);

        result.IsSuccess.ShouldBeTrue($"ExtractFullAsync failed: {result.Error}");
        var fb = result.Value!.FiscalBlock;
        fb.ShouldNotBeNull();
        fb.BlockPresent.ShouldBeTrue();
        fb.ReceiverRfc.ShouldBeNull(
            "an unanchored RFC-shaped token elsewhere on the page must never be positionally "
            + "guessed as the receiver RFC");
    }
}
