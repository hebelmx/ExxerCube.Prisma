using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;
using IndQuestResults;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Regression coverage for <see cref="FieldResolutionOrchestrator.ResolveAsync{TValue}"/>'s
/// per-call <c>validatorOverride</c> parameter (S3.2): when supplied, it must supersede the
/// field's ladder-registered <see cref="FieldEscalationLadder.Validator"/> for <b>both</b> places
/// a validator is consulted — the <see cref="EscalationTrigger.ValidatorFailure"/> escalation gate
/// and the disagreement-credibility gate — not just one of the two.
/// </summary>
public sealed class FieldResolutionOrchestratorValidatorOverrideTests
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
    public async Task ResolveAsync_ValidatorOverrideAccepts_SkipsEscalation_EvenThoughLadderValidatorWouldReject()
    {
        var positional = ExtractedField<string>.Found("B", FieldLocator.PageHint(1));

        // Decoy: the ladder's own validator rejects "B" — if it (rather than the override) were
        // consulted, this would escalate.
        var ladder = new FieldEscalationLadder(
            FieldKind.PaymentDueDate,
            ConfidenceFloor: 0.0,
            Rungs: [new FieldEscalationRung(StageId.FuzzyLabel, EscalationTrigger.ValidatorFailure)],
            Validator: new AllowListValidator("A"));

        var overrideValidator = new AllowListValidator("B");
        var orchestrator = CreateOrchestrator(ladder);

        var result = await orchestrator.ResolveAsync(
            FieldKind.PaymentDueDate,
            positional,
            pdfBytes: [0x25, 0x50, 0x44, 0x46],
            corpus: new LazyPdfCorpus([0x25, 0x50, 0x44, 0x46]),
            budget: new StageBudget(),
            higherStages: [new FixedStage(StageId.FuzzyLabel, FieldCandidate<string>.Found("OTHER", 0.9, StageId.FuzzyLabel, FieldLocator.PageHint(1)))],
            validatorOverride: overrideValidator,
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        // Override accepts "B" so ValidatorFailure never fires: the positional value is final,
        // never touched by the higher-stage fake (which would have returned "OTHER").
        result.Value!.Value.ShouldBe("B");
        result.Value.Provenance.Stage.ShouldBe(StageId.Positional);
    }

    [Fact]
    public async Task ResolveAsync_ValidatorOverrideRejects_ForcesEscalation_EvenThoughLadderValidatorWouldAccept()
    {
        var positional = ExtractedField<string>.Found("B", FieldLocator.PageHint(1));

        // Decoy: the ladder's own validator accepts "B" — if it (rather than the override) were
        // consulted, this would NOT escalate.
        var ladder = new FieldEscalationLadder(
            FieldKind.PaymentDueDate,
            ConfidenceFloor: 0.0,
            Rungs: [new FieldEscalationRung(StageId.FuzzyLabel, EscalationTrigger.ValidatorFailure)],
            Validator: new AllowListValidator("B"));

        // Accepts "A" (never seen) and "RECOVERED" (the rung's answer) — NOT "B", so the
        // escalation gate still forces the rung to run. Also accepting "RECOVERED" means the
        // Story 3.3a terminal-validator-abstain rule (FieldResolutionOrchestrator.ResolveAsync)
        // does not reject the winning candidate — this test's job is to prove escalation was
        // FORCED by the override, not to re-prove the terminal gate (that has its own coverage).
        var overrideValidator = new AllowListValidator("A", "RECOVERED");
        var orchestrator = CreateOrchestrator(ladder);

        var recovered = FieldCandidate<string>.Found("RECOVERED", 0.9, StageId.FuzzyLabel, FieldLocator.PageHint(1));

        var result = await orchestrator.ResolveAsync(
            FieldKind.PaymentDueDate,
            positional,
            pdfBytes: [0x25, 0x50, 0x44, 0x46],
            corpus: new LazyPdfCorpus([0x25, 0x50, 0x44, 0x46]),
            budget: new StageBudget(),
            higherStages: [new FixedStage(StageId.FuzzyLabel, recovered)],
            validatorOverride: overrideValidator,
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        // Override rejects "B" so ValidatorFailure fires and the rung's recovery wins.
        result.Value!.Value.ShouldBe("RECOVERED");
        result.Value.Provenance.Stage.ShouldBe(StageId.FuzzyLabel);
    }

    [Fact]
    public async Task ResolveAsync_ValidatorOverride_GatesDisagreementCredibility_NotLadderValidator()
    {
        // Positional found a value the domain considers wrong.
        var positional = ExtractedField<string>.Found("WRONG", FieldLocator.PageHint(1));

        // Decoy: the ladder's own validator would treat "WRONG" as credible (accepts it). If
        // credibility were still keyed off ladder.Validator instead of the override, "WRONG"
        // would count as a competing opinion against the recovered "RIGHT" and the disagreement
        // gate would abstain (Missing) instead of letting the recovery win.
        var ladder = new FieldEscalationLadder(
            FieldKind.PaymentDueDate,
            ConfidenceFloor: 0.0,
            Rungs: [new FieldEscalationRung(StageId.FuzzyLabel, EscalationTrigger.ValidatorFailure)],
            Validator: new AllowListValidator("WRONG"));

        // Override: only "RIGHT" is credible, so "WRONG" is excluded from the disagreement check
        // entirely (same reasoning as FieldResolutionOrchestratorDisagreementTests, but proving
        // it holds for the override path too).
        var overrideValidator = new AllowListValidator("RIGHT");
        var recovered = FieldCandidate<string>.Found("RIGHT", 0.9, StageId.FuzzyLabel, FieldLocator.PageHint(1));
        var orchestrator = CreateOrchestrator(ladder);

        var result = await orchestrator.ResolveAsync(
            FieldKind.PaymentDueDate,
            positional,
            pdfBytes: [0x25, 0x50, 0x44, 0x46],
            corpus: new LazyPdfCorpus([0x25, 0x50, 0x44, 0x46]),
            budget: new StageBudget(),
            higherStages: [new FixedStage(StageId.FuzzyLabel, recovered)],
            validatorOverride: overrideValidator,
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        // If credibility had used the ladder's decoy validator, this would be Missing (abstain)
        // instead — the core assertion this test exists to make.
        result.Value!.Status.ShouldBe(ExtractionStatus.Extracted);
        result.Value.Value.ShouldBe("RIGHT");
        result.Value.Provenance.Stage.ShouldBe(StageId.FuzzyLabel);
    }
}
