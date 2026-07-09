using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;
using IndQuestResults;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Honesty;

/// <summary>
/// S3.1 deliverable 4 — cross-field contamination guard: proves a
/// <see cref="FieldResolutionOrchestrator"/> resolving field A never returns field B's value.
/// </summary>
/// <remarks>
/// <b>Structural decision:</b> this is a focused, stage-level test with synthetic inputs (per the
/// S3.1 brief's explicit fallback: "a focused FieldResolutionOrchestrator/stage-level test with
/// synthetic inputs is acceptable if a full-pipeline form is awkward"). A full-pipeline form is
/// awkward here because <see cref="Domain.Extraction.StatementModel"/> only ever carries ONE
/// value per <see cref="Domain.Extraction.FieldKind"/> — contamination between, say, Product and
/// PaymentDueDate would have to be inferred indirectly from two real PDFs producing suspiciously
/// identical values, which is a weak, coincidence-prone signal. Driving
/// <see cref="FieldResolutionOrchestrator.ResolveAsync{TValue}"/> directly with a stage that
/// echoes back a value keyed by <see cref="FieldResolutionContext{TValue}.FieldKind"/> is a
/// direct, deterministic proof that the orchestrator threads the correct <c>FieldKind</c> through
/// to every stage call for every field — the same real orchestrator class the production
/// <c>EscalatingStatementFieldExtractor</c> calls once per field of every document.
/// </remarks>
public sealed class FieldResolutionCrossContaminationTests
{
    /// <summary>
    /// A fake higher-stage that returns a value looked up by <c>context.FieldKind</c> — if the
    /// orchestrator ever passed the WRONG <see cref="FieldKind"/> into a stage call (e.g. reused
    /// a stale context, or mixed up two concurrent per-field resolutions sharing the same
    /// document-scoped <see cref="StageBudget"/>/<see cref="LazyPdfCorpus"/>), the returned value
    /// would betray it by not matching the field kind actually being resolved.
    /// </summary>
    private sealed class FieldKindEchoStage : IFieldResolutionStage<string>
    {
        private readonly IReadOnlyDictionary<FieldKind, string> _valueByFieldKind;

        public FieldKindEchoStage(IReadOnlyDictionary<FieldKind, string> valueByFieldKind, StageId stage)
        {
            _valueByFieldKind = valueByFieldKind;
            Stage = stage;
        }

        public StageId Stage { get; }

        public Task<Result<FieldCandidate<string>>> TryResolveAsync(
            FieldResolutionContext<string> context,
            CancellationToken cancellationToken = default)
        {
            var value = _valueByFieldKind[context.FieldKind];
            return Task.FromResult(Result<FieldCandidate<string>>.WithSuccess(
                FieldCandidate<string>.Found(value, 1.0, Stage, FieldLocator.PageHint())));
        }
    }

    /// <summary>Test-only ladder registry: every <see cref="FieldKind"/> escalates unconditionally
    /// (<see cref="EscalationTrigger.StatusGate"/>) to the one rung under test.</summary>
    private sealed class AlwaysEscalateLadderRegistry : IFieldEscalationLadderRegistry
    {
        private readonly StageId _stage;

        public AlwaysEscalateLadderRegistry(StageId stage) => _stage = stage;

        public FieldEscalationLadder GetLadder(FieldKind fieldKind) =>
            new(fieldKind, ConfidenceFloor: 0.0, Rungs: [new FieldEscalationRung(_stage, EscalationTrigger.StatusGate)]);
    }

    [Fact]
    public async Task ResolveAsync_TwoDifferentFieldKinds_SharedBudgetAndCorpus_NeverCrossContaminates()
    {
        var ct = TestContext.Current.CancellationToken;
        var stageId = new StageId("TestEcho");

        // Two fields, two distinct expected values — if the orchestrator ever conflated the two
        // ResolveAsync calls (e.g. leaked FieldKind via shared mutable state on the shared
        // budget/corpus below), one call would return the OTHER field's value.
        var valueByFieldKind = new Dictionary<FieldKind, string>
        {
            [FieldKind.Product] = "PRODUCT-VALUE-111",
            [FieldKind.PaymentDueDate] = "PAYMENTDUEDATE-VALUE-222",
        };
        var stage = new FieldKindEchoStage(valueByFieldKind, stageId);

        var orchestrator = new FieldResolutionOrchestrator(
            new AlwaysEscalateLadderRegistry(stageId),
            new EmptyFieldStageProvider(), // unused: higherStages is supplied explicitly below.
            XUnitLogger.CreateLogger<FieldResolutionOrchestrator>());

        // Deliberately SHARED across both calls — the real EscalatingStatementFieldExtractor
        // shares one budget and one corpus handle across every field of a single document, so a
        // contamination bug rooted in that sharing must be caught by sharing them here too.
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // "%PDF" header bytes; never parsed (this stage never touches Corpus.Value).
        using var corpus = new LazyPdfCorpus(pdfBytes);
        var budget = new StageBudget();

        var positionalProduct = ExtractedField<string>.Missing(FieldLocator.PageHint());
        var positionalPaymentDueDate = ExtractedField<string>.Missing(FieldLocator.PageHint());

        var productResult = await orchestrator.ResolveAsync(
            FieldKind.Product,
            positionalProduct,
            pdfBytes,
            corpus,
            budget,
            higherStages: [stage],
            cancellationToken: ct);

        var paymentDueDateResult = await orchestrator.ResolveAsync(
            FieldKind.PaymentDueDate,
            positionalPaymentDueDate,
            pdfBytes,
            corpus,
            budget,
            higherStages: [stage],
            cancellationToken: ct);

        productResult.IsSuccess.ShouldBeTrue(productResult.Error);
        paymentDueDateResult.IsSuccess.ShouldBeTrue(paymentDueDateResult.Error);

        productResult.Value!.Value.ShouldBe(
            "PRODUCT-VALUE-111",
            "Resolving FieldKind.Product must never return FieldKind.PaymentDueDate's value " +
            "(cross-field contamination) even though both calls shared the same document-scoped " +
            "StageBudget and LazyPdfCorpus.");
        paymentDueDateResult.Value!.Value.ShouldBe(
            "PAYMENTDUEDATE-VALUE-222",
            "Resolving FieldKind.PaymentDueDate must never return FieldKind.Product's value " +
            "(cross-field contamination) even though both calls shared the same document-scoped " +
            "StageBudget and LazyPdfCorpus.");

        // Not vacuous: prove the values really did come from the escalation rung, not a
        // pass-through of the (Missing) positional field.
        productResult.Value!.Status.ShouldBe(ExtractionStatus.Extracted);
        paymentDueDateResult.Value!.Status.ShouldBe(ExtractionStatus.Extracted);
    }
}
