namespace ExxerCube.Prisma.Tests.Application.Services;

/// <summary>
/// Mutation-killing tests for <see cref="SLATrackingService"/>.
/// Pins the exact failure/cancellation/null-fallback branches and error-message strings of every
/// delegating method so that operator/string/branch mutants on the pure orchestration surface die.
/// The service is a deterministic delegator over <see cref="ISLAEnforcer"/>; all paths are mock-driven.
/// </summary>
public class SLATrackingServiceMutationTests
{
    private readonly ISLAEnforcer _slaEnforcer;
    private readonly SLATrackingService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="SLATrackingServiceMutationTests"/> class.
    /// </summary>
    public SLATrackingServiceMutationTests(ITestOutputHelper output)
    {
        _slaEnforcer = Substitute.For<ISLAEnforcer>();
        var logger = XUnitLogger.CreateLogger<SLATrackingService>(output);
        _service = new SLATrackingService(_slaEnforcer, logger);
    }

    private static SLAStatus Status(string fileId = "f1", bool atRisk = false) => new()
    {
        FileId = fileId,
        IntakeDate = new DateTime(2026, 1, 1),
        DaysPlazo = 5,
        Deadline = new DateTime(2026, 1, 8),
        RemainingTime = TimeSpan.FromDays(7),
        IsAtRisk = atRisk,
        IsBreached = false,
        EscalationLevel = atRisk ? EscalationLevel.Critical : EscalationLevel.None,
    };

    // ---------- Constructor guards ----------

    [Fact]
    public void Constructor_NullEnforcer_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new SLATrackingService(null!, Substitute.For<ILogger<SLATrackingService>>()))
            .ParamName.ShouldBe("slaEnforcer");

    [Fact]
    public void Constructor_NullLogger_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new SLATrackingService(Substitute.For<ISLAEnforcer>(), null!))
            .ParamName.ShouldBe("logger");

    // ---------- TrackSLAAsync ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TrackSLAAsync_BlankFileId_ReturnsExactFailure(string? fileId)
    {
        var result = await _service.TrackSLAAsync(fileId!, new DateTime(2026, 1, 1), 5, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("FileId cannot be null or empty");
        await _slaEnforcer.DidNotReceive().CalculateSLAStatusAsync(
            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TrackSLAAsync_NonPositiveDaysPlazo_ReturnsExactFailure(int daysPlazo)
    {
        var result = await _service.TrackSLAAsync("f1", new DateTime(2026, 1, 1), daysPlazo, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("DaysPlazo must be greater than zero");
        await _slaEnforcer.DidNotReceive().CalculateSLAStatusAsync(
            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrackSLAAsync_DaysPlazoOne_PassesGuardAndDelegates()
    {
        // Boundary: 1 must NOT be rejected (kills <= -> < and the literal boundary mutant).
        _slaEnforcer.CalculateSLAStatusAsync("f1", Arg.Any<DateTime>(), 1, Arg.Any<CancellationToken>())
            .Returns(Result<SLAStatus>.Success(Status()));

        var result = await _service.TrackSLAAsync("f1", new DateTime(2026, 1, 1), 1, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _slaEnforcer.Received(1).CalculateSLAStatusAsync("f1", Arg.Any<DateTime>(), 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrackSLAAsync_EnforcerFailure_WrapsErrorExactly()
    {
        _slaEnforcer.CalculateSLAStatusAsync("f1", Arg.Any<DateTime>(), 5, Arg.Any<CancellationToken>())
            .Returns(Result<SLAStatus>.WithFailure("DB down"));

        var result = await _service.TrackSLAAsync("f1", new DateTime(2026, 1, 1), 5, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Failed to track SLA: DB down");
    }

    [Fact]
    public async Task TrackSLAAsync_EnforcerCancelled_ReturnsCancelled()
    {
        _slaEnforcer.CalculateSLAStatusAsync("f1", Arg.Any<DateTime>(), 5, Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<SLAStatus>());

        var result = await _service.TrackSLAAsync("f1", new DateTime(2026, 1, 1), 5, TestContext.Current.CancellationToken);

        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task TrackSLAAsync_SuccessNotAtRisk_ReturnsStatus()
    {
        var status = Status(atRisk: false);
        _slaEnforcer.CalculateSLAStatusAsync("f1", Arg.Any<DateTime>(), 5, Arg.Any<CancellationToken>())
            .Returns(Result<SLAStatus>.Success(status));

        var result = await _service.TrackSLAAsync("f1", new DateTime(2026, 1, 1), 5, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(status);
        result.Value!.IsAtRisk.ShouldBeFalse();
    }

    [Fact]
    public async Task TrackSLAAsync_SuccessAtRisk_StillReturnsStatus()
    {
        // Exercises the `slaStatus != null && slaStatus.IsAtRisk` warning branch (true side).
        var status = Status(atRisk: true);
        _slaEnforcer.CalculateSLAStatusAsync("f1", Arg.Any<DateTime>(), 5, Arg.Any<CancellationToken>())
            .Returns(Result<SLAStatus>.Success(status));

        var result = await _service.TrackSLAAsync("f1", new DateTime(2026, 1, 1), 5, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(status);
        result.Value!.IsAtRisk.ShouldBeTrue();
    }

    [Fact]
    public async Task TrackSLAAsync_SuccessWithNullStatus_ReturnsNullWithoutFailing()
    {
        // The `slaStatus != null` short-circuit: a null value must NOT enter the IsAtRisk warning (would NRE ->
        // be caught -> become a failure). So removing the null guard is killed by asserting "not failure".
        _slaEnforcer.CalculateSLAStatusAsync("f1", Arg.Any<DateTime>(), 5, Arg.Any<CancellationToken>())
            .Returns(Result<SLAStatus>.Success(null!));

        var result = await _service.TrackSLAAsync("f1", new DateTime(2026, 1, 1), 5, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeFalse();
        result.IsCancelled().ShouldBeFalse();
        result.Value.ShouldBeNull();
    }

    [Fact]
    public async Task TrackSLAAsync_CancelledBeforeStart_DoesNotCallEnforcer()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _service.TrackSLAAsync("f1", new DateTime(2026, 1, 1), 5, cts.Token);

        result.IsCancelled().ShouldBeTrue();
        await _slaEnforcer.DidNotReceive().CalculateSLAStatusAsync(
            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrackSLAAsync_EnforcerThrows_ReturnsWrappedError()
    {
        _slaEnforcer.CalculateSLAStatusAsync("f1", Arg.Any<DateTime>(), 5, Arg.Any<CancellationToken>())
            .Returns<Task<Result<SLAStatus>>>(_ => throw new InvalidOperationException("boom"));

        var result = await _service.TrackSLAAsync("f1", new DateTime(2026, 1, 1), 5, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Error tracking SLA: boom");
    }

    // ---------- UpdateSLAStatusAsync ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateSLAStatusAsync_BlankFileId_ReturnsExactFailure(string? fileId)
    {
        var result = await _service.UpdateSLAStatusAsync(fileId!, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("FileId cannot be null or empty");
        await _slaEnforcer.DidNotReceive().UpdateSLAStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateSLAStatusAsync_Success_ReturnsValue()
    {
        var status = Status();
        _slaEnforcer.UpdateSLAStatusAsync("f1", Arg.Any<CancellationToken>())
            .Returns(Result<SLAStatus>.Success(status));

        var result = await _service.UpdateSLAStatusAsync("f1", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(status);
    }

    [Fact]
    public async Task UpdateSLAStatusAsync_EnforcerCancelled_ReturnsCancelled()
    {
        _slaEnforcer.UpdateSLAStatusAsync("f1", Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<SLAStatus>());

        var result = await _service.UpdateSLAStatusAsync("f1", TestContext.Current.CancellationToken);

        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateSLAStatusAsync_EnforcerFailure_WrapsErrorExactly()
    {
        _slaEnforcer.UpdateSLAStatusAsync("f1", Arg.Any<CancellationToken>())
            .Returns(Result<SLAStatus>.WithFailure("no record"));

        var result = await _service.UpdateSLAStatusAsync("f1", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Failed to update SLA status: no record");
    }

    [Fact]
    public async Task UpdateSLAStatusAsync_SuccessButNullValue_ReturnsFallbackFailure()
    {
        // Kills the `result.IsSuccess && result.Value is not null` branch and the fallback message.
        _slaEnforcer.UpdateSLAStatusAsync("f1", Arg.Any<CancellationToken>())
            .Returns(Result<SLAStatus>.Success(null!));

        var result = await _service.UpdateSLAStatusAsync("f1", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("SLA status update returned null value");
    }

    [Fact]
    public async Task UpdateSLAStatusAsync_EnforcerThrows_ReturnsWrappedError()
    {
        _slaEnforcer.UpdateSLAStatusAsync("f1", Arg.Any<CancellationToken>())
            .Returns<Task<Result<SLAStatus>>>(_ => throw new InvalidOperationException("boom"));

        var result = await _service.UpdateSLAStatusAsync("f1", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Error updating SLA status: boom");
    }

    [Fact]
    public async Task UpdateSLAStatusAsync_CancelledBeforeStart_DoesNotCallEnforcer()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _service.UpdateSLAStatusAsync("f1", cts.Token);

        result.IsCancelled().ShouldBeTrue();
        await _slaEnforcer.DidNotReceive().UpdateSLAStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---------- GetActiveCasesAsync ----------

    [Fact]
    public async Task GetActiveCasesAsync_Success_ReturnsValue()
    {
        var cases = new List<SLAStatus> { Status("a"), Status("b") };
        _slaEnforcer.GetActiveCasesAsync(Arg.Any<CancellationToken>())
            .Returns(Result<List<SLAStatus>>.Success(cases));

        var result = await _service.GetActiveCasesAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(cases);
    }

    [Fact]
    public async Task GetActiveCasesAsync_SuccessNullValue_ReturnsEmptyList()
    {
        _slaEnforcer.GetActiveCasesAsync(Arg.Any<CancellationToken>())
            .Returns(Result<List<SLAStatus>>.Success(null!));

        var result = await _service.GetActiveCasesAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetActiveCasesAsync_EnforcerFailure_WrapsErrorExactly()
    {
        _slaEnforcer.GetActiveCasesAsync(Arg.Any<CancellationToken>())
            .Returns(Result<List<SLAStatus>>.WithFailure("oops"));

        var result = await _service.GetActiveCasesAsync(TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Failed to get active cases: oops");
    }

    [Fact]
    public async Task GetActiveCasesAsync_EnforcerCancelled_ReturnsCancelled()
    {
        _slaEnforcer.GetActiveCasesAsync(Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<List<SLAStatus>>());

        var result = await _service.GetActiveCasesAsync(TestContext.Current.CancellationToken);

        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task GetActiveCasesAsync_EnforcerThrows_ReturnsWrappedError()
    {
        _slaEnforcer.GetActiveCasesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<Result<List<SLAStatus>>>>(_ => throw new InvalidOperationException("boom"));

        var result = await _service.GetActiveCasesAsync(TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Error getting active cases: boom");
    }

    [Fact]
    public async Task GetActiveCasesAsync_CancelledBeforeStart_DoesNotCallEnforcer()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _service.GetActiveCasesAsync(cts.Token);

        result.IsCancelled().ShouldBeTrue();
        await _slaEnforcer.DidNotReceive().GetActiveCasesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- GetAtRiskCasesAsync ----------

    [Fact]
    public async Task GetAtRiskCasesAsync_Success_ReturnsValue()
    {
        var cases = new List<SLAStatus> { Status("a", atRisk: true) };
        _slaEnforcer.GetAtRiskCasesAsync(Arg.Any<CancellationToken>())
            .Returns(Result<List<SLAStatus>>.Success(cases));

        var result = await _service.GetAtRiskCasesAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(cases);
    }

    [Fact]
    public async Task GetAtRiskCasesAsync_SuccessNullValue_ReturnsEmptyList()
    {
        _slaEnforcer.GetAtRiskCasesAsync(Arg.Any<CancellationToken>())
            .Returns(Result<List<SLAStatus>>.Success(null!));

        var result = await _service.GetAtRiskCasesAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAtRiskCasesAsync_EnforcerFailure_WrapsErrorExactly()
    {
        _slaEnforcer.GetAtRiskCasesAsync(Arg.Any<CancellationToken>())
            .Returns(Result<List<SLAStatus>>.WithFailure("oops"));

        var result = await _service.GetAtRiskCasesAsync(TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Failed to get at-risk cases: oops");
    }

    [Fact]
    public async Task GetAtRiskCasesAsync_EnforcerCancelled_ReturnsCancelled()
    {
        _slaEnforcer.GetAtRiskCasesAsync(Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<List<SLAStatus>>());

        var result = await _service.GetAtRiskCasesAsync(TestContext.Current.CancellationToken);

        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task GetAtRiskCasesAsync_EnforcerThrows_ReturnsWrappedError()
    {
        _slaEnforcer.GetAtRiskCasesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<Result<List<SLAStatus>>>>(_ => throw new InvalidOperationException("boom"));

        var result = await _service.GetAtRiskCasesAsync(TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Error getting at-risk cases: boom");
    }

    [Fact]
    public async Task GetAtRiskCasesAsync_CancelledBeforeStart_DoesNotCallEnforcer()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _service.GetAtRiskCasesAsync(cts.Token);

        result.IsCancelled().ShouldBeTrue();
        await _slaEnforcer.DidNotReceive().GetAtRiskCasesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- GetBreachedCasesAsync ----------

    [Fact]
    public async Task GetBreachedCasesAsync_Success_ReturnsValue()
    {
        var cases = new List<SLAStatus> { Status("a") };
        _slaEnforcer.GetBreachedCasesAsync(Arg.Any<CancellationToken>())
            .Returns(Result<List<SLAStatus>>.Success(cases));

        var result = await _service.GetBreachedCasesAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(cases);
    }

    [Fact]
    public async Task GetBreachedCasesAsync_SuccessNullValue_ReturnsEmptyList()
    {
        _slaEnforcer.GetBreachedCasesAsync(Arg.Any<CancellationToken>())
            .Returns(Result<List<SLAStatus>>.Success(null!));

        var result = await _service.GetBreachedCasesAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetBreachedCasesAsync_EnforcerFailure_WrapsErrorExactly()
    {
        _slaEnforcer.GetBreachedCasesAsync(Arg.Any<CancellationToken>())
            .Returns(Result<List<SLAStatus>>.WithFailure("oops"));

        var result = await _service.GetBreachedCasesAsync(TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Failed to get breached cases: oops");
    }

    [Fact]
    public async Task GetBreachedCasesAsync_EnforcerCancelled_ReturnsCancelled()
    {
        _slaEnforcer.GetBreachedCasesAsync(Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled<List<SLAStatus>>());

        var result = await _service.GetBreachedCasesAsync(TestContext.Current.CancellationToken);

        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task GetBreachedCasesAsync_EnforcerThrows_ReturnsWrappedError()
    {
        _slaEnforcer.GetBreachedCasesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<Result<List<SLAStatus>>>>(_ => throw new InvalidOperationException("boom"));

        var result = await _service.GetBreachedCasesAsync(TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Error getting breached cases: boom");
    }

    [Fact]
    public async Task GetBreachedCasesAsync_CancelledBeforeStart_DoesNotCallEnforcer()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _service.GetBreachedCasesAsync(cts.Token);

        result.IsCancelled().ShouldBeTrue();
        await _slaEnforcer.DidNotReceive().GetBreachedCasesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- EscalateCaseAsync ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EscalateCaseAsync_BlankFileId_ReturnsExactFailure(string? fileId)
    {
        var result = await _service.EscalateCaseAsync(fileId!, EscalationLevel.Critical, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("FileId cannot be null or empty");
        await _slaEnforcer.DidNotReceive().EscalateCaseAsync(
            Arg.Any<string>(), Arg.Any<EscalationLevel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EscalateCaseAsync_Success_DelegatesWithLevel()
    {
        _slaEnforcer.EscalateCaseAsync("f1", EscalationLevel.Critical, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await _service.EscalateCaseAsync("f1", EscalationLevel.Critical, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _slaEnforcer.Received(1).EscalateCaseAsync("f1", EscalationLevel.Critical, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EscalateCaseAsync_EnforcerCancelled_ReturnsCancelled()
    {
        _slaEnforcer.EscalateCaseAsync("f1", EscalationLevel.Critical, Arg.Any<CancellationToken>())
            .Returns(ResultExtensions.Cancelled());

        var result = await _service.EscalateCaseAsync("f1", EscalationLevel.Critical, TestContext.Current.CancellationToken);

        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public async Task EscalateCaseAsync_EnforcerFailure_WrapsErrorExactly()
    {
        _slaEnforcer.EscalateCaseAsync("f1", EscalationLevel.Critical, Arg.Any<CancellationToken>())
            .Returns(Result.WithFailure("denied"));

        var result = await _service.EscalateCaseAsync("f1", EscalationLevel.Critical, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Failed to escalate case: denied");
    }

    [Fact]
    public async Task EscalateCaseAsync_EnforcerThrows_ReturnsWrappedError()
    {
        _slaEnforcer.EscalateCaseAsync("f1", EscalationLevel.Critical, Arg.Any<CancellationToken>())
            .Returns<Task<Result>>(_ => throw new InvalidOperationException("boom"));

        var result = await _service.EscalateCaseAsync("f1", EscalationLevel.Critical, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("Error escalating case: boom");
    }

    [Fact]
    public async Task EscalateCaseAsync_CancelledBeforeStart_DoesNotCallEnforcer()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _service.EscalateCaseAsync("f1", EscalationLevel.Critical, cts.Token);

        result.IsCancelled().ShouldBeTrue();
        await _slaEnforcer.DidNotReceive().EscalateCaseAsync(
            Arg.Any<string>(), Arg.Any<EscalationLevel>(), Arg.Any<CancellationToken>());
    }
}
