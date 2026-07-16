using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;
using IndQuestResults;
using Meziantou.Extensions.Logging.Xunit.v3;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// B2 — the load-bearing end-to-end suite for the empty-rung-ladder validator guard added to
/// <see cref="FieldResolutionOrchestrator.ResolveAsync{TValue}"/>'s <c>ladder.Rungs.Count == 0</c>
/// short-circuit. Without this guard a validator registered on a positional-only ladder (Tasa,
/// Cat, and the 11 money fields all have zero rungs — <see cref="FieldEscalationLadderRegistry"/>)
/// would be dead code: <see cref="FieldResolutionOrchestratorTerminalAbstainTests"/> only ever
/// proves the terminal-abstain rule fires on the ESCALATING path (non-empty rungs), which none of
/// the B2 fields ever take. This suite exercises the actual code path B2's fields use.
/// </summary>
public sealed class FieldResolutionOrchestratorEmptyLadderValidatorGuardTests
{
    // ── Test doubles (mirrors FieldResolutionOrchestratorTerminalAbstainTests) ────────────────

    private sealed class FixedLadderRegistry(FieldEscalationLadder ladder) : IFieldEscalationLadderRegistry
    {
        public FieldEscalationLadder GetLadder(FieldKind fieldKind) => ladder;
    }

    private static FieldResolutionOrchestrator CreateOrchestrator(FieldEscalationLadder ladder) =>
        new(new FixedLadderRegistry(ladder), new EmptyFieldStageProvider(), XUnitLogger.CreateLogger<FieldResolutionOrchestrator>());

    private static readonly byte[] MinimalPdfBytes = [0x25, 0x50, 0x44, 0x46]; // "%PDF" — never parsed on the empty-rung path.

    // ── 1. Implausible positional value abstains (the load-bearing assertion) ─────────────────

    [Fact]
    public async Task ResolveAsync_EmptyLadderValidatorRejectsPositionalValue_Abstains()
    {
        // 0.2736 (27.36%) misread with the decimal point dropped → 27.36, i.e. "2736%" — well
        // outside TasaPlausibilityValidator's [0, 2.0] fraction band.
        var implausiblePositional = ExtractedField<decimal>.Found(27.36m, FieldLocator.PageHint(3));

        var ladder = new FieldEscalationLadder(
            FieldKind.Tasa, ConfidenceFloor: 0.0, Rungs: [], Validator: new TasaPlausibilityValidator());

        var orchestrator = CreateOrchestrator(ladder);

        var result = await orchestrator.ResolveAsync(
            FieldKind.Tasa,
            implausiblePositional,
            pdfBytes: MinimalPdfBytes,
            corpus: new LazyPdfCorpus(MinimalPdfBytes),
            budget: new StageBudget(),
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Status.ShouldBe(ExtractionStatus.NotExtracted,
            "A positionally-found value that fails the ladder's validator must abstain, not gate a compliance verdict.");
        result.Value.Value.ShouldBe(0m, "Missing() carries the type's default value, not the rejected raw value.");
        result.Value.Locator.ShouldBe(implausiblePositional.Locator,
            "The rejected value's locator hint is preserved so a reviewer can still find where it was read.");
    }

    // ── 2. Plausible positional value is returned unchanged ───────────────────────────────────

    [Fact]
    public async Task ResolveAsync_EmptyLadderValidatorAcceptsPositionalValue_ReturnsUnchanged()
    {
        var plausiblePositional = ExtractedField<decimal>.Found(0.2736m, FieldLocator.PageHint(3));

        var ladder = new FieldEscalationLadder(
            FieldKind.Tasa, ConfidenceFloor: 0.0, Rungs: [], Validator: new TasaPlausibilityValidator());

        var orchestrator = CreateOrchestrator(ladder);

        var result = await orchestrator.ResolveAsync(
            FieldKind.Tasa,
            plausiblePositional,
            pdfBytes: MinimalPdfBytes,
            corpus: new LazyPdfCorpus(MinimalPdfBytes),
            budget: new StageBudget(),
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Status.ShouldBe(ExtractionStatus.Extracted);
        result.Value.Value.ShouldBe(0.2736m);
        ReferenceEquals(result.Value, plausiblePositional).ShouldBeTrue(
            "A value that clears the validator is the exact same instance, not a rebuilt one.");
    }

    // ── 3. Behavior-neutrality: empty ladder + no validator is completely inert ───────────────

    [Fact]
    public async Task ResolveAsync_EmptyLadderNoValidator_GuardInert_ReturnsPositionalFieldUnchanged()
    {
        // An implausible-looking value on purpose — proves the guard does not even attempt a
        // plausibility judgment when no validator is registered (the E1 default for every field
        // before B2).
        var positional = ExtractedField<decimal>.Found(999_999_999m, FieldLocator.PageHint(1));

        var ladder = FieldEscalationLadder.PositionalOnly(FieldKind.MontoIntereses);
        ladder.Validator.ShouldBeNull("Sanity check: PositionalOnly carries no validator.");

        var orchestrator = CreateOrchestrator(ladder);

        var result = await orchestrator.ResolveAsync(
            FieldKind.MontoIntereses,
            positional,
            pdfBytes: MinimalPdfBytes,
            corpus: new LazyPdfCorpus(MinimalPdfBytes),
            budget: new StageBudget(),
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        ReferenceEquals(result.Value, positional).ShouldBeTrue(
            "With no validator registered, the empty-rung branch returns positionalField byte-identical — the guard must be inert.");
    }

    // ── 4. A validator is never asked to judge "no value" ─────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_EmptyLadderValidator_PositionalAlreadyMissing_ValidatorNeverConsulted()
    {
        var neverCalled = new AlwaysCalledSpyValidator();
        var missingPositional = ExtractedField<decimal>.Missing(FieldLocator.PageHint(2));

        var ladder = new FieldEscalationLadder(
            FieldKind.TotalCargos, ConfidenceFloor: 0.0, Rungs: [], Validator: neverCalled);

        var orchestrator = CreateOrchestrator(ladder);

        var result = await orchestrator.ResolveAsync(
            FieldKind.TotalCargos,
            missingPositional,
            pdfBytes: MinimalPdfBytes,
            corpus: new LazyPdfCorpus(MinimalPdfBytes),
            budget: new StageBudget(),
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Status.ShouldBe(ExtractionStatus.NotExtracted);
        neverCalled.WasCalled.ShouldBeFalse(
            "IFieldValidator.IsValid is called only when the candidate already has a value — never to judge \"no value\".");
    }

    /// <summary>Records whether <see cref="IsValid"/> was ever invoked.</summary>
    private sealed class AlwaysCalledSpyValidator : IFieldValidator
    {
        public bool WasCalled { get; private set; }

        public bool IsValid(object? value)
        {
            WasCalled = true;
            return true;
        }
    }

    // ── 5. Per-call validatorOverride is honored on the empty-rung path too ───────────────────

    [Fact]
    public async Task ResolveAsync_EmptyLadderValidatorOverride_SupersedesLadderValidator()
    {
        // The ladder's own validator would accept this value; the override rejects it. If the
        // guard consulted the ladder's Validator instead of the override, this would NOT abstain.
        var positional = ExtractedField<decimal>.Found(1_000m, FieldLocator.PageHint(1));

        var ladder = new FieldEscalationLadder(
            FieldKind.TotalAbonos, ConfidenceFloor: 0.0, Rungs: [], Validator: new MoneyMagnitudeValidator());

        var strictOverride = new MoneyMagnitudeValidator(ceiling: 500m);
        var orchestrator = CreateOrchestrator(ladder);

        var result = await orchestrator.ResolveAsync(
            FieldKind.TotalAbonos,
            positional,
            pdfBytes: MinimalPdfBytes,
            corpus: new LazyPdfCorpus(MinimalPdfBytes),
            budget: new StageBudget(),
            validatorOverride: strictOverride,
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Status.ShouldBe(ExtractionStatus.NotExtracted);
    }
}
