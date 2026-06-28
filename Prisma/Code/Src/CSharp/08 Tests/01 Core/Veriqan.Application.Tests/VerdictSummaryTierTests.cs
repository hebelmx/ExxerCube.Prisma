using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using System.Collections.Generic;

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

    // -----------------------------------------------------------------------
    // Story 1.2: tier-map partition logic in VerdictAggregator
    // -----------------------------------------------------------------------

    /// <summary>
    /// When only bank-tier checks fail (and no CONDUSEF-tier checks fail),
    /// the overall signal must be Yellow, BankTierVerdict = Yellow,
    /// CondusefTierVerdict = Green.
    /// </summary>
    [Fact]
    public void Aggregate_WithTierMap_BankOnlyFail_OverallYellow_CondusefGreen()
    {
        var tierMap = new Dictionary<string, ChecklistTier>
        {
            ["CL-BANK-ONLY"] = ChecklistTier.Bank,
        };

        var findings = new[] { APass("CL-01"), AFail("CL-BANK-ONLY") };

        var result = _sut.Aggregate(
            findings,
            ct: TestContext.Current.CancellationToken,
            checklistTiers: tierMap);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;

        summary.Signal.ShouldBe(VerdictSignal.Yellow,
            "Bank-only fail with no CONDUSEF-tier fail → overall Yellow.");
        summary.BankTierVerdict.ShouldBe(VerdictSignal.Yellow,
            "At least one bank-tier check failed → BankTierVerdict = Yellow.");
        summary.CondusefTierVerdict.ShouldBe(VerdictSignal.Green,
            "No CONDUSEF-tier check failed → CondusefTierVerdict = Green.");

        summary.BankFailCheckIds.ShouldContain("CL-BANK-ONLY");
        summary.CondusefFailCheckIds.ShouldBeEmpty();
        summary.FailCheckIds.ShouldContain("CL-BANK-ONLY",
            "FailCheckIds must still list all fails regardless of tier.");
    }

    /// <summary>
    /// When a CONDUSEF-tier check fails (and no bank-tier check fails),
    /// the overall signal must be Red, CondusefTierVerdict = Red,
    /// BankTierVerdict = Green.
    /// </summary>
    [Fact]
    public void Aggregate_WithTierMap_CondusefOnlyFail_OverallRed_BankGreen()
    {
        var tierMap = new Dictionary<string, ChecklistTier>
        {
            ["LAW-COND-ONLY"] = ChecklistTier.Condusef,
        };

        var findings = new[] { APass("CL-01"), AFail("LAW-COND-ONLY") };

        var result = _sut.Aggregate(
            findings,
            ct: TestContext.Current.CancellationToken,
            checklistTiers: tierMap);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;

        summary.Signal.ShouldBe(VerdictSignal.Red,
            "CONDUSEF-tier fail → overall Red (regulatory floor breached).");
        summary.CondusefTierVerdict.ShouldBe(VerdictSignal.Red,
            "At least one CONDUSEF-tier check failed → CondusefTierVerdict = Red.");
        summary.BankTierVerdict.ShouldBe(VerdictSignal.Green,
            "No bank-tier check failed → BankTierVerdict = Green.");

        summary.CondusefFailCheckIds.ShouldContain("LAW-COND-ONLY");
        summary.BankFailCheckIds.ShouldBeEmpty();
    }

    /// <summary>
    /// When a Both-tier check fails it escalates BOTH tiers:
    /// CondusefTierVerdict = Red, BankTierVerdict = Yellow, overall = Red.
    /// </summary>
    [Fact]
    public void Aggregate_WithTierMap_BothTierFail_CondusefRedAndBankYellow_OverallRed()
    {
        var tierMap = new Dictionary<string, ChecklistTier>
        {
            ["CL-BOTH"] = ChecklistTier.Both,
        };

        var findings = new[] { APass("CL-01"), AFail("CL-BOTH") };

        var result = _sut.Aggregate(
            findings,
            ct: TestContext.Current.CancellationToken,
            checklistTiers: tierMap);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;

        summary.Signal.ShouldBe(VerdictSignal.Red,
            "Both-tier fail → CONDUSEF Red → overall Red (CONDUSEF takes precedence).");
        summary.CondusefTierVerdict.ShouldBe(VerdictSignal.Red,
            "Both-tier fail escalates CONDUSEF tier to Red.");
        summary.BankTierVerdict.ShouldBe(VerdictSignal.Yellow,
            "Both-tier fail also escalates Bank tier to Yellow.");

        summary.CondusefFailCheckIds.ShouldContain("CL-BOTH",
            "A Both-tier fail appears in CondusefFailCheckIds.");
        summary.BankFailCheckIds.ShouldContain("CL-BOTH",
            "A Both-tier fail also appears in BankFailCheckIds.");
    }

    /// <summary>
    /// A null tier map must preserve byte-for-byte legacy behavior:
    /// all fails treated as CONDUSEF, BankTierVerdict = Green,
    /// CondusefTierVerdict mirrors overall Signal, partition lists empty.
    /// </summary>
    [Fact]
    public void Aggregate_NullTierMap_LegacyBehaviorUnchanged_PartitionListsEmpty()
    {
        var findings = new[] { APass("CL-01"), AFail("CL-FAIL") };

        // Legacy call — no checklistTiers param
        var result = _sut.Aggregate(
            findings,
            ct: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;

        summary.Signal.ShouldBe(VerdictSignal.Red,
            "Legacy path: any fail → Red, unchanged.");
        summary.BankTierVerdict.ShouldBe(VerdictSignal.Green,
            "Legacy path: BankTierVerdict always defaults Green.");
        summary.CondusefTierVerdict.ShouldBe(VerdictSignal.Red,
            "Legacy path: CondusefTierVerdict mirrors overall Red.");

        summary.BankFailCheckIds.ShouldBeEmpty(
            "Legacy path (null map): BankFailCheckIds is always empty.");
        summary.CondusefFailCheckIds.ShouldBeEmpty(
            "Legacy path (null map): CondusefFailCheckIds is always empty.");
    }

    /// <summary>
    /// A CheckId NOT present in the tier map must be treated as Condusef
    /// (conservative default) — it must appear in CondusefFailCheckIds and
    /// escalate CondusefTierVerdict to Red.
    /// </summary>
    [Fact]
    public void Aggregate_WithTierMap_UnmappedCheckId_DefaultsToCondusef()
    {
        // Tier map does NOT contain "CL-UNMAPPED"
        var tierMap = new Dictionary<string, ChecklistTier>
        {
            ["CL-OTHER"] = ChecklistTier.Bank,
        };

        var findings = new[] { AFail("CL-UNMAPPED") };

        var result = _sut.Aggregate(
            findings,
            ct: TestContext.Current.CancellationToken,
            checklistTiers: tierMap);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;

        summary.Signal.ShouldBe(VerdictSignal.Red,
            "Unmapped CheckId defaults to Condusef → Condusef Red → overall Red.");
        summary.CondusefTierVerdict.ShouldBe(VerdictSignal.Red,
            "Unmapped CheckId is treated as Condusef — conservative default.");
        summary.BankTierVerdict.ShouldBe(VerdictSignal.Green,
            "No bank-tier fail (unmapped is Condusef only).");

        summary.CondusefFailCheckIds.ShouldContain("CL-UNMAPPED");
        summary.BankFailCheckIds.ShouldBeEmpty();
    }

    /// <summary>
    /// InsufficientData findings must NEVER escalate to a tier fail,
    /// even when the check is mapped to a tier.
    /// </summary>
    [Fact]
    public void Aggregate_WithTierMap_InsufficientDataNeverEscalates()
    {
        var tierMap = new Dictionary<string, ChecklistTier>
        {
            ["CL-INSUFF"] = ChecklistTier.Condusef,
        };

        // Only InsufficientData — no Fail findings
        var findings = new[]
        {
            RuleFinding.InsufficientData("CL-INSUFF", TechniqueClass.Deterministic, "1.0.0", reason: "no data"),
        };

        var result = _sut.Aggregate(
            findings,
            ct: TestContext.Current.CancellationToken,
            checklistTiers: tierMap);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;

        summary.Signal.ShouldBe(VerdictSignal.Green,
            "InsufficientData findings must not escalate to Red even with tier map.");
        summary.CondusefTierVerdict.ShouldBe(VerdictSignal.Green,
            "InsufficientData abstains — no tier escalation.");
        summary.BankTierVerdict.ShouldBe(VerdictSignal.Green);

        summary.BankFailCheckIds.ShouldBeEmpty();
        summary.CondusefFailCheckIds.ShouldBeEmpty();
        summary.InsufficientDataCount.ShouldBe(1);
    }
}
