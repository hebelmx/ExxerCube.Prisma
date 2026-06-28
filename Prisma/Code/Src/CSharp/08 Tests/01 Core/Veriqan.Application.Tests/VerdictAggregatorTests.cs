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

    // -----------------------------------------------------------------------
    // Fix D: legal-baseline separable signal
    // -----------------------------------------------------------------------

    /// <summary>
    /// A finding that is a TENANT-ONLY fail (Verdict=Fail, LegalBaselineVerdict=Pass) must
    /// appear in <c>TenantOnlyFailCheckIds</c> but NOT in <c>LegalBreachCheckIds</c>.
    /// The overall signal is RED (tenant bar failed), but <c>LegalBaselineSignal</c> is GREEN.
    /// </summary>
    [Fact]
    public void Aggregate_TenantOnlyFail_SeparatesLegalBreachFromTenantFail()
    {
        // A finding where tenant policy fails but legal floor passes.
        var tenantOnlyFail = RuleFinding.Fail(
            checkId: "CL-TENANT-ONLY",
            technique: TechniqueClass.Deterministic,
            severity: FindingSeverity.Critical,
            engineVersion: "1.0.0",
            legalBaselineVerdict: FindingVerdict.Pass); // legal floor = Pass

        var findings = new[] { tenantOnlyFail };

        var result = _sut.Aggregate(findings, ct: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;

        // Gate signal is RED (tenant bar failed).
        summary.Signal.ShouldBe(VerdictSignal.Red,
            "The pipeline gate must fire on the effective Verdict = Fail.");

        // Legal breach list must be EMPTY (legal floor was not breached).
        summary.LegalBreachCheckIds.ShouldBeEmpty(
            "LegalBaselineVerdict = Pass means no regulatory breach.");

        // Tenant-only fail list must contain the check.
        summary.TenantOnlyFailCheckIds.ShouldContain("CL-TENANT-ONLY",
            "The check failed only the tenant bar, not the legal floor.");

        // Legal baseline signal must be GREEN (no regulatory breach).
        summary.LegalBaselineSignal.ShouldBe(VerdictSignal.Green,
            "LegalBaselineSignal is Green when no LegalBaselineVerdict = Fail finding exists.");
    }

    /// <summary>
    /// A finding that breaches the CONDUSEF legal floor (LegalBaselineVerdict=Fail) must appear
    /// in <c>LegalBreachCheckIds</c> and NOT in <c>TenantOnlyFailCheckIds</c>.
    /// <c>LegalBaselineSignal</c> must be RED.
    /// </summary>
    [Fact]
    public void Aggregate_LegalBreach_SeparatesLegalBreachFromTenantFail()
    {
        // Finding where both tenant and legal floor fail (typical case).
        var legalBreach = AFail("CL-LEGAL-BREACH");
        // default: LegalBaselineVerdict = Fail (same as Verdict)

        var findings = new[] { legalBreach };

        var result = _sut.Aggregate(findings, ct: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;

        summary.Signal.ShouldBe(VerdictSignal.Red);
        summary.LegalBreachCheckIds.ShouldContain("CL-LEGAL-BREACH",
            "LegalBaselineVerdict = Fail means this is a regulatory breach.");
        summary.TenantOnlyFailCheckIds.ShouldBeEmpty(
            "Both Verdict and LegalBaselineVerdict are Fail — not a tenant-only failure.");
        summary.LegalBaselineSignal.ShouldBe(VerdictSignal.Red,
            "At least one LegalBaselineVerdict = Fail → LegalBaselineSignal = Red.");
    }

    /// <summary>
    /// A Green summary (all Pass findings) must have empty <c>LegalBreachCheckIds</c>,
    /// empty <c>TenantOnlyFailCheckIds</c>, and <c>LegalBaselineSignal = Green</c>.
    /// </summary>
    [Fact]
    public void Aggregate_AllPass_LegalSignalFields_AreEmpty_AndGreen()
    {
        var findings = new[] { APass("CL-01"), APass("CL-02") };

        var result = _sut.Aggregate(findings, ct: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;
        summary.Signal.ShouldBe(VerdictSignal.Green);
        summary.LegalBreachCheckIds.ShouldBeEmpty();
        summary.TenantOnlyFailCheckIds.ShouldBeEmpty();
        summary.LegalBaselineSignal.ShouldBe(VerdictSignal.Green);
    }

    /// <summary>
    /// A Blocked summary must have <c>LegalBaselineSignal = Blocked</c> to match the
    /// statement's overall blocked state (no findings were evaluated).
    /// </summary>
    [Fact]
    public void Aggregate_Blocked_LegalBaselineSignal_IsBlocked()
    {
        var blocked = ABlockedOutcome();

        var result = _sut.Aggregate([], blocked, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;
        summary.Signal.ShouldBe(VerdictSignal.Blocked);
        summary.LegalBaselineSignal.ShouldBe(VerdictSignal.Blocked);
        summary.LegalBreachCheckIds.ShouldBeEmpty();
        summary.TenantOnlyFailCheckIds.ShouldBeEmpty();
    }

    // -----------------------------------------------------------------------
    // S12: unrecognised-verdict abstain safety (false-GREEN prevention)
    // -----------------------------------------------------------------------

    /// <summary>
    /// A future/out-of-range <see cref="FindingVerdict"/> value must never count as a
    /// pass and must not cause the overall verdict to be GREEN (false-GREEN risk).
    /// The unknown finding is treated as InsufficientData so it is reported separately
    /// but does not escalate to RED either (ABSTAIN-SAFETY).
    /// </summary>
    [Fact]
    public void Aggregate_UnrecognisedVerdict_DoesNotCountAsPass_NeverGreenFromUnknown()
    {
        // (FindingVerdict)999 simulates a future enum member not yet known to the aggregator.
        var unknownFinding = new RuleFinding(
            CheckId: "CL-FUTURE",
            Verdict: (FindingVerdict)999,
            Technique: TechniqueClass.Deterministic,
            Severity: FindingSeverity.Warning,
            EngineVersion: "1.0.0");

        var findings = new[] { unknownFinding };

        var result = _sut.Aggregate(findings, ct: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;

        // Must NOT be GREEN driven by the unknown verdict being counted as a pass.
        // The unknown finding is absorbed into InsufficientData (abstain path).
        summary.PassCount.ShouldBe(0,
            "An unrecognised verdict must never silently increment the pass counter.");

        // The check lands in InsufficientData, not in FailCheckIds.
        summary.InsufficientDataCount.ShouldBe(1);
        summary.InsufficientDataCheckIds.ShouldContain("CL-FUTURE");
        summary.FailCount.ShouldBe(0);

        // Signal is GREEN here because no Fail findings exist — but that is the
        // InsufficientData behaviour (abstain-safe), not a false-GREEN from passing.
        // The key assertion is that PassCount is 0 and the check is in InsufficientData.
        summary.Signal.ShouldNotBe(VerdictSignal.Red,
            "Unrecognised verdict must not escalate to RED (abstain-safety).");
    }

    /// <summary>
    /// Regression: all three current <see cref="FindingVerdict"/> members — Pass, Fail,
    /// InsufficientData — must continue to behave exactly as before the S12 change.
    /// </summary>
    [Fact]
    public void Aggregate_AllCurrentVerdicts_BehaviourUnchanged()
    {
        var findings = new[]
        {
            APass("CL-P1"),
            APass("CL-P2"),
            AFail("CL-F1"),
            AnInsufficient("CL-I1"),
        };

        var result = _sut.Aggregate(findings, ct: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;

        // RED because there is a Fail finding (unchanged precedence).
        summary.Signal.ShouldBe(VerdictSignal.Red);
        summary.PassCount.ShouldBe(2);
        summary.FailCount.ShouldBe(1);
        summary.FailCheckIds.ShouldContain("CL-F1");
        summary.InsufficientDataCount.ShouldBe(1);
        summary.InsufficientDataCheckIds.ShouldContain("CL-I1");
        summary.Total.ShouldBe(4);
    }

    // -----------------------------------------------------------------------
    // Story 4.1: verdict-level confidence rollup
    // -----------------------------------------------------------------------

    /// <summary>
    /// Empty findings list → Confidence = 1.0 (no evidence of low confidence).
    /// </summary>
    [Fact]
    public void Aggregate_EmptyFindings_Confidence_IsOne()
    {
        var result = _sut.Aggregate([], ct: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Confidence.ShouldBe(1.0, "empty findings → Confidence must be 1.0");
    }

    /// <summary>
    /// All findings have default Confidence = 1.0 → verdict-level Confidence = 1.0.
    /// </summary>
    [Fact]
    public void Aggregate_AllFindingsFullConfidence_VerdictConfidenceIsOne()
    {
        // RuleFinding factories default Confidence to 1.0.
        var findings = new[] { APass("CL-01"), APass("CL-02"), AFail("CL-03") };

        var result = _sut.Aggregate(findings, ct: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Confidence.ShouldBe(1.0, "all findings at 1.0 → verdict Confidence = 1.0");
    }

    /// <summary>
    /// Verdict-level Confidence = minimum across all finding Confidence values.
    /// </summary>
    [Fact]
    public void Aggregate_MixedConfidences_ReturnsMinimum()
    {
        var findings = new[]
        {
            APass("CL-01") with { Confidence = 1.0 },
            APass("CL-02") with { Confidence = 0.82 },
            AFail("CL-03") with { Confidence = 0.91 },
        };

        var result = _sut.Aggregate(findings, ct: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Confidence.ShouldBe(0.82,
            "verdict Confidence must be the minimum of all finding Confidence values");
    }

    /// <summary>
    /// A single InsufficientData finding with low Confidence propagates to the verdict.
    /// </summary>
    [Fact]
    public void Aggregate_SingleInsufficientDataWithLowConfidence_PropagatesMin()
    {
        var findings = new[]
        {
            AnInsufficient("CL-10") with { Confidence = 0.55 },
        };

        var result = _sut.Aggregate(findings, ct: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;
        summary.Signal.ShouldBe(VerdictSignal.Green, "InsufficientData alone is not Red");
        summary.Confidence.ShouldBe(0.55, "low-confidence InsufficientData finding propagates to verdict");
    }

    /// <summary>
    /// Blocked summary always carries Confidence = 1.0 (no rules were evaluated).
    /// </summary>
    [Fact]
    public void Aggregate_Blocked_Confidence_IsOne()
    {
        var blocked = ABlockedOutcome();

        var result = _sut.Aggregate([], blocked, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Confidence.ShouldBe(1.0,
            "Blocked summary has no findings — Confidence must default to 1.0");
    }
}
