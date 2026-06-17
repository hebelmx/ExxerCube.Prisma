using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Visual.DependencyInjection;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Visual.Tests;

/// <summary>
/// Unit tests for the CL-28 text-overlap detection rule (Story 5.2).
/// All tests are pure in-memory — no PDF fixtures required.
/// Rules are discovered via the same Scrutor DI path used in production.
/// </summary>
public sealed class Cl28TextOverlapRuleTests
{
    // -----------------------------------------------------------------------
    // DI factory
    // -----------------------------------------------------------------------

    private static IVecValidationRule GetCl28Rule()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeriqanVisual();

        using var sp = services.BuildServiceProvider();
        return sp.GetServices<IVecValidationRule>()
            .Single(r => r.CheckId == "CL-28");
    }

    // -----------------------------------------------------------------------
    // Fixture helpers
    // -----------------------------------------------------------------------

    private const string ProductId = "TC-CL28-TEST";

    private static VecProduct Product() =>
        new(ProductId: ProductId, ProductName: "Test Card",
            Aliases: null, HasRewardsProgram: false,
            CardImage: null, ImportantMessageImage: null,
            Tariffs: null);

    private static VecReferenceBundle Bundle() =>
        new(
            BundleMetadata: new("1.0.0", "Test Bank", null, null, null, null),
            Products: [Product()],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null);

    private static FieldLocator P1() => FieldLocator.PageHint(1);

    private static StatementModel EmptyModel(
        IReadOnlyList<TextOverlapIncident>? incidents = null) =>
        new(
            clientName: ExtractedField<ExtractedClientName>.Missing(P1()),
            address: ExtractedField<ExtractedAddress>.Missing(P1()),
            branchNumber: ExtractedField<string>.Missing(P1()),
            cardNumber: ExtractedField<string>.Missing(P1()),
            clabe: ExtractedField<string>.Missing(P1()),
            clientNumber: ExtractedField<string>.Missing(P1()),
            rfc: ExtractedField<string>.Missing(P1()))
        {
            TextOverlapIncidents = incidents ?? [],
        };

    private static TextOverlapIncident Incident(double overlapPts, int page = 1) =>
        new(page, overlapPts, new FieldLocator(page, 100.0, 300.0, 50.0, 10.0),
            $"'wordA' ∩ 'wordB' ({overlapPts:0.##} pt)");

    private static VerificationContext Ctx(StatementModel? model) =>
        new(bundle: Bundle(),
            resolvedProduct: Product(),
            availability: ReferenceDataAvailability.FromBundle(Bundle()),
            priorStatement: null,
            toleranceConfig: null,
            statementModel: model);

    // -----------------------------------------------------------------------
    // Test 1: No incidents → Pass
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_NoIncidents_ReturnsPass()
    {
        var ct = TestContext.Current.CancellationToken;
        var model = EmptyModel(incidents: []);
        var rule = GetCl28Rule();

        var result = rule.Evaluate(Ctx(model), ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
        result.Value.CheckId.ShouldBe("CL-28");
        // Threshold must be recorded (ADR-V3).
        result.Value.ToleranceApplied.ShouldNotBeNull();
    }

    // -----------------------------------------------------------------------
    // Test 2: Incident above threshold → Fail with locator and threshold
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_IncidentAboveThreshold_ReturnsFail()
    {
        var ct = TestContext.Current.CancellationToken;
        // Default threshold is 1.0 pt; incident at 3.5 pt must trigger Fail.
        var model = EmptyModel(incidents: [Incident(3.5)]);
        var rule = GetCl28Rule();

        var result = rule.Evaluate(Ctx(model), ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        finding.CheckId.ShouldBe("CL-28");
        finding.Locator.ShouldNotBeNull();
        finding.Locator!.PageNumber.ShouldBe(1);
        finding.Observed.ShouldNotBeNullOrEmpty();
        finding.Observed!.ShouldContain("3.5");
        // Threshold must be recorded on failure (ADR-V3).
        finding.ToleranceApplied.ShouldNotBeNull();
        finding.ToleranceApplied!.Value.ShouldBe(1.0m);
    }

    // -----------------------------------------------------------------------
    // Test 3: Incident below threshold → Pass
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_IncidentBelowThreshold_ReturnsPass()
    {
        var ct = TestContext.Current.CancellationToken;
        // Default threshold is 1.0 pt; an extractor epsilon survivor is ≥ 2.0 pt,
        // but simulate an incident exactly at threshold boundary — 0.5 pt ≤ 1.0 pt → Pass.
        var model = EmptyModel(incidents: [Incident(0.5)]);
        var rule = GetCl28Rule();

        var result = rule.Evaluate(Ctx(model), ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.Pass);
    }

    // -----------------------------------------------------------------------
    // Test 4: Null StatementModel → InsufficientData
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_NullStatementModel_ReturnsInsufficientData()
    {
        var ct = TestContext.Current.CancellationToken;
        var rule = GetCl28Rule();

        var result = rule.Evaluate(Ctx(model: null), ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Verdict.ShouldBe(FindingVerdict.InsufficientData);
        result.Value.CheckId.ShouldBe("CL-28");
    }

    // -----------------------------------------------------------------------
    // Test 5: Cancellation → Cancelled result
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_Cancelled_ReturnsCancelledResult()
    {
        var rule = GetCl28Rule();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = rule.Evaluate(Ctx(model: null), cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // Test 6: Multiple incidents — worst offender reported
    // -----------------------------------------------------------------------

    [Fact]
    public void Evaluate_MultipleIncidents_ReportsWorstOffender()
    {
        var ct = TestContext.Current.CancellationToken;
        var model = EmptyModel(incidents:
        [
            Incident(2.5, page: 1),
            Incident(5.8, page: 2),
            Incident(3.1, page: 1),
        ]);
        var rule = GetCl28Rule();

        var result = rule.Evaluate(Ctx(model), ct);

        result.IsSuccess.ShouldBeTrue();
        var finding = result.Value!;
        finding.Verdict.ShouldBe(FindingVerdict.Fail);
        // Worst offender (5.8 pt on page 2) must be in the observed text.
        var observed = finding.Observed;
        observed.ShouldNotBeNullOrEmpty();
        observed!.ShouldContain("5.8");
        finding.Locator!.PageNumber.ShouldBe(2);
        // Total count mentioned.
        observed.ShouldContain("3");
    }
}
