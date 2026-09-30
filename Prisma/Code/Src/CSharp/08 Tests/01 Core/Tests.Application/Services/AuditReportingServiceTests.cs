namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Unit tests for <see cref="AuditReportingService"/> covering CSV/JSON report generation paths.
/// </summary>
public class AuditReportingServiceTests
{
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<AuditReportingService> _logger;
    private readonly AuditReportingService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditReportingServiceTests"/> class with mocked audit logger.
    /// </summary>
    public AuditReportingServiceTests(ITestOutputHelper output)
    {
        _auditLogger = Substitute.For<IAuditLogger>();
        _logger = XUnitLogger.CreateLogger<AuditReportingService>(output);
        _service = new AuditReportingService(_auditLogger, _logger);
    }

    /// <summary>
    /// Tests that <see cref="AuditReportingService.GenerateClassificationReportCsvAsync"/> generates CSV correctly.
    /// </summary>
    /// <returns>A task that completes after verifying CSV content.</returns>
    [Fact]
    public async Task GenerateClassificationReportCsvAsync_ValidRecords_ReturnsCsv()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var correlationId = Guid.NewGuid().ToString();
        var fileId = Guid.NewGuid().ToString();

        var records = new List<AuditRecord>
        {
            new AuditRecord
            {
                AuditId = Guid.NewGuid().ToString(),
                CorrelationId = correlationId,
                FileId = fileId,
                ActionType = AuditActionType.Classification,
                Stage = ProcessingStage.Extraction,
                Timestamp = DateTime.UtcNow.AddDays(-1),
                Success = true,
                ActionDetails = "{\"Level1\":\"Aseguramiento\",\"Level2\":\"Bienes\",\"Confidence\":85}"
            }
        };

        _auditLogger.GetAuditRecordsAsync(
            startDate,
            endDate,
            AuditActionType.Classification,
            null,
            Arg.Any<CancellationToken>())
            .Returns(Result<List<AuditRecord>>.Success(records));

        // Act
        var result = await _service.GenerateClassificationReportCsvAsync(
            startDate,
            endDate,
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain("Timestamp");
        result.Value.ShouldContain("FileId");
        result.Value.ShouldContain("CorrelationId");
        result.Value.ShouldContain("Stage");
        result.Value.ShouldContain("Success");
        result.Value.ShouldContain(fileId);
        result.Value.ShouldContain(correlationId);
    }

    /// <summary>
    /// Tests that <see cref="AuditReportingService.GenerateClassificationReportJsonAsync"/> generates JSON correctly.
    /// </summary>
    /// <returns>A task that completes after verifying JSON content.</returns>
    [Fact]
    public async Task GenerateClassificationReportJsonAsync_ValidRecords_ReturnsJson()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var correlationId = Guid.NewGuid().ToString();
        var fileId = Guid.NewGuid().ToString();

        var records = new List<AuditRecord>
        {
            new AuditRecord
            {
                AuditId = Guid.NewGuid().ToString(),
                CorrelationId = correlationId,
                FileId = fileId,
                ActionType = AuditActionType.Classification,
                Stage = ProcessingStage.Extraction,
                Timestamp = DateTime.UtcNow.AddDays(-1),
                Success = true,
                ActionDetails = "{\"Level1\":\"Aseguramiento\",\"Confidence\":85}"
            }
        };

        _auditLogger.GetAuditRecordsAsync(
            startDate,
            endDate,
            AuditActionType.Classification,
            null,
            Arg.Any<CancellationToken>())
            .Returns(Result<List<AuditRecord>>.Success(records));

        // Act
        var result = await _service.GenerateClassificationReportJsonAsync(
            startDate,
            endDate,
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain("startDate");
        result.Value.ShouldContain("endDate");
        result.Value.ShouldContain("recordCount");
        result.Value.ShouldContain("records");
        result.Value.ShouldContain(fileId);
        result.Value.ShouldContain(correlationId);
    }

    /// <summary>
    /// Tests that <see cref="AuditReportingService.ExportAuditLogCsvAsync"/> generates CSV with all filters.
    /// </summary>
    [Fact]
    public async Task ExportAuditLogCsvAsync_WithFilters_ReturnsFilteredCsv()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var userId = "user-123";
        var correlationId = Guid.NewGuid().ToString();

        var records = new List<AuditRecord>
        {
            new AuditRecord
            {
                AuditId = Guid.NewGuid().ToString(),
                CorrelationId = correlationId,
                FileId = Guid.NewGuid().ToString(),
                ActionType = AuditActionType.Review,
                Stage = ProcessingStage.DecisionLogic,
                UserId = userId,
                Timestamp = DateTime.UtcNow.AddDays(-1),
                Success = true
            }
        };

        _auditLogger.GetAuditRecordsAsync(
            startDate,
            endDate,
            AuditActionType.Review,
            userId,
            Arg.Any<CancellationToken>())
            .Returns(Result<List<AuditRecord>>.Success(records));

        // Act
        var result = await _service.ExportAuditLogCsvAsync(
            startDate,
            endDate,
            AuditActionType.Review,
            userId,
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain("AuditId");
        result.Value.ShouldContain("ActionType");
        result.Value.ShouldContain("UserId");
        result.Value.ShouldContain(userId);
    }

    /// <summary>
    /// Tests that <see cref="AuditReportingService.ExportAuditLogJsonAsync"/> generates JSON with all filters.
    /// </summary>
    [Fact]
    public async Task ExportAuditLogJsonAsync_WithFilters_ReturnsFilteredJson()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var userId = "user-123";

        var records = new List<AuditRecord>
        {
            new AuditRecord
            {
                AuditId = Guid.NewGuid().ToString(),
                CorrelationId = Guid.NewGuid().ToString(),
                FileId = Guid.NewGuid().ToString(),
                ActionType = AuditActionType.Export,
                Stage = ProcessingStage.Export,
                UserId = userId,
                Timestamp = DateTime.UtcNow.AddDays(-1),
                Success = true
            }
        };

        _auditLogger.GetAuditRecordsAsync(
            startDate,
            endDate,
            AuditActionType.Export,
            userId,
            Arg.Any<CancellationToken>())
            .Returns(Result<List<AuditRecord>>.Success(records));

        // Act
        var result = await _service.ExportAuditLogJsonAsync(
            startDate,
            endDate,
            AuditActionType.Export,
            userId,
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain("actionType");
        result.Value.ShouldContain("userId");
        result.Value.ShouldContain("Export");
        result.Value.ShouldContain(userId);
    }

    /// <summary>
    /// Tests that <see cref="AuditReportingService.GenerateClassificationReportCsvAsync"/> validates date range.
    /// </summary>
    [Fact]
    public async Task GenerateClassificationReportCsvAsync_InvalidDateRange_ReturnsFailure()
    {
        // Act
        var result = await _service.GenerateClassificationReportCsvAsync(
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(-1), // End before start
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("EndDate");
    }

    /// <summary>
    /// Tests that <see cref="AuditReportingService.GenerateClassificationReportCsvAsync"/> handles empty results.
    /// </summary>
    [Fact]
    public async Task GenerateClassificationReportCsvAsync_NoRecords_ReturnsEmptyCsv()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        _auditLogger.GetAuditRecordsAsync(
            startDate,
            endDate,
            AuditActionType.Classification,
            null,
            Arg.Any<CancellationToken>())
            .Returns(Result<List<AuditRecord>>.Success(new List<AuditRecord>()));

        // Act
        var result = await _service.GenerateClassificationReportCsvAsync(
            startDate,
            endDate,
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain("Timestamp"); // Header should be present
    }

    /// <summary>
    /// Tests that CSV export properly escapes special characters.
    /// </summary>
    [Fact]
    public async Task ExportAuditLogCsvAsync_SpecialCharacters_EscapesCorrectly()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var fileId = "file\"with,quotes";

        var records = new List<AuditRecord>
        {
            new AuditRecord
            {
                AuditId = Guid.NewGuid().ToString(),
                CorrelationId = Guid.NewGuid().ToString(),
                FileId = fileId,
                ActionType = AuditActionType.Download,
                Stage = ProcessingStage.Ingestion,
                Timestamp = DateTime.UtcNow.AddDays(-1),
                Success = true,
                ActionDetails = "Test\"details,with\"commas"
            }
        };

        _auditLogger.GetAuditRecordsAsync(
            startDate,
            endDate,
            null,
            null,
            Arg.Any<CancellationToken>())
            .Returns(Result<List<AuditRecord>>.Success(records));

        // Act
        var result = await _service.ExportAuditLogCsvAsync(
            startDate,
            endDate,
            null,
            null,
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        // CSV should properly escape quotes
        result.Value.ShouldContain("\"\"");
    }

    /// <summary>
    /// Tests that cancellation is handled correctly.
    /// </summary>
    [Fact]
    public async Task GenerateClassificationReportCsvAsync_CancellationRequested_ReturnsCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _service.GenerateClassificationReportCsvAsync(
            DateTime.UtcNow.AddDays(-7),
            DateTime.UtcNow,
            cts.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }
}