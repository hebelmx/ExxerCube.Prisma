using ExxerCube.Prisma.Veriqan.Application.Tenant;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;
using ExxerCube.Prisma.Veriqan.Domain.Tolerances;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;
using NSubstitute;

namespace ExxerCube.Prisma.Veriqan.Application.Tests.Tenant;

/// <summary>
/// Unit tests for <see cref="TenantProfileResolver"/> covering the classification-gating rules
/// and deviation surfacing for Story 9.3b.
/// </summary>
public sealed class TenantProfileResolverTests
{
    // -----------------------------------------------------------------------
    // CL-10 tolerance spec: LegalDefault=0.50, Min=0.00, Max=1.00
    // -----------------------------------------------------------------------

    private const string CL10 = "CL-10";
    private const decimal CL10LegalDefault = 0.50m;
    private const decimal CL10Min = 0.00m;
    private const decimal CL10Max = 1.00m;

    private static Tolerance CL10Tolerance() =>
        new(legalDefault: CL10LegalDefault, min: CL10Min, max: CL10Max);

    // -----------------------------------------------------------------------
    // Helpers — build mock provider and rule list
    // -----------------------------------------------------------------------

    private static ILegalToleranceProvider BuildProvider(string checkId, Tolerance tolerance)
    {
        var provider = Substitute.For<ILegalToleranceProvider>();
        provider.Has(checkId).Returns(true);
        provider.For(checkId).Returns(tolerance);
        return provider;
    }

    private static ILegalToleranceProvider BuildEmptyProvider()
    {
        var provider = Substitute.For<ILegalToleranceProvider>();
        provider.Has(Arg.Any<string>()).Returns(false);
        return provider;
    }

    private static IVecValidationRule BuildRule(
        string checkId,
        RuleClassification classification)
    {
        var rule = Substitute.For<IVecValidationRule>();
        rule.CheckId.Returns(checkId);
        rule.Classification.Returns(classification);
        return rule;
    }

    private static TenantProfileResolver Sut() => new();

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// The legal-baseline profile has no overrides — result must contain no effective
    /// tolerances and no deviations.
    /// </summary>
    [Fact]
    public void Resolve_LegalBaselineProfile_ReturnsAllLegalDefaults_NoDeviations()
    {
        var ct = TestContext.Current.CancellationToken;
        var profile = TenantProfile.LegalBaseline();
        var provider = BuildProvider(CL10, CL10Tolerance());
        var rules = new List<IVecValidationRule> { BuildRule(CL10, RuleClassification.TenantTightenableOnly) };

        var result = Sut().Resolve(profile, provider, rules, ct);

        result.IsSuccess.ShouldBeTrue();
        var resolved = result.Value!;
        resolved.TenantId.ShouldBe("LEGAL-BASELINE");
        resolved.EffectiveTolerances.Count.ShouldBe(0, "no overrides — map stays empty");
        resolved.HasDeviations.ShouldBeFalse();
    }

    /// <summary>
    /// A tightening override (0.25 &lt; LegalDefault 0.50) on a TenantTightenableOnly rule
    /// must be accepted and appear in EffectiveTolerances with no deviation.
    /// </summary>
    [Fact]
    public void Resolve_TighteningOverride_TenantTightenableOnly_AppliesOverride()
    {
        var ct = TestContext.Current.CancellationToken;
        var overrides = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            [CL10] = 0.25m  // < LegalDefault 0.50 → tightening
        };
        var profile = new TenantProfile("T-001", "Tenant One", overrides);
        var provider = BuildProvider(CL10, CL10Tolerance());
        var rules = new List<IVecValidationRule> { BuildRule(CL10, RuleClassification.TenantTightenableOnly) };

        var result = Sut().Resolve(profile, provider, rules, ct);

        result.IsSuccess.ShouldBeTrue();
        var resolved = result.Value!;
        resolved.EffectiveTolerances[CL10].ShouldBe(0.25m);
        resolved.HasDeviations.ShouldBeFalse();
        resolved.GetEffectiveTolerance(CL10, CL10LegalDefault).ShouldBe(0.25m);
    }

    /// <summary>
    /// A loosening override (0.75 &gt; LegalDefault 0.50, but within [0,1]) on a
    /// TenantTightenableOnly rule must be rejected and produce a deviation. The effective
    /// tolerance falls back to LegalDefault.
    /// </summary>
    [Fact]
    public void Resolve_LoosenOverride_TenantTightenableOnly_RejectsAndDeviates()
    {
        var ct = TestContext.Current.CancellationToken;
        var overrides = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            [CL10] = 0.75m  // > LegalDefault 0.50 → loosening
        };
        var profile = new TenantProfile("T-002", "Tenant Two", overrides);
        var provider = BuildProvider(CL10, CL10Tolerance());
        var rules = new List<IVecValidationRule> { BuildRule(CL10, RuleClassification.TenantTightenableOnly) };

        var result = Sut().Resolve(profile, provider, rules, ct);

        result.IsSuccess.ShouldBeTrue();
        var resolved = result.Value!;
        resolved.EffectiveTolerances.ContainsKey(CL10).ShouldBeFalse("loosening rejected — not in effective map");
        resolved.HasDeviations.ShouldBeTrue();

        var dev = resolved.Deviations[0];
        dev.CheckId.ShouldBe(CL10);
        dev.RequestedValue.ShouldBe(0.75m);
        dev.LegalDefaultUsed.ShouldBe(CL10LegalDefault);
        dev.Reason.ShouldContain("loosening");

        // Fallback to LegalDefault when override was rejected
        resolved.GetEffectiveTolerance(CL10, CL10LegalDefault).ShouldBe(CL10LegalDefault);
    }

    /// <summary>
    /// Any override on a BaselineLocked rule must be rejected and produce a deviation.
    /// The effective tolerance map must not contain the check ID.
    /// </summary>
    [Fact]
    public void Resolve_BaselineLockedOverride_RejectsAndDeviates()
    {
        var ct = TestContext.Current.CancellationToken;
        const string checkId = "BL-RULE";
        var tolerance = new Tolerance(legalDefault: 1.00m, min: 0.00m, max: 2.00m);
        var overrides = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            [checkId] = 0.50m
        };
        var profile = new TenantProfile("T-003", "Tenant Three", overrides);
        var provider = BuildProvider(checkId, tolerance);
        var rules = new List<IVecValidationRule> { BuildRule(checkId, RuleClassification.BaselineLocked) };

        var result = Sut().Resolve(profile, provider, rules, ct);

        result.IsSuccess.ShouldBeTrue();
        var resolved = result.Value!;
        resolved.EffectiveTolerances.ContainsKey(checkId).ShouldBeFalse();
        resolved.HasDeviations.ShouldBeTrue();

        var dev = resolved.Deviations[0];
        dev.CheckId.ShouldBe(checkId);
        dev.RequestedValue.ShouldBe(0.50m);
        dev.LegalDefaultUsed.ShouldBe(1.00m);
        dev.Reason.ShouldContain("BaselineLocked");
    }

    /// <summary>
    /// An override below Min (e.g. -0.10 for a tolerance with Min=0.00) must be rejected
    /// and produce a deviation regardless of classification.
    /// </summary>
    [Fact]
    public void Resolve_OverrideOutsideRange_RejectsAndDeviates()
    {
        var ct = TestContext.Current.CancellationToken;
        var overrides = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            [CL10] = -0.10m  // < Min 0.00 → out of range
        };
        var profile = new TenantProfile("T-004", "Tenant Four", overrides);
        var provider = BuildProvider(CL10, CL10Tolerance());
        var rules = new List<IVecValidationRule> { BuildRule(CL10, RuleClassification.TenantTightenableOnly) };

        var result = Sut().Resolve(profile, provider, rules, ct);

        result.IsSuccess.ShouldBeTrue();
        var resolved = result.Value!;
        resolved.EffectiveTolerances.ContainsKey(CL10).ShouldBeFalse();
        resolved.HasDeviations.ShouldBeTrue();

        var dev = resolved.Deviations[0];
        dev.CheckId.ShouldBe(CL10);
        dev.RequestedValue.ShouldBe(-0.10m);
        dev.LegalDefaultUsed.ShouldBe(CL10LegalDefault);
        dev.Reason.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// A TenantOverridable rule that receives an override within [Min, Max] must be accepted.
    /// </summary>
    [Fact]
    public void Resolve_TenantOverridable_WithinRange_Applies()
    {
        var ct = TestContext.Current.CancellationToken;
        const string checkId = "OV-RULE";
        var tolerance = new Tolerance(legalDefault: 1.00m, min: 0.50m, max: 2.00m);
        var overrides = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            [checkId] = 1.50m  // within [0.50, 2.00]
        };
        var profile = new TenantProfile("T-005", "Tenant Five", overrides);
        var provider = BuildProvider(checkId, tolerance);
        var rules = new List<IVecValidationRule> { BuildRule(checkId, RuleClassification.TenantOverridable) };

        var result = Sut().Resolve(profile, provider, rules, ct);

        result.IsSuccess.ShouldBeTrue();
        var resolved = result.Value!;
        resolved.EffectiveTolerances[checkId].ShouldBe(1.50m);
        resolved.HasDeviations.ShouldBeFalse();
    }

    /// <summary>
    /// A profile with no overrides and no tolerance provider registrations must yield
    /// empty effective tolerances and empty deviations.
    /// </summary>
    [Fact]
    public void Resolve_DefaultProfile_NoOverrides_EmptyDeviations()
    {
        var ct = TestContext.Current.CancellationToken;
        var profile = new TenantProfile("T-006", "Tenant Six");
        var provider = BuildEmptyProvider();
        var rules = new List<IVecValidationRule>();

        var result = Sut().Resolve(profile, provider, rules, ct);

        result.IsSuccess.ShouldBeTrue();
        var resolved = result.Value!;
        resolved.EffectiveTolerances.Count.ShouldBe(0);
        resolved.Deviations.Count.ShouldBe(0);
        resolved.HasDeviations.ShouldBeFalse();
    }

    /// <summary>
    /// When the cancellation token is already cancelled the resolver returns a cancelled Result.
    /// </summary>
    [Fact]
    public void Resolve_Cancelled_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var profile = TenantProfile.LegalBaseline();
        var provider = BuildEmptyProvider();
        var rules = new List<IVecValidationRule>();

        var result = Sut().Resolve(profile, provider, rules, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // Fix B: MinFieldConfidence legal floor enforcement (Story 9 remediation)
    // -----------------------------------------------------------------------

    /// <summary>
    /// A tenant that raises MinFieldConfidence above the legal floor (e.g. 0.95) must have
    /// the higher value accepted — the resolved profile carries 0.95, no deviation recorded.
    /// </summary>
    [Fact]
    public void Resolve_MinFieldConfidenceRaisedAboveFloor_AcceptsAndCarriesValue()
    {
        var ct = TestContext.Current.CancellationToken;
        // Tenant wants 0.95 — stricter than the legal floor of 0.8 — allowed.
        var profile = new TenantProfile("T-RAISE", "Tenant Raise", minFieldConfidence: 0.95);
        var provider = BuildEmptyProvider();
        var rules = new List<IVecValidationRule>();

        var result = Sut().Resolve(profile, provider, rules, ct);

        result.IsSuccess.ShouldBeTrue();
        var resolved = result.Value!;
        resolved.MinFieldConfidence.ShouldBe(0.95,
            "Raising the confidence threshold is allowed; the resolved profile must carry 0.95.");
        resolved.HasDeviations.ShouldBeFalse(
            "Raising confidence generates no deviation.");
    }

    /// <summary>
    /// A tenant that sets MinFieldConfidence below the legal floor (e.g. 0.5) must have
    /// the value rejected: a deviation with CheckId = "MIN-FIELD-CONFIDENCE" is recorded
    /// and the effective value is clamped to the legal floor (0.8).
    /// </summary>
    [Fact]
    public void Resolve_MinFieldConfidenceBelowFloor_RejectsAndDeviates_EffectiveValueIsFloor()
    {
        var ct = TestContext.Current.CancellationToken;
        // 0.5 is below the legal floor of 0.8 — must be rejected.
        var profile = new TenantProfile("T-SUB-FLOOR", "Tenant Sub-floor", minFieldConfidence: 0.5);
        var provider = BuildEmptyProvider();
        var rules = new List<IVecValidationRule>();

        var result = Sut().Resolve(profile, provider, rules, ct);

        result.IsSuccess.ShouldBeTrue();
        var resolved = result.Value!;

        // Effective value must be the legal floor.
        resolved.MinFieldConfidence.ShouldBe(TenantProfile.MinFieldConfidenceLegalFloor,
            "Sub-floor value must be rejected; effective value falls back to the legal floor (0.8).");

        // A deviation must be recorded for the rejection.
        resolved.HasDeviations.ShouldBeTrue();
        var dev = resolved.Deviations
            .FirstOrDefault(d => d.CheckId == "MIN-FIELD-CONFIDENCE");
        dev.ShouldNotBeNull("A TenantDeviation with CheckId='MIN-FIELD-CONFIDENCE' must be recorded.");
        dev!.RequestedValue.ShouldBe(0.5m);
        dev.LegalDefaultUsed.ShouldBe((decimal)TenantProfile.MinFieldConfidenceLegalFloor);
        dev.Reason.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// A profile with the default MinFieldConfidence (0.8 = the legal floor) must be accepted
    /// with no deviation. (Fix B — no spurious deviation at the exact floor value.)
    /// </summary>
    [Fact]
    public void Resolve_DefaultProfile_MinFieldConfidenceAtFloor_NoDeviation()
    {
        var ct = TestContext.Current.CancellationToken;
        var profile = TenantProfile.LegalBaseline(); // MinFieldConfidence = 0.8
        var provider = BuildEmptyProvider();
        var rules = new List<IVecValidationRule>();

        var result = Sut().Resolve(profile, provider, rules, ct);

        result.IsSuccess.ShouldBeTrue();
        var resolved = result.Value!;
        resolved.MinFieldConfidence.ShouldBe(TenantProfile.LegalMinFieldConfidenceDefault);
        resolved.Deviations
            .ShouldNotContain(d => d.CheckId == "MIN-FIELD-CONFIDENCE",
                "0.8 is exactly the legal floor — no deviation should be generated.");
    }

    // -----------------------------------------------------------------------
    // Fix E: deviation ordering (determinism)
    // -----------------------------------------------------------------------

    /// <summary>
    /// When multiple overrides are rejected the resulting
    /// <see cref="ResolvedTenantProfile.Deviations"/> list must be sorted by CheckId (ordinal)
    /// so the audit/output order is deterministic regardless of iteration order.
    /// </summary>
    [Fact]
    public void Resolve_MultipleRejectedOverrides_DeviationsAreSortedByCheckId()
    {
        var ct = TestContext.Current.CancellationToken;

        // Override two BaselineLocked rules whose CheckIds are intentionally NOT in
        // alphabetical order in the dictionary — the result must still be sorted.
        const string id1 = "ZZ-LOCKED";
        const string id2 = "AA-LOCKED";

        var tol = new Tolerance(legalDefault: 1.00m, min: 0.00m, max: 2.00m);
        var provider = Substitute.For<ILegalToleranceProvider>();
        provider.Has(id1).Returns(true);
        provider.Has(id2).Returns(true);
        provider.For(id1).Returns(tol);
        provider.For(id2).Returns(tol);

        var rules = new List<IVecValidationRule>
        {
            BuildRule(id1, RuleClassification.BaselineLocked),
            BuildRule(id2, RuleClassification.BaselineLocked),
        };

        var overrides = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            [id1] = 0.5m,
            [id2] = 0.5m,
        };
        var profile = new TenantProfile("T-ORDER", "Tenant Order", overrides);

        var result = Sut().Resolve(profile, provider, rules, ct);

        result.IsSuccess.ShouldBeTrue();
        var resolved = result.Value!;
        resolved.Deviations.Count.ShouldBe(2);

        // Deviations must be in CheckId ordinal order: AA-LOCKED < ZZ-LOCKED.
        resolved.Deviations[0].CheckId.ShouldBe(id2,
            "Deviations must be sorted by CheckId; 'AA-LOCKED' < 'ZZ-LOCKED' (ordinal).");
        resolved.Deviations[1].CheckId.ShouldBe(id1);
    }
}
