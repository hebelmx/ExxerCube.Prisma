using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using IndQuestResults.Async;
using IndQuestResults.Operations;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="IManualReviewerPanel"/> — every implementation (and the mock
/// blueprint) must pass these tests unchanged (ADR-005).
/// </summary>
/// <remarks>
/// <para>
/// Uses the ADR-005 §3 <c>CreateSut()</c> fallback because the production
/// <c>ManualReviewerService</c> needs a per-test persistence fixture (EF InMemory); the blueprint owns a
/// <see cref="ManualReviewerPanelMockFactory"/> in-memory reference fake.
/// </para>
/// <para>
/// Phase 5 of the ITDD refactor converted the standalone mock-stub blueprint <c>IIManualReviewerPanelTests</c>
/// (18 tautological <c>Substitute.For</c> tests) into this inheritable base. The base pins the
/// cross-implementation invariants any correct panel must satisfy: pagination/argument validation, Result
/// semantics, cancellation, the documented review-case identification rules, and a genuine cross-method
/// <em>identify→submit</em> flow (seeded <strong>through the interface</strong>). Implementation-specific
/// tests that need direct fixture seeding — arbitrary status/confidence filtering, status-mapping
/// verification, duplicate-decision prevention, the notes-required-on-override rule, field-annotation
/// retrieval (needs seeded <c>FileMetadata</c>) — stay in the impl twin <c>ManualReviewerServiceTests</c>
/// (ADR-005 §5).
/// </para>
/// </remarks>
public abstract class ManualReviewerPanelContract
{
    /// <summary>
    /// Creates the implementation under test. Called once per test; the implementation owns its
    /// fixture lifetime.
    /// </summary>
    /// <returns>The <see cref="IManualReviewerPanel"/> implementation to verify.</returns>
    protected abstract IManualReviewerPanel CreateSut();

    private static UnifiedMetadataRecord MetadataWith(ClassificationResult classification, MatchedFields? matchedFields = null)
        => new() { Classification = classification, MatchedFields = matchedFields! };

    private static ClassificationResult Classification(int confidence, ClassificationLevel2? level2 = null)
        => new() { Level1 = ClassificationLevel1.Aseguramiento, Level2 = level2, Confidence = confidence };

    private static ReviewDecision Decision(string caseId)
        => new()
        {
            DecisionId = $"DEC-{Guid.NewGuid():N}",
            CaseId = caseId,
            DecisionType = DecisionType.Approve,
            ReviewerId = "REVIEWER-001",
            ReviewedAt = DateTime.UtcNow,
            Notes = "Approved after review"
        };

    //
    // GetReviewCasesAsync
    //

    /// <summary>Contract: null filters are accepted and yield a successful (possibly empty) result.</summary>
    [Fact]
    public async Task GetReviewCasesAsync_WithNullFilters_ReturnsSuccess()
    {
        var sut = CreateSut();

        var result = await sut.GetReviewCasesAsync(null, 1, 50, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
    }

    /// <summary>Contract: a page number below 1 is rejected with a failure.</summary>
    [Fact]
    public async Task GetReviewCasesAsync_WithInvalidPageNumber_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.GetReviewCasesAsync(null, 0, 50, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>Contract: a page size outside 1..1000 is rejected with a failure.</summary>
    [Fact]
    public async Task GetReviewCasesAsync_WithInvalidPageSize_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.GetReviewCasesAsync(null, 1, 0, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>Contract: a pre-cancelled token yields a cancelled result.</summary>
    [Fact]
    public async Task GetReviewCasesAsync_WhenCancelled_ReturnsCancelledResult()
    {
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await sut.GetReviewCasesAsync(null, 1, 50, cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    //
    // SubmitReviewDecisionAsync
    //

    /// <summary>Contract: submitting against a non-existent case fails (not a throw).</summary>
    [Fact]
    public async Task SubmitReviewDecisionAsync_WithNonExistentCase_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.SubmitReviewDecisionAsync("NOPE-" + Guid.NewGuid().ToString("N"), Decision("NOPE"), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>Contract: a null decision is rejected with a failure.</summary>
    [Fact]
    public async Task SubmitReviewDecisionAsync_WithNullDecision_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.SubmitReviewDecisionAsync("CASE-001", null!, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>Contract: a pre-cancelled token yields a cancelled result.</summary>
    [Fact]
    public async Task SubmitReviewDecisionAsync_WhenCancelled_ReturnsCancelledResult()
    {
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await sut.SubmitReviewDecisionAsync("CASE-001", Decision("CASE-001"), cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Contract: a decision for a case that was just identified (seeded through the interface) is accepted.
    /// This is the genuine cross-method flow the mock-stub blueprint could only fake.
    /// </summary>
    [Fact]
    public async Task SubmitReviewDecisionAsync_ForIdentifiedCase_ReturnsSuccess()
    {
        var sut = CreateSut();
        var fileId = "FILE-" + Guid.NewGuid().ToString("N");

        var identify = await sut.IdentifyReviewCasesAsync(
            fileId, MetadataWith(Classification(75)), Classification(75), TestContext.Current.CancellationToken);
        identify.IsSuccess.ShouldBeTrue();
        identify.Value.ShouldNotBeNull();
        identify.Value!.ShouldNotBeEmpty();
        var caseId = identify.Value![0].CaseId;

        var result = await sut.SubmitReviewDecisionAsync(caseId, Decision(caseId), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    //
    // GetFieldAnnotationsAsync
    //

    /// <summary>Contract: an empty case id is rejected with a failure.</summary>
    [Fact]
    public async Task GetFieldAnnotationsAsync_WithEmptyCaseId_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.GetFieldAnnotationsAsync("   ", TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>Contract: annotations for a non-existent case fail (not a throw).</summary>
    [Fact]
    public async Task GetFieldAnnotationsAsync_WithNonExistentCase_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.GetFieldAnnotationsAsync("NOPE-" + Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>Contract: a pre-cancelled token yields a cancelled result.</summary>
    [Fact]
    public async Task GetFieldAnnotationsAsync_WhenCancelled_ReturnsCancelledResult()
    {
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await sut.GetFieldAnnotationsAsync("CASE-001", cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }

    //
    // IdentifyReviewCasesAsync
    //

    /// <summary>Contract: null metadata is rejected with a failure.</summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WithNullMetadata_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.IdentifyReviewCasesAsync("FILE-001", null!, Classification(95), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>Contract: null classification is rejected with a failure.</summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WithNullClassification_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.IdentifyReviewCasesAsync("FILE-001", MetadataWith(Classification(95)), null!, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>Contract: classification below the confidence threshold produces a low-confidence review case.</summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WithLowConfidence_IdentifiesLowConfidenceCase()
    {
        var sut = CreateSut();
        var fileId = "FILE-" + Guid.NewGuid().ToString("N");

        var result = await sut.IdentifyReviewCasesAsync(
            fileId, MetadataWith(Classification(75)), Classification(75), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.ShouldContain(c => c.RequiresReviewReason == ReviewReason.LowConfidence);
    }

    /// <summary>Contract: a classification missing its second level is flagged as ambiguous.</summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WithAmbiguousClassification_IdentifiesAmbiguousCase()
    {
        var sut = CreateSut();
        var fileId = "FILE-" + Guid.NewGuid().ToString("N");
        var classification = Classification(85, level2: null);

        var result = await sut.IdentifyReviewCasesAsync(
            fileId, MetadataWith(classification), classification, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.ShouldContain(c => c.RequiresReviewReason == ReviewReason.AmbiguousClassification && c.ClassificationAmbiguity);
    }

    /// <summary>Contract: conflicting matched fields produce an extraction-error review case.</summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WithExtractionErrors_IdentifiesExtractionErrorCase()
    {
        var sut = CreateSut();
        var fileId = "FILE-" + Guid.NewGuid().ToString("N");
        var classification = Classification(90);
        var metadata = MetadataWith(classification, new MatchedFields { ConflictingFields = new List<string> { "Expediente", "Causa" } });

        var result = await sut.IdentifyReviewCasesAsync(fileId, metadata, classification, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.ShouldContain(c => c.RequiresReviewReason == ReviewReason.ExtractionError);
    }

    /// <summary>Contract: a high-confidence, unambiguous, conflict-free document yields no review cases.</summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WithHighConfidenceNoIssues_ReturnsEmptyList()
    {
        var sut = CreateSut();
        var fileId = "FILE-" + Guid.NewGuid().ToString("N");
        var classification = Classification(95, ClassificationLevel2.Judicial);
        var metadata = MetadataWith(classification, new MatchedFields { ConflictingFields = new List<string>() });

        var result = await sut.IdentifyReviewCasesAsync(fileId, metadata, classification, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Count.ShouldBe(0);
    }

    /// <summary>Contract: a pre-cancelled token yields a cancelled result.</summary>
    [Fact]
    public async Task IdentifyReviewCasesAsync_WhenCancelled_ReturnsCancelledResult()
    {
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await sut.IdentifyReviewCasesAsync("FILE-001", new UnifiedMetadataRecord(), new ClassificationResult(), cts.Token);

        result.IsCancelled().ShouldBeTrue();
    }
}
