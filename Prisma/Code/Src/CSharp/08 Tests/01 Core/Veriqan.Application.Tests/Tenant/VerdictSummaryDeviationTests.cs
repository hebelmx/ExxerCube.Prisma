using ExxerCube.Prisma.Veriqan.Application.Verdict;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;
using ExxerCube.Prisma.Veriqan.Domain.Verification;

namespace ExxerCube.Prisma.Veriqan.Application.Tests.Tenant;

/// <summary>
/// Tests that <see cref="VerdictSummary.TenantDeviations"/> is propagated correctly
/// through <see cref="VerdictAggregator"/> (Story 9.3b).
/// </summary>
public sealed class VerdictSummaryDeviationTests
{
    private static VerdictAggregator Sut() => new();

    private static TenantDeviation MakeDeviation(string checkId) =>
        new(
            CheckId: checkId,
            RequestedValue: 0.75m,
            LegalDefaultUsed: 0.50m,
            Reason: "TenantTightenableOnly rule does not permit loosening; override exceeds legal default.");

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the aggregator receives a non-empty deviation list the resulting
    /// <see cref="VerdictSummary.TenantDeviations"/> must expose them.
    /// </summary>
    [Fact]
    public void VerdictSummary_WithDeviations_ExposesDeviationsOnSummary()
    {
        var ct = TestContext.Current.CancellationToken;
        var deviation = MakeDeviation("CL-10");
        var deviations = new List<TenantDeviation> { deviation };

        // One passing finding → Green signal
        var findings = new List<RuleFinding>
        {
            RuleFinding.Pass(
                checkId: "CL-10",
                technique: TechniqueClass.Deterministic,
                engineVersion: "1.0.0",
                observed: "28.50%",
                toleranceApplied: 0.25m)
        };

        var result = Sut().Aggregate(findings, blocked: null, ct: ct, tenantDeviations: deviations);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;
        summary.Signal.ShouldBe(VerdictSignal.Green);
        summary.TenantDeviations.Count.ShouldBe(1);
        summary.TenantDeviations[0].CheckId.ShouldBe("CL-10");
        summary.TenantDeviations[0].RequestedValue.ShouldBe(0.75m);
        summary.TenantDeviations[0].LegalDefaultUsed.ShouldBe(0.50m);
    }

    /// <summary>
    /// When no deviation list is supplied the <see cref="VerdictSummary.TenantDeviations"/>
    /// must be an empty list (not null).
    /// </summary>
    [Fact]
    public void VerdictSummary_NoDeviations_EmptyList()
    {
        var ct = TestContext.Current.CancellationToken;

        var findings = new List<RuleFinding>
        {
            RuleFinding.Pass(
                checkId: "CL-21",
                technique: TechniqueClass.Deterministic,
                engineVersion: "1.0.0")
        };

        var result = Sut().Aggregate(findings, blocked: null, ct: ct, tenantDeviations: null);

        result.IsSuccess.ShouldBeTrue();
        var summary = result.Value!;
        summary.TenantDeviations.ShouldNotBeNull();
        summary.TenantDeviations.Count.ShouldBe(0);
    }
}
