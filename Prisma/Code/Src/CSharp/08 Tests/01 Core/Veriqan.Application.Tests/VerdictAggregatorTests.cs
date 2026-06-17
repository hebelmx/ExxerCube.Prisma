using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Application.Tests;

/// <summary>
/// Unit tests for <see cref="VerdictAggregator"/> covering FR-15 precedence rules
/// (Blocked &gt; Red &gt; Green) and InsufficientData isolation semantics (Story 7.1).
/// </summary>
public sealed class VerdictAggregatorTests
{
    // -----------------------------------------------------------------------
    // Subject under test
    // -----------------------------------------------------------------------

    private readonly IVerdictAggregator _sut = new VerdictAggregator();

    // -----------------------------------------------------------------------
    // Factories — keep noise out of test bodies
    // -----------------------------------------------------------------------

    private static RuleFinding APass(string checkId = "CL-PASS") =>
        RuleFinding.Pass(checkId, TechniqueClass.Deterministic, "1.0.0");

    private static RuleFinding AFail(string checkId = "CL-FAIL") =>
        RuleFinding.Fail(checkId, TechniqueClass.Deterministic, FindingSeverity.Critical, "1.0.0");

    private static RuleFinding AnInsufficient(string checkId = "CL-INSUFF") =>
        RuleFinding.InsufficientData(checkId, TechniqueClass.Deterministic, "1.0.0", reason: "no data");

    private static BlockedOutcome ABlockedOutcome(BlockReason reason = BlockReason.UnknownProduct) =>
        new(reason, "Test block detail.");

    // -----------------------------------------------------------------------
    // GREEN scenarios
    // -----------------------------------------------------------------------

    [Fact]
    public void Aggregate_AllPass_ReturnsGreen()
    {
        var findings = new[] { APass("CL-01"), APass("CL-02"), APass("CL-03") };

        var result = _sut.Aggregate(findings, ct: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Signal.ShouldBe(VerdictSignal.Green);
    }

    [Fact]
    public void Aggregate_EmptyFindings_NoBlock_ReturnsGreen()
    {
        var result = _sut.Aggregate([], ct: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Signal.ShouldBe(VerdictSignal.Green);
        result.Value.Total.ShouldBe(0);
    }

    [Fact]
    public void Aggregate_InsufficientDataOnly_ReturnsGreen_WithInsufficientCountReportedSeparately()
    {
        // Key AC: InsufficientData alone must NOT produce RED.
        var findings = new[] { AnInsufficient("CL-10"), AnInsufficient("CL-11") };

        var result = _sut.Aggregate(findings, ct: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;
        summary.Signal.ShouldBe(VerdictSignal.Green);
        summary.InsufficientDataCount.ShouldBe(2);
        summary.InsufficientDataCheckIds.ShouldContain("CL-10");
        summary.InsufficientDataCheckIds.ShouldContain("CL-11");
        summary.FailCount.ShouldBe(0);
        summary.FailCheckIds.ShouldBeEmpty();
    }

    // -----------------------------------------------------------------------
    // RED scenarios
    // -----------------------------------------------------------------------

    [Fact]
    public void Aggregate_AnyFail_ReturnsRed_WithFailCheckIdsSurfaced()
    {
        var findings = new[] { APass("CL-01"), AFail("CL-FAIL-A"), APass("CL-03") };

        var result = _sut.Aggregate(findings, ct: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;
        summary.Signal.ShouldBe(VerdictSignal.Red);
        summary.FailCount.ShouldBe(1);
        summary.FailCheckIds.ShouldContain("CL-FAIL-A");
    }

    [Fact]
    public void Aggregate_FailAndInsufficient_ReturnsRed()
    {
        var findings = new[] { AFail("CL-FAIL-X"), AnInsufficient("CL-INSUFF-Y"), APass("CL-03") };

        var result = _sut.Aggregate(findings, ct: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;
        summary.Signal.ShouldBe(VerdictSignal.Red);
        summary.FailCount.ShouldBe(1);
        summary.FailCheckIds.ShouldContain("CL-FAIL-X");
        // Insufficient is still surfaced even though Red wins.
        summary.InsufficientDataCount.ShouldBe(1);
        summary.InsufficientDataCheckIds.ShouldContain("CL-INSUFF-Y");
    }

    // -----------------------------------------------------------------------
    // BLOCKED scenarios
    // -----------------------------------------------------------------------

    [Fact]
    public void Aggregate_Blocked_ReturnsBlocked_WithReasonSurfaced()
    {
        var blocked = ABlockedOutcome(BlockReason.UnknownProduct);
        // Even pass-only findings: Blocked wins.
        var findings = new[] { APass("CL-01") };

        var result = _sut.Aggregate(findings, blocked, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;
        summary.Signal.ShouldBe(VerdictSignal.Blocked);
        summary.BlockedOutcome.ShouldNotBeNull();
        summary.BlockedOutcome!.Reason.ShouldBe(BlockReason.UnknownProduct);
    }

    [Fact]
    public void Aggregate_Blocked_WinsEvenWhenFindingsWouldBeRed()
    {
        var blocked = ABlockedOutcome(BlockReason.InvalidBundle);
        var findings = new[] { AFail("CL-FAIL-1") };

        var result = _sut.Aggregate(findings, blocked, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Signal.ShouldBe(VerdictSignal.Blocked);
        result.Value.BlockedOutcome!.Reason.ShouldBe(BlockReason.InvalidBundle);
        result.Value.BlockedOutcome.Detail.ShouldBe("Test block detail.");
    }

    // -----------------------------------------------------------------------
    // Count correctness
    // -----------------------------------------------------------------------

    [Fact]
    public void Aggregate_CountsAreCorrect()
    {
        var findings = new[]
        {
            APass("CL-01"),
            APass("CL-02"),
            AFail("CL-F1"),
            AFail("CL-F2"),
            AnInsufficient("CL-I1"),
        };

        var result = _sut.Aggregate(findings, ct: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;
        summary.PassCount.ShouldBe(2);
        summary.FailCount.ShouldBe(2);
        summary.InsufficientDataCount.ShouldBe(1);
        summary.Total.ShouldBe(5);
        summary.Signal.ShouldBe(VerdictSignal.Red);
    }

    [Fact]
    public void Aggregate_GreenCounts_AreCorrect()
    {
        var findings = new[]
        {
            APass("CL-01"),
            APass("CL-02"),
            AnInsufficient("CL-I1"),
            AnInsufficient("CL-I2"),
        };

        var result = _sut.Aggregate(findings, ct: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;
        summary.Signal.ShouldBe(VerdictSignal.Green);
        summary.PassCount.ShouldBe(2);
        summary.FailCount.ShouldBe(0);
        summary.InsufficientDataCount.ShouldBe(2);
        summary.Total.ShouldBe(4);
    }

    // -----------------------------------------------------------------------
    // Cancellation
    // -----------------------------------------------------------------------

    [Fact]
    public void Aggregate_CancelledToken_ReturnsCancelledResult()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var findings = new[] { APass("CL-01") };
        var result = _sut.Aggregate(findings, ct: cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }
}
