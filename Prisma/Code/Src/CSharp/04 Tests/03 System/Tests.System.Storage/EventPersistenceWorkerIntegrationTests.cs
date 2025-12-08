using System.Text.Json;
using System.Text.Json.Serialization;
using ExxerCube.Prisma.Application.Services;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Infrastructure.Database.EntityFramework;
using ExxerCube.Prisma.Infrastructure.Database.Services;
using ExxerCube.Prisma.Infrastructure.Events;
using ExxerCube.Prisma.Testing.Infrastructure;
using ExxerCube.Prisma.Testing.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Tests.System.Storage;

/// <summary>
/// Integration tests for <see cref="EventPersistenceWorker"/> with real database operations.
/// Tests that domain events are correctly persisted to the AuditRecords table.
/// Uses containerized SQL Server via SqlServerContainerFixture.
/// </summary>
[Collection("DatabaseInfrastructure")]
public class EventPersistenceWorkerIntegrationTests : IDisposable
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly DbContextOptions<PrismaDbContext> _dbOptions;
    private readonly IEventPublisher _eventPublisher;
    private readonly EventPersistenceWorker _worker;
    private readonly ServiceProvider _serviceProvider;
    private readonly ILogger<EventPersistenceWorker> _logger;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// JSON serializer options for deserializing events - must match EventPersistenceWorker.
    /// Uses PascalCase naming (default) to match C# property names exactly.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null, // PascalCase (default)
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="EventPersistenceWorkerIntegrationTests"/> class.
    /// </summary>
    public EventPersistenceWorkerIntegrationTests(SqlServerContainerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;

        // Ensure SQL Server container is available
        _fixture.EnsureAvailable();

        // Set up SQL Server Container with real connection string
        _dbOptions = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseSqlServer(_fixture.ConnectionString)
            .Options;

        // Apply EF Core database creation strategy using DatabaseFacade API
        // Strategy: Use EnsureCreatedAsync for tests (creates schema from model without migrations)
        // This is faster and doesn't require maintaining migration files for test databases
        using (var context = new PrismaDbContext(_dbOptions))
        {
            var database = context.Database;

            // EnsureCreatedAsync creates the database schema from the current model
            // Idempotent - safe to call multiple times (only creates if not exists)
            // Better for tests than Migrate() which requires migration files
            database.EnsureCreatedAsync(TestContext.Current.CancellationToken)
                .GetAwaiter()
                .GetResult();
        }

        // Clean database before each test to ensure isolated test state
        _fixture.CleanDatabaseAsync().GetAwaiter().GetResult();

        // Set up service collection for dependency injection
        var services = new ServiceCollection();

        // Register DbContext as scoped (not singleton!) so each scope gets its own instance
        services.AddScoped<IPrismaDbContext>(_ => new PrismaDbContext(_dbOptions));
        services.AddScoped<PrismaDbContext>(_ => new PrismaDbContext(_dbOptions));

        services.AddSingleton<IEventPublisher, EventPublisher>(sp =>
            new EventPublisher(XUnitLogger.CreateLogger<EventPublisher>(output)));
        services.AddSingleton<ILogger<EventPersistenceWorker>>(XUnitLogger.CreateLogger<EventPersistenceWorker>(output));

        _serviceProvider = services.BuildServiceProvider();

        _eventPublisher = _serviceProvider.GetRequiredService<IEventPublisher>();
        _logger = _serviceProvider.GetRequiredService<ILogger<EventPersistenceWorker>>();

        // Create service scope factory
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();

        _worker = new EventPersistenceWorker(_eventPublisher, _logger, scopeFactory);
    }

    /// <summary>
    /// Creates a FileMetadata record to satisfy foreign key constraints.
    /// Real database reveals FK constraints that InMemory database ignores!
    /// </summary>
    private async Task CreateFileMetadataAsync(Guid fileId)
    {
        using var context = new PrismaDbContext(_dbOptions);
        context.FileMetadata.Add(new FileMetadata
        {
            FileId = fileId.ToString(),
            FileName = $"test-file-{fileId}.pdf",
            DownloadDateTime = DateTime.UtcNow,
            Format = FileFormat.Pdf
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Tests that <see cref="DocumentDownloadedEvent"/> is persisted to AuditRecords.
    /// </summary>
    [Fact]
    public async Task PersistEvent_DocumentDownloadedEvent_SavesToDatabase()
    {
        // Arrange - Start worker FIRST
        await _worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken); // Wait for worker to be ready

        var correlationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        // Create FileMetadata to satisfy FK constraint
        await CreateFileMetadataAsync(fileId);

        var evt = new DocumentDownloadedEvent
        {
            FileId = fileId,
            FileName = "test.pdf",
            Source = "SIARA",
            FileSizeBytes = 1024,
            Format = FileFormat.Pdf,
            DownloadUrl = "https://test.com/file.pdf",
            CorrelationId = correlationId
        };

        // Act
        _eventPublisher.Publish(evt);
        await Task.Delay(1000, TestContext.Current.CancellationToken); // Wait for async persistence

        // Assert - Create new DbContext to query (InMemory database is shared by database name)
        using var queryContext = new PrismaDbContext(_dbOptions);
        var auditRecords = await queryContext.AuditRecords.ToListAsync(TestContext.Current.CancellationToken);
        auditRecords.Count.ShouldBe(1);

        var record = auditRecords[0];
        record.CorrelationId.ShouldBe(correlationId.ToString());
        record.FileId.ShouldBe(fileId.ToString());
        record.ActionType.ShouldBe(AuditActionType.Download);
        record.Stage.ShouldBe(ProcessingStage.Ingestion);
        record.Success.ShouldBeTrue();
        record.ErrorMessage.ShouldBeNull();
        record.ActionDetails.ShouldNotBeEmpty();

        // Verify event details are serialized
        var deserializedEvent = JsonSerializer.Deserialize<DocumentDownloadedEvent>(record.ActionDetails, JsonOptions);
        deserializedEvent.ShouldNotBeNull();
        deserializedEvent!.FileId.ShouldBe(fileId);
        deserializedEvent.FileName.ShouldBe("test.pdf");
    }

    /// <summary>
    /// Tests that <see cref="OcrCompletedEvent"/> is persisted with correct mapping.
    /// </summary>
    [Fact]
    public async Task PersistEvent_OcrCompletedEvent_MapsCorrectly()
    {
        // Arrange - Start worker FIRST
        await _worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var fileId = Guid.NewGuid();
        await CreateFileMetadataAsync(fileId);

        var evt = new OcrCompletedEvent
        {
            FileId = fileId,
            OcrEngine = "Tesseract",
            Confidence = 95.5m,
            ExtractedTextLength = 2500,
            ProcessingTime = TimeSpan.FromMilliseconds(1500),
            FallbackTriggered = false
        };

        // Act
        _eventPublisher.Publish(evt);
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Assert
        using var queryContext = new PrismaDbContext(_dbOptions);
        var auditRecords = await queryContext.AuditRecords.ToListAsync(TestContext.Current.CancellationToken);
        auditRecords.Count.ShouldBe(1);

        var record = auditRecords[0];
        record.FileId.ShouldBe(fileId.ToString());
        record.ActionType.ShouldBe(AuditActionType.Extraction);
        record.Stage.ShouldBe(ProcessingStage.Extraction);
        record.Success.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that <see cref="ClassificationCompletedEvent"/> is persisted with correct mapping.
    /// </summary>
    [Fact]
    public async Task PersistEvent_ClassificationCompletedEvent_MapsCorrectly()
    {
        // Arrange - Start worker FIRST
        await _worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var fileId = Guid.NewGuid();
        await CreateFileMetadataAsync(fileId);

        var evt = new ClassificationCompletedEvent
        {
            FileId = fileId,
            RequirementTypeId = 5,
            RequirementTypeName = "Aseguramiento",
            Confidence = 92,
            RequiresManualReview = false
        };

        // Act
        _eventPublisher.Publish(evt);
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Assert
        using var queryContext = new PrismaDbContext(_dbOptions);
        var auditRecords = await queryContext.AuditRecords.ToListAsync(TestContext.Current.CancellationToken);
        auditRecords.Count.ShouldBe(1);

        var record = auditRecords[0];
        record.FileId.ShouldBe(fileId.ToString());
        record.ActionType.ShouldBe(AuditActionType.Classification);
        record.Stage.ShouldBe(ProcessingStage.DecisionLogic);
        record.Success.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that <see cref="ProcessingErrorEvent"/> is persisted with Success=false and ErrorMessage.
    /// </summary>
    [Fact]
    public async Task PersistEvent_ProcessingErrorEvent_MarksAsFailure()
    {
        // Arrange - Start worker FIRST
        await _worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var fileId = Guid.NewGuid();
        await CreateFileMetadataAsync(fileId);

        var evt = new ProcessingErrorEvent
        {
            FileId = fileId,
            ErrorMessage = "Failed to process PDF",
            StackTrace = "at System.IO.File.ReadAllBytes(String path)",
            Component = "OCR"
        };

        // Act
        _eventPublisher.Publish(evt);
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Assert
        using var queryContext = new PrismaDbContext(_dbOptions);
        var auditRecords = await queryContext.AuditRecords.ToListAsync(TestContext.Current.CancellationToken);
        auditRecords.Count.ShouldBe(1);

        var record = auditRecords[0];
        record.FileId.ShouldBe(fileId.ToString());
        record.ActionType.ShouldBe(AuditActionType.Other);
        record.Stage.ShouldBe(ProcessingStage.Unknown);
        record.Success.ShouldBeFalse();
        record.ErrorMessage.ShouldBe("Failed to process PDF");
    }

    /// <summary>
    /// Tests that multiple events are persisted in order.
    /// </summary>
    [Fact]
    public async Task PersistEvents_MultipleEvents_AllSavedInOrder()
    {
        // Arrange - Start worker FIRST
        await _worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var fileId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        await CreateFileMetadataAsync(fileId);

        var downloadEvent = new DocumentDownloadedEvent
        {
            FileId = fileId,
            FileName = "test.pdf",
            Source = "SIARA",
            CorrelationId = correlationId
        };

        var ocrEvent = new OcrCompletedEvent
        {
            FileId = fileId,
            OcrEngine = "Tesseract",
            Confidence = 95.5m,
            CorrelationId = correlationId
        };

        var classificationEvent = new ClassificationCompletedEvent
        {
            FileId = fileId,
            RequirementTypeId = 5,
            RequirementTypeName = "Aseguramiento",
            Confidence = 92,
            CorrelationId = correlationId
        };

        // Act
        _eventPublisher.Publish(downloadEvent);
        _eventPublisher.Publish(ocrEvent);
        _eventPublisher.Publish(classificationEvent);
        await Task.Delay(1000, TestContext.Current.CancellationToken); // Wait for all async persistence

        // Assert
        using var queryContext = new PrismaDbContext(_dbOptions);
        var auditRecords = await queryContext.AuditRecords
            .OrderBy(r => r.Timestamp)
            .ToListAsync(TestContext.Current.CancellationToken);

        auditRecords.Count.ShouldBe(3);

        // All should have same FileId and CorrelationId
        auditRecords.ShouldAllBe(r => r.FileId == fileId.ToString());
        auditRecords.ShouldAllBe(r => r.CorrelationId == correlationId.ToString());

        // Verify action types match event order
        auditRecords[0].ActionType.ShouldBe(AuditActionType.Download);
        auditRecords[1].ActionType.ShouldBe(AuditActionType.Extraction);
        auditRecords[2].ActionType.ShouldBe(AuditActionType.Classification);
    }

    /// <summary>
    /// Tests that <see cref="ConflictDetectedEvent"/> is persisted correctly.
    /// </summary>
    [Fact]
    public async Task PersistEvent_ConflictDetectedEvent_MapsCorrectly()
    {
        // Arrange - Start worker FIRST
        await _worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var fileId = Guid.NewGuid();
        await CreateFileMetadataAsync(fileId);

        var evt = new ConflictDetectedEvent
        {
            FileId = fileId,
            FieldName = "InvoiceNumber",
            XmlValue = "INV-12345",
            OcrValue = "INV-12346",
            SimilarityScore = 0.92m,
            ConflictSeverity = "Medium"
        };

        // Act
        _eventPublisher.Publish(evt);
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Assert
        using var queryContext = new PrismaDbContext(_dbOptions);
        var auditRecords = await queryContext.AuditRecords.ToListAsync(TestContext.Current.CancellationToken);
        auditRecords.Count.ShouldBe(1);

        var record = auditRecords[0];
        record.FileId.ShouldBe(fileId.ToString());
        record.ActionType.ShouldBe(AuditActionType.Review);
        record.Stage.ShouldBe(ProcessingStage.DecisionLogic);
        record.Success.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that <see cref="DocumentFlaggedForReviewEvent"/> is persisted correctly.
    /// Defensive Intelligence: Flagged documents are tracked, not rejected.
    /// </summary>
    [Fact]
    public async Task PersistEvent_DocumentFlaggedForReview_TracksDefensiveIntelligence()
    {
        // Arrange - Start worker FIRST
        await _worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var fileId = Guid.NewGuid();
        await CreateFileMetadataAsync(fileId);

        var evt = new DocumentFlaggedForReviewEvent
        {
            FileId = fileId,
            Reasons = new List<string> { "Low OCR confidence", "Missing required fields" },
            Priority = "High"
        };

        // Act
        _eventPublisher.Publish(evt);
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Assert
        using var queryContext = new PrismaDbContext(_dbOptions);
        var auditRecords = await queryContext.AuditRecords.ToListAsync(TestContext.Current.CancellationToken);
        auditRecords.Count.ShouldBe(1);

        var record = auditRecords[0];
        record.FileId.ShouldBe(fileId.ToString());
        record.ActionType.ShouldBe(AuditActionType.Review);
        record.Stage.ShouldBe(ProcessingStage.DecisionLogic);
        record.Success.ShouldBeTrue(); // Flagged for review is not an error
    }

    /// <summary>
    /// Tests that <see cref="DocumentProcessingCompletedEvent"/> is persisted correctly.
    /// </summary>
    [Fact]
    public async Task PersistEvent_DocumentProcessingCompletedEvent_MapsCorrectly()
    {
        // Arrange - Start worker FIRST
        await _worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var fileId = Guid.NewGuid();
        await CreateFileMetadataAsync(fileId);

        var evt = new DocumentProcessingCompletedEvent
        {
            FileId = fileId,
            TotalProcessingTime = TimeSpan.FromSeconds(45),
            AutoProcessed = true
        };

        // Act
        _eventPublisher.Publish(evt);
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Assert
        using var queryContext = new PrismaDbContext(_dbOptions);
        var auditRecords = await queryContext.AuditRecords.ToListAsync(TestContext.Current.CancellationToken);
        auditRecords.Count.ShouldBe(1);

        var record = auditRecords[0];
        record.FileId.ShouldBe(fileId.ToString());
        record.ActionType.ShouldBe(AuditActionType.Export);
        record.Stage.ShouldBe(ProcessingStage.Export);
        record.Success.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that event with null FileId (system-level error) is handled correctly.
    /// Defensive Intelligence: System continues even when FileId is null.
    /// </summary>
    [Fact]
    public async Task PersistEvent_SystemLevelError_AllowsNullFileId()
    {
        // Arrange - Start worker FIRST
        await _worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var evt = new ProcessingErrorEvent
        {
            FileId = null, // System-level error
            ErrorMessage = "Database connection timeout",
            Component = "Storage"
        };

        // Act
        _eventPublisher.Publish(evt);
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Assert
        using var queryContext = new PrismaDbContext(_dbOptions);
        var auditRecords = await queryContext.AuditRecords.ToListAsync(TestContext.Current.CancellationToken);
        auditRecords.Count.ShouldBe(1);

        var record = auditRecords[0];
        record.FileId.ShouldBeNullOrEmpty();
        record.ActionType.ShouldBe(AuditActionType.Other);
        record.Stage.ShouldBe(ProcessingStage.Unknown);
        record.Success.ShouldBeFalse();
        record.ErrorMessage.ShouldBe("Database connection timeout");
    }

    /// <summary>
    /// Tests that worker can be stopped gracefully.
    /// </summary>
    [Fact]
    public async Task Worker_StopAsync_StopsGracefully()
    {
        // Arrange
        await _worker.StartAsync(TestContext.Current.CancellationToken);
        var fileId = Guid.NewGuid();
        await CreateFileMetadataAsync(fileId);

        var evt1 = new DocumentDownloadedEvent
        {
            FileId = fileId,
            FileName = "before-stop.pdf",
            Source = "SIARA"
        };

        // Act - Publish event before stopping
        _eventPublisher.Publish(evt1);
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Stop worker
        await _worker.StopAsync(TestContext.Current.CancellationToken);

        // Publish event after stopping
        var evt2 = new DocumentDownloadedEvent
        {
            FileId = fileId,
            FileName = "after-stop.pdf",
            Source = "SIARA"
        };
        _eventPublisher.Publish(evt2);
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Assert - Only first event should be persisted
        using var queryContext = new PrismaDbContext(_dbOptions);
        var auditRecords = await queryContext.AuditRecords.ToListAsync(TestContext.Current.CancellationToken);
        auditRecords.Count.ShouldBe(1);

        var actionDetails = auditRecords[0].ActionDetails;
        actionDetails.ShouldNotBeNullOrEmpty();
        var deserializedEvent = JsonSerializer.Deserialize<DocumentDownloadedEvent>(actionDetails!, JsonOptions);
        deserializedEvent.ShouldNotBeNull();
        deserializedEvent!.FileName.ShouldBe("before-stop.pdf");
    }

    /// <summary>
    /// Tests that events preserve CorrelationId for distributed tracing.
    /// </summary>
    [Fact]
    public async Task PersistEvent_WithCorrelationId_PreservesTracing()
    {
        // Arrange - Start worker FIRST
        await _worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var fileId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        await CreateFileMetadataAsync(fileId);

        var evt = new DocumentDownloadedEvent
        {
            FileId = fileId,
            FileName = "traced.pdf",
            Source = "SIARA",
            CorrelationId = correlationId
        };

        // Act
        _eventPublisher.Publish(evt);
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Assert
        using var queryContext = new PrismaDbContext(_dbOptions);
        var auditRecords = await queryContext.AuditRecords.ToListAsync(TestContext.Current.CancellationToken);
        auditRecords.Count.ShouldBe(1);

        var record = auditRecords[0];
        record.CorrelationId.ShouldBe(correlationId.ToString());

        // CorrelationId enables distributed tracing across services
        Guid.Parse(record.CorrelationId).ShouldBe(correlationId);
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        _worker.StopAsync(CancellationToken.None).Wait();
        _worker.Dispose();
        _serviceProvider.Dispose();
    }
}