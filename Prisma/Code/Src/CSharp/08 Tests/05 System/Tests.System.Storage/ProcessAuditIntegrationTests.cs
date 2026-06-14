using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using ExxerCube.Prisma.Infrastructure.BrowserAutomation.ProcessIdentity;
using ExxerCube.Prisma.Infrastructure.Database;
using ExxerCube.Prisma.Infrastructure.Database.DependencyInjection;
using ExxerCube.Prisma.Infrastructure.Database.EntityFramework;
using ExxerCube.Prisma.Infrastructure.Database.Services;
using ExxerCube.Prisma.Infrastructure.Events;
using ExxerCube.Prisma.Testing.Contracts;
using IndFusion.Ember.Abstractions.Hubs;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Prisma.Athena.Processing;
using Prisma.Athena.Processing.Reconciliation;
using Prisma.Orion.Ingestion;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.System.Storage;

/// <summary>
/// A6 DoD gate tests: prove "an audit query answers who/which-process touched doc X."
/// One test per pipeline process (Downloader / Extractor / Reconciliator). Each runs the real
/// pipeline service against Testcontainers SQL, then asserts that
/// <see cref="IAuditLogger.GetAuditRecordsByFileIdAsync"/> returns records carrying
/// <see cref="AuditRecord.ProcessId"/> and <see cref="AuditRecord.UserId"/> equal to the stub
/// actor's <c>ActorId</c> (MVP-PATH 1.6 A6). A fourth test exercises the PRODUCTION queued
/// audit path (<see cref="QueuedAuditLoggerService"/> drained by
/// <see cref="QueuedAuditProcessorService"/>) to prove eventual-consistency persists records.
/// </summary>
public sealed class ProcessAuditIntegrationTests : IDisposable
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly DbContextOptions<PrismaDbContext> _dbOptions;
    private readonly ServiceProvider _serviceProvider;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// Known actor identity returned by the stub <see cref="ISiaraActorIdentityProvider"/> so tests can assert
    /// the exact actor id written to <see cref="AuditRecord.ProcessId"/> and <see cref="AuditRecord.UserId"/>.
    /// </summary>
    private static readonly SiaraActor KnownActor = new()
    {
        ActorId = "prisma-test-actor-A6",
        ActorType = SiaraActorType.ServiceAccount,
        DisplayName = "A6 DoD test actor"
    };

    /// <summary>
    /// Initializes the per-class isolated database and the shared DI service provider used by all tests.
    /// </summary>
    public ProcessAuditIntegrationTests(SqlServerContainerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _fixture.EnsureAvailable();

        var connectionString = _fixture
            .CreateIsolatedDatabaseAsync(nameof(ProcessAuditIntegrationTests), Ct)
            .GetAwaiter()
            .GetResult();

        _dbOptions = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        // EnsureCreated applies the current EF model (no FK from AuditRecords to FileMetadata
        // after the DropAuditFileMetadataFk migration — FK was dropped in FIX 1).
        using (var ctx = new PrismaDbContext(_dbOptions))
        {
            ctx.Database.EnsureCreatedAsync(Ct).GetAwaiter().GetResult();
        }

        // Build a service provider with a scoped IAuditLogger (AuditLoggerService — direct synchronous
        // writes to the DB). The pipeline services resolve it via IServiceScopeFactory per audit call.
        var services = new ServiceCollection();
        services.AddScoped<PrismaDbContext>(_ => new PrismaDbContext(_dbOptions));
        services.AddScoped<IAuditLogger>(sp =>
        {
            var ctx = sp.GetRequiredService<PrismaDbContext>();
            return new AuditLoggerService(ctx, XUnitLogger.CreateLogger<AuditLoggerService>(output));
        });

        _serviceProvider = services.BuildServiceProvider();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private ISiaraActorIdentityProvider CreateStubActorProvider()
    {
        var actorProvider = Substitute.For<ISiaraActorIdentityProvider>();
        actorProvider.GetCurrentActorAsync(Arg.Any<CancellationToken>())
            .Returns(Result<SiaraActor>.Success(KnownActor));
        return actorProvider;
    }

    /// <summary>
    /// Queries <see cref="IAuditLogger.GetAuditRecordsByFileIdAsync"/> for the given
    /// <paramref name="fileId"/> and asserts that the record identified by
    /// <paramref name="expectedActionType"/> + <paramref name="expectedStage"/> carries the
    /// known actor's <see cref="AuditRecord.ProcessId"/> / <see cref="AuditRecord.UserId"/>
    /// (A6 DoD key: "who/which-process touched doc X?" answered by document id).
    /// </summary>
    private async Task AssertAuditRecordHasProcessIdAsync(
        string fileId,
        AuditActionType expectedActionType,
        ProcessingStage expectedStage,
        string expectedClearanceKeyword)
    {
        // Query by fileId — the A6 DoD primary key ("which-process touched doc X").
        // No FK constraint on AuditRecord.FileId after FIX 1; plain indexed column.
        using var queryScope = _serviceProvider.CreateScope();
        var auditLogger = queryScope.ServiceProvider.GetRequiredService<IAuditLogger>();
        var auditResult = await auditLogger.GetAuditRecordsByFileIdAsync(fileId, Ct);

        auditResult.IsSuccess.ShouldBeTrue();
        var records = auditResult.Value!;
        records.ShouldNotBeEmpty($"Expected at least one audit record for fileId {fileId}");

        var record = records.FirstOrDefault(r =>
            r.ActionType == expectedActionType &&
            r.Stage == expectedStage);
        record.ShouldNotBeNull(
            $"Expected a {expectedActionType}/{expectedStage} audit record for fileId {fileId}");

        // A6 core assertions.
        record!.ProcessId.ShouldBe(KnownActor.ActorId,
            "ProcessId must equal the actor id from ISiaraActorIdentityProvider");
        record.UserId.ShouldBe(KnownActor.ActorId,
            "UserId must carry the actor id (double-entry for standard userId filter)");
        record.ActionDetails.ShouldNotBeNullOrWhiteSpace(
            "ActionDetails must be non-empty JSON containing the process-identity payload");
        record.ActionDetails!.ShouldContain(expectedClearanceKeyword);
        record.ActionDetails.ShouldContain(KnownActor.ActorId);
    }

    // -------------------------------------------------------------------------
    // A6-1: Orion Downloader — IngestionOrchestrator emits audit with ProcessId
    // -------------------------------------------------------------------------

    /// <summary>
    /// A6 gate for the Orion Downloader: after <see cref="IngestionOrchestrator.IngestCaseAsync"/> runs
    /// successfully, <see cref="IAuditLogger.GetAuditRecordsByFileIdAsync"/> (queried using the
    /// <see cref="IngestionResult.FileId"/> returned by the orchestrator) returns at least one audit record
    /// whose <see cref="AuditRecord.ProcessId"/> equals the stub actor id and whose
    /// <see cref="AuditRecord.ActionDetails"/> contains the process clearance ("Download").
    /// No FileMetadata row is seeded — the FK was dropped (FIX 1), so the INSERT succeeds without it.
    /// </summary>
    [Fact]
    public async Task IngestionOrchestrator_IngestCase_AuditRecordsCarryProcessId()
    {
        // Arrange
        var actorProvider = CreateStubActorProvider();

        const string docUrl = "https://siara.test/doc/A6-1";
        const string caseId = "test-case-A6-1";

        // Stub downloader — returns a minimal document with known provenance.
        var downloader = Substitute.For<IDocumentDownloader>();
        downloader.DownloadAsync(docUrl, Arg.Any<CancellationToken>())
            .Returns(Result<DownloadedDocument>.Success(new DownloadedDocument
            {
                Content = new byte[] { 0x25, 0x50, 0x44, 0x46 }, // minimal PDF header
                DocumentId = docUrl,
                SourceUrl = docUrl,
                AcquiredBy = KnownActor,
                SessionId = Guid.NewGuid().ToString(),
                Format = FileFormat.Pdf
            }));

        // Stub journal: no duplicate.
        var journal = Substitute.For<IIngestionJournal>();
        journal.ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Stub event hub.
        var eventHub = Substitute.For<IExxerHub<DocumentDownloadedEvent>>();
        eventHub.SendToAllAsync(Arg.Any<DocumentDownloadedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Use a temp directory so the orchestrator can actually write the file.
        var storageBase = Path.Combine(Path.GetTempPath(), $"prisma-a6-1-{Guid.NewGuid():N}");
        Directory.CreateDirectory(storageBase);

        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var orchestrator = new IngestionOrchestrator(
            journal: journal,
            downloader: downloader,
            eventHub: eventHub,
            logger: NullLogger<IngestionOrchestrator>.Instance,
            storageBasePath: storageBase,
            scopeFactory: scopeFactory,
            actorIdentityProvider: actorProvider,
            processClearance: ProcessClearance.Download,
            postWriteFlushDelay: TimeSpan.Zero);

        var correlationId = Guid.NewGuid();
        var siaraCase = new SiaraCase
        {
            CaseId = caseId,
            Files = new[]
            {
                new DownloadableFile { Url = docUrl, FileName = "doc.pdf", Format = FileFormat.Pdf },
            },
        };

        // Act
        var result = await orchestrator.IngestCaseAsync(siaraCase, correlationId, Ct);

        // Assert — ingestion must succeed.
        result.IsSuccess.ShouldBeTrue(
            $"IngestionOrchestrator.IngestCaseAsync failed: {string.Join(", ", result.Errors)}");

        // Capture the real FileId assigned by the orchestrator (not the null DocumentReceived record).
        var fileId = result.Value!.FileId.ToString();

        // A6 assertion: query by FileId (the document identity key for A6 DoD).
        // No FileMetadata seed needed — FK is dropped; the audit INSERT succeeds directly.
        // Asserts the "DocumentStored" record (not just the null-fileId "DocumentReceived" record).
        await AssertAuditRecordHasProcessIdAsync(
            fileId,
            AuditActionType.Download,
            ProcessingStage.Ingestion,
            expectedClearanceKeyword: "Download");

        // Cleanup.
        try { Directory.Delete(storageBase, recursive: true); } catch { /* non-fatal cleanup */ }
    }

    // -------------------------------------------------------------------------
    // A6-2: Athena Extractor — ExtractionPipelineService emits audit with ProcessId
    // -------------------------------------------------------------------------

    /// <summary>
    /// A6 gate for the Athena Extractor: after <see cref="ExtractionPipelineService.ProcessAsync"/> runs
    /// a successful extraction, <see cref="IAuditLogger.GetAuditRecordsByFileIdAsync"/> returns at least one
    /// record whose <see cref="AuditRecord.ProcessId"/> equals the stub actor id and whose
    /// <see cref="AuditRecord.ActionDetails"/> contains the process clearance ("Extract").
    /// No FileMetadata row is seeded — FK is dropped (FIX 1).
    /// </summary>
    [Fact]
    public async Task ExtractionPipelineService_ProcessAsync_AuditRecordsCarryProcessId()
    {
        // Arrange
        var actorProvider = CreateStubActorProvider();

        var fileId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var fusedExpediente = new Expediente { NumeroExpediente = "A6-EXP-EXTRACTOR" };

        var eventPublisher = new EventPublisher(NullLogger<EventPublisher>.Instance);

        var fileLoader = Substitute.For<IFileLoader>();
        fileLoader.LoadImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ImageData>.Success(new ImageData(new byte[] { 1, 2, 3 }, "test.pdf")));

        var qualityAnalyzer = Substitute.For<IImageQualityAnalyzer>();
        qualityAnalyzer.AnalyzeAsync(Arg.Any<ImageData>())
            .Returns(Result<ImageQualityAssessment>.Success(new ImageQualityAssessment
            {
                QualityLevel = ImageQualityLevel.Pristine,
                Confidence = 0.99f
            }));

        var ocrExecutor = Substitute.For<IOcrExecutor>();
        ocrExecutor.ExecuteOcrAsync(Arg.Any<ImageData>(), Arg.Any<ExxerCube.Prisma.Domain.Models.OCRConfig>())
            .Returns(Result<OCRResult>.Success(
                new OCRResult("Extracted text", 95.0f, 95.0f, new List<float> { 95f }, "spa")));

        var fusionService = Substitute.For<IFusionExpediente>();
        fusionService.FuseAsync(
                Arg.Any<Expediente?>(), Arg.Any<Expediente?>(), Arg.Any<Expediente?>(),
                Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(), Arg.Any<ExtractionMetadata>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<FusionResult>.Success(new FusionResult
            {
                OverallConfidence = 0.9,
                ConflictingFields = new List<string>(),
                FusedExpediente = fusedExpediente
            }));

        var extractionOrchestrator = new ExtractionOrchestrator(
            eventPublisher,
            NullLogger<ExtractionOrchestrator>.Instance,
            qualityAnalyzer: qualityAnalyzer,
            ocrExecutor: ocrExecutor,
            fusionService: fusionService,
            fileLoader: fileLoader);

        var handoffStore = new FakeExpedienteHandoffStore();

        var reconciliationHub = Substitute.For<IExxerHub<ExtractionCompletedEvent>>();
        reconciliationHub.SendToAllAsync(Arg.Any<ExtractionCompletedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var sut = new ExtractionPipelineService(
            eventPublisher: eventPublisher,
            extractionOrchestrator: extractionOrchestrator,
            handoffStore: handoffStore,
            reconciliationHub: reconciliationHub,
            logger: NullLogger<ExtractionPipelineService>.Instance,
            scopeFactory: scopeFactory,
            actorIdentityProvider: actorProvider,
            processClearance: ProcessClearance.Extract);

        // No FileMetadata seed — FK is dropped. The audit INSERT with this FileId must now succeed.
        var downloadEvent = new DocumentDownloadedEvent
        {
            FileId = fileId,
            CorrelationId = correlationId,
            FileName = "a6-test.pdf",
            Source = "SIARA",
            Path = "2026/06/13/a6-test.pdf",
            Format = FileFormat.Pdf
        };

        // Act
        var result = await sut.ProcessAsync(downloadEvent, Ct);

        // Assert — extraction must succeed.
        result.IsSuccess.ShouldBeTrue(
            $"ExtractionPipelineService.ProcessAsync failed: {string.Join(", ", result.Errors)}");

        // A6 assertion: query by FileId — proves "which-process touched doc X" by document id.
        await AssertAuditRecordHasProcessIdAsync(
            fileId.ToString(),
            AuditActionType.Extraction,
            ProcessingStage.Extraction,
            expectedClearanceKeyword: "Extract");
    }

    // -------------------------------------------------------------------------
    // A6-3: Reconciliator — ReconciliationPipelineService emits audit with ProcessId
    // -------------------------------------------------------------------------

    /// <summary>
    /// A6 gate for the Reconciliator: after <see cref="ReconciliationPipelineService.ProcessAsync"/> runs
    /// a successful reconciliation, <see cref="IAuditLogger.GetAuditRecordsByFileIdAsync"/> returns at least
    /// one record whose <see cref="AuditRecord.ProcessId"/> equals the stub actor id and whose
    /// <see cref="AuditRecord.ActionDetails"/> contains the process clearance ("Reconcile").
    /// No FileMetadata row is seeded — FK is dropped (FIX 1).
    /// </summary>
    [Fact]
    public async Task ReconciliationPipelineService_ProcessAsync_AuditRecordsCarryProcessId()
    {
        // Arrange
        var actorProvider = CreateStubActorProvider();

        var fileId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var fusedExpediente = new Expediente { NumeroExpediente = "A6-EXP-RECONCILIATOR" };

        var eventPublisher = new EventPublisher(NullLogger<EventPublisher>.Instance);

        // Pre-seed the in-memory handoff store so the LoadAsync step succeeds.
        var handoffPath = $"2026/06/13/{fileId}.fusion.json";
        var handoffStore = new FakeExpedienteHandoffStore();
        await handoffStore.SaveAsync(fusedExpediente, handoffPath, Ct);

        // ReconciliationOrchestrator with no classifier and no exporter — both optional.
        // Classification and Export stages are skipped; the service still emits the
        // ReconciliationStarted + ReconciliationCompleted audit records.
        var reconciliationOrchestrator = new ReconciliationOrchestrator(
            eventPublisher,
            NullLogger<ReconciliationOrchestrator>.Instance,
            classifier: null,
            exporter: null);

        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var sut = new ReconciliationPipelineService(
            eventPublisher: eventPublisher,
            reconciliationOrchestrator: reconciliationOrchestrator,
            handoffStore: handoffStore,
            logger: NullLogger<ReconciliationPipelineService>.Instance,
            scopeFactory: scopeFactory,
            actorIdentityProvider: actorProvider,
            processClearance: ProcessClearance.Reconcile);

        // No FileMetadata seed — FK is dropped. The audit INSERT with this FileId must now succeed.
        var completedEvent = new ExtractionCompletedEvent
        {
            FileId = fileId,
            CorrelationId = correlationId,
            Path = handoffPath,
            FieldsFused = 5,
            ConflictsDetected = 0
        };

        // Act
        var result = await sut.ProcessAsync(completedEvent, Ct);

        // Assert — reconciliation must succeed.
        result.IsSuccess.ShouldBeTrue(
            $"ReconciliationPipelineService.ProcessAsync failed: {string.Join(", ", result.Errors)}");

        // A6 assertion: query by FileId — proves "which-process touched doc X" by document id.
        await AssertAuditRecordHasProcessIdAsync(
            fileId.ToString(),
            AuditActionType.Classification,
            ProcessingStage.DecisionLogic,
            expectedClearanceKeyword: "Reconcile");

        // Also verify the ReconciliationCompleted (Export stage) record.
        await AssertAuditRecordHasProcessIdAsync(
            fileId.ToString(),
            AuditActionType.Export,
            ProcessingStage.Export,
            expectedClearanceKeyword: "Reconcile");
    }

    // -------------------------------------------------------------------------
    // A6-4: Queued path — QueuedAuditLoggerService + QueuedAuditProcessorService
    // -------------------------------------------------------------------------

    /// <summary>
    /// Proves that the PRODUCTION queued audit path (<see cref="QueuedAuditLoggerService"/> writing to a
    /// <see cref="System.Threading.Channels.Channel{T}"/> drained by <see cref="QueuedAuditProcessorService"/>)
    /// eventually persists records to the database so that
    /// <see cref="IAuditLogger.GetAuditRecordsByFileIdAsync"/> can retrieve them.
    /// This is the actual path all three workers use at runtime (registered via
    /// <see cref="ServiceCollectionExtensions.AddDatabaseServices"/>).
    /// Uses a bounded-timeout poll (up to 5 s) rather than a fixed sleep to accommodate
    /// the asynchronous channel drain without coupling the test to a specific flush time.
    /// </summary>
    [Fact]
    public async Task QueuedAuditLogger_AfterDrain_RecordIsQueryableByFileId()
    {
        // Arrange — build a dedicated service provider that wires the full production stack:
        // QueuedAuditProcessorService (singleton + hosted) + QueuedAuditLoggerService (scoped).
        var services = new ServiceCollection();
        services.AddLogging();                          // ILogger<T> needed by QueuedAuditProcessorService
        services.AddOptions();                          // IOptions<T> infrastructure
        services.AddScoped<PrismaDbContext>(_ => new PrismaDbContext(_dbOptions));
        services.AddScoped<IPrismaDbContext, PrismaDbContext>();
        services.Configure<AuditOptions>(_ => { });     // defaults are fine
        services.AddSingleton<QueuedAuditProcessorService>();

        // Register QueuedAuditLoggerService so it can be resolved as IAuditLogger.
        services.AddScoped<IAuditLogger>(sp =>
        {
            var processor = sp.GetRequiredService<QueuedAuditProcessorService>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            var logger = NullLogger<QueuedAuditLoggerService>.Instance;
            return new QueuedAuditLoggerService(processor, scopeFactory, logger);
        });

        await using var queuedSp = services.BuildServiceProvider();

        // Start the background processor so it drains the channel into the DB.
        // Await StartAsync so the hosted service is confirmed running before we enqueue records.
        var processor = queuedSp.GetRequiredService<QueuedAuditProcessorService>();
        using var processorCts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await processor.StartAsync(processorCts.Token);
        // Brief pause to let the ExecuteAsync drain-loop enter its blocking ReadAllAsync wait.
        await Task.Delay(200, Ct);

        var fileId = Guid.NewGuid().ToString();
        var correlationId = Guid.NewGuid().ToString();

        // Act — log an audit record through the queued path (fire-and-forget to channel).
        using (var writeScope = queuedSp.CreateScope())
        {
            var queuedLogger = writeScope.ServiceProvider.GetRequiredService<IAuditLogger>();
            var logResult = await queuedLogger.LogAuditAsync(
                actionType: AuditActionType.Extraction,
                stage: ProcessingStage.Extraction,
                fileId: fileId,
                correlationId: correlationId,
                userId: KnownActor.ActorId,
                actionDetails: $"{{\"processId\":\"{KnownActor.ActorId}\",\"clearance\":\"Extract\"}}",
                success: true,
                errorMessage: null,
                cancellationToken: Ct,
                processId: KnownActor.ActorId);
            logResult.IsSuccess.ShouldBeTrue("Queued LogAuditAsync must succeed");
        }

        // Trigger graceful shutdown: cancel the processor CTS and await StopAsync.
        // QueuedAuditProcessorService.GetBatchesAsync catches OCE and breaks out of the loop,
        // then the final partial batch (our one record) is yielded and written via ProcessBatchAsync.
        // StopAsync completes only after ExecuteAsync exits, so the DB write is done by the time
        // we reach the assertion below — no polling needed.
        await processorCts.CancelAsync();
        try { await processor.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { /* expected on graceful drain */ }

        // Query via the DIRECT AuditLoggerService (_serviceProvider uses the same _dbOptions =
        // same physical SQL Server database as the processor's scoped PrismaDbContext).
        List<AuditRecord>? records = null;
        using (var queryScope = _serviceProvider.CreateScope())
        {
            var directLogger = queryScope.ServiceProvider.GetRequiredService<IAuditLogger>();
            var queryResult = await directLogger.GetAuditRecordsByFileIdAsync(fileId, Ct);
            if (queryResult.IsSuccess && queryResult.Value is { Count: > 0 })
            {
                records = queryResult.Value;
            }
        }

        // Assert — the queued record must have been drained into the DB.
        records.ShouldNotBeNull("Record must be visible via GetAuditRecordsByFileIdAsync after the channel drains");
        records!.ShouldNotBeEmpty("At least one audit record must be persisted for the given fileId");

        var record = records.FirstOrDefault(r =>
            r.ActionType == AuditActionType.Extraction &&
            r.Stage == ProcessingStage.Extraction);
        record.ShouldNotBeNull("Expected an Extraction/Extraction record persisted by the queued path");
        record!.ProcessId.ShouldBe(KnownActor.ActorId, "ProcessId must survive the channel round-trip");
        record.UserId.ShouldBe(KnownActor.ActorId, "UserId must survive the channel round-trip");
        record.FileId.ShouldBe(fileId, "FileId must survive the channel round-trip");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _serviceProvider.Dispose();
    }
}
