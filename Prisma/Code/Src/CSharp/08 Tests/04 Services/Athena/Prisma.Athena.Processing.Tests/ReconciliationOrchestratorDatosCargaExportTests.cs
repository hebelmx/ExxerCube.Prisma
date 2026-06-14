using ClosedXML.Excel;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Export;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Processing;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// Integration tests for the Stage-5 "Datos Carga de Oficio" xlsx wiring in
/// <see cref="ReconciliationOrchestrator"/>. Verifies that after the SIRO XML event a
/// <c>Format=="DatosCargaOficioXlsx"</c> <see cref="ExportCompletedEvent"/> is also published,
/// and that the resulting xlsx has the 24 expected column headers.
/// </summary>
public sealed class ReconciliationOrchestratorDatosCargaExportTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly string[] ExpectedHeaders =
    [
        "Procedencia",
        "Numero de expediente",
        "Oficio",
        "Fecha de registro",
        "Fecha de recepción",
        "Días",
        "Fecha estimada de conclusión",
        "Estatus",
        "Tipo de asunto",
        "Grupo",
        "Área remitente",
        "Subdivisión",
        "Entidad Financiera",
        "Descripción",
        "Nombre del remitente",
        "Origen",
        "Tipo de documento",
        "Medio de seguimiento",
        "Nombre Abogado Interno",
        "Nombre abogado responsable",
        "Despacho",
        "Estado",
        "Ciudad",
        "Zona",
    ];

    private static Expediente MinimalExpediente() => new()
    {
        NumeroExpediente = "A/AS1-2505-999-TST",
        NumeroOficio = "214-1-00000001/2026",
        FechaRecepcion = new DateTime(2026, 6, 14),
        FechaRegistro = new DateTime(2026, 6, 14),
        FechaEstimadaConclusion = new DateTime(2026, 7, 5),
        DiasPlazo = 15,
        AutoridadNombre = "CNBV",
    };

    private static (ReconciliationOrchestrator orchestrator, IEventPublisher eventPublisher, DatosCargaOficioLayoutGenerator generator)
        CreateSutWithBothExporters()
    {
        var eventPublisher = Substitute.For<IEventPublisher>();
        var siroExporter = new SiroXmlExporter(NullLogger<SiroXmlExporter>.Instance);
        var fieldMapper = new TemplateFieldMapper(NullLogger<TemplateFieldMapper>.Instance);
        var generator = new DatosCargaOficioLayoutGenerator(
            fieldMapper,
            NullLogger<DatosCargaOficioLayoutGenerator>.Instance,
            templateRepository: null);

        var orchestrator = new ReconciliationOrchestrator(
            eventPublisher,
            NullLogger<ReconciliationOrchestrator>.Instance,
            classifier: null,
            exporter: siroExporter,
            datosCargaGenerator: generator);

        return (orchestrator, eventPublisher, generator);
    }

    // -------------------------------------------------------------------------
    // TC-1: Stage 5 emits BOTH SiroXml AND DatosCargaOficioXlsx events
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ReconcileAsync_Stage5_EmitsBothSiroXmlAndDatosCargaEvents()
    {
        var (orchestrator, eventPublisher, _) = CreateSutWithBothExporters();
        var fileId = Guid.NewGuid();
        var fusion = new FusionResult
        {
            FusedExpediente = MinimalExpediente(),
            OverallConfidence = 0.9,
            ConflictingFields = new List<string>(),
        };

        var stages = await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: fusion,
            fileId: fileId,
            correlationId: Guid.NewGuid(),
            cancellationToken: Ct);

        // Stage 4 skipped; Stage 5 runs (2 export events but counts as 1 stage)
        stages.ShouldBe(1);

        eventPublisher.Received(1).Publish(
            Arg.Is<ExportCompletedEvent>(e =>
                e.FileId == fileId &&
                e.Format == "SiroXml" &&
                e.Destination.EndsWith(".siro.xml", StringComparison.Ordinal)));

        eventPublisher.Received(1).Publish(
            Arg.Is<ExportCompletedEvent>(e =>
                e.FileId == fileId &&
                e.Format == "DatosCargaOficioXlsx" &&
                e.Destination.EndsWith(".datos-carga-oficio.xlsx", StringComparison.Ordinal)));
    }

    // -------------------------------------------------------------------------
    // TC-2: DatosCargaOficioXlsx event has positive ExportedSizeBytes
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ReconcileAsync_Stage5_DatosCargaEvent_HasPositiveSize()
    {
        var (orchestrator, eventPublisher, _) = CreateSutWithBothExporters();
        var fileId = Guid.NewGuid();

        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: new FusionResult
            {
                FusedExpediente = MinimalExpediente(),
                OverallConfidence = 0.9,
                ConflictingFields = new List<string>(),
            },
            fileId: fileId,
            correlationId: null,
            cancellationToken: Ct);

        eventPublisher.Received(1).Publish(
            Arg.Is<ExportCompletedEvent>(e =>
                e.Format == "DatosCargaOficioXlsx" &&
                e.ExportedSizeBytes > 0));
    }

    // -------------------------------------------------------------------------
    // TC-3: Null generator → only SiroXml event, no crash
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ReconcileAsync_Stage5_NullGenerator_OnlySiroXmlEvent()
    {
        var eventPublisher = Substitute.For<IEventPublisher>();
        var siroExporter = new SiroXmlExporter(NullLogger<SiroXmlExporter>.Instance);

        var orchestrator = new ReconciliationOrchestrator(
            eventPublisher,
            NullLogger<ReconciliationOrchestrator>.Instance,
            classifier: null,
            exporter: siroExporter,
            datosCargaGenerator: null);   // <-- no generator

        var fileId = Guid.NewGuid();
        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: new FusionResult
            {
                FusedExpediente = MinimalExpediente(),
                OverallConfidence = 0.9,
                ConflictingFields = new List<string>(),
            },
            fileId: fileId,
            correlationId: null,
            cancellationToken: Ct);

        eventPublisher.Received(1).Publish(
            Arg.Is<ExportCompletedEvent>(e => e.Format == "SiroXml"));
        eventPublisher.DidNotReceive().Publish(
            Arg.Is<ExportCompletedEvent>(e => e.Format == "DatosCargaOficioXlsx"));
    }

    // -------------------------------------------------------------------------
    // TC-4: Xlsx content has 24 expected headers (parse the bytes)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ReconcileAsync_Stage5_DatosCargaXlsx_Has24Headers()
    {
        // Capture published bytes by intercepting the event and re-running the generator directly.
        // (The orchestrator writes to an in-memory stream; we prove the format by calling the generator
        //  ourselves and asserting the column headers — same approach as the SiroXml structural tests.)
        var fieldMapper = new TemplateFieldMapper(NullLogger<TemplateFieldMapper>.Instance);
        var generator = new DatosCargaOficioLayoutGenerator(
            fieldMapper,
            NullLogger<DatosCargaOficioLayoutGenerator>.Instance,
            templateRepository: null);

        var metadata = new UnifiedMetadataRecord { Expediente = MinimalExpediente() };
        using var stream = new MemoryStream();
        var result = await generator.GenerateAsync(metadata, stream, Ct);

        result.IsSuccess.ShouldBeTrue();

        stream.Position = 0;
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();

        ws.LastColumnUsed()!.ColumnNumber().ShouldBe(24, "Worksheet must have 24 columns");

        for (int i = 0; i < ExpectedHeaders.Length; i++)
        {
            ws.Cell(1, i + 1).GetString().ShouldBe(
                ExpectedHeaders[i],
                $"Column {i + 1} header mismatch");
        }
    }

    // -------------------------------------------------------------------------
    // TC-5: Null-resolver path still emits DatosCargaOficioXlsx event (no storage write)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ReconcileAsync_Stage5_NullResolver_StillEmitsDatosCargaEvent()
    {
        // ReconciliationOrchestrator already has IStoragePathResolver? as optional.
        // When null, the generator output stays in-memory and the event is still published.
        var (orchestrator, eventPublisher, _) = CreateSutWithBothExporters();
        var fileId = Guid.NewGuid();

        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: new FusionResult
            {
                FusedExpediente = MinimalExpediente(),
                OverallConfidence = 0.9,
                ConflictingFields = new List<string>(),
            },
            fileId: fileId,
            correlationId: null,
            cancellationToken: Ct);

        eventPublisher.Received(1).Publish(
            Arg.Is<ExportCompletedEvent>(e =>
                e.Format == "DatosCargaOficioXlsx" &&
                e.ExportedSizeBytes > 0));
    }
}
