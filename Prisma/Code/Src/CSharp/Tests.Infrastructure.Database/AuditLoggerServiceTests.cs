namespace ExxerCube.Prisma.Tests.Infrastructure.Database;

/// <summary>
/// Unit tests for <see cref="AuditLoggerService"/>.
/// </summary>
public class AuditLoggerServiceTests : IDisposable
{
    private readonly PrismaDbContext _dbContext;
    private readonly ILogger<AuditLoggerService> _logger;
    private readonly AuditLoggerService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLoggerServiceTests"/> class.
    /// </summary>
    public AuditLoggerServiceTests(ITestOutputHelper output)
    {
        var options = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PrismaDbContext(options);
        _dbContext.Database.EnsureCreated();
        _logger = XUnitLogger.CreateLogger<AuditLoggerService>(output);
        _service = new AuditLoggerService(_dbContext, _logger);
    }

    /// <summary>
    /// Tests that <see cref="AuditLoggerService.LogAuditAsync"/> successfully logs audit records.
    /// </summary>
    [Fact]
    public async Task LogAuditAsync_ValidRecord_ReturnsSuccess()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var fileId = Guid.NewGuid().ToString();

        // Act
        var result = await _service.LogAuditAsync(
            AuditActionType.Download,
            ProcessingStage.Ingestion,
            fileId,
            correlationId,
            null,
            "{\"FileName\":\"test.pdf\"}",
            true,
            null,
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // Verify the audit record was saved
        var savedRecord = await _dbContext.AuditRecords
            .FirstOrDefaultAsync(r => r.CorrelationId == correlationId, CancellationToken.None);
        savedRecord.ShouldNotBeNull();
        savedRecord!.FileId.ShouldBe(fileId);
        savedRecord.ActionType.ShouldBe(AuditActionType.Download);
        savedRecord.Stage.ShouldBe(ProcessingStage.Ingestion);
        savedRecord.Success.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that <see cref="AuditLoggerService.LogAuditAsync"/> handles cancellation correctly.
    /// </summary>
    [Fact]
    public async Task LogAuditAsync_CancellationRequested_ReturnsCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _service.LogAuditAsync(
            AuditActionType.Download,
            ProcessingStage.Ingestion,
            null,
            Guid.NewGuid().ToString(),
            null,
            null,
            true,
            null,
            cts.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Tests that <see cref="AuditLoggerService.LogAuditAsync"/> validates correlation ID.
    /// </summary>
    [Fact]
    public async Task LogAuditAsync_EmptyCorrelationId_ReturnsFailure()
    {
        // Act
        var result = await _service.LogAuditAsync(
            AuditActionType.Download,
            ProcessingStage.Ingestion,
            null,
            string.Empty,
            null,
            null,
            true,
            null,
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("CorrelationId");
    }

    /// <summary>
    /// Tests that <see cref="AuditLoggerService.GetAuditRecordsByFileIdAsync"/> retrieves records correctly.
    /// </summary>
    [Fact]
    public async Task GetAuditRecordsByFileIdAsync_ValidFileId_ReturnsRecords()
    {
        // Arrange
        var fileId = Guid.NewGuid().ToString();
        var correlationId1 = Guid.NewGuid().ToString();
        var correlationId2 = Guid.NewGuid().ToString();

        await _service.LogAuditAsync(
            AuditActionType.Download,
            ProcessingStage.Ingestion,
            fileId,
            correlationId1,
            null,
            null,
            true,
            null,
            CancellationToken.None);

        await _service.LogAuditAsync(
            AuditActionType.Classification,
            ProcessingStage.Extraction,
            fileId,
            correlationId2,
            null,
            null,
            true,
            null,
            CancellationToken.None);

        // Act
        var result = await _service.GetAuditRecordsByFileIdAsync(fileId, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Count.ShouldBe(2);
        result.Value.All(r => r.FileId == fileId).ShouldBeTrue();
    }

    /// <summary>
    /// Tests that <see cref="AuditLoggerService.GetAuditRecordsByCorrelationIdAsync"/> retrieves records correctly.
    /// </summary>
    [Fact]
    public async Task GetAuditRecordsByCorrelationIdAsync_ValidCorrelationId_ReturnsRecords()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var fileId = Guid.NewGuid().ToString();

        await _service.LogAuditAsync(
            AuditActionType.Download,
            ProcessingStage.Ingestion,
            fileId,
            correlationId,
            null,
            null,
            true,
            null,
            CancellationToken.None);

        await _service.LogAuditAsync(
            AuditActionType.Extraction,
            ProcessingStage.Extraction,
            fileId,
            correlationId,
            null,
            null,
            true,
            null,
            CancellationToken.None);

        // Act
        var result = await _service.GetAuditRecordsByCorrelationIdAsync(correlationId, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Count.ShouldBe(2);
        result.Value.All(r => r.CorrelationId == correlationId).ShouldBeTrue();
        result.Value.OrderBy(r => r.Timestamp).First().Stage.ShouldBe(ProcessingStage.Ingestion);
    }

    /// <summary>
    /// Tests that <see cref="AuditLoggerService.GetAuditRecordsAsync"/> filters by date range correctly.
    /// </summary>
    [Fact]
    public async Task GetAuditRecordsAsync_DateRangeFilter_ReturnsFilteredRecords()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();

        // Create record first (will have current timestamp)
        await _service.LogAuditAsync(
            AuditActionType.Download,
            ProcessingStage.Ingestion,
            null,
            correlationId,
            null,
            null,
            true,
            null,
            CancellationToken.None);

        // Set date range to include the record we just created (from 2 days ago to now + 1 day to ensure it's included)
        var startDate = DateTime.UtcNow.AddDays(-2);
        var endDate = DateTime.UtcNow.AddDays(1);

        // Act
        var result = await _service.GetAuditRecordsAsync(
            startDate,
            endDate,
            null,
            null,
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        // Should include the record we just created (within range)
        result.Value!.Any(r => r.CorrelationId == correlationId).ShouldBeTrue();
    }

    /// <summary>
    /// Tests that <see cref="AuditLoggerService.GetAuditRecordsAsync"/> filters by action type correctly.
    /// </summary>
    [Fact]
    public async Task GetAuditRecordsAsync_ActionTypeFilter_ReturnsFilteredRecords()
    {
        // Arrange
        var correlationId1 = Guid.NewGuid().ToString();
        var correlationId2 = Guid.NewGuid().ToString();

        await _service.LogAuditAsync(
            AuditActionType.Download,
            ProcessingStage.Ingestion,
            null,
            correlationId1,
            null,
            null,
            true,
            null,
            CancellationToken.None);

        await _service.LogAuditAsync(
            AuditActionType.Classification,
            ProcessingStage.Extraction,
            null,
            correlationId2,
            null,
            null,
            true,
            null,
            CancellationToken.None);

        // Act
        var result = await _service.GetAuditRecordsAsync(
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            AuditActionType.Download,
            null,
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.All(r => r.ActionType == AuditActionType.Download).ShouldBeTrue();
        result.Value.Any(r => r.CorrelationId == correlationId1).ShouldBeTrue();
        result.Value.Any(r => r.CorrelationId == correlationId2).ShouldBeFalse();
    }

    /// <summary>
    /// Tests that <see cref="AuditLoggerService.GetAuditRecordsAsync"/> filters by user ID correctly.
    /// </summary>
    [Fact]
    public async Task GetAuditRecordsAsync_UserIdFilter_ReturnsFilteredRecords()
    {
        // Arrange
        var userId = "user-123";
        var correlationId1 = Guid.NewGuid().ToString();
        var correlationId2 = Guid.NewGuid().ToString();

        await _service.LogAuditAsync(
            AuditActionType.Review,
            ProcessingStage.DecisionLogic,
            null,
            correlationId1,
            userId,
            null,
            true,
            null,
            CancellationToken.None);

        await _service.LogAuditAsync(
            AuditActionType.Review,
            ProcessingStage.DecisionLogic,
            null,
            correlationId2,
            "user-456",
            null,
            true,
            null,
            CancellationToken.None);

        // Act
        var result = await _service.GetAuditRecordsAsync(
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            null,
            userId,
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.All(r => r.UserId == userId).ShouldBeTrue();
        result.Value.Any(r => r.CorrelationId == correlationId1).ShouldBeTrue();
        result.Value.Any(r => r.CorrelationId == correlationId2).ShouldBeFalse();
    }

    /// <summary>
    /// Tests that <see cref="AuditLoggerService.GetAuditRecordsAsync"/> validates date range.
    /// </summary>
    [Fact]
    public async Task GetAuditRecordsAsync_InvalidDateRange_ReturnsFailure()
    {
        // Act
        var result = await _service.GetAuditRecordsAsync(
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(-1), // End before start
            null,
            null,
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("EndDate");
    }

    /// <summary>
    /// Tests that audit logging is async and non-blocking (performance requirement).
    /// </summary>
    [Fact]
    public async Task LogAuditAsync_MultipleRecords_CompletesQuickly()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;

        // Act - Log multiple records
        var tasks = Enumerable.Range(0, 10).Select(i =>
            _service.LogAuditAsync(
                AuditActionType.Download,
                ProcessingStage.Ingestion,
                null,
                $"{correlationId}-{i}",
                null,
                null,
                true,
                null,
                CancellationToken.None));

        await Task.WhenAll(tasks);

        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        // Assert - Should complete quickly (less than 1 second for 10 records)
        duration.TotalSeconds.ShouldBeLessThan(1.0);

        // Verify all records were saved
        var count = await _dbContext.AuditRecords.CountAsync(r => r.CorrelationId.StartsWith(correlationId), CancellationToken.None);
        count.ShouldBe(10);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}