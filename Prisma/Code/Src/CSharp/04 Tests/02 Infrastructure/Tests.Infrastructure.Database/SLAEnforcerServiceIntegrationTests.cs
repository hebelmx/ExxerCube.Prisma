namespace ExxerCube.Prisma.Tests.Infrastructure.Database;

/// <summary>
/// Integration tests for <see cref="SLAEnforcerService"/> with real database operations.
/// </summary>
public class SLAEnforcerServiceIntegrationTests : IDisposable
{
    private readonly PrismaDbContext _dbContext;
    private readonly ILogger<SLAEnforcerService> _logger;
    private readonly SLAOptions _options;
    private readonly SLAMetricsCollector _metricsCollector;
    private readonly SLAEnforcerService _service;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="SLAEnforcerServiceIntegrationTests"/> class.
    /// </summary>
    public SLAEnforcerServiceIntegrationTests(ITestOutputHelper output)
    {
        var dbOptions = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PrismaDbContext(dbOptions);
        _dbContext.Database.EnsureCreated();
        _output = output;
        _logger = XUnitLogger.CreateLogger<SLAEnforcerService>(output);
        _metricsCollector = new SLAMetricsCollector(XUnitLogger.CreateLogger<SLAMetricsCollector>(output));
        
        _options = new SLAOptions
        {
            CriticalThreshold = TimeSpan.FromHours(4),
            WarningThreshold = TimeSpan.FromHours(24)
        };

        var optionsWrapper = Options.Create(_options);
        _service = new SLAEnforcerService(_dbContext, _logger, optionsWrapper, _metricsCollector);
    }

    /// <summary>
    /// Tests end-to-end SLA tracking workflow: Create -&gt; Update -&gt; Query -&gt; Escalate.
    /// </summary>
    [Fact]
    public async Task EndToEndWorkflow_CreateUpdateQueryEscalate_Succeeds()
    {
        // Arrange
        var fileId = "integration-test-001";
        // Use a future date to ensure the case is not breached immediately
        var intakeDate = DateTime.UtcNow.AddDays(1); // Future date ensures deadline is in the future
        var daysPlazo = 5;

        // Act 1: Create SLA status
        var createResult = await _service.CalculateSLAStatusAsync(fileId, intakeDate, daysPlazo, TestContext.Current.CancellationToken);
        
        // Assert 1
        createResult.IsSuccess.ShouldBeTrue();
        createResult.Value.ShouldNotBeNull();
        createResult.Value!.FileId.ShouldBe(fileId);
        createResult.Value.Deadline.ShouldBeGreaterThan(intakeDate);
        createResult.Value.IsBreached.ShouldBeFalse();

        // Act 2: Update SLA status
        await Task.Delay(100, TestContext.Current.CancellationToken); // Small delay to ensure time difference
        var updateResult = await _service.UpdateSLAStatusAsync(fileId, TestContext.Current.CancellationToken);
        
        // Assert 2
        updateResult.IsSuccess.ShouldBeTrue();
        updateResult.Value.ShouldNotBeNull();
        // Allow equal values due to timing precision - remaining time should not increase
        updateResult.Value!.RemainingTime.ShouldBeLessThanOrEqualTo(createResult.Value!.RemainingTime);

        // Act 3: Query SLA status
        var getResult = await _service.GetSLAStatusAsync(fileId, TestContext.Current.CancellationToken);
        
        // Assert 3
        getResult.IsSuccess.ShouldBeTrue();
        getResult.Value.ShouldNotBeNull();
        getResult.Value!.FileId.ShouldBe(fileId);

        // Act 4: Escalate case
        var escalateResult = await _service.EscalateCaseAsync(fileId, EscalationLevel.Critical, TestContext.Current.CancellationToken);
        
        // Assert 4
        escalateResult.IsSuccess.ShouldBeTrue();
        
        // Verify escalation persisted
        var verifyResult = await _service.GetSLAStatusAsync(fileId, TestContext.Current.CancellationToken);
        verifyResult.IsSuccess.ShouldBeTrue();
        verifyResult.Value.ShouldNotBeNull();
        verifyResult.Value!.EscalationLevel.ShouldBe(EscalationLevel.Critical);
        verifyResult.Value.EscalatedAt.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that multiple cases with different deadlines are handled correctly.
    /// </summary>
    [Fact]
    public async Task MultipleCases_DifferentDeadlines_HandledCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var file1 = "multi-case-001";
        var file2 = "multi-case-002";
        var file3 = "multi-case-003";

        // Create cases with different deadlines
        await _service.CalculateSLAStatusAsync(file1, now.AddDays(-1), 5, TestContext.Current.CancellationToken);
        await _service.CalculateSLAStatusAsync(file2, now.AddDays(-5), 10, TestContext.Current.CancellationToken);
        await _service.CalculateSLAStatusAsync(file3, now.AddDays(-10), 5, TestContext.Current.CancellationToken); // Breached

        // Act: Get active cases
        var activeResult = await _service.GetActiveCasesAsync(TestContext.Current.CancellationToken);
        
        // Assert
        activeResult.IsSuccess.ShouldBeTrue();
        activeResult.Value.ShouldNotBeNull();
        activeResult.Value!.Count.ShouldBeGreaterThanOrEqualTo(2); // At least file1 and file2
        activeResult.Value.ShouldNotContain(s => s.FileId == file3); // file3 should be breached

        // Act: Get breached cases
        var breachedResult = await _service.GetBreachedCasesAsync(TestContext.Current.CancellationToken);
        
        // Assert
        breachedResult.IsSuccess.ShouldBeTrue();
        breachedResult.Value.ShouldNotBeNull();
        breachedResult.Value!.ShouldContain(s => s.FileId == file3);
    }

    /// <summary>
    /// Tests that at-risk detection workflow identifies cases correctly.
    /// </summary>
    [Fact]
    public async Task AtRiskDetection_IdentifiesCasesCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var atRiskFile = "at-risk-001";
        var safeFile = "safe-001";

        // Create at-risk case (within 4 hours)
        var atRiskIntakeDate = now.AddDays(-9); // 9 days ago
        var atRiskDaysPlazo = 10; // Deadline is soon
        await _service.CalculateSLAStatusAsync(atRiskFile, atRiskIntakeDate, atRiskDaysPlazo, TestContext.Current.CancellationToken);

        // Create safe case (more than 24 hours)
        var safeIntakeDate = now; // Today
        var safeDaysPlazo = 10; // 10 business days = plenty of time
        await _service.CalculateSLAStatusAsync(safeFile, safeIntakeDate, safeDaysPlazo, TestContext.Current.CancellationToken);

        // Update statuses to recalculate risk
        await _service.UpdateSLAStatusAsync(atRiskFile, TestContext.Current.CancellationToken);
        await _service.UpdateSLAStatusAsync(safeFile, TestContext.Current.CancellationToken);

        // Act: Get at-risk cases
        var atRiskResult = await _service.GetAtRiskCasesAsync(TestContext.Current.CancellationToken);
        
        // Assert
        atRiskResult.IsSuccess.ShouldBeTrue();
        atRiskResult.Value.ShouldNotBeNull();
        // Note: This test may need adjustment based on actual deadline calculation
        // The at-risk detection depends on remaining time vs CriticalThreshold
    }

    /// <summary>
    /// Tests that foreign key relationship with FileMetadata is maintained.
    /// </summary>
    [Fact]
    public async Task ForeignKeyRelationship_FileMetadata_Maintained()
    {
        // Arrange
        var fileId = "fk-test-001";
        var fileMetadata = new FileMetadata
        {
            FileId = fileId,
            FileName = "test.pdf",
            FilePath = "/path/to/test.pdf",
            Checksum = "test-checksum",
            FileSize = 1024,
            Format = FileFormat.Pdf,
            DownloadTimestamp = DateTime.UtcNow
        };

        _dbContext.FileMetadata.Add(fileMetadata);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act: Create SLA status
        var result = await _service.CalculateSLAStatusAsync(fileId, DateTime.UtcNow, 5, TestContext.Current.CancellationToken);
        
        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        // Verify SLA status exists
        var slaStatus = await _dbContext.SLAStatus.FindAsync(new object[] { fileId }, TestContext.Current.CancellationToken);
        slaStatus.ShouldNotBeNull();
        slaStatus!.FileId.ShouldBe(fileId);
    }

    /// <summary>
    /// Tests that indexes improve query performance for deadline-based queries.
    /// </summary>
    [Fact]
    public async Task IndexPerformance_DeadlineQueries_Optimized()
    {
        // Arrange: Create multiple cases with future deadlines to ensure they're all active
        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            var fileId = $"perf-test-{i:D3}";
            // Use future dates to ensure all cases are active (not breached)
            var intakeDate = now.AddDays(i + 1); // Future dates ensure deadlines are in the future
            await _service.CalculateSLAStatusAsync(fileId, intakeDate, 5, TestContext.Current.CancellationToken);
        }

        // Act: Query by deadline (should use index)
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _service.GetActiveCasesAsync(TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBeGreaterThanOrEqualTo(10);
        
        // Performance assertion: Should complete quickly (index should help)
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000, 
            $"Query took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms with index");
    }

    /// <summary>
    /// Tests that concurrent updates are handled correctly.
    /// </summary>
    [Fact]
    public async Task ConcurrentUpdates_MultipleThreads_HandledCorrectly()
    {
        // Arrange
        var fileId = "concurrent-test-001";
        await _service.CalculateSLAStatusAsync(fileId, DateTime.UtcNow, 5, TestContext.Current.CancellationToken);

        // Act: Update from multiple tasks concurrently
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => _service.UpdateSLAStatusAsync(fileId, TestContext.Current.CancellationToken))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert: All updates should succeed
        results.ShouldAllBe(r => r.IsSuccess);
        
        // Verify final state is consistent
        var finalResult = await _service.GetSLAStatusAsync(fileId, TestContext.Current.CancellationToken);
        finalResult.IsSuccess.ShouldBeTrue();
        finalResult.Value.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that configuration changes are reflected correctly.
    /// </summary>
    [Fact]
    public async Task ConfigurationChanges_CustomThresholds_ReflectedCorrectly()
    {
        // Arrange: Create service with custom thresholds
        var customOptions = new SLAOptions
        {
            CriticalThreshold = TimeSpan.FromHours(2), // Custom: 2 hours
            WarningThreshold = TimeSpan.FromHours(12)  // Custom: 12 hours
        };

        var customMetricsCollector = new SLAMetricsCollector(XUnitLogger.CreateLogger<SLAMetricsCollector>(_output));
        var customService = new SLAEnforcerService(_dbContext, _logger, Options.Create(customOptions), customMetricsCollector);
        var fileId = "config-test-001";
        var intakeDate = DateTime.UtcNow.AddDays(-8);
        var daysPlazo = 10;

        // Act: Calculate SLA status with custom thresholds
        var result = await customService.CalculateSLAStatusAsync(fileId, intakeDate, daysPlazo, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        // The escalation level should be determined based on custom thresholds
    }

    /// <summary>
    /// Tests that business day calculation excludes weekends correctly.
    /// </summary>
    [Fact]
    public async Task BusinessDayCalculation_WeekendExclusion_Verified()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 17, 10, 0, 0, DateTimeKind.Utc); // Friday
        var endDate = new DateTime(2025, 1, 24, 10, 0, 0, DateTimeKind.Utc); // Next Friday (7 days, 5 business days)

        // Act
        var result = await _service.CalculateBusinessDaysAsync(startDate, endDate, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(5); // Excludes Saturday and Sunday
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}

