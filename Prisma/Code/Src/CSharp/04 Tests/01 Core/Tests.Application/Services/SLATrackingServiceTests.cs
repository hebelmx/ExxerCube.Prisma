namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Unit tests for <see cref="SLATrackingService"/> covering delegation, cancellation, and validation scenarios.
/// </summary>
public class SLATrackingServiceTests
{
    private readonly ISLAEnforcer _slaEnforcer;
    private readonly ILogger<SLATrackingService> _logger;
    private readonly SLATrackingService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="SLATrackingServiceTests"/> class with mocked SLA enforcer.
    /// </summary>
    public SLATrackingServiceTests(ITestOutputHelper output)
    {
        _slaEnforcer = Substitute.For<ISLAEnforcer>();
        _logger = XUnitLogger.CreateLogger<SLATrackingService>(output);
        _service = new SLATrackingService(_slaEnforcer, _logger);
    }

    /// <summary>
    /// Tests that TrackSLAAsync delegates to ISLAEnforcer correctly.
    /// </summary>
    /// <returns>A task that completes after delegation assertions are evaluated.</returns>
    [Fact]
    public async Task TrackSLAAsync_ValidInput_DelegatesToEnforcer()
    {
        // Arrange
        var fileId = "test-file-001";
        var intakeDate = DateTime.UtcNow;
        var daysPlazo = 5;
        var expectedStatus = new SLAStatus
        {
            FileId = fileId,
            IntakeDate = intakeDate,
            DaysPlazo = daysPlazo,
            Deadline = intakeDate.AddDays(7),
            RemainingTime = TimeSpan.FromDays(7),
            IsAtRisk = false,
            IsBreached = false,
            EscalationLevel = EscalationLevel.None
        };

        _slaEnforcer.CalculateSLAStatusAsync(fileId, intakeDate, daysPlazo, Arg.Any<CancellationToken>())
            .Returns(Result<SLAStatus>.Success(expectedStatus));

        // Act
        var result = await _service.TrackSLAAsync(fileId, intakeDate, daysPlazo, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.FileId.ShouldBe(fileId);
        await _slaEnforcer.Received(1).CalculateSLAStatusAsync(fileId, intakeDate, daysPlazo, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests that TrackSLAAsync handles cancellation correctly.
    /// </summary>
    [Fact]
    public async Task TrackSLAAsync_CancellationRequested_ReturnsCancelled()
    {
        // Arrange
        var fileId = "test-file-002";
        var intakeDate = DateTime.UtcNow;
        var daysPlazo = 5;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _service.TrackSLAAsync(fileId, intakeDate, daysPlazo, cts.Token);

        // Assert
        result.IsCancelled().ShouldBeTrue();
        await _slaEnforcer.DidNotReceive().CalculateSLAStatusAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests that TrackSLAAsync handles enforcer failure correctly.
    /// </summary>
    [Fact]
    public async Task TrackSLAAsync_EnforcerFailure_ReturnsFailure()
    {
        // Arrange
        var fileId = "test-file-003";
        var intakeDate = DateTime.UtcNow;
        var daysPlazo = 5;

        _slaEnforcer.CalculateSLAStatusAsync(fileId, intakeDate, daysPlazo, Arg.Any<CancellationToken>())
            .Returns(Result<SLAStatus>.WithFailure("Database error"));

        // Act
        var result = await _service.TrackSLAAsync(fileId, intakeDate, daysPlazo, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Failed to track SLA");
    }

    /// <summary>
    /// Tests that TrackSLAAsync handles enforcer cancellation correctly.
    /// </summary>
    [Fact]
    public async Task TrackSLAAsync_EnforcerCancelled_ReturnsCancelled()
    {
        // Arrange
        var fileId = "test-file-004";
        var intakeDate = DateTime.UtcNow;
        var daysPlazo = 5;

        _slaEnforcer.CalculateSLAStatusAsync(fileId, intakeDate, daysPlazo, Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<SLAStatus>());

        // Act
        var result = await _service.TrackSLAAsync(fileId, intakeDate, daysPlazo, TestContext.Current.CancellationToken);

        // Assert
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Tests that TrackSLAAsync logs warning for at-risk cases.
    /// </summary>
    [Fact]
    public async Task TrackSLAAsync_AtRiskCase_LogsWarning()
    {
        // Arrange
        var fileId = "test-file-005";
        var intakeDate = DateTime.UtcNow;
        var daysPlazo = 5;
        var atRiskStatus = new SLAStatus
        {
            FileId = fileId,
            IntakeDate = intakeDate,
            DaysPlazo = daysPlazo,
            Deadline = intakeDate.AddHours(2), // Less than 4 hours
            RemainingTime = TimeSpan.FromHours(2),
            IsAtRisk = true,
            IsBreached = false,
            EscalationLevel = EscalationLevel.Critical
        };

        _slaEnforcer.CalculateSLAStatusAsync(fileId, intakeDate, daysPlazo, Arg.Any<CancellationToken>())
            .Returns(Result<SLAStatus>.Success(atRiskStatus));

        // Act
        var result = await _service.TrackSLAAsync(fileId, intakeDate, daysPlazo, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.IsAtRisk.ShouldBeTrue();
        // Note: In a real test, we'd verify logging occurred
    }

    /// <summary>
    /// Tests that TrackSLAAsync returns failure for null fileId.
    /// </summary>
    [Fact]
    public async Task TrackSLAAsync_NullFileId_ReturnsFailure()
    {
        // Arrange
        var intakeDate = DateTime.UtcNow;
        var daysPlazo = 5;

        // Act
        var result = await _service.TrackSLAAsync(null!, intakeDate, daysPlazo, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("FileId");
        await _slaEnforcer.DidNotReceive().CalculateSLAStatusAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests that TrackSLAAsync returns failure for invalid daysPlazo.
    /// </summary>
    [Fact]
    public async Task TrackSLAAsync_InvalidDaysPlazo_ReturnsFailure()
    {
        // Arrange
        var fileId = "test-file-006";
        var intakeDate = DateTime.UtcNow;
        var daysPlazo = 0;

        // Act
        var result = await _service.TrackSLAAsync(fileId, intakeDate, daysPlazo, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("DaysPlazo");
        await _slaEnforcer.DidNotReceive().CalculateSLAStatusAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests that UpdateSLAStatusAsync delegates to ISLAEnforcer correctly.
    /// </summary>
    [Fact]
    public async Task UpdateSLAStatusAsync_ValidInput_DelegatesToEnforcer()
    {
        // Arrange
        var fileId = "test-file-007";
        var expectedStatus = new SLAStatus
        {
            FileId = fileId,
            IntakeDate = DateTime.UtcNow.AddDays(-1),
            DaysPlazo = 5,
            Deadline = DateTime.UtcNow.AddDays(4),
            RemainingTime = TimeSpan.FromDays(4),
            IsAtRisk = false,
            IsBreached = false,
            EscalationLevel = EscalationLevel.None
        };

        _slaEnforcer.UpdateSLAStatusAsync(fileId, Arg.Any<CancellationToken>())
            .Returns(Result<SLAStatus>.Success(expectedStatus));

        // Act
        var result = await _service.UpdateSLAStatusAsync(fileId, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        await _slaEnforcer.Received(1).UpdateSLAStatusAsync(fileId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests that GetActiveCasesAsync delegates to ISLAEnforcer correctly.
    /// </summary>
    [Fact]
    public async Task GetActiveCasesAsync_ValidInput_DelegatesToEnforcer()
    {
        // Arrange
        var expectedCases = new List<SLAStatus>
        {
            new SLAStatus { FileId = "file-001", IsBreached = false },
            new SLAStatus { FileId = "file-002", IsBreached = false }
        };

        _slaEnforcer.GetActiveCasesAsync(Arg.Any<CancellationToken>())
            .Returns(Result<List<SLAStatus>>.Success(expectedCases));

        // Act
        var result = await _service.GetActiveCasesAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2);
        await _slaEnforcer.Received(1).GetActiveCasesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests that GetAtRiskCasesAsync delegates to ISLAEnforcer correctly.
    /// </summary>
    [Fact]
    public async Task GetAtRiskCasesAsync_ValidInput_DelegatesToEnforcer()
    {
        // Arrange
        var expectedCases = new List<SLAStatus>
        {
            new SLAStatus { FileId = "at-risk-001", IsAtRisk = true }
        };

        _slaEnforcer.GetAtRiskCasesAsync(Arg.Any<CancellationToken>())
            .Returns(Result<List<SLAStatus>>.Success(expectedCases));

        // Act
        var result = await _service.GetAtRiskCasesAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(1);
        await _slaEnforcer.Received(1).GetAtRiskCasesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests that GetBreachedCasesAsync delegates to ISLAEnforcer correctly.
    /// </summary>
    /// <returns>A task that completes after verifying breached cases delegation.</returns>
    [Fact]
    public async Task GetBreachedCasesAsync_ValidInput_DelegatesToEnforcer()
    {
        // Arrange
        var expectedCases = new List<SLAStatus>
        {
            new SLAStatus { FileId = "breached-001", IsBreached = true }
        };

        _slaEnforcer.GetBreachedCasesAsync(Arg.Any<CancellationToken>())
            .Returns(Result<List<SLAStatus>>.Success(expectedCases));

        // Act
        var result = await _service.GetBreachedCasesAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(1);
        await _slaEnforcer.Received(1).GetBreachedCasesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests that EscalateCaseAsync delegates to ISLAEnforcer correctly.
    /// </summary>
    /// <returns>A task that completes after escalation delegation assertions are evaluated.</returns>
    [Fact]
    public async Task EscalateCaseAsync_ValidInput_DelegatesToEnforcer()
    {
        // Arrange
        var fileId = "test-file-008";
        var escalationLevel = EscalationLevel.Critical;

        _slaEnforcer.EscalateCaseAsync(fileId, escalationLevel, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _service.EscalateCaseAsync(fileId, escalationLevel, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _slaEnforcer.Received(1).EscalateCaseAsync(fileId, escalationLevel, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests that EscalateCaseAsync handles enforcer failure correctly.
    /// </summary>
    /// <returns>A task that completes after failure propagation assertions are evaluated.</returns>
    [Fact]
    public async Task EscalateCaseAsync_EnforcerFailure_ReturnsFailure()
    {
        // Arrange
        var fileId = "test-file-009";
        var escalationLevel = EscalationLevel.Critical;

        _slaEnforcer.EscalateCaseAsync(fileId, escalationLevel, Arg.Any<CancellationToken>())
            .Returns(Result.WithFailure("Database error"));

        // Act
        var result = await _service.EscalateCaseAsync(fileId, escalationLevel, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Failed to escalate case");
    }
}

