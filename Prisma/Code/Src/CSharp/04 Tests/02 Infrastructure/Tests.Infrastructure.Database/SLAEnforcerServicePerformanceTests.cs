namespace ExxerCube.Prisma.Tests.Infrastructure.Database;

/// <summary>
/// Performance tests for <see cref="SLAEnforcerService"/> to verify NFR requirements (IV1).
/// </summary>
public class SLAEnforcerServicePerformanceTests : IDisposable
{
    private readonly string _databaseName;
    private readonly PrismaDbContext _dbContext;
    private readonly ILogger<SLAEnforcerService> _logger;
    private readonly SLAOptions _options;
    private readonly SLAMetricsCollector _metricsCollector;
    private readonly SLAEnforcerService _service;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="SLAEnforcerServicePerformanceTests"/> class.
    /// </summary>
    public SLAEnforcerServicePerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _databaseName = Guid.NewGuid().ToString();
        var dbOptions = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseInMemoryDatabase(databaseName: _databaseName)
            .Options;

        _dbContext = new PrismaDbContext(dbOptions);
        _dbContext.Database.EnsureCreated();
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
    /// Tests that CalculateSLAStatusAsync completes within 200ms (p95 target).
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task CalculateSLAStatusAsync_CompletesWithin200ms()
    {
        // Arrange
        var fileId = "perf-calc-001";
        var intakeDate = DateTime.UtcNow;
        var daysPlazo = 5;

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _service.CalculateSLAStatusAsync(fileId, intakeDate, daysPlazo, TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(200,
            $"CalculateSLAStatusAsync took {stopwatch.ElapsedMilliseconds}ms, exceeding 200ms target");
    }

    /// <summary>
    /// Tests that UpdateSLAStatusAsync completes within 200ms (p95 target).
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task UpdateSLAStatusAsync_CompletesWithin200ms()
    {
        // Arrange
        var fileId = "perf-update-001";
        await _service.CalculateSLAStatusAsync(fileId, DateTime.UtcNow, 5, TestContext.Current.CancellationToken);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _service.UpdateSLAStatusAsync(fileId, TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(200,
            $"UpdateSLAStatusAsync took {stopwatch.ElapsedMilliseconds}ms, exceeding 200ms target");
    }

    /// <summary>
    /// Tests that GetAtRiskCasesAsync completes within 200ms (p95 target).
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task GetAtRiskCasesAsync_CompletesWithin200ms()
    {
        // Arrange: Create multiple cases
        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            var fileId = $"perf-atrisk-{i:D3}";
            var intakeDate = now.AddDays(-i);
            await _service.CalculateSLAStatusAsync(fileId, intakeDate, 10, TestContext.Current.CancellationToken);
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _service.GetAtRiskCasesAsync(TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(200,
            $"GetAtRiskCasesAsync took {stopwatch.ElapsedMilliseconds}ms, exceeding 200ms target");
    }

    /// <summary>
    /// Tests that GetBreachedCasesAsync completes within 200ms (p95 target).
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task GetBreachedCasesAsync_CompletesWithin200ms()
    {
        // Arrange: Create multiple cases (some breached)
        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            var fileId = $"perf-breached-{i:D3}";
            var intakeDate = now.AddDays(-20 - i); // All breached
            await _service.CalculateSLAStatusAsync(fileId, intakeDate, 5, TestContext.Current.CancellationToken);
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _service.GetBreachedCasesAsync(TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(200,
            $"GetBreachedCasesAsync took {stopwatch.ElapsedMilliseconds}ms, exceeding 200ms target");
    }

    /// <summary>
    /// Tests that GetActiveCasesAsync completes within 200ms (p95 target).
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task GetActiveCasesAsync_CompletesWithin200ms()
    {
        // Arrange: Create multiple active cases
        var now = DateTime.UtcNow;
        for (int i = 0; i < 100; i++)
        {
            var fileId = $"perf-active-{i:D3}";
            var intakeDate = now.AddDays(-i);
            await _service.CalculateSLAStatusAsync(fileId, intakeDate, 10, TestContext.Current.CancellationToken);
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _service.GetActiveCasesAsync(TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(200,
            $"GetActiveCasesAsync took {stopwatch.ElapsedMilliseconds}ms, exceeding 200ms target");
    }

    /// <summary>
    /// Tests that SLA tracking operations don't impact document processing performance (IV1).
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task SLA_Tracking_DoesNotImpactDocumentProcessing()
    {
        // Arrange: Simulate document processing scenario
        var fileIds = Enumerable.Range(0, 20)
            .Select(i => $"doc-process-{i:D3}")
            .ToArray();

        // Act: Process documents with SLA tracking
        // Note: Each task uses a separate DbContext to avoid concurrency issues
        var stopwatch = Stopwatch.StartNew();
        var tasks = fileIds.Select(async fileId =>
        {
            // Simulate document processing
            await Task.Delay(10, TestContext.Current.CancellationToken);

            // Track SLA (should be fast and non-blocking)
            // Create a separate service instance with its own DbContext for concurrent operations
            // Use the same database name to share data across instances
            var dbOptions = new DbContextOptionsBuilder<PrismaDbContext>()
                .UseInMemoryDatabase(databaseName: _databaseName)
                .Options;

            using var testDbContext = new PrismaDbContext(dbOptions);
            var testLogger = XUnitLogger.CreateLogger<SLAEnforcerService>(_output);
            var testMetricsCollector = new SLAMetricsCollector(XUnitLogger.CreateLogger<SLAMetricsCollector>(_output));
            var testService = new SLAEnforcerService(testDbContext, testLogger, Options.Create(_options), testMetricsCollector);

            await testService.CalculateSLAStatusAsync(fileId, DateTime.UtcNow, 5, TestContext.Current.CancellationToken);
        });

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert: Total time should be dominated by document processing, not SLA tracking
        // If SLA tracking is efficient, total time should be close to sum of processing times
        // Increased threshold to account for DbContext creation overhead in test scenario
        var expectedMinTime = 20 * 10; // 20 documents * 10ms each = 200ms minimum
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(expectedMinTime * 4,
            $"SLA tracking significantly impacted processing time: {stopwatch.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// Tests that bulk SLA status updates perform efficiently.
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task BulkUpdates_MultipleFiles_PerformsEfficiently()
    {
        // Arrange: Create multiple SLA statuses
        var now = DateTime.UtcNow;
        var fileIds = Enumerable.Range(0, 100)
            .Select(i => $"bulk-update-{i:D3}")
            .ToArray();

        foreach (var fileId in fileIds)
        {
            await _service.CalculateSLAStatusAsync(fileId, now, 5, TestContext.Current.CancellationToken);
        }

        // Act: Update all statuses
        var stopwatch = Stopwatch.StartNew();
        var updateTasks = fileIds.Select(fileId =>
            _service.UpdateSLAStatusAsync(fileId, TestContext.Current.CancellationToken));
        var results = await Task.WhenAll(updateTasks);
        stopwatch.Stop();

        // Assert: All updates should succeed
        results.ShouldAllBe(r => r.IsSuccess);

        // Performance: Bulk updates should complete reasonably fast
        var avgTimePerUpdate = stopwatch.ElapsedMilliseconds / (double)fileIds.Length;
        avgTimePerUpdate.ShouldBeLessThan(10,
            $"Average update time: {avgTimePerUpdate}ms per file, exceeding 10ms target");
    }

    /// <summary>
    /// Tests that business day calculation is efficient even for large date ranges.
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task CalculateBusinessDays_LargeDateRange_PerformsEfficiently()
    {
        // Arrange: Large date range (1 year)
        var startDate = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2025, 12, 31, 10, 0, 0, DateTimeKind.Utc);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _service.CalculateBusinessDaysAsync(startDate, endDate, TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeGreaterThan(0);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000,
            $"CalculateBusinessDaysAsync took {stopwatch.ElapsedMilliseconds}ms for large range, exceeding 1000ms target");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}