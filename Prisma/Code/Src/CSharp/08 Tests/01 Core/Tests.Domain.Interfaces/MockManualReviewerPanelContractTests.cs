using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Tests.Domain.Interfaces;

/// <summary>
/// Blueprint instance of <see cref="ManualReviewerPanelContract"/> — the reference-fake-backed
/// inheritor that encodes the design specification (ADR-005 §6).
/// </summary>
/// <remarks>
/// <para>
/// Converted in Phase 5 of the ITDD refactor from the standalone mock-stub blueprint
/// <c>IIManualReviewerPanelTests</c>. Its 18 inline <c>Substitute.For</c> tests were tautological
/// (each stubbed a return then asserted it); the contract base now exercises those same behaviours
/// genuinely against the reference fake (and against the real service via the impl instance), so they
/// are not re-listed here. Its <c>CreateSut()</c> returns a fresh
/// <see cref="ManualReviewerPanelMockFactory"/> reference fake per test.
/// </para>
/// <para>
/// The four <em>fault-injection</em> tests below are kept on the blueprint as self-contained
/// <c>[Fact]</c>s (the Phase-3 "blueprint-only tests stay on the blueprint" rule): they assert "an
/// operation failure is surfaced as a failure Result, not a throw", which no healthy SUT can be made to
/// produce without fault injection. Preserved, never deleted (invariant 5).
/// </para>
/// </remarks>
public sealed class MockManualReviewerPanelContractTests : ManualReviewerPanelContract
{
    private readonly IManualReviewerPanel _faultyPanel = Substitute.For<IManualReviewerPanel>();

    /// <inheritdoc />
    protected override IManualReviewerPanel CreateSut()
        => ManualReviewerPanelMockFactory.CreateContractConformingMock();

    /// <summary>Blueprint: a query failure is surfaced as a failure Result, not a throw.</summary>
    [Fact]
    public async Task GetReviewCasesAsync_OnQueryError_ReturnsFailureResult()
    {
        var filters = new ReviewFilters { Status = ReviewStatus.Pending };
        _faultyPanel.GetReviewCasesAsync(filters, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<ReviewCase>>.WithFailure("Database query failed"));

        var result = await _faultyPanel.GetReviewCasesAsync(filters, 1, 50, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Database query failed");
    }

    /// <summary>Blueprint: a save failure is surfaced as a failure Result, not a throw.</summary>
    [Fact]
    public async Task SubmitReviewDecisionAsync_OnSaveError_ReturnsFailureResult()
    {
        var decision = new ReviewDecision
        {
            DecisionId = "DEC-001",
            CaseId = "CASE-001",
            DecisionType = DecisionType.Approve,
            ReviewerId = "REVIEWER-001",
            ReviewedAt = DateTime.UtcNow,
            Notes = "Approved"
        };
        _faultyPanel.SubmitReviewDecisionAsync("CASE-001", decision, Arg.Any<CancellationToken>())
            .Returns(Result.WithFailure("Database save failed"));

        var result = await _faultyPanel.SubmitReviewDecisionAsync("CASE-001", decision, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Database save failed");
    }

    /// <summary>Blueprint: a query failure is surfaced as a failure Result, not a throw.</summary>
    [Fact]
    public async Task GetFieldAnnotationsAsync_OnQueryError_ReturnsFailureResult()
    {
        _faultyPanel.GetFieldAnnotationsAsync("CASE-001", Arg.Any<CancellationToken>())
            .Returns(Result<FieldAnnotations>.WithFailure("Database query failed"));

        var result = await _faultyPanel.GetFieldAnnotationsAsync("CASE-001", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Database query failed");
    }

    /// <summary>Blueprint: an identification failure is surfaced as a failure Result, not a throw.</summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_OnIdentificationError_ReturnsFailureResult()
    {
        var metadata = new UnifiedMetadataRecord();
        var classification = new ClassificationResult();
        _faultyPanel.IdentifyReviewCasesAsync("FILE-001", metadata, classification, Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<List<ReviewCase>>.WithFailure("Identification failed"));

        var result = await _faultyPanel.IdentifyReviewCasesAsync("FILE-001", metadata, classification, cancellationToken: TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Identification failed");
    }
}
