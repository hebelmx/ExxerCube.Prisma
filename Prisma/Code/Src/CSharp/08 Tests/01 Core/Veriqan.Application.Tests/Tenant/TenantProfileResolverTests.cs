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
}
