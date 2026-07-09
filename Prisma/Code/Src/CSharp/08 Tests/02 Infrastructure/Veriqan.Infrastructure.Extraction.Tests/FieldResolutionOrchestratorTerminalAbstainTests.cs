using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;
using IndQuestResults;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Story 3.3a — regression coverage for the <b>terminal-validator-abstain rule</b> added to
/// <see cref="FieldResolutionOrchestrator.ResolveAsync{TValue}"/>: a validator (ladder-registered
/// or per-call override) gated escalation and disagreement-credibility (S3.2), but did NOT stop a
/// terminal candidate that fails the validator from being returned as-is. This suite proves the
/// gap is closed, and that the rule only fires when a validator actually exists (inert otherwise).
/// </summary>
public sealed class FieldResolutionOrchestratorTerminalAbstainTests
{
    // ── Test doubles (mirrors FieldResolutionOrchestratorValidatorOverrideTests) ──────────────

    private sealed class FixedLadderRegistry(FieldEscalationLadder ladder) : IFieldEscalationLadderRegistry
    {
        public FieldEscalationLadder GetLadder(FieldKind fieldKind) => ladder;
    }

    private sealed class AllowListValidator(params string[] valid) : IFieldValidator
    {
        private readonly HashSet<string> _valid = new(valid, StringComparer.Ordinal);
        public bool IsValid(object? value) => value is string s && _valid.Contains(s);
    }

    private sealed class FixedStage<TValue>(StageId stage, FieldCandidate<TValue> candidate) : IFieldResolutionStage<TValue>
    {
        public StageId Stage => stage;

        public Task<Result<FieldCandidate<TValue>>> TryResolveAsync(
            FieldResolutionContext<TValue> context, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<FieldCandidate<TValue>>.WithSuccess(candidate));
    }

    private static FieldResolutionOrchestrator CreateOrchestrator(FieldEscalationLadder ladder) =>
        new(new FixedLadderRegistry(ladder), new EmptyFieldStageProvider(), XUnitLogger.CreateLogger<FieldResolutionOrchestrator>());

    private static readonly byte[] MinimalPdfBytes = [0x25, 0x50, 0x44, 0x46]; // "%PDF" — never parsed by the fakes below.

    // ── 1. Generic terminal-abstain: a lone recovered candidate that fails the validator ──────

    [Fact]
    public async Task ResolveAsync_TerminalCandidateFailsValidator_Abstains_NotReturnedAsIs()
    {
        // Positional found nothing, so StatusGate escalates unconditionally — there is only ever
        // ONE credible-or-not candidate at the end, so the disagreement gate (which needs 2+
        // credible candidates) can never fire here. Before Story 3.3a this terminal value would
        // have been returned even though it fails the validator.
        var positional = ExtractedField<string>.Missing(FieldLocator.PageHint(1));
        var recovered = FieldCandidate<string>.Found("NOT-ALLOWED", 0.9, StageId.FuzzyLabel, FieldLocator.PageHint(1));

        var ladder = new FieldEscalationLadder(
            FieldKind.PaymentDueDate,
            ConfidenceFloor: 0.0,
            Rungs: [new FieldEscalationRung(StageId.FuzzyLabel, EscalationTrigger.StatusGate)],
            Validator: new AllowListValidator("ONLY-THIS-IS-VALID"));

        var orchestrator = CreateOrchestrator(ladder);

        var result = await orchestrator.ResolveAsync(
            FieldKind.PaymentDueDate,
            positional,
            pdfBytes: MinimalPdfBytes,
            corpus: new LazyPdfCorpus(MinimalPdfBytes),
            budget: new StageBudget(),
            higherStages: [new FixedStage<string>(StageId.FuzzyLabel, recovered)],
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Status.ShouldBe(ExtractionStatus.NotExtracted,
            "The terminal candidate fails the (only) validator and must be abstained, not returned.");
        result.Value.Provenance.Stage.ShouldBe(StageId.FuzzyLabel,
            "Provenance still records which stage produced the (rejected) candidate.");
    }

    [Fact]
    public async Task ResolveAsync_TerminalCandidatePassesValidator_StillReturned()
    {
        // Sanity counterpart: when the terminal candidate DOES pass the validator, the new rule
        // must not touch it — proves the rule discriminates on IsValid, not on "a validator exists".
        var positional = ExtractedField<string>.Missing(FieldLocator.PageHint(1));
        var recovered = FieldCandidate<string>.Found("ALLOWED", 0.9, StageId.FuzzyLabel, FieldLocator.PageHint(1));

        var ladder = new FieldEscalationLadder(
            FieldKind.PaymentDueDate,
            ConfidenceFloor: 0.0,
            Rungs: [new FieldEscalationRung(StageId.FuzzyLabel, EscalationTrigger.StatusGate)],
            Validator: new AllowListValidator("ALLOWED"));

        var orchestrator = CreateOrchestrator(ladder);

        var result = await orchestrator.ResolveAsync(
            FieldKind.PaymentDueDate,
            positional,
            pdfBytes: MinimalPdfBytes,
            corpus: new LazyPdfCorpus(MinimalPdfBytes),
            budget: new StageBudget(),
            higherStages: [new FixedStage<string>(StageId.FuzzyLabel, recovered)],
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Status.ShouldBe(ExtractionStatus.Extracted);
        result.Value.Value.ShouldBe("ALLOWED");
    }

    [Fact]
    public async Task ResolveAsync_NoValidatorRegistered_TerminalRuleInert_ReturnsValueUnchanged()
    {
        // No ladder Validator and no override — the terminal-abstain rule's `validator is not
        // null` guard must keep it completely inert, exactly like every field before Story 3.3a.
        var positional = ExtractedField<string>.Missing(FieldLocator.PageHint(1));
        var recovered = FieldCandidate<string>.Found("ANYTHING", 0.9, StageId.LlmExtraction, FieldLocator.PageHint(1));

        var ladder = new FieldEscalationLadder(
            FieldKind.Product,
            ConfidenceFloor: 0.0,
            Rungs: [new FieldEscalationRung(StageId.LlmExtraction, EscalationTrigger.StatusGate)]);

        var orchestrator = CreateOrchestrator(ladder);

        var result = await orchestrator.ResolveAsync(
            FieldKind.Product,
            positional,
            pdfBytes: MinimalPdfBytes,
            corpus: new LazyPdfCorpus(MinimalPdfBytes),
            budget: new StageBudget(),
            higherStages: [new FixedStage<string>(StageId.LlmExtraction, recovered)],
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Status.ShouldBe(ExtractionStatus.ExtractedByInference);
        result.Value.Value.ShouldBe("ANYTHING");
    }

    // ── 2. S3.2 activation: PaymentDueDate period-relative override now bites end-to-end ──────

    [Fact]
    public async Task ResolveAsync_PaymentDueDate_PeriodRelativeOverride_InStaticWindowButOutsideCutPlus60_Abstains()
    {
        // A date that IS inside the static sanity window [2020-01-01, 2035-12-31] — so it would
        // have been accepted before Story 3.2's per-call override even existed — but falls OUTSIDE
        // [cutDate, cutDate + 60 days], the tighter period-relative window. Before Story 3.3a the
        // override only gated escalation/disagreement, never the terminal return, so this exact
        // scenario would have silently returned the implausible date. This is what the brief calls
        // the "S3.2 activation".
        var cutDate = new DateOnly(2025, 8, 4);
        var outOfWindowButStaticPlausible = cutDate.AddDays(120); // within [2020,2035], outside [cut, cut+60]

        var positional = ExtractedField<DateOnly>.Missing(FieldLocator.PageHint(1));
        var recovered = FieldCandidate<DateOnly>.Found(
            outOfWindowButStaticPlausible, 0.9, StageId.FuzzyLabel, FieldLocator.PageHint(1));

        var ladder = new FieldEscalationLadder(
            FieldKind.PaymentDueDate,
            ConfidenceFloor: 0.0,
            Rungs: [new FieldEscalationRung(StageId.FuzzyLabel, EscalationTrigger.StatusGate)],
            Validator: new PaymentDueDatePlausibilityValidator()); // static-window ladder default

        var orchestrator = CreateOrchestrator(ladder);

        // The period-relative override an EscalatingStatementFieldExtractor would build once
        // PeriodCutDate is resolved (see EscalatingStatementFieldExtractor.EscalateAsync).
        var periodRelativeOverride = new PaymentDueDatePlausibilityValidator(cutDate);

        var result = await orchestrator.ResolveAsync(
            FieldKind.PaymentDueDate,
            positional,
            pdfBytes: MinimalPdfBytes,
            corpus: new LazyPdfCorpus(MinimalPdfBytes),
            budget: new StageBudget(),
            higherStages: [new FixedStage<DateOnly>(StageId.FuzzyLabel, recovered)],
            validatorOverride: periodRelativeOverride,
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        PaymentDueDatePlausibilityValidator.IsPlausible(outOfWindowButStaticPlausible).ShouldBeTrue(
            "Sanity check: the recovered date must be inside the static window (or this test proves nothing new).");
        result.Value!.Status.ShouldBe(ExtractionStatus.NotExtracted,
            "The period-relative override now has the final say on the terminal value, not just on escalation.");
    }

    // ── 3. Product OCR candidate not in catalog abstains (orchestrator-level) ─────────────────

    [Fact]
    public async Task ResolveAsync_Product_OcrCandidateNotInCatalog_Abstains()
    {
        var services = new ServiceCollection();
        services.AddVeriqanBinding();
        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IProductResolver>();

        var bundle = new VecReferenceBundle(
            BundleMetadata: new BundleMetadata("1.0.0", "Test Bank", null, null, null, null),
            Products:
            [
                new VecProduct(
                    ProductId: "TC-NL",
                    ProductName: "Tarjeta de Crédito NL",
                    Aliases: ["NL"],
                    HasRewardsProgram: false,
                    CardImage: null,
                    ImportantMessageImage: null,
                    Tariffs: null),
            ],
            InterestRates: null,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
            ToleranceConfig: null,
            ValidationConstants: null);

        var catalogValidator = new ProductCatalogMembershipValidator(resolver, bundle);

        var positional = ExtractedField<string>.Missing(FieldLocator.PageHint(1));
        var recovered = FieldCandidate<string>.Found(
            "Completely Unrelated Product Banner", 0.9, StageId.HeaderImageOcr, FieldLocator.PageHint(1));

        var ladder = new FieldEscalationLadder(
            FieldKind.Product,
            ConfidenceFloor: 0.0,
            Rungs: [new FieldEscalationRung(StageId.HeaderImageOcr, EscalationTrigger.StatusGate)]);

        var orchestrator = CreateOrchestrator(ladder);

        var result = await orchestrator.ResolveAsync(
            FieldKind.Product,
            positional,
            pdfBytes: MinimalPdfBytes,
            corpus: new LazyPdfCorpus(MinimalPdfBytes),
            budget: new StageBudget(),
            higherStages: [new FixedStage<string>(StageId.HeaderImageOcr, recovered)],
            validatorOverride: catalogValidator,
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Status.ShouldBe(ExtractionStatus.NotExtracted,
            "An OCR-recovered Product token that does not resolve in the tenant catalog must abstain.");
    }
}
