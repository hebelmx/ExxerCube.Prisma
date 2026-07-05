using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;
using IndQuestResults;
using Meziantou.Extensions.Logging.Xunit.v3;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Regression coverage for the escalation <b>disagreement gate</b>
/// (<see cref="FieldResolutionOrchestrator"/>). A stage-1 value that the ladder escalated past —
/// precisely because it failed the field's validator — must NOT be treated as a competing opinion
/// that forces an abstention: otherwise every validator-triggered recovery would abstain (it always
/// differs from the bad value it was recovering from), silently defeating the whole fallback chain.
/// This is the E1-boundary adversarial-review finding (Finding 1).
/// </summary>
public sealed class FieldResolutionOrchestratorDisagreementTests
{
    // ── Test doubles ────────────────────────────────────────────────────────

    /// <summary>Returns a single fixed ladder regardless of the requested field kind.</summary>
    private sealed class FixedLadderRegistry(FieldEscalationLadder ladder) : IFieldEscalationLadderRegistry
    {
        public FieldEscalationLadder GetLadder(FieldKind fieldKind) => ladder;
    }

    /// <summary>Accepts only the exact set of values passed to it; everything else is invalid.</summary>
    private sealed class AllowListValidator(params string[] valid) : IFieldValidator
    {
        private readonly HashSet<string> _valid = new(valid, StringComparer.Ordinal);
        public bool IsValid(object? value) => value is string s && _valid.Contains(s);
    }

    /// <summary>A higher stage that always returns one predetermined candidate.</summary>
    private sealed class FixedStage(StageId stage, FieldCandidate<string> candidate) : IFieldResolutionStage<string>
    {
        public StageId Stage => stage;

        public Task<Result<FieldCandidate<string>>> TryResolveAsync(
            FieldResolutionContext<string> context, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<FieldCandidate<string>>.WithSuccess(candidate));
    }

    private static FieldResolutionOrchestrator CreateOrchestrator(FieldEscalationLadder ladder) =>
        new(new FixedLadderRegistry(ladder), new EmptyFieldStageProvider(), XUnitLogger.CreateLogger<FieldResolutionOrchestrator>());

    // ── Tests ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidatorFailureLadder_PositionalInvalid_ReturnsRecoveredValue_NotAbstain()
    {
        // Positional produced a present-but-wrong value that fails the domain (catalog) validator.
        var positional = ExtractedField<string>.Found("WRONG-PRODUCT", FieldLocator.PageHint(1));

        // Fuzzy/catalog rung recovers the correct, catalog-valid value.
        var recovered = FieldCandidate<string>.Found(
            "TC-BSSB", score: 0.92, StageId.FuzzyLabel, FieldLocator.PageHint(1));

        var ladder = new FieldEscalationLadder(
            FieldKind.Product,
            ConfidenceFloor: 0.0,
            Rungs: [new FieldEscalationRung(StageId.FuzzyLabel, EscalationTrigger.ValidatorFailure)],
            Validator: new AllowListValidator("TC-BSSB"));

        var orchestrator = CreateOrchestrator(ladder);

        var result = await orchestrator.ResolveAsync(
            FieldKind.Product,
            positional,
            pdfBytes: [0x25, 0x50, 0x44, 0x46], // "%PDF" — never parsed (the fake stage ignores the corpus)
            corpus: new LazyPdfCorpus([0x25, 0x50, 0x44, 0x46]),
            budget: new StageBudget(),
            higherStages: [new FixedStage(StageId.FuzzyLabel, recovered)],
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        // The core assertion: recovery WINS. Before the fix, the disagreement gate compared the
        // discarded "WRONG-PRODUCT" against "TC-BSSB", saw they differ, and abstained (Missing).
        result.Value!.Status.ShouldBe(ExtractionStatus.Extracted);
        result.Value.Value.ShouldBe("TC-BSSB");
        result.Value.Provenance.Stage.ShouldBe(StageId.FuzzyLabel);
    }

    [Fact]
    public async Task StatusGateLadder_PositionalMissing_InferenceStage_ReturnsExtractedByInference()
    {
        // Positional found nothing — a StatusGate ladder escalates to an inference stage.
        var positional = ExtractedField<string>.Missing(FieldLocator.PageHint(1));

        var inferred = FieldCandidate<string>.Found(
            "INFERRED", score: 0.81, StageId.LlmExtraction, FieldLocator.PageHint(1));

        var ladder = new FieldEscalationLadder(
            FieldKind.Product,
            ConfidenceFloor: 0.0,
            Rungs: [new FieldEscalationRung(StageId.LlmExtraction, EscalationTrigger.StatusGate)]);

        var orchestrator = CreateOrchestrator(ladder);

        var result = await orchestrator.ResolveAsync(
            FieldKind.Product,
            positional,
            pdfBytes: [0x25, 0x50, 0x44, 0x46],
            corpus: new LazyPdfCorpus([0x25, 0x50, 0x44, 0x46]),
            budget: new StageBudget(),
            higherStages: [new FixedStage(StageId.LlmExtraction, inferred)],
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Value.ShouldBe("INFERRED");
        // Semantic/LLM values are flagged for downstream human-confirmation, not plain Extracted.
        result.Value.Status.ShouldBe(ExtractionStatus.ExtractedByInference);
        result.Value.Provenance.Stage.ShouldBe(StageId.LlmExtraction);
    }
}
