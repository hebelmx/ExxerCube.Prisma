using System.Xml.Linq;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.Export;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Athena.Processing;

namespace ExxerCube.Prisma.Athena.Processing.Tests;

/// <summary>
/// Tests for the MVP-PATH #8 SIRO XML export wired into Stage 5 of
/// <see cref="ReconciliationOrchestrator"/>. Uses a real <see cref="SiroXmlExporter"/> (no mock) to
/// prove the end-to-end plumbing is wired: fused expediente in → SIRO-conformant XML event out.
/// Structural assertions (root element name, namespace) are done here; per-field assertions are
/// already in <c>SiroXmlExporterTests</c> and are not duplicated.
/// </summary>
public sealed class ReconciliationOrchestratorSiroExportTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string SiroNamespace = "http://siro.regulatory.namespace";

    /// <summary>
    /// A minimal-valid expediente that satisfies <c>ValidateMetadata()</c> (both required fields set).
    /// </summary>
    private static Expediente MinimalExpediente() => new()
    {
        NumeroExpediente = "A/AS1-2505-999-TST",
        NumeroOficio = "214-1-00000001/2026",
    };

    /// <summary>
    /// A fully-populated expediente exercising the optional fields and collections.
    /// </summary>
    private static Expediente FullyPopulatedExpediente() => new()
    {
        NumeroExpediente = "A/AS1-2505-088637-PHM",
        NumeroOficio = "214-1-18714972/2026",
        SolicitudSiara = "SIARA-TEST-001",
        Folio = 42,
        OficioYear = 2026,
        AreaClave = 3,
        AreaDescripcion = "ASEGURAMIENTO",
        FechaPublicacion = new DateTime(2026, 6, 13),
        DiasPlazo = 15,
        AutoridadNombre = "CNBV",
    };

    /// <summary>
    /// Builds a <see cref="ReconciliationOrchestrator"/> with a real <see cref="SiroXmlExporter"/>
    /// as Stage 5. Events are captured via NSubstitute <c>Received()</c> calls on the publisher.
    /// </summary>
    private static (ReconciliationOrchestrator orchestrator, IEventPublisher eventPublisher) CreateSutWithRealExporter()
    {
        var eventPublisher = Substitute.For<IEventPublisher>();
        var realExporter = new SiroXmlExporter(NullLogger<SiroXmlExporter>.Instance);

        var orchestrator = new ReconciliationOrchestrator(
            eventPublisher,
            NullLogger<ReconciliationOrchestrator>.Instance,
            classifier: null,     // Stage 4 not under test here
            exporter: realExporter);

        return (orchestrator, eventPublisher);
    }

    // -------------------------------------------------------------------------
    // TC-1: Stage 5 with a valid expediente emits an ExportCompletedEvent in SiroXml format
    // -------------------------------------------------------------------------

    /// <summary>
    /// Given a real <see cref="SiroXmlExporter"/> and a minimal-valid expediente,
    /// when <see cref="ReconciliationOrchestrator.ReconcileAsync"/> runs,
    /// then <see cref="ExportCompletedEvent.Format"/> is "SiroXml", destination ends with ".siro.xml",
    /// and <see cref="ExportCompletedEvent.ExportedSizeBytes"/> is greater than zero.
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_Stage5_EmitsSiroXmlEvent()
    {
        var (orchestrator, eventPublisher) = CreateSutWithRealExporter();
        var fileId = Guid.NewGuid();
        var fusionResult = new FusionResult
        {
            FusedExpediente = MinimalExpediente(),
            Confidence = Confidence.FromFusion(0.9),
            ConflictingFields = new List<string>(),
            NextAction = NextAction.AutoProcess, // G-C2: AutoProcess so export gate allows Stage 5
        };

        var stagesCompleted = await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: fusionResult,
            fileId: fileId,
            correlationId: Guid.NewGuid(),
            cancellationToken: Ct);

        // Stage 4 was skipped (no classifier), Stage 5 completed.
        stagesCompleted.ShouldBe(1);

        eventPublisher.Received(1).Publish(
            Arg.Is<ExportCompletedEvent>(e =>
                e.FileId == fileId &&
                e.Format == "SiroXml" &&
                e.Destination.EndsWith(".siro.xml", StringComparison.Ordinal) &&
                e.ExportedSizeBytes > 0));
    }

    // -------------------------------------------------------------------------
    // TC-2: SIRO XML structural assertion — root element, namespace, key fields
    // -------------------------------------------------------------------------

    /// <summary>
    /// Given a fully-populated expediente and a real <see cref="SiroXmlExporter"/>,
    /// when Stage 5 runs, the event carries ExportedSizeBytes &gt; 200 bytes (SIRO XML
    /// for a fully-populated record is always larger). The XML structure is verified by
    /// driving the same <see cref="SiroXmlExporter"/> directly: root local name "SiroResponse",
    /// SIRO namespace, required field values, and ISO date format.
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_Stage5_SiroXml_HasCorrectStructure()
    {
        var expediente = FullyPopulatedExpediente();
        var (orchestrator, eventPublisher) = CreateSutWithRealExporter();
        var fileId = Guid.NewGuid();
        var fusionResult = new FusionResult
        {
            FusedExpediente = expediente,
            Confidence = Confidence.FromFusion(0.95),
            ConflictingFields = new List<string>(),
            NextAction = NextAction.AutoProcess, // G-C2: AutoProcess so export gate allows Stage 5
        };

        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: fusionResult,
            fileId: fileId,
            correlationId: Guid.NewGuid(),
            cancellationToken: Ct);

        // The export event proves the size — SIRO XML for a fully-populated record is always >200 bytes.
        eventPublisher.Received(1).Publish(
            Arg.Is<ExportCompletedEvent>(e =>
                e.FileId == fileId &&
                e.ExportedSizeBytes > 200));

        // Drive the same SiroXmlExporter to parse the actual XML and assert structure.
        var siroExporter = new SiroXmlExporter(NullLogger<SiroXmlExporter>.Instance);
        using var stream = new MemoryStream();
        var exportResult = await siroExporter.ExportSiroXmlAsync(
            new UnifiedMetadataRecord { Expediente = expediente }, stream, Ct);
        exportResult.IsSuccess.ShouldBeTrue();

        var raw = Encoding.UTF8.GetString(stream.ToArray());
        var doc = XDocument.Parse(raw);

        // Root element assertions (the key discriminator vs. AdaptiveExporter which emits <Export>).
        doc.Root.ShouldNotBeNull();
        doc.Root!.Name.LocalName.ShouldBe("SiroResponse", "Root must be <SiroResponse>, not <Export>");
        doc.Root.Name.NamespaceName.ShouldBe(SiroNamespace, "Root must carry the SIRO namespace");

        // Required regulatory fields.
        GetValue(doc, "NumeroExpediente").ShouldBe(expediente.NumeroExpediente);
        GetValue(doc, "NumeroOficio").ShouldBe(expediente.NumeroOficio);

        // Date format.
        GetValue(doc, "FechaPublicacion").ShouldBe("2026-06-13", "FechaPublicacion must be ISO yyyy-MM-dd");
    }

    // -------------------------------------------------------------------------
    // TC-3: Null exporter → Stage 5 skipped (stagesCompleted == 0 with no classifier)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Given <c>exporter = null</c>, when <see cref="ReconciliationOrchestrator.ReconcileAsync"/> runs,
    /// then Stage 5 is skipped and no <see cref="ExportCompletedEvent"/> is published.
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_Stage5_SkipsExport_WhenExporterNull()
    {
        var eventPublisher = Substitute.For<IEventPublisher>();
        var orchestrator = new ReconciliationOrchestrator(
            eventPublisher,
            NullLogger<ReconciliationOrchestrator>.Instance,
            classifier: null,
            exporter: null);

        var stagesCompleted = await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: new FusionResult
            {
                FusedExpediente = MinimalExpediente(),
                Confidence = Confidence.FromFusion(0.9),
                ConflictingFields = new List<string>(),
            },
            fileId: Guid.NewGuid(),
            correlationId: null,
            cancellationToken: Ct);

        stagesCompleted.ShouldBe(0, "Both Stage 4 and Stage 5 must be skipped when neither is configured");
        eventPublisher.DidNotReceive().Publish(Arg.Any<ExportCompletedEvent>());
    }

    // -------------------------------------------------------------------------
    // TC-4: Null FusedExpediente → Stage 5 skipped with warning
    // -------------------------------------------------------------------------

    /// <summary>
    /// Given a real exporter and <c>fusionResult = null</c>,
    /// when Stage 5 runs, it skips (no <see cref="ExportCompletedEvent"/> published).
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_Stage5_SkipsExport_WhenFusionResultNull()
    {
        var (orchestrator, eventPublisher) = CreateSutWithRealExporter();

        var stagesCompleted = await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: null,   // null fusionResult → FusedExpediente cannot be unwrapped
            fileId: Guid.NewGuid(),
            correlationId: null,
            cancellationToken: Ct);

        stagesCompleted.ShouldBe(0);
        eventPublisher.DidNotReceive().Publish(Arg.Any<ExportCompletedEvent>());
    }

    // -------------------------------------------------------------------------
    // TC-5: Blank NumeroExpediente → ValidateMetadata fails → ProcessingErrorEvent
    // -------------------------------------------------------------------------

    /// <summary>
    /// Given a real <see cref="SiroXmlExporter"/> and an expediente with a blank
    /// <see cref="Expediente.NumeroExpediente"/>, when Stage 5 runs,
    /// <c>ValidateMetadata()</c> returns failure and a <see cref="ProcessingErrorEvent"/>
    /// with <see cref="ProcessingErrorEvent.Component"/> == "Export" is published instead.
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_Stage5_EmitsError_WhenValidationFails()
    {
        var (orchestrator, eventPublisher) = CreateSutWithRealExporter();

        var invalidExpediente = new Expediente
        {
            NumeroExpediente = "",    // blank → ValidateMetadata rejects it
            NumeroOficio = "OF-001",
        };

        await orchestrator.ReconcileAsync(
            ocrResult: null,
            fusionResult: new FusionResult
            {
                FusedExpediente = invalidExpediente,
                Confidence = Confidence.FromFusion(0.0),
                ConflictingFields = new List<string>(),
                NextAction = NextAction.AutoProcess, // G-C2: force gate open so Stage 5 runs and fails on validation
            },
            fileId: Guid.NewGuid(),
            correlationId: null,
            cancellationToken: Ct);

        eventPublisher.Received(1).Publish(
            Arg.Is<ProcessingErrorEvent>(e => e.Component == "Export"));
        eventPublisher.DidNotReceive().Publish(Arg.Any<ExportCompletedEvent>());
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string? GetValue(XDocument doc, string localName)
        => doc.Descendants().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;
}
