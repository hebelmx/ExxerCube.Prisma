using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;

namespace ExxerCube.Prisma.Veriqan.Application.Tests;

/// <summary>
/// Unit tests for the Story 1.1 two-tier verdict surface:
/// <see cref="VerdictSignal.Yellow"/>, <see cref="VerdictSummary.BankTierVerdict"/>,
/// <see cref="VerdictSummary.CondusefTierVerdict"/>, and the overall-signal combination rule
/// exposed by <see cref="VerdictSummary.CombineOverallSignal"/>.
/// </summary>
public sealed class VerdictSummaryTierTests
{
    // -----------------------------------------------------------------------
    // Subject under test
    // -----------------------------------------------------------------------

    private readonly IVerdictAggregator _sut = new VerdictAggregator();

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static RuleFinding APass(string checkId = "T-PASS") =>
        RuleFinding.Pass(checkId, TechniqueClass.Deterministic, "1.0.0");

    private static RuleFinding AFail(string checkId = "T-FAIL") =>
        RuleFinding.Fail(checkId, TechniqueClass.Deterministic, FindingSeverity.Critical, "1.0.0");

    private static BlockedOutcome ABlockedOutcome() =>
        new(BlockReason.UnknownProduct, "Tier test block.");

    // -----------------------------------------------------------------------
    // Yellow enum value
    // -----------------------------------------------------------------------

    [Fact]
    public void VerdictSignal_Yellow_ExistsAndEqualsThree()
    {
        // Yellow must be a stable, named member at ordinal 3.
        ((int)VerdictSignal.Yellow).ShouldBe(3);
        Enum.IsDefined(typeof(VerdictSignal), VerdictSignal.Yellow).ShouldBeTrue();
    }

    [Fact]
    public void VerdictSignal_Yellow_DoesNotCollideWithExistingMembers()
    {
        // Numeric stability: existing members must keep their values.
        ((int)VerdictSignal.Green).ShouldBe(0);
        ((int)VerdictSignal.Red).ShouldBe(1);
        ((int)VerdictSignal.Blocked).ShouldBe(2);
    }

    // -----------------------------------------------------------------------
    // Tier properties on Green summaries (via aggregator)
    // -----------------------------------------------------------------------

    [Fact]
    public void Aggregate_AllPass_GreenSummary_BothTierVerdicts_AreGreen()
    {
        var findings = new[] { APass("T-01"), APass("T-02") };

        var result = _sut.Aggregate(findings, ct: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;
        summary.Signal.ShouldBe(VerdictSignal.Green);
        summary.BankTierVerdict.ShouldBe(VerdictSignal.Green,
            "Story 1.1: no bank-tier data yet — BankTierVerdict defaults Green for Green summaries.");
        summary.CondusefTierVerdict.ShouldBe(VerdictSignal.Green,
            "Story 1.1: all rules are CONDUSEF-mandated by default — CondusefTierVerdict mirrors overall Green.");
    }

    // -----------------------------------------------------------------------
    // Tier properties on Red summaries (via aggregator)
    // -----------------------------------------------------------------------

    [Fact]
    public void Aggregate_AnyFail_RedSummary_CondusefTierVerdict_IsRed_BankTierVerdict_IsGreen()
    {
        var findings = new[] { APass("T-01"), AFail("T-FAIL-A") };

        var result = _sut.Aggregate(findings, ct: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;
        summary.Signal.ShouldBe(VerdictSignal.Red);
        summary.CondusefTierVerdict.ShouldBe(VerdictSignal.Red,
            "Story 1.1: every rule is CONDUSEF-mandated by default — a Red overall maps to CondusefTierVerdict = Red.");
        summary.BankTierVerdict.ShouldBe(VerdictSignal.Green,
            "Story 1.1: no bank-tier rule classification yet — BankTierVerdict defaults Green.");
    }

    // -----------------------------------------------------------------------
    // Tier properties on Blocked summaries (via aggregator)
    // -----------------------------------------------------------------------

    [Fact]
    public void Aggregate_Blocked_BothTierVerdicts_AreBlocked()
    {
        var result = _sut.Aggregate(
            findings: [],
            blocked: ABlockedOutcome(),
            ct: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;
        summary.Signal.ShouldBe(VerdictSignal.Blocked);
        summary.BankTierVerdict.ShouldBe(VerdictSignal.Blocked,
            "Story 1.1: Blocked takes absolute precedence — BankTierVerdict mirrors Blocked.");
        summary.CondusefTierVerdict.ShouldBe(VerdictSignal.Blocked,
            "Story 1.1: Blocked takes absolute precedence — CondusefTierVerdict mirrors Blocked.");
    }

    // -----------------------------------------------------------------------
    // CombineOverallSignal — the two-tier combination rule
    // -----------------------------------------------------------------------

    [Fact]
    public void CombineOverallSignal_BothGreen_ReturnsGreen()
    {
        var overall = VerdictSummary.CombineOverallSignal(
            bankTier: VerdictSignal.Green,
            condusefTier: VerdictSignal.Green);

        overall.ShouldBe(VerdictSignal.Green,
            "GREEN overall requires both tier verdicts to be Green.");
    }

    [Fact]
    public void CombineOverallSignal_CondusefRed_ReturnsRed_RegardlessOfBankTier()
    {
        // CONDUSEF-red dominates even when bank tier is Green.
        var overallGreenBank = VerdictSummary.CombineOverallSignal(
            bankTier: VerdictSignal.Green,
            condusefTier: VerdictSignal.Red);

        overallGreenBank.ShouldBe(VerdictSignal.Red,
            "RED overall when CONDUSEF tier is Red, regardless of bank tier.");

        // CONDUSEF-red dominates even when bank tier is Yellow.
        var overallYellowBank = VerdictSummary.CombineOverallSignal(
            bankTier: VerdictSignal.Yellow,
            condusefTier: VerdictSignal.Red);

        overallYellowBank.ShouldBe(VerdictSignal.Red,
            "CONDUSEF Red takes precedence over bank Yellow — regulatory floor failed.");
    }

    [Fact]
    public void CombineOverallSignal_BankYellow_CondusefGreen_ReturnsYellow()
    {
        var overall = VerdictSummary.CombineOverallSignal(
            bankTier: VerdictSignal.Yellow,
            condusefTier: VerdictSignal.Green);

        overall.ShouldBe(VerdictSignal.Yellow,
            "YELLOW overall when bank tier has improvement opportunities and CONDUSEF floor is satisfied.");
    }

    // -----------------------------------------------------------------------
    // Regression: existing Signal values unchanged by Story 1.1 additions
    // -----------------------------------------------------------------------

    [Fact]
    public void Aggregate_ExistingBehavior_SignalUnchanged_AfterTierSurfaceAdded()
    {
        // Green regression
        var greenResult = _sut.Aggregate(
            new[] { APass("R-01"), APass("R-02") },
            ct: TestContext.Current.CancellationToken);
        greenResult.Value!.Signal.ShouldBe(VerdictSignal.Green);

        // Red regression
        var redResult = _sut.Aggregate(
            new[] { APass("R-01"), AFail("R-FAIL") },
            ct: TestContext.Current.CancellationToken);
        redResult.Value!.Signal.ShouldBe(VerdictSignal.Red);

        // Blocked regression
        var blockedResult = _sut.Aggregate(
            findings: [],
            blocked: ABlockedOutcome(),
            ct: TestContext.Current.CancellationToken);
        blockedResult.Value!.Signal.ShouldBe(VerdictSignal.Blocked);
    }
}
