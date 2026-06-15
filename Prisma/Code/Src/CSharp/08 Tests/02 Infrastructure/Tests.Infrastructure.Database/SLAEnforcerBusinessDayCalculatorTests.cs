namespace ExxerCube.Prisma.Tests.Infrastructure.Database;

/// <summary>
/// Minimal deterministic <see cref="IBusinessDayCalculator"/> that records calls and delegates to a
/// provided function, allowing test assertions without a real holiday-calendar dependency.
/// </summary>
file sealed class StubBusinessDayCalculator : IBusinessDayCalculator
{
    private readonly Func<DateTime, int, DateTime> _add;
    private readonly Func<DateTime, DateTime, int> _count;

    public StubBusinessDayCalculator(
        Func<DateTime, int, DateTime> add,
        Func<DateTime, DateTime, int>? count = null)
    {
        _add = add;
        _count = count ?? ((_, _) => 0);
    }

    public int AddCallCount { get; private set; }
    public int CountCallCount { get; private set; }

    public DateTime AddBusinessDays(DateTime startDate, int businessDays)
    {
        AddCallCount++;
        return _add(startDate, businessDays);
    }

    public int CountBusinessDays(DateTime startDate, DateTime endDate)
    {
        CountCallCount++;
        return _count(startDate, endDate);
    }
}

/// <summary>
/// Tests that <see cref="SLAEnforcerService"/> delegates business-day calculations to the
/// injected <see cref="IBusinessDayCalculator"/> when one is provided, and falls back to the
/// built-in weekend-only logic when none is injected.
/// </summary>
public sealed class SLAEnforcerBusinessDayCalculatorTests : IDisposable
{
    private readonly PrismaDbContext _dbContext;
    private readonly ILogger<SLAEnforcerService> _logger;
    private readonly IOptions<SLAOptions> _options;
    private readonly SLAMetricsCollector _metricsCollector;
    private readonly ITestOutputHelper _output;

    public SLAEnforcerBusinessDayCalculatorTests(ITestOutputHelper output)
    {
        _output = output;
        var dbOpts = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PrismaDbContext(dbOpts);
        _dbContext.Database.EnsureCreated();
        _logger = XUnitLogger.CreateLogger<SLAEnforcerService>(output);
        _metricsCollector = new SLAMetricsCollector(XUnitLogger.CreateLogger<SLAMetricsCollector>(output));
        _options = Options.Create(new SLAOptions
        {
            CriticalThreshold = TimeSpan.FromHours(4),
            WarningThreshold = TimeSpan.FromHours(24),
        });
    }

    public void Dispose() => _dbContext.Dispose();

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private SLAEnforcerService BuildService(IBusinessDayCalculator? calculator = null) =>
        new(_dbContext, _logger, _options, _metricsCollector, calculator);

    // ──────────────────────────────────────────────────────────────────────────────────
    // AddBusinessDays delegation — deadline calculation
    // ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CalculateSLAStatusAsync_NoCalculator_DeadlineUsesWeekendOnlyFallback()
    {
        // Wed 11 Jun 2025 + 1 business day (no holiday) = Thu 12 Jun
        var service = BuildService(calculator: null);
        var intakeDate = new DateTime(2025, 6, 11, 0, 0, 0, DateTimeKind.Utc);

        var result = await service.CalculateSLAStatusAsync("file-001", intakeDate, daysPlazo: 1, Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Deadline.Date.ShouldBe(new DateTime(2025, 6, 12));
    }

    [Fact]
    public async Task CalculateSLAStatusAsync_WithCalculator_DeadlineUsesCalculatorResult()
    {
        // The calculator is asked AddBusinessDays(intakeDate, 1) and returns a specific date.
        var intakeDate = new DateTime(2025, 9, 15, 0, 0, 0, DateTimeKind.Utc); // Mon 15 Sep 2025
        var expectedDeadline = new DateTime(2025, 9, 17, 0, 0, 0, DateTimeKind.Utc); // Wed 17 Sep (skips holiday Tue 16)

        var calculator = new StubBusinessDayCalculator(
            add: (start, days) => expectedDeadline);

        var service = BuildService(calculator);
        var result = await service.CalculateSLAStatusAsync("file-002", intakeDate, daysPlazo: 1, Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Deadline.ShouldBe(expectedDeadline);
        calculator.AddCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task CalculateSLAStatusAsync_WithHolidayAwareCalculator_DeadlineLandsLater()
    {
        // Simulates a holiday-aware calculator that returns Wed 17 Sep instead of Tue 16 Sep.
        // (Mon 15 Sep 2025 + 1 bd, holiday-aware: Tue 16 = Día de la Independencia → Wed 17)
        var intakeDate = new DateTime(2025, 9, 15, 0, 0, 0, DateTimeKind.Utc);
        var holidayAwareResult = new DateTime(2025, 9, 17, 0, 0, 0, DateTimeKind.Utc);

        var calculator = new StubBusinessDayCalculator(
            add: (start, days) => holidayAwareResult);

        var service = BuildService(calculator);
        var result = await service.CalculateSLAStatusAsync("file-003", intakeDate, daysPlazo: 1, Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Deadline.ShouldBe(holidayAwareResult,
            "Service must delegate to the injected calculator, which returns the holiday-aware deadline");
        calculator.AddCallCount.ShouldBe(1);
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    // CountBusinessDays delegation — elapsed-days calculation
    // ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CalculateBusinessDaysAsync_NoCalculator_UsesWeekendOnlyFallback()
    {
        // Mon 9 Jun to Fri 13 Jun (exclusive) = 4 business days (Mon-Thu)
        var service = BuildService(calculator: null);
        var start = new DateTime(2025, 6, 9);
        var end = new DateTime(2025, 6, 13);

        var result = await service.CalculateBusinessDaysAsync(start, end, Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(4);
    }

    [Fact]
    public async Task CalculateBusinessDaysAsync_WithCalculator_DelegatesToCalculator()
    {
        var start = new DateTime(2025, 9, 15);
        var end = new DateTime(2025, 9, 18);

        var calculator = new StubBusinessDayCalculator(
            add: (s, d) => s.AddDays(d),
            count: (s, e) => 2); // Holiday-aware answer (skips Tue 16 = Día de la Independencia)

        var service = BuildService(calculator);
        var result = await service.CalculateBusinessDaysAsync(start, end, Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(2);
        calculator.CountCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task CalculateBusinessDaysAsync_WithHolidayAwareCalculator_HolidayExcluded()
    {
        // Stub returns 2 (holiday-aware: Mon 15 + Wed 17, skipping Tue 16 = Día de la Independencia)
        var start = new DateTime(2025, 9, 15);
        var end = new DateTime(2025, 9, 18);

        var calculator = new StubBusinessDayCalculator(
            add: (s, d) => s.AddDays(d),
            count: (s, e) => 2); // holiday-aware count

        var service = BuildService(calculator);
        var result = await service.CalculateBusinessDaysAsync(start, end, Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(2,
            "Service must delegate count to the injected calculator; stub returns holiday-aware result 2");
        calculator.CountCallCount.ShouldBe(1);
    }
}
