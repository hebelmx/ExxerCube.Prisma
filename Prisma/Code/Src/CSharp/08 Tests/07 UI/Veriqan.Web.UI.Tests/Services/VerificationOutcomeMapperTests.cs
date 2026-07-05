using ExxerCube.Prisma.Veriqan.Application.Verdict;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using ExxerCube.Prisma.Veriqan.Web.UI.Services;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Tests.Services;

/// <summary>
/// Verifies <see cref="VerificationOutcomeMapper"/> (VLD-S4a) enriches findings from the real
/// check ledger — honest tiers, human-readable labels, and law-vs-brand DOF-numeral citations —
/// instead of the prior hardcoded <c>Tier = Condusef</c> / <c>Label = CheckId</c> placeholder.
/// </summary>
/// <remarks>
/// <see cref="VerdictSummary"/> has no public constructor outside <c>Veriqan.Application</c> —
/// these tests build a real summary via the public <see cref="VerdictAggregator"/> (mirrors how
/// the production pipeline produces one), rather than a ledger-agnostic mock.
/// </remarks>
public sealed class VerificationOutcomeMapperTests
{
    private static readonly VerdictAggregator Aggregator = new();

    private static VerificationOutcome CreateOutcome(params RuleFinding[] findings)
    {
        var summary = Aggregator.Aggregate(findings).Value!;
        var job = new VerificationJob(Guid.NewGuid(), "dummy-hash", DateTimeOffset.UtcNow, VerificationJobStatus.Completed);
        return new VerificationOutcome(job, summary, findings);
    }

    [Fact]
    public void Map_RedOutcome_PopulatesFindingsWithRealCheckIdsAndTiers()
    {
        var sut = new VerificationOutcomeMapper(new RealCheckLedger());

        var brandFinding = RuleFinding.Fail(
            "CL-35", TechniqueClass.LightweightCv, FindingSeverity.Warning, "1.0.0",
            expected: "Aptos", observed: "Arial");
        var lawFinding = RuleFinding.Fail(
            "LAW-TYPO-BOLD", TechniqueClass.Deterministic, FindingSeverity.Critical, "1.0.0",
            expected: "bold", observed: "regular");

        var outcome = CreateOutcome(brandFinding, lawFinding);

        var result = sut.Map(outcome, new Dictionary<int, byte[]>(), "estado-cuenta.pdf", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var findings = result.Value!.Findings;
        findings.Count.ShouldBe(2);

        var brand = findings.Single(f => f.CheckId == "CL-35");
        brand.Tier.ShouldBe(ChecklistTier.Bank);
        brand.IsVisual.ShouldBeTrue();
        brand.Label.ShouldNotBe(brand.CheckId);
        brand.DofNumeral.ShouldBe(RealCheckLedger.BrandTag);

        var law = findings.Single(f => f.CheckId == "LAW-TYPO-BOLD");
        law.Tier.ShouldBe(ChecklistTier.Condusef);
        law.IsVisual.ShouldBeTrue();
        law.Label.ShouldNotBe(law.CheckId);
    }

    [Fact]
    public void Map_UncataloguedCheckId_ShowsEngineeringGapLabel()
    {
        var sut = new VerificationOutcomeMapper(new RealCheckLedger());

        var finding = RuleFinding.Fail(
            "CL-DOESNOTEXIST", TechniqueClass.Deterministic, FindingSeverity.Warning, "1.0.0");

        var outcome = CreateOutcome(finding);

        var result = sut.Map(outcome, new Dictionary<int, byte[]>(), "estado-cuenta.pdf", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var mapped = result.Value!.Findings.Single();
        mapped.Label.ShouldBe(RealCheckLedger.UncataloguedLabel);
        mapped.Tier.ShouldBe(ChecklistTier.Condusef);
        mapped.IsVisual.ShouldBeFalse();
    }

    [Fact]
    public void Map_NullOutcome_ReturnsFailure()
    {
        var sut = new VerificationOutcomeMapper(new RealCheckLedger());

        var result = sut.Map(null!, new Dictionary<int, byte[]>(), "estado-cuenta.pdf", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Map_CancelledToken_ReturnsCancelled()
    {
        var sut = new VerificationOutcomeMapper(new RealCheckLedger());
        var outcome = CreateOutcome(RuleFinding.Pass("CL-1", TechniqueClass.Deterministic, "1.0.0"));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = sut.Map(outcome, new Dictionary<int, byte[]>(), "estado-cuenta.pdf", cts.Token);

        result.IsFailure.ShouldBeTrue();
        result.IsCancelled().ShouldBeTrue();
    }

    [Fact]
    public void Map_PassesThroughMarkedPagePngs()
    {
        var sut = new VerificationOutcomeMapper(new RealCheckLedger());
        var outcome = CreateOutcome(RuleFinding.Pass("CL-1", TechniqueClass.Deterministic, "1.0.0"));
        var pngs = new Dictionary<int, byte[]> { [1] = [0x89, 0x50, 0x4E, 0x47] };

        var result = sut.Map(outcome, pngs, "estado-cuenta.pdf", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.MarkedPagePngs.ShouldContainKey(1);
        result.Value.MarkedPagePngs[1].ShouldBe(pngs[1]);
    }
}
