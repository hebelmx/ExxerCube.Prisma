using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// RC1.S4.b chunk B3 — LAW-SEC section-anchor real-corpus recalibration.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ground truth (RC1.S1–S3, real Banamex/Citibanamex Visa + Mastercard corpus, 2026-07-22):</b>
/// every one of the 23 detectable CONDUSEF §-anchors is genuinely image/graphic-rendered on the
/// real statement family — zero bold/short standalone heading bands match any anchor across 8
/// real credit-card statements (both the B/Visa and C/Mastercard series). The pre-existing
/// band-<c>Contains</c> matcher was nonetheless reporting 5 sections falsely "Present" via three
/// non-heading shapes: cross-reference sentences (§26/§27 — CONDUSEF-mandated footer boilerplate
/// quoting another section's title), a data-table row whose label happens to contain another
/// anchor as a trailing fragment (§16, from the §19 "SALDO SOBRE…" table), and glossary/legend
/// prose that starts with (or embeds) an anchor but runs on as a defining sentence rather than a
/// title (§9, §22). <see cref="PdfPigStatementFieldExtractor.IsHeadingLikeMatch"/> replaces the
/// bare substring match with a heading-shaped check; these tests pin that behavior using the
/// EXACT band text observed on the real corpus (verbatim CONDUSEF-mandated boilerplate — not
/// account-specific, so it carries no PII) plus the existing demo fixtures (already committed,
/// no PII concern).
/// </para>
/// <para>
/// Full evidence, per-statement before/after, and the anchor inventory:
/// <c>docs/qa/calibration/real-corpus-triage-2026-07.md</c> and the RC1 epic tracker.
/// </para>
/// </remarks>
public sealed class LawSecAnchorCalibrationTests
{
    private static readonly string FixturesDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static string FixturePath(string fileName) =>
        Path.Combine(FixturesDir, fileName);

    private static PdfPigStatementFieldExtractor CreateExtractor() =>
        new(XUnitLogger.CreateLogger<PdfPigStatementFieldExtractor>(),
            Microsoft.Extensions.Options.Options.Create(new PdfExtractionOptions()),
            new NullPasswordProvider());

    // -----------------------------------------------------------------------
    // Cross-reference sentence rejected as an anchor.
    // -----------------------------------------------------------------------

    /// <summary>
    /// The real corpus's §26 footer boilerplate — reproduced verbatim byte-for-byte across all 8
    /// real B/C statements' footers AND the demo fixture (it is CONDUSEF-mandated template text,
    /// not account-specific — carries no PII). Before this story, this single band was enough to
    /// mark §26 falsely Present on every statement.
    /// </summary>
    private const string RealCrossReferenceBandNotas =
        "VER NOTAS EN LA SECCION “NOTAS ACLARATORIAS” EN ESTE ESTADO DE CUENTA.";

    /// <summary>
    /// The real corpus's §27 cross-reference footnote — reproduced verbatim across all 8 real
    /// B/C statements (CONDUSEF template text, no PII).
    /// </summary>
    private const string RealCrossReferenceBandGlosario =
        "CONSULTA LA SECCION “GLOSARIO DE TERMINOS Y ABREVIATURAS” PARA CONOCER COMO INTERPRETAR ESTE INDICADOR.";

    [Theory]
    [InlineData(RealCrossReferenceBandNotas)]
    [InlineData(RealCrossReferenceBandGlosario)]
    public void IsCrossReferenceSentence_RealCorpusFooterBoilerplate_ReturnsTrue(string bandText)
    {
        PdfPigStatementFieldExtractor.IsCrossReferenceSentence(bandText).ShouldBeTrue(
            $"'{bandText}' is a CONDUSEF cross-reference sentence (a lead-in verb + 'sección') " +
            "and must be recognized as such — this is the literal text that false-hit §26/§27 " +
            "on every real corpus statement before the RC1.S4.b/B3 fix.");
    }

    [Fact]
    public void IsHeadingLikeMatch_NotasAclaratoriasCrossReference_RejectedForSection26Anchor()
    {
        // §26 anchor per s_sectionAnchors: "NOTAS ACLARATORIAS".
        PdfPigStatementFieldExtractor.IsHeadingLikeMatch(
                RealCrossReferenceBandNotas, "NOTAS ACLARATORIAS")
            .ShouldBeFalse(
                "The §26 footer cross-reference sentence must NOT count as the §26 heading — " +
                "the anchor is embedded mid-sentence ('Ver notas en la sección \"...\"'), not at " +
                "the start of a genuine heading band.");
    }

    [Fact]
    public void IsHeadingLikeMatch_GlosarioCrossReference_RejectedForSection27Anchor()
    {
        // §27 anchor per s_sectionAnchors: "GLOSARIO DE TERMINOS".
        PdfPigStatementFieldExtractor.IsHeadingLikeMatch(
                RealCrossReferenceBandGlosario, "GLOSARIO DE TERMINOS")
            .ShouldBeFalse(
                "The §27 cross-reference footnote ('Consulta la sección \"...\"') must NOT count " +
                "as the §27 heading.");
    }

    // -----------------------------------------------------------------------
    // Other real-corpus false-positive shapes (data-table row; legend/definition prose).
    // -----------------------------------------------------------------------

    [Fact]
    public void IsHeadingLikeMatch_Section19TableRow_RejectedForSection16Anchor()
    {
        // Real §19 "SALDO SOBRE..." table row label — contains the §16 anchor
        // ("OTRAS LINEAS DE CREDITO") as a trailing fragment of an unrelated table cell, not a
        // §16 heading. Verbatim CONDUSEF row-label boilerplate (also present in the extractor's
        // own s_sec19RowLabels table) — not account-specific, no PII.
        const string tableRowBand = "POR DISPOSICIONES DE EFECTIVO DE OTRAS LINEAS DE CREDITO";

        PdfPigStatementFieldExtractor.IsHeadingLikeMatch(tableRowBand, "OTRAS LINEAS DE CREDITO")
            .ShouldBeFalse(
                "A §19 table row that merely ends with the §16 anchor phrase must not count as " +
                "the §16 'Información de otras líneas de crédito' heading — the anchor is a " +
                "trailing fragment of unrelated table-cell text, not a title.");
    }

    [Fact]
    public void IsHeadingLikeMatch_GlossaryDefinitionProse_RejectedForSection9Anchor()
    {
        // The real corpus's §27 Glosario CAT definition, 2-column-merged with the neighbouring
        // TASA definition (same Y-band) — starts with the §9 anchor ("COSTO ANUAL TOTAL") but
        // runs on as a defining sentence, not a heading. Verbatim CONDUSEF glossary boilerplate.
        const string glossaryBand =
            "COSTO ANUAL TOTAL DE FINANCIAMIENTO EXPRESADO EN TERMINOS PORCENTUALES ANUALES " +
            "TASA DE INTERES ANUAL QUE SE APLICA A LOS SALDOS NO PAGADOS DE CADA PERIODO";

        PdfPigStatementFieldExtractor.IsHeadingLikeMatch(glossaryBand, "COSTO ANUAL TOTAL")
            .ShouldBeFalse(
                "A glossary definition sentence that merely starts with the §9 anchor phrase " +
                "must not count as the §9 'CAT' heading — the long prose trailing the anchor " +
                "contains no other section anchor, so it cannot be a multi-column compound " +
                "heading band either.");
    }

    // -----------------------------------------------------------------------
    // Real-family heading shape recognized: multi-column compound heading band.
    // -----------------------------------------------------------------------

    /// <summary>
    /// On the real B/C corpus, NONE of the 23 detectable CONDUSEF anchors have literal heading
    /// text in the PDF text layer at all (ground truth, RC1.S1–S3) — there is no "real-family
    /// heading variant" to recognize on that family. The structural mechanism this story adds
    /// (<see cref="PdfPigStatementFieldExtractor.IsHeadingLikeMatch"/>) is nonetheless a general,
    /// layout-family-agnostic heading recognizer, and its hardest case — two section headings
    /// sharing one Y-band in a multi-column layout — IS present and real on the Dummie VEC demo
    /// fixture family (§7 "RESUMEN DE CARGOS Y ABONOS DEL PERIODO" and §8 "INDICADORES DEL COSTO
    /// ANUAL DE LA TARJETA" print side-by-side on one line). This test pins that a genuine
    /// compound heading band is still recognized for BOTH anchors after the recalibration
    /// (verified against the actual band text extracted from the committed jul_ago fixture).
    /// </summary>
    [Fact]
    public void IsHeadingLikeMatch_MultiColumnCompoundHeadingBand_RecognizedForBothAnchors()
    {
        // Actual band text from Prisma/Fixtures/PRP2/01+Dummie+VEC+jul_ago+20252.pdf (§8's
        // column is left, §7's column is right — both share one Y-band).
        const string compoundBand =
            "INDICADORES DEL COSTO ANUAL DE LA TARJETA RESUMEN DE CARGOS Y ABONOS DEL PERIODO";

        PdfPigStatementFieldExtractor.IsHeadingLikeMatch(
                compoundBand, "INDICADORES DEL COSTO ANUAL")
            .ShouldBeTrue("§8's anchor starts the compound band; the trailing text is another " +
                          "section's (§7's) complete anchor plus a short decoration — still " +
                          "heading-shaped.");

        PdfPigStatementFieldExtractor.IsHeadingLikeMatch(
                compoundBand, "RESUMEN DE CARGOS Y ABONOS")
            .ShouldBeTrue("§7's anchor is preceded by another section's (§8's) complete anchor " +
                          "plus a short decoration, not unrelated prose — still heading-shaped.");
    }

    // -----------------------------------------------------------------------
    // Image-rendered section honestly missing (extractor-level, real demo fixture).
    // -----------------------------------------------------------------------

    /// <summary>
    /// Regression guard for the exact defect this story fixes: before RC1.S4.b/B3, §27
    /// "Glosario de términos" was reported falsely Present on the jul_ago demo fixture solely
    /// because of the cross-reference footnote (§26's own footnote #10 mentions "la sección
    /// 'GLOSARIO DE TÉRMINOS Y ABREVIATURAS'"). §27 has no genuine heading band anywhere in this
    /// fixture (confirmed by direct text-layer inspection) — it must now be honestly Absent, not
    /// silently forced to a fabricated Present.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_JulAgo_Section27Glosario_HonestlyAbsentNotFalselyPresent()
    {
        var ct = TestContext.Current.CancellationToken;
        var path = FixturePath("01+Dummie+VEC+jul_ago+20252.pdf");
        File.Exists(path).ShouldBeTrue($"Fixture not found: {path}");

        var pdfBytes = await File.ReadAllBytesAsync(path, ct);
        var result = await CreateExtractor().ExtractFullAsync(pdfBytes, ct);
        result.IsSuccess.ShouldBeTrue($"Extraction failed: {result.Error}");

        var sec27 = result.Value!.Sections.First(s => s.SectionNumber == 27);

        sec27.IsPresent.ShouldBeFalse(
            "§27 Glosario de términos has no genuine heading band in this fixture — it must be " +
            "Absent, not falsely Present via the §26 footnote's cross-reference to 'la sección " +
            "\"GLOSARIO DE TÉRMINOS Y ABREVIATURAS\"'.");
        sec27.DetectionStatus.ShouldBe(SectionDetectionStatus.Absent);
        sec27.IsApplicable.ShouldBeTrue(
            "§27 is unconditional — honestly Absent means IsApplicable stays true so " +
            "LAW-SEC-PRESENCE counts it as genuinely missing (never silently skipped).");
    }

    /// <summary>
    /// Same regression, for §16 "Información de otras líneas de crédito" — before this story it
    /// was falsely Present via the §19 table row "Por disposiciones de efectivo de OTRAS LÍNEAS
    /// DE CRÉDITO". §16 is conditional, so an honest Absent must leave IsApplicable=false (never
    /// counted as mandatory-missing) — this also proves the fix didn't collapse the
    /// conditional-section carve-out.
    /// </summary>
    [Fact]
    public async Task ExtractFullAsync_JulAgo_Section16OtrasLineas_HonestlyAbsentNotFalselyPresent()
    {
        var ct = TestContext.Current.CancellationToken;
        var path = FixturePath("01+Dummie+VEC+jul_ago+20252.pdf");
        File.Exists(path).ShouldBeTrue($"Fixture not found: {path}");

        var pdfBytes = await File.ReadAllBytesAsync(path, ct);
        var result = await CreateExtractor().ExtractFullAsync(pdfBytes, ct);
        result.IsSuccess.ShouldBeTrue($"Extraction failed: {result.Error}");

        var sec16 = result.Value!.Sections.First(s => s.SectionNumber == 16);

        sec16.IsPresent.ShouldBeFalse(
            "§16 has no genuine heading band in this fixture — it must be Absent, not falsely " +
            "Present via the §19 table row that merely ends with the §16 anchor phrase.");
        sec16.IsApplicable.ShouldBeFalse(
            "§16 is conditional (trigger: other credit lines) and genuinely absent here — " +
            "IsApplicable must stay false so it is never counted mandatory-missing.");
    }
}
