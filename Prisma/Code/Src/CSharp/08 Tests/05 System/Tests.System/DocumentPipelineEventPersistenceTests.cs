using System.Text.Json;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Infrastructure.Database.Services;
using ExxerCube.Prisma.Infrastructure.Events;

namespace ExxerCube.Prisma.Tests.System.Ocr.Pipeline;

/// <summary>
/// System tests for event persistence during full document processing pipeline.
/// Verifies that domain events published by DocumentIngestionService and OcrProcessingService
/// are correctly persisted to the database by EventPersistenceWorker.
/// </summary>
public class DocumentPipelineEventPersistenceTests : IDisposable
{
    private readonly DbContextOptions<PrismaDbContext> _dbOptions;
    private readonly IEventPublisher _eventPublisher;
    private readonly EventPersistenceWorker _worker;
    private readonly ServiceProvider _serviceProvider;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentPipelineEventPersistenceTests"/> class.
    /// </summary>
    public DocumentPipelineEventPersistenceTests(ITestOutputHelper output)
    {
        _output = output;

        // Set up InMemory database
        _dbOptions = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Set up dependency injection
        var services = new ServiceCollection();

        // Register DbContext as scoped
        services.AddScoped<IPrismaDbContext>(_ => new PrismaDbContext(_dbOptions));
        services.AddScoped<PrismaDbContext>(_ => new PrismaDbContext(_dbOptions));

        // Register EventPublisher as singleton
        services.AddSingleton<IEventPublisher, EventPublisher>(sp =>
            new EventPublisher(XUnitLogger.CreateLogger<EventPublisher>(output)));

        services.AddSingleton<ILogger<EventPersistenceWorker>>(
            XUnitLogger.CreateLogger<EventPersistenceWorker>(output));

        _serviceProvider = services.BuildServiceProvider();

        _eventPublisher = _serviceProvider.GetRequiredService<IEventPublisher>();
        var logger = _serviceProvider.GetRequiredService<ILogger<EventPersistenceWorker>>();
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();

        _worker = new EventPersistenceWorker(_eventPublisher, logger, scopeFactory);
    }

    /// <summary>
    /// Tests that DocumentDownloadedEvent published during document ingestion is persisted to database.
    /// </summary>
    [Fact]
    public async Task DocumentIngestion_PublishesEvent_PersistsToDatabase()
    {
        // Arrange - Start worker
        await _worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken); // Wait for worker to be ready

        var correlationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        // Simulate DocumentIngestionService publishing DocumentDownloadedEvent
        var evt = new DocumentDownloadedEvent
        {
            FileId = fileId,
            FileName = "siara-regulation-001.pdf",
            Source = "SIARA",
            FileSizeBytes = 2048576, // 2MB
            Format = FileFormat.Pdf,
            DownloadUrl = "https://siara.gob.mx/regulations/001.pdf",
            CorrelationId = correlationId
        };

        // Act
        _eventPublisher.Publish(evt);
        await Task.Delay(1000, TestContext.Current.CancellationToken); // Wait for async persistence

        // Assert
        var auditRecords = await WaitForAuditRecordsAsync(
            ctx => ctx.AuditRecords.Where(r => r.FileId == fileId.ToString()),
            expectedCount: 1,
            timeout: TimeSpan.FromSeconds(2));

        auditRecords.Count.ShouldBe(1, "Expected exactly one audit record for the ingested document");

        var record = auditRecords[0];
        record.CorrelationId.ShouldBe(correlationId.ToString(), "CorrelationId should match for distributed tracing");
        record.FileId.ShouldBe(fileId.ToString(), "FileId should match the ingested file");
        record.ActionType.ShouldBe(AuditActionType.Download, "ActionType should be Download for ingestion");
        record.Stage.ShouldBe(ProcessingStage.Ingestion, "Processing stage should be Ingestion");
        record.Success.ShouldBeTrue("Event should be marked as successful");
        record.ErrorMessage.ShouldBeNull("No error message for successful event");
        record.ActionDetails.ShouldNotBeEmpty("Event details should be serialized");

        // Verify event details are correctly serialized
        var deserializedEvent = JsonSerializer.Deserialize<DocumentDownloadedEvent>(record.ActionDetails);
        deserializedEvent.ShouldNotBeNull("Event should deserialize successfully");
        deserializedEvent!.FileId.ShouldBe(fileId, "Deserialized FileId should match");
        deserializedEvent.FileName.ShouldBe("siara-regulation-001.pdf", "Deserialized FileName should match");
        deserializedEvent.Source.ShouldBe("SIARA", "Deserialized Source should match");
        deserializedEvent.FileSizeBytes.ShouldBe(2048576, "Deserialized file size should match");
        deserializedEvent.Format.ShouldBe(FileFormat.Pdf, "Deserialized format should match");
    }

    /// <summary>
    /// Tests that OcrCompletedEvent published during OCR processing is persisted to database.
    /// </summary>
    [Fact]
    public async Task OcrProcessing_PublishesEvent_PersistsToDatabase()
    {
        // Arrange - Start worker
        await _worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var fileId = Guid.NewGuid();

        // Simulate OcrProcessingService publishing OcrCompletedEvent
        var evt = new OcrCompletedEvent
        {
            FileId = fileId,
            OcrEngine = "Tesseract/GOT-OCR2",
            Confidence = 92.5m,
            ExtractedTextLength = 15234,
            ProcessingTime = TimeSpan.FromMilliseconds(2500),
            FallbackTriggered = false
        };

        // Act
        _eventPublisher.Publish(evt);
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Assert
        var auditRecords = await WaitForAuditRecordsAsync(
            ctx => ctx.AuditRecords.Where(r => r.FileId == fileId.ToString()),
            expectedCount: 1,
            timeout: TimeSpan.FromSeconds(2));

        auditRecords.Count.ShouldBe(1, "Expected exactly one audit record for OCR processing");

        var record = auditRecords[0];
        record.FileId.ShouldBe(fileId.ToString(), "FileId should match the processed file");
        record.ActionType.ShouldBe(AuditActionType.Extraction, "ActionType should be Extraction for OCR");
        record.Stage.ShouldBe(ProcessingStage.Extraction, "Processing stage should be Extraction");
        record.Success.ShouldBeTrue("Event should be marked as successful");
        record.ActionDetails.ShouldNotBeEmpty("Event details should be serialized");

        // Verify OCR metrics are serialized
        var deserializedEvent = JsonSerializer.Deserialize<OcrCompletedEvent>(record.ActionDetails!);
        deserializedEvent.ShouldNotBeNull("Event should deserialize successfully");
        deserializedEvent!.OcrEngine.ShouldBe("Tesseract/GOT-OCR2", "OCR engine should match");
        deserializedEvent.Confidence.ShouldBe(92.5m, "Confidence should match");
        deserializedEvent.ExtractedTextLength.ShouldBe(15234, "Extracted text length should match");
        deserializedEvent.ProcessingTime.ShouldBe(TimeSpan.FromMilliseconds(2500), "Processing time should match");
        deserializedEvent.FallbackTriggered.ShouldBeFalse("Fallback flag should match");
    }

    /// <summary>
    /// Tests that multiple events published in sequence maintain correct ordering and correlation.
    /// Simulates ingestion and OCR pipeline stages.
    /// </summary>
    [Fact]
    public async Task FullPipeline_PublishesMultipleEvents_AllPersistWithCorrectOrdering()
    {
        // Arrange - Start worker
        await _worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var correlationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        // Act - Simulate ingestion and OCR pipeline events
        var downloadEvent = new DocumentDownloadedEvent
        {
            FileId = fileId,
            FileName = "pipeline-test.pdf",
            Source = "SIARA",
            FileSizeBytes = 1024,
            Format = FileFormat.Pdf,
            DownloadUrl = "https://test.com/pipeline-test.pdf",
            CorrelationId = correlationId
        };
        _eventPublisher.Publish(downloadEvent);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var ocrEvent = new OcrCompletedEvent
        {
            FileId = fileId,
            OcrEngine = "Tesseract",
            Confidence = 95.0m,
            ExtractedTextLength = 5000,
            ProcessingTime = TimeSpan.FromSeconds(2),
            FallbackTriggered = false,
            CorrelationId = correlationId
        };
        _eventPublisher.Publish(ocrEvent);

        // Wait for all events to be persisted
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Assert - Verify all events persisted with same correlation ID
        var auditRecords = await WaitForAuditRecordsAsync(
            ctx => ctx.AuditRecords
                .Where(r => r.CorrelationId == correlationId.ToString())
                .OrderBy(r => r.Timestamp),
            expectedCount: 2,
            timeout: TimeSpan.FromSeconds(2));

        auditRecords.Count.ShouldBe(2, "Both ingestion and OCR events should be persisted");

        // Verify ordering: Download -> OCR
        auditRecords[0].ActionType.ShouldBe(AuditActionType.Download, "First event should be Download");
        auditRecords[0].Stage.ShouldBe(ProcessingStage.Ingestion, "First stage should be Ingestion");

        auditRecords[1].ActionType.ShouldBe(AuditActionType.Extraction, "Second event should be Extraction (OCR)");
        auditRecords[1].Stage.ShouldBe(ProcessingStage.Extraction, "Second stage should be Extraction");

        // Verify all events share same FileId and CorrelationId
        auditRecords.All(r => r.FileId == fileId.ToString()).ShouldBeTrue("All events should share same FileId");
        auditRecords.All(r => r.CorrelationId == correlationId.ToString()).ShouldBeTrue("All events should share same CorrelationId");

        // Verify timestamps are in ascending order
        (auditRecords[1].Timestamp >= auditRecords[0].Timestamp).ShouldBeTrue(
            "OCR event timestamp should be >= download event timestamp");
    }

    /// <summary>
    /// Tests that events with different correlation IDs are kept separate for independent processing workflows.
    /// </summary>
    [Fact]
    public async Task MultipleIndependentWorkflows_DifferentCorrelationIds_EventsKeptSeparate()
    {
        // Arrange - Start worker
        await _worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var correlationId1 = Guid.NewGuid();
        var correlationId2 = Guid.NewGuid();
        var fileId1 = Guid.NewGuid();
        var fileId2 = Guid.NewGuid();

        // Act - Publish events for two independent workflows
        var event1 = new DocumentDownloadedEvent
        {
            FileId = fileId1,
            FileName = "workflow1.pdf",
            Source = "SIARA",
            CorrelationId = correlationId1
        };

        var event2 = new DocumentDownloadedEvent
        {
            FileId = fileId2,
            FileName = "workflow2.pdf",
            Source = "SIARA",
            CorrelationId = correlationId2
        };

        _eventPublisher.Publish(event1);
        _eventPublisher.Publish(event2);
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Assert
        var workflow1Records = await WaitForAuditRecordsAsync(
            ctx => ctx.AuditRecords.Where(r => r.CorrelationId == correlationId1.ToString()),
            expectedCount: 1,
            timeout: TimeSpan.FromSeconds(2));

        var workflow2Records = await WaitForAuditRecordsAsync(
            ctx => ctx.AuditRecords.Where(r => r.CorrelationId == correlationId2.ToString()),
            expectedCount: 1,
            timeout: TimeSpan.FromSeconds(2));

        workflow1Records.Count.ShouldBe(1, "Workflow 1 should have exactly 1 event");
        workflow2Records.Count.ShouldBe(1, "Workflow 2 should have exactly 1 event");

        workflow1Records[0].FileId.ShouldBe(fileId1.ToString(), "Workflow 1 should track file 1");
        workflow2Records[0].FileId.ShouldBe(fileId2.ToString(), "Workflow 2 should track file 2");
    }

    /// <summary>
    /// Cleans up resources.
    /// </summary>
    public void Dispose()
    {
        _worker.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        _worker.Dispose();
        _serviceProvider.Dispose();
    }

    private async Task<List<AuditRecord>> WaitForAuditRecordsAsync(
        Func<PrismaDbContext, IQueryable<AuditRecord>> queryFactory,
        int expectedCount,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (true)
        {
            using var queryContext = new PrismaDbContext(_dbOptions);
            var records = await queryFactory(queryContext).ToListAsync(TestContext.Current.CancellationToken);

            if (records.Count >= expectedCount || DateTime.UtcNow >= deadline)
            {
                return records;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }
}
