using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Sources;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Classification;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Processing;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// Integration tests proving the document pipeline honours the owner requirement:
/// a SIARA "requerimiento de la autoridad" with any subset of case files
/// (even only a DOCX, or only an XML, or both without a PDF) is a VALID request
/// that must NEVER be invalidated. The system always answers best-effort.
///
/// Key real-corpus facts encoded here (and documented in the prompt that created
/// this file):
/// - The REAL SIARA XML carries
///   &lt;Cnbv_NumeroExpediente&gt;EXP-2598-2020&lt;/Cnbv_NumeroExpediente&gt; and
///   XmlFieldExtractor reads it directly. XML DOES contribute the expediente.
/// - The REAL SIARA DOCX is an SAT requerimiento letter whose only identifier
///   is like "AGAFADAFSON2/2023/031698". DocxFieldExtractor.ExtractExpediente
///   ONLY matches the regex [A-Z]/[A-Z]{1,2}\d+-\d+-\d+-[A-Z]+, which that
///   identifier does NOT match → the real DOCX contributes no expediente.
///   THIS IS EXPECTED — the DOCX is still a valid file and the case must proceed.
/// </summary>
public sealed class CaseCombinationBestEffortTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------------
    // Real-shaped XML fixture: uses the EXP-NNNN-YYYY format that SIARA produces
    // (mirrors the real Cnbv_NumeroExpediente in production XML files).
    // ---------------------------------------------------------------------------
    private const string RealShapedXmlContent = """
        <?xml version="1.0" encoding="utf-8"?>
        <Expediente xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                    xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                    xmlns="http://www.cnbv.gob.mx">
          <Cnbv_NumeroOficio>ABC/DEF/-1234567890/2020</Cnbv_NumeroOficio>
          <Cnbv_NumeroExpediente>EXP-2598-2020</Cnbv_NumeroExpediente>
          <Cnbv_SolicitudSiara>AGAFADAFSON2/2020/002598</Cnbv_SolicitudSiara>
          <Cnbv_Folio>2598</Cnbv_Folio>
          <Cnbv_OficioYear>2020</Cnbv_OficioYear>
          <Cnbv_AreaClave>2</Cnbv_AreaClave>
          <Cnbv_AreaDescripcion>REQUERIMIENTO</Cnbv_AreaDescripcion>
          <Cnbv_FechaPublicacion>2020-03-10</Cnbv_FechaPublicacion>
          <Cnbv_DiasPlazo>15</Cnbv_DiasPlazo>
          <AutoridadNombre>ADMINISTRACION GENERAL DE AUDITORIA FISCAL FEDERAL</AutoridadNombre>
          <NombreSolicitante xsi:nil="true" />
          <Referencia>AGAFADAFSON2/2023/031698</Referencia>
          <TieneAseguramiento>false</TieneAseguramiento>
        </Expediente>
        """;

    // The expediente in the XML — must be asserted verbatim (XmlFieldExtractor reads the
    // element content directly, no regex, no transformation beyond trimming).
    private const string XmlExpedienteNumber = "EXP-2598-2020";

    // ---------------------------------------------------------------------------
    // Helper: build a real-shaped SAT requerimiento DOCX whose only identifier
    // is "AGAFADAFSON2/2023/031698" — a SIARA solicitud reference that does NOT
    // match DocxFieldExtractor's expediente regex [A-Z]/[A-Z]{1,2}\d+-\d+-\d+-[A-Z]+.
    // This mirrors the real SIARA DOCX and intentionally yields no expediente.
    // ---------------------------------------------------------------------------
    private static byte[] BuildRealShapedRequerimientoDocxBytes()
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(
                new Body(
                    new Paragraph(new Run(new Text("SERVICIO DE ADMINISTRACION TRIBUTARIA"))),
                    new Paragraph(new Run(new Text("ADMINISTRACION GENERAL DE AUDITORIA FISCAL FEDERAL"))),
                    new Paragraph(new Run(new Text("Oficio: AGAFADAFSON2/2023/031698"))),
                    new Paragraph(new Run(new Text("Mexico, Ciudad de Mexico, a 10 de marzo de 2023."))),
                    new Paragraph(new Run(new Text("Asunto: Requerimiento de informacion financiera."))),
                    new Paragraph(new Run(new Text("Por medio del presente, se le requiere proporcionar informacion."))),
                    new Paragraph(new Run(new Text("ATENTAMENTE")))));
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    // ---------------------------------------------------------------------------
    // Shared orchestrator factory: same wiring used by MultiSourceFusionIntegrationTests.
    // ---------------------------------------------------------------------------
    private static ExtractionOrchestrator BuildOrchestrator() =>
        new(
            eventPublisher: Substitute.For<IEventPublisher>(),
            logger: NullLogger<ExtractionOrchestrator>.Instance,
            qualityAnalyzer: null,
            ocrExecutor: null,
            fusionService: new FusionExpedienteService(NullLogger<FusionExpedienteService>.Instance),
            fileLoader: null,
            txtFieldExtractor: null,
            xmlFieldExtractor: new XmlFieldExtractor(),
            docxFieldExtractor: new DocxFieldExtractor(NullLogger<DocxFieldExtractor>.Instance));

    // ===========================================================================
    // TEST 1 — DOCX-only, real-shaped (no expediente match expected from DOCX)
    //
    // Owner requirement: a case with only the DOCX companion present is a valid
    // request. The system must not throw and must not reject the case (QualityRejected
    // must be false). It answers best-effort.
    //
    // Real-corpus observation: the real DOCX carries no string matching
    // [A-Z]/[A-Z]{1,2}\d+-\d+-\d+-[A-Z]+ → DocxFieldExtractor extracts no
    // expediente → the Expediente object passed to FuseAsync has NumeroExpediente = "".
    // FusionExpedienteService receives a non-null docxExpediente (with all-empty fields)
    // so the "at-least-one-source" guard passes and FuseAsync SUCCEEDS — returning a
    // FusionResult with an empty NumeroExpediente (no conflict, one zero-weight source).
    //
    // FINDING (encoded as assertion below): FusionResult IS non-null; FusedExpediente
    // IS non-null; NumeroExpediente IS empty or whitespace (DOCX yielded nothing);
    // QualityRejected IS false. The case is never invalidated.
    // ===========================================================================

    /// <summary>
    /// A SIARA case whose only present file is the real-shaped SAT DOCX requerimiento
    /// (no XML, no PDF) must not crash and must not be invalidated.
    /// The system answers best-effort: FusionResult non-null, QualityRejected false.
    /// The DOCX contributes no expediente (real corpus expectation) — that is acceptable.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_DocxOnly_RealShapedRequerimiento_DoesNotInvalidate_DegradesAnswerable()
    {
        var orchestrator = BuildOrchestrator();

        var tempDocxPath = Path.GetTempFileName() + ".docx";
        await File.WriteAllBytesAsync(tempDocxPath, BuildRealShapedRequerimientoDocxBytes(), Ct);

        try
        {
            var downloadEvent = new DocumentDownloadedEvent
            {
                FileId = Guid.NewGuid(),
                CorrelationId = Guid.NewGuid(),
                FileName = tempDocxPath,
                Source = "SIARA",
                // Primary is Docx → primaryIsImageBased = false → Stages 1-2 are skipped.
                // Stage 3 goes straight to companion extraction.
                Format = FileFormat.Docx,
                IsComplete = false, // only one of the three expected files is present — still valid
                CaseFiles = new List<CaseFileReference>
                {
                    new() { RelativePath = tempDocxPath, Format = FileFormat.Docx },
                },
            };

            // Act — must NOT throw regardless of what the DOCX contains.
            var result = await orchestrator.ExtractAsync(downloadEvent, Ct);

            // --- Core best-effort contract ---
            // The pipeline must never throw and must never reject a non-image-based source
            // on quality grounds. QualityRejected only fires for image/PDF primaries when
            // the quality analyzer rejects the scan. DOCX is always non-image.
            result.QualityRejected.ShouldBeFalse(
                "a DOCX-primary case is never subject to quality rejection — best-effort must proceed");

            // The ExtractionResult itself is always non-null (a record returned by ExtractAsync).
            // (The result variable is non-nullable by the return type — confirmed here via ShouldNotBeNull
            // for clarity and documentation.)
            result.ShouldNotBeNull("ExtractAsync always returns a non-null ExtractionResult");

            // --- FusionResult behavior (the real finding) ---
            // FINDING: FusionExpedienteService receives a non-null docxExpediente
            // (MapExtractedFieldsToExpediente always returns non-null, with NumeroExpediente = "")
            // so the "at-least-one-source" guard passes. FuseAsync returns SUCCESS.
            // FusionResult IS non-null; the DOCX-only best-effort case DOES reach Stage 3 completion.
            result.FusionResult.ShouldNotBeNull(
                "Stage 3 must complete even with a DOCX that extracted no expediente; " +
                "FusionExpedienteService receives a non-null Expediente with empty fields " +
                "and the at-least-one-source guard passes");

            result.FusionResult!.FusedExpediente.ShouldNotBeNull(
                "FusionExpedienteService always populates FusedExpediente");

            // FINDING: The real SIARA DOCX does not match the expediente regex →
            // NumeroExpediente is empty (or whitespace only). This is EXPECTED and is
            // NOT a defect. The case was answered best-effort.
            // We assert the known behavior: the fused expediente is empty.
            result.FusionResult!.FusedExpediente!.NumeroExpediente.ShouldBeNullOrWhiteSpace(
                "the real-shaped SAT DOCX requerimiento carries no string matching " +
                "DocxFieldExtractor's expediente regex — NumeroExpediente is empty by design; " +
                "this is expected best-effort degradation, not a defect");
        }
        finally
        {
            try { File.Delete(tempDocxPath); } catch { /* ignore */ }
        }
    }

    // ===========================================================================
    // TEST 2 — XML-only, real EXP-NNNN-YYYY format
    //
    // XmlFieldExtractor reads Cnbv_NumeroExpediente directly from the CNBV namespace.
    // With primary = Xml, Stages 1-2 are skipped. Stage 3 picks up the XML companion
    // and fuses it. With only one source the fusion is unambiguous.
    // ===========================================================================

    /// <summary>
    /// A SIARA case with only the XML companion (EXP-2598-2020 format) must produce
    /// a FusionResult whose NumeroExpediente exactly equals the value in the XML.
    /// SourceReliabilities must contain XML_HandFilled.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_XmlOnly_RealExpFormat_FusesExpedienteFromXml()
    {
        var orchestrator = BuildOrchestrator();

        var tempXmlPath = Path.GetTempFileName() + ".xml";
        await File.WriteAllTextAsync(tempXmlPath, RealShapedXmlContent, System.Text.Encoding.UTF8, Ct);

        try
        {
            var downloadEvent = new DocumentDownloadedEvent
            {
                FileId = Guid.NewGuid(),
                CorrelationId = Guid.NewGuid(),
                FileName = tempXmlPath,
                Source = "SIARA",
                // Primary is Xml → primaryIsImageBased = false → Stages 1-2 are skipped.
                Format = FileFormat.Xml,
                IsComplete = false, // only the XML is present — still a valid best-effort request
                CaseFiles = new List<CaseFileReference>
                {
                    new() { RelativePath = tempXmlPath, Format = FileFormat.Xml },
                },
            };

            var result = await orchestrator.ExtractAsync(downloadEvent, Ct);

            // The pipeline must not throw and must not quality-reject a non-image source.
            result.QualityRejected.ShouldBeFalse(
                "XML-primary is never subject to quality rejection");

            result.FusionResult.ShouldNotBeNull(
                "Stage 3 must complete when an XML companion is present and parseable");
            result.FusionResult!.FusedExpediente.ShouldNotBeNull();

            // The XML fixture's Cnbv_NumeroExpediente = "EXP-2598-2020" must survive to
            // the fused result. XmlFieldExtractor reads the element value directly (no regex)
            // and trims whitespace. With no competing source the value is unambiguous.
            result.FusionResult.FusedExpediente!.NumeroExpediente.Trim()
                .ShouldBe(XmlExpedienteNumber,
                    "XmlFieldExtractor must read Cnbv_NumeroExpediente from the CNBV namespace directly");

            // SourceReliabilities must contain the XML source key — proves FusionExpedienteService
            // processed the XML metadata.
            result.FusionResult.SourceReliabilities.ShouldContainKey(
                SourceType.XML_HandFilled,
                "XML source reliability must be tracked when an XML companion was extracted");
        }
        finally
        {
            try { File.Delete(tempXmlPath); } catch { /* ignore */ }
        }
    }

    // ===========================================================================
    // TEST 3 — XML + DOCX (no PDF), real-shaped DOCX that contributes no expediente
    //
    // Most realistic real-world combination: SIARA delivers the XML metadata record
    // plus the SAT authority letter (DOCX), without any scanned PDF. The XML carries
    // the canonical expediente; the DOCX does not match the regex. The pipeline must:
    //   (a) not crash
    //   (b) fuse to the XML expediente
    //   (c) reflect the XML source in SourceReliabilities
    // ===========================================================================

    /// <summary>
    /// A SIARA case with XML + real-shaped DOCX (no PDF) must produce a FusionResult
    /// whose NumeroExpediente came from the XML. The DOCX absence-of-contribution
    /// must not break, invalidate, or crash the case.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_XmlAndDocx_NoPdf_FusesBestEffort_XmlCarriesExpediente()
    {
        var orchestrator = BuildOrchestrator();

        var tempXmlPath  = Path.GetTempFileName() + ".xml";
        var tempDocxPath = Path.GetTempFileName() + ".docx";

        await File.WriteAllTextAsync(tempXmlPath, RealShapedXmlContent, System.Text.Encoding.UTF8, Ct);
        await File.WriteAllBytesAsync(tempDocxPath, BuildRealShapedRequerimientoDocxBytes(), Ct);

        try
        {
            var downloadEvent = new DocumentDownloadedEvent
            {
                FileId = Guid.NewGuid(),
                CorrelationId = Guid.NewGuid(),
                FileName = tempXmlPath,
                Source = "SIARA",
                // Primary is Xml → Stages 1-2 skipped; both companions go to Stage 3.
                Format = FileFormat.Xml,
                IsComplete = false, // PDF is missing — still valid per owner ruling
                CaseFiles = new List<CaseFileReference>
                {
                    new() { RelativePath = tempXmlPath,  Format = FileFormat.Xml },
                    new() { RelativePath = tempDocxPath, Format = FileFormat.Docx },
                },
            };

            var result = await orchestrator.ExtractAsync(downloadEvent, Ct);

            // The case must not be invalidated.
            result.QualityRejected.ShouldBeFalse(
                "XML+DOCX primary (no PDF) is never subject to quality rejection");

            result.FusionResult.ShouldNotBeNull(
                "Stage 3 must complete when at least one extractable companion is present");
            result.FusionResult!.FusedExpediente.ShouldNotBeNull();

            // XML wins: it carries the expediente; the DOCX contributes nothing.
            // With no competing DOCX expediente there is no conflict and XML value is unambiguous.
            result.FusionResult.FusedExpediente!.NumeroExpediente.Trim()
                .ShouldBe(XmlExpedienteNumber,
                    "XML companion must supply the expediente; real-shaped DOCX does not match " +
                    "DocxFieldExtractor's regex so it contributes no conflicting value");

            // XML source was processed.
            result.FusionResult.SourceReliabilities.ShouldContainKey(
                SourceType.XML_HandFilled,
                "XML source reliability must be tracked — XML companion was extracted");

            // NumeroExpediente field must show XML as a contributing source.
            result.FusionResult.FieldResults.ShouldContainKey("NumeroExpediente");
            var nroField = result.FusionResult.FieldResults["NumeroExpediente"];
            nroField.ContributingSources.ShouldContain(
                SourceType.XML_HandFilled,
                "NumeroExpediente ContributingSources must include XML_HandFilled — " +
                "XML produced a non-empty expediente value");

            // The DOCX must NOT appear as a contributing source for NumeroExpediente
            // because the real-shaped DOCX extracts no expediente (regex mismatch).
            nroField.ContributingSources.ShouldNotContain(
                SourceType.DOCX_OCR_Authority,
                "real-shaped SAT DOCX extracts no expediente — must not contribute to " +
                "NumeroExpediente ContributingSources");
        }
        finally
        {
            try { File.Delete(tempDocxPath); } catch { /* ignore */ }
            try { File.Delete(tempXmlPath); } catch { /* ignore */ }
        }
    }
}
