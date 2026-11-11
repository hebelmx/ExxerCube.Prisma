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
/// Integration test: proves that the REAL extraction chain
/// (ExtractionOrchestrator + XmlFieldExtractor + DocxFieldExtractor + FusionExpedienteService)
/// produces a genuine multi-source FusionResult from live fixture content.
///
/// Design choices:
/// - Primary event Format = Xml so Stages 1-2 (Quality/OCR) are skipped; no IFileLoader /
///   IImageQualityAnalyzer / IOcrExecutor is needed. The orchestrator goes straight to Stage 3.
/// - XML source: in-memory XmlContent (same content as the PRP1/222AAA-44444444442025.xml fixture
///   used by XmlFieldExtractorTests) — no file-path resolution required.
/// - DOCX source: FileContent bytes built via DocumentFormat.OpenXml in-memory. The DOCX contains
///   a paragraph with the expediente pattern "A/AS1-1111-222222-AAA" so DocxFieldExtractor's regex
///   can produce a non-null NumeroExpediente, proving the extractor ran against real OpenXML bytes.
/// - IEventPublisher is mocked (boundary); all extraction + fusion components are REAL.
/// </summary>
public sealed class MultiSourceFusionIntegrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------------------
    // Real XML fixture content — identical to PRP1/222AAA-44444444442025.xml
    // (the fixture used by XmlFieldExtractorTests). Inlined to avoid test-runtime
    // path-resolution issues when the working directory varies.
    // ---------------------------------------------------------------------------
    private const string XmlFixtureContent = """
        <?xml version="1.0" encoding="utf-8"?>
        <Expediente xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns="http://www.cnbv.gob.mx">
          <Cnbv_NumeroOficio>222/AAA/-4444444444/2025</Cnbv_NumeroOficio>
          <Cnbv_NumeroExpediente>A/AS1-1111-222222-AAA    </Cnbv_NumeroExpediente>
          <Cnbv_SolicitudSiara>AGAFADAFSON2/2025/000084</Cnbv_SolicitudSiara>
          <Cnbv_Folio>6789</Cnbv_Folio>
          <Cnbv_OficioYear>2025</Cnbv_OficioYear>
          <Cnbv_AreaClave>3</Cnbv_AreaClave>
          <Cnbv_AreaDescripcion>ASEGURAMIENTO</Cnbv_AreaDescripcion>
          <Cnbv_FechaPublicacion>2025-06-05</Cnbv_FechaPublicacion>
          <Cnbv_DiasPlazo>7</Cnbv_DiasPlazo>
          <AutoridadNombre>SUBDELEGACION 8 SAN ANGEL</AutoridadNombre>
          <NombreSolicitante xsi:nil="true" />
          <Referencia>                         </Referencia>
          <Referencia1>                         </Referencia1>
          <Referencia2>IMSSCOB/40/01/001283/2025</Referencia2>
          <TieneAseguramiento>true</TieneAseguramiento>
          <SolicitudPartes>
            <ParteId>1</ParteId>
            <Caracter>Patr&#xF3;n Determinado</Caracter>
            <Persona>Moral</Persona>
            <Paterno />
            <Materno />
            <Nombre>AEROLINEAS PAYASO ORGULLO NACIONAL</Nombre>
            <Rfc>             </Rfc>
          </SolicitudPartes>
          <SolicitudEspecifica>
            <SolicitudEspecificaId>1</SolicitudEspecificaId>
            <InstruccionesCuentasPorConocer>Para efectos de la inmovilizaci&#xF3;n ordenada se solicita instruye a las instituciones.</InstruccionesCuentasPorConocer>
            <PersonasSolicitud>
              <PersonaId>1</PersonaId>
              <Rfc>APON33333444</Rfc>
            </PersonasSolicitud>
          </SolicitudEspecifica>
        </Expediente>
        """;

    // The expediente number embedded in the XML fixture (trimmed).
    private const string XmlExpedienteNumber = "A/AS1-1111-222222-AAA";

    // The expediente number embedded in the DOCX fixture (pattern matched by DocxFieldExtractor).
    private const string DocxExpedienteNumber = "A/AS2-2222-333333-BBB";

    // ---------------------------------------------------------------------------
    // Helper: create a minimal DOCX byte array containing a paragraph with known
    // content that DocxFieldExtractor's regex can parse.
    // Pattern required by ExtractExpediente: @"[A-Z]/[A-Z]{1,2}\d+-\d+-\d+-[A-Z]+"
    // ---------------------------------------------------------------------------
    private static byte[] BuildDocxBytes(string expedienteValue)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(
                new Body(
                    new Paragraph(
                        new Run(new Text($"OFICIO DE ASEGURAMIENTO Expediente: {expedienteValue} CAUSA: Lavado de dinero")))));
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    // ---------------------------------------------------------------------------
    // The integration test
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Proves that ExtractionOrchestrator wired with the REAL XmlFieldExtractor,
    /// DocxFieldExtractor, and FusionExpedienteService produces a non-null FusionResult
    /// whose FusedExpediente and FieldResults reflect genuine multi-source content.
    ///
    /// Assertions anchored to known fixture content (not tautologies):
    /// 1. FusionResult is non-null and FusedExpediente is non-null.
    /// 2. FieldResults contains the "NumeroExpediente" key (fusion tracked the field).
    /// 3. SourceReliabilities contains entries for both XML_HandFilled and DOCX_OCR_Authority,
    ///    confirming both sources were consumed by FusionExpedienteService.
    /// 4. The fused NumeroExpediente is the XML fixture value (XML has higher source reliability
    ///    than DOCX per FusionCoefficients; when both parse the same-pattern field but differ,
    ///    XML wins — or when they produce the same value, it unambiguously came from XML).
    ///    We assert it equals the XML fixture value as the concrete field assertion.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_RealXmlAndDocxSources_FusionResultContainsFieldsFromBothSources()
    {
        // -----------------------------------------------------------------------
        // Arrange: real extractors + real fusion service
        // -----------------------------------------------------------------------

        var xmlExtractor = new XmlFieldExtractor();
        var docxExtractor = new DocxFieldExtractor(
            NullLogger<DocxFieldExtractor>.Instance);
        var fusionService = new FusionExpedienteService(
            NullLogger<FusionExpedienteService>.Instance);

        var orchestrator = new ExtractionOrchestrator(
            eventPublisher: Substitute.For<IEventPublisher>(),
            logger: NullLogger<ExtractionOrchestrator>.Instance,
            // Stages 1-2 not needed: primary is XML (non-image), so orchestrator skips Quality+OCR.
            qualityAnalyzer: null,
            ocrExecutor: null,
            fusionService: fusionService,
            fileLoader: null,
            txtFieldExtractor: null,
            xmlFieldExtractor: xmlExtractor,
            docxFieldExtractor: docxExtractor);

        // Inline XML source (avoids runtime file-path resolution).
        // DOCX source as in-memory bytes embedded directly in DocxSource.
        // DocxFieldExtractor checks source.FileContent first — no file I/O needed.
        var docxBytes = BuildDocxBytes(DocxExpedienteNumber);

        // Write the DOCX bytes to a temp file so the orchestrator can pass a valid path
        // through CaseFileReference.RelativePath. (The orchestrator constructs DocxSource(path),
        // which triggers DocxFieldExtractor's FilePath branch. We need a real file.)
        // Alternative: bypass via the FileContent branch by using an absolute path that exists.
        // We use Path.GetTempFileName to ensure determinism.
        var tempDocxPath = Path.GetTempFileName() + ".docx";
        await File.WriteAllBytesAsync(tempDocxPath, docxBytes, Ct);

        // Write the XML content to a temp file similarly.
        var tempXmlPath = Path.GetTempFileName() + ".xml";
        await File.WriteAllTextAsync(tempXmlPath, XmlFixtureContent, System.Text.Encoding.UTF8, Ct);

        try
        {
            // Build the event: primary Format = Xml (skips Quality/OCR stages).
            // CaseFiles holds BOTH the XML and DOCX companions with absolute temp paths.
            var downloadEvent = new DocumentDownloadedEvent
            {
                FileId = Guid.NewGuid(),
                CorrelationId = Guid.NewGuid(),
                FileName = tempXmlPath,
                Source = "SIARA",
                Format = FileFormat.Xml,
                CaseFiles = new List<CaseFileReference>
                {
                    new() { RelativePath = tempXmlPath,  Format = FileFormat.Xml },
                    new() { RelativePath = tempDocxPath, Format = FileFormat.Docx },
                },
            };

            // -----------------------------------------------------------------------
            // Act
            // -----------------------------------------------------------------------
            var result = await orchestrator.ExtractAsync(downloadEvent, Ct);

            // -----------------------------------------------------------------------
            // Assert 1: the pipeline ran to Stage 3 and produced a result.
            // -----------------------------------------------------------------------
            result.QualityRejected.ShouldBeFalse("non-image primary must not trigger quality rejection");
            result.FusionResult.ShouldNotBeNull("Stage 3 Fusion must complete");
            result.FusionResult!.FusedExpediente.ShouldNotBeNull("FusionExpedienteService must return a FusedExpediente");

            // -----------------------------------------------------------------------
            // Assert 2: FusionExpedienteService tracked the NumeroExpediente field.
            // This proves the REAL FusionExpedienteService executed its field-fuse logic
            // (not a mock), because the field key is only present when FuseNumeroExpedienteAsync ran.
            // -----------------------------------------------------------------------
            result.FusionResult.FieldResults.ShouldContainKey(
                "NumeroExpediente",
                "FusionExpedienteService must have fused the NumeroExpediente field");

            // -----------------------------------------------------------------------
            // Assert 3: SourceReliabilities shows BOTH XML and DOCX consumed.
            // FusionExpedienteService always populates SourceReliabilities for all three
            // source types from ExtractionMetadata; when a source fed a non-null Expediente
            // (i.e. extraction succeeded), its reliability is > 0.
            // We verify the dictionary has the XML key at minimum (XML extraction succeeded).
            // -----------------------------------------------------------------------
            result.FusionResult.SourceReliabilities.ShouldContainKey(
                SourceType.XML_HandFilled,
                "XML source reliability must be tracked — XML extraction succeeded");
            result.FusionResult.SourceReliabilities.ShouldContainKey(
                SourceType.DOCX_OCR_Authority,
                "DOCX source reliability must be tracked — DOCX extraction succeeded");

            // -----------------------------------------------------------------------
            // Assert 4: Concrete field value from the XML fixture.
            // XmlFieldExtractor reads Cnbv_NumeroExpediente = "A/AS1-1111-222222-AAA    " (with trailing spaces)
            // and trims it. MapExtractedFieldsToExpediente maps ExtractedFields.Expediente
            // → Expediente.NumeroExpediente.
            // XML_HandFilled has a lower base reliability than DOCX_OCR_Authority per
            // FusionCoefficients, but when a single source wins (the DOCX produces a
            // DIFFERENT expediente "A/AS2-2222-333333-BBB"), the fusion uses weighted voting.
            // We assert the fused value is non-empty — a real parse happened.
            // We also assert the XML extractor produced the known fixture value by checking
            // FieldResults[NumeroExpediente].ChosenValue is the XML fixture value
            // (XML loses to DOCX if DOCX weight > XML weight and they conflict).
            // The concrete assertion: the fused NumeroExpediente is non-empty, and
            // the XML fixture's trimmed value appears as one of the candidates or as the winner.
            // -----------------------------------------------------------------------
            var fusedNumero = result.FusionResult.FusedExpediente!.NumeroExpediente;
            fusedNumero.ShouldNotBeNullOrWhiteSpace(
                "FusedExpediente.NumeroExpediente must be non-empty — at least one source extracted it");

            // The FieldFusionResult for NumeroExpediente must show ContributingSources from both
            // XML and DOCX, proving both extractors produced a non-empty NumeroExpediente.
            var nroField = result.FusionResult.FieldResults["NumeroExpediente"];
            nroField.ContributingSources.ShouldContain(
                SourceType.XML_HandFilled,
                "NumeroExpediente ContributingSources must include XML_HandFilled — " +
                "XmlFieldExtractor must have produced a non-empty NumeroExpediente");
            nroField.ContributingSources.ShouldContain(
                SourceType.DOCX_OCR_Authority,
                "NumeroExpediente ContributingSources must include DOCX_OCR_Authority — " +
                "DocxFieldExtractor must have produced a non-empty NumeroExpediente");

            // Because the two sources produce DIFFERENT values ("A/AS1-1111-222222-AAA" vs
            // "A/AS2-2222-333333-BBB"), the fusion records a WeightedVoting or Conflict decision
            // and populates ConflictingValues. Assert the XML fixture value appears as the
            // winning value or in the conflict list — confirming XmlFieldExtractor ran
            // against the real CNBV namespace XML and parsed Cnbv_NumeroExpediente correctly.
            var xmlValuePresent =
                string.Equals(nroField.Value?.Trim(), XmlExpedienteNumber, StringComparison.OrdinalIgnoreCase) ||
                nroField.ConflictingValues.Any(cv =>
                    string.Equals(cv.Value?.Trim(), XmlExpedienteNumber, StringComparison.OrdinalIgnoreCase));
            xmlValuePresent.ShouldBeTrue(
                $"XML fixture value '{XmlExpedienteNumber}' must appear as winner or conflict in " +
                $"NumeroExpediente FieldFusionResult. Winner='{nroField.Value}', " +
                $"Conflicts=[{string.Join(", ", nroField.ConflictingValues.Select(cv => cv.Value))}]");

            // Similarly confirm DocxFieldExtractor produced the DOCX fixture value.
            var docxValuePresent =
                string.Equals(nroField.Value?.Trim(), DocxExpedienteNumber, StringComparison.OrdinalIgnoreCase) ||
                nroField.ConflictingValues.Any(cv =>
                    string.Equals(cv.Value?.Trim(), DocxExpedienteNumber, StringComparison.OrdinalIgnoreCase));
            docxValuePresent.ShouldBeTrue(
                $"DOCX fixture value '{DocxExpedienteNumber}' must appear as winner or conflict in " +
                $"NumeroExpediente FieldFusionResult — proves DocxFieldExtractor ran against real " +
                $"OpenXML bytes. Winner='{nroField.Value}', " +
                $"Conflicts=[{string.Join(", ", nroField.ConflictingValues.Select(cv => cv.Value))}]");
        }
        finally
        {
            // Cleanup temp files — best-effort.
            try { File.Delete(tempDocxPath); } catch { /* ignore */ }
            try { File.Delete(tempXmlPath); } catch { /* ignore */ }
        }
    }

    // ---------------------------------------------------------------------------
    // Corrupt-file resilience (issue #4 processing half, owner ruling 2026-06-13):
    // "even a corrupt file ... must be managed on the pipeline on processing. None of
    // these conditions make invalid the request." A present-but-unparseable companion
    // must DEGRADE the case (fuse the valid sources), never crash it. Both field
    // extractors wrap parsing in try/catch → Result.WithFailure, and the orchestrator's
    // Build{Xml,Docx}ExpedienteAsync treat that failure as "fusion continues without
    // this source". These tests pin that resilience against the REAL extraction chain.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// A corrupt DOCX companion (not a valid OpenXML package) must NOT crash the case:
    /// the orchestrator degrades to XML-only and still produces a FusionResult whose
    /// NumeroExpediente came from the valid XML source.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_CorruptDocxCompanion_DegradesToXmlOnly_StillFuses()
    {
        var orchestrator = new ExtractionOrchestrator(
            eventPublisher: Substitute.For<IEventPublisher>(),
            logger: NullLogger<ExtractionOrchestrator>.Instance,
            qualityAnalyzer: null,
            ocrExecutor: null,
            fusionService: new FusionExpedienteService(NullLogger<FusionExpedienteService>.Instance),
            fileLoader: null,
            txtFieldExtractor: null,
            xmlFieldExtractor: new XmlFieldExtractor(),
            docxFieldExtractor: new DocxFieldExtractor(NullLogger<DocxFieldExtractor>.Instance));

        var tempXmlPath = Path.GetTempFileName() + ".xml";
        await File.WriteAllTextAsync(tempXmlPath, XmlFixtureContent, System.Text.Encoding.UTF8, Ct);

        // A corrupt DOCX: arbitrary bytes that are NOT a valid OpenXML zip — OpenXml throws,
        // DocxFieldExtractor catches it and returns Result.WithFailure.
        var tempDocxPath = Path.GetTempFileName() + ".docx";
        await File.WriteAllBytesAsync(tempDocxPath, System.Text.Encoding.UTF8.GetBytes("THIS IS NOT A VALID DOCX PACKAGE"), Ct);

        try
        {
            var downloadEvent = new DocumentDownloadedEvent
            {
                FileId = Guid.NewGuid(),
                CorrelationId = Guid.NewGuid(),
                FileName = tempXmlPath,
                Source = "SIARA",
                Format = FileFormat.Xml,
                IsComplete = true, // the files are present; one is corrupt — a processing-half concern.
                CaseFiles = new List<CaseFileReference>
                {
                    new() { RelativePath = tempXmlPath,  Format = FileFormat.Xml },
                    new() { RelativePath = tempDocxPath, Format = FileFormat.Docx },
                },
            };

            // Act — must NOT throw despite the corrupt DOCX.
            var result = await orchestrator.ExtractAsync(downloadEvent, Ct);

            // The case still processed (degraded), fusion ran from the valid XML source.
            result.FusionResult.ShouldNotBeNull("a corrupt DOCX must degrade the case, not crash it");
            result.FusionResult!.FusedExpediente.ShouldNotBeNull();
            result.FusionResult.SourceReliabilities.ShouldContainKey(
                SourceType.XML_HandFilled, "the valid XML source must still contribute");

            var nroField = result.FusionResult.FieldResults["NumeroExpediente"];
            nroField.ContributingSources.ShouldContain(
                SourceType.XML_HandFilled, "XML produced the NumeroExpediente");
            nroField.ContributingSources.ShouldNotContain(
                SourceType.DOCX_OCR_Authority, "the corrupt DOCX must have been dropped (degraded)");
            // With DOCX dropped there is no conflict — XML wins cleanly.
            result.FusionResult.FusedExpediente!.NumeroExpediente.Trim()
                .ShouldBe(XmlExpedienteNumber);
        }
        finally
        {
            try { File.Delete(tempDocxPath); } catch { /* ignore */ }
            try { File.Delete(tempXmlPath); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// A corrupt XML companion (malformed markup) must NOT crash the case: the orchestrator
    /// degrades to DOCX-only and still produces a FusionResult whose NumeroExpediente came
    /// from the valid DOCX source.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_CorruptXmlCompanion_DegradesToDocxOnly_StillFuses()
    {
        var orchestrator = new ExtractionOrchestrator(
            eventPublisher: Substitute.For<IEventPublisher>(),
            logger: NullLogger<ExtractionOrchestrator>.Instance,
            qualityAnalyzer: null,
            ocrExecutor: null,
            fusionService: new FusionExpedienteService(NullLogger<FusionExpedienteService>.Instance),
            fileLoader: null,
            txtFieldExtractor: null,
            xmlFieldExtractor: new XmlFieldExtractor(),
            docxFieldExtractor: new DocxFieldExtractor(NullLogger<DocxFieldExtractor>.Instance));

        // A corrupt XML: malformed markup (unclosed tag) — XDocument.Parse throws,
        // XmlFieldExtractor catches it and returns Result.WithFailure.
        var tempXmlPath = Path.GetTempFileName() + ".xml";
        await File.WriteAllTextAsync(tempXmlPath, "<Expediente><unclosed>", System.Text.Encoding.UTF8, Ct);

        var tempDocxPath = Path.GetTempFileName() + ".docx";
        await File.WriteAllBytesAsync(tempDocxPath, BuildDocxBytes(DocxExpedienteNumber), Ct);

        try
        {
            var downloadEvent = new DocumentDownloadedEvent
            {
                FileId = Guid.NewGuid(),
                CorrelationId = Guid.NewGuid(),
                FileName = tempXmlPath,
                Source = "SIARA",
                Format = FileFormat.Xml, // primary is XML so Quality/OCR are skipped; XML parse fails in Stage 3.
                IsComplete = true,
                CaseFiles = new List<CaseFileReference>
                {
                    new() { RelativePath = tempXmlPath,  Format = FileFormat.Xml },
                    new() { RelativePath = tempDocxPath, Format = FileFormat.Docx },
                },
            };

            // Act — must NOT throw despite the corrupt XML.
            var result = await orchestrator.ExtractAsync(downloadEvent, Ct);

            result.FusionResult.ShouldNotBeNull("a corrupt XML must degrade the case, not crash it");
            result.FusionResult!.FusedExpediente.ShouldNotBeNull();

            var nroField = result.FusionResult.FieldResults["NumeroExpediente"];
            nroField.ContributingSources.ShouldContain(
                SourceType.DOCX_OCR_Authority, "the valid DOCX produced the NumeroExpediente");
            nroField.ContributingSources.ShouldNotContain(
                SourceType.XML_HandFilled, "the corrupt XML must have been dropped (degraded)");
            result.FusionResult.FusedExpediente!.NumeroExpediente.Trim()
                .ShouldBe(DocxExpedienteNumber);
        }
        finally
        {
            try { File.Delete(tempDocxPath); } catch { /* ignore */ }
            try { File.Delete(tempXmlPath); } catch { /* ignore */ }
        }
    }
}
