using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// Walks a field's <see cref="FieldEscalationLadder"/> honoring escalation triggers and the
/// document's <see cref="StageBudget"/>, and is the <b>sole</b> place a winning
/// <see cref="FieldCandidate{TValue}"/> becomes a domain <see cref="ExtractedField{T}"/> (design
/// doc, "Architecture — per-field resolver pipeline").
/// </summary>
/// <remarks>
/// With an empty ladder and no registered validator — the case for every <see cref="FieldKind"/>
/// as of E1 — this returns the stage-1 field completely unchanged without constructing any
/// candidate machinery, so E1 is behavior-neutral by construction rather than by coincidence. B2
/// adds one exception: an empty-rung ladder that DOES carry a validator (e.g. a money/rate
/// plausibility gate) still lets that validator downgrade an implausible positional value to
/// <see cref="ExtractedField{T}.Missing"/> — see the guard inside the empty-rung branch below.
/// </remarks>
public sealed class FieldResolutionOrchestrator
{
    private readonly IFieldEscalationLadderRegistry _ladderRegistry;
    private readonly IFieldStageProvider _stageProvider;
    private readonly ILogger<FieldResolutionOrchestrator> _logger;

    /// <summary>
    /// Initializes a <see cref="FieldResolutionOrchestrator"/>.
    /// </summary>
    /// <param name="ladderRegistry">Per-field ladder lookup.</param>
    /// <param name="stageProvider">
    /// Resolves the higher-stage implementations available for a field when the caller does not
    /// pass an explicit <c>higherStages</c> override to <see cref="ResolveAsync{TValue}"/>.
    /// </param>
    /// <param name="logger">Logger used for escalation-stop diagnostics (missing stage, exhausted budget).</param>
    public FieldResolutionOrchestrator(
        IFieldEscalationLadderRegistry ladderRegistry,
        IFieldStageProvider stageProvider,
        ILogger<FieldResolutionOrchestrator> logger)
    {
        _ladderRegistry = ladderRegistry ?? throw new ArgumentNullException(nameof(ladderRegistry));
        _stageProvider = stageProvider ?? throw new ArgumentNullException(nameof(stageProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Resolves one field, escalating beyond the positional result only as far as the field's
    /// ladder and triggers require.
    /// </summary>
    /// <typeparam name="TValue">The type of the field's value.</typeparam>
    /// <param name="fieldKind">Which field is being resolved.</param>
    /// <param name="positionalField">The field as already extracted by stage 1 (positional).</param>
    /// <param name="pdfBytes">Raw bytes of the statement PDF.</param>
    /// <param name="corpus">Document-scoped lazy PdfPig corpus handle, shared across all fields.</param>
    /// <param name="budget">Document-scoped cost guard (e.g. remaining LLM calls).</param>
    /// <param name="higherStages">
    /// Stage implementations available for rungs beyond stage 1, keyed by their
    /// <see cref="IFieldResolutionStage{TValue}.Stage"/>. When <see langword="null"/> (the normal
    /// production path), the stages are instead obtained from the injected
    /// <see cref="IFieldStageProvider"/>. When explicitly supplied (e.g. by a test), that list is
    /// used as-is and the provider is not consulted — this preserves direct stage-injection for
    /// callers that construct their own <see cref="IFieldResolutionStage{TValue}"/> fakes.
    /// </param>
    /// <param name="validatorOverride">
    /// When non-<see langword="null"/>, supersedes the field's ladder-registered
    /// <see cref="FieldEscalationLadder.Validator"/> for this call only — used for both the
    /// <see cref="EscalationTrigger.ValidatorFailure"/> escalation gate and disagreement
    /// credibility. When <see langword="null"/> (the normal production path), the ladder's own
    /// validator is used unchanged. Intended for callers that can supply a tighter,
    /// document-relative validator instance once earlier fields (e.g. a statement's period cut
    /// date) have been resolved (E3, <see cref="PaymentDueDatePlausibilityValidator"/>).
    /// </param>
    /// <param name="cancellationToken">Cancellation token propagated to every stage invocation.</param>
    /// <returns>
    /// <see cref="Result{T}.IsSuccess"/> with the resolved <see cref="ExtractedField{T}"/> — equal
    /// to <paramref name="positionalField"/> whenever the field's ladder has no rungs (always true
    /// in E1); a cancelled result when <paramref name="cancellationToken"/> is triggered; a
    /// failure result only when a stage itself reports an infrastructure failure.
    /// </returns>
    public async Task<Result<ExtractedField<TValue>>> ResolveAsync<TValue>(
        FieldKind fieldKind,
        ExtractedField<TValue> positionalField,
        byte[] pdfBytes,
        LazyPdfCorpus corpus,
        StageBudget budget,
        IReadOnlyList<IFieldResolutionStage<TValue>>? higherStages = null,
        IFieldValidator? validatorOverride = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(positionalField);
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(budget);

        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.Cancelled<ExtractedField<TValue>>();

        var ladder = _ladderRegistry.GetLadder(fieldKind);
        if (ladder.Rungs.Count == 0)
        {
            // The dominant E1 path: no rung is registered for this field, so the positional
            // result is final. No candidate, context, or corpus machinery is constructed —
            // the PDF is never re-parsed for a field that never escalates.
            //
            // B2: a validator registered on this empty-rung ladder (validatorOverride ?? the
            // ladder's own Validator) still gets the final say on the positional value, mirroring
            // the terminal-validator-abstain rule below for the escalating path — there is no
            // rung to recover through here, so an implausible positional value can only ever be
            // downgraded to Missing, never replaced. This is the ONLY new behavior in this
            // branch: when no validator is registered (every field before B2), the `validator is
            // not null` guard keeps this completely inert and the branch returns
            // positionalField unchanged, exactly as before.
            var shortCircuitValidator = validatorOverride ?? ladder.Validator;
            if (shortCircuitValidator is not null
                && positionalField.Status != ExtractionStatus.NotExtracted
                && !shortCircuitValidator.IsValid(positionalField.Value))
            {
                return Result<ExtractedField<TValue>>.WithSuccess(
                    ExtractedField<TValue>.Missing(
                        positionalField.Locator, new ExtractionProvenance(positionalField.Provenance.Stage)));
            }

            return Result<ExtractedField<TValue>>.WithSuccess(positionalField);
        }

        // An explicit higherStages argument (test-injection contract) always wins and bypasses
        // the provider; only when the caller passes null do we ask IFieldStageProvider — this
        // keeps existing tests that inject stage fakes directly working unchanged.
        var stages = higherStages ?? _stageProvider.GetHigherStages<TValue>(fieldKind);

        // Symmetric with higherStages: an explicit validatorOverride wins for this call only; a
        // null override falls back to the ladder's own registered validator, unchanged.
        var validator = validatorOverride ?? ladder.Validator;

        var stage1 = new PositionalStage<TValue>(positionalField);
        var stage1Context = new FieldResolutionContext<TValue>(
            fieldKind, pdfBytes, corpus, Array.Empty<FieldCandidate<TValue>>(), budget);
        var stage1Result = await stage1.TryResolveAsync(stage1Context, cancellationToken).ConfigureAwait(false);
        if (stage1Result.IsCancelled())
            return ResultExtensions.Cancelled<ExtractedField<TValue>>();
        if (stage1Result.IsFailure)
            return Result<ExtractedField<TValue>>.WithFailure(stage1Result.Errors);

        var candidates = new List<FieldCandidate<TValue>> { stage1Result.Value! };

        foreach (var rung in ladder.Rungs)
        {
            if (cancellationToken.IsCancellationRequested)
                return ResultExtensions.Cancelled<ExtractedField<TValue>>();

            var current = candidates[^1];
            if (!ShouldEscalate(rung.Trigger, current, candidates, ladder, validator))
                break;

            if (rung.Stage == StageId.LlmExtraction && !budget.TryConsumeLlmCall())
            {
                _logger.LogInformation(
                    "LLM budget exhausted for this document; field {FieldKind} stops at stage {Stage}.",
                    fieldKind,
                    current.Stage);
                break;
            }

            var stage = stages.FirstOrDefault(s => s.Stage == rung.Stage);
            if (stage is null)
            {
                _logger.LogWarning(
                    "Ladder for {FieldKind} references stage {Stage} but no implementation was supplied; stopping escalation.",
                    fieldKind,
                    rung.Stage);
                break;
            }

            var rungContext = new FieldResolutionContext<TValue>(
                fieldKind, pdfBytes, corpus, candidates.ToArray(), budget);
            var rungResult = await stage.TryResolveAsync(rungContext, cancellationToken).ConfigureAwait(false);
            if (rungResult.IsCancelled())
                return ResultExtensions.Cancelled<ExtractedField<TValue>>();
            if (rungResult.IsFailure)
                return Result<ExtractedField<TValue>>.WithFailure(rungResult.Errors);

            candidates.Add(rungResult.Value!);
        }

        var best = candidates[^1];

        // Story 3.3a — terminal-validator-abstain rule: a validator gates escalation and
        // disagreement-credibility above, but neither of those stops a terminal value that FAILS
        // the validator from being returned as-is (e.g. a lone OCR-recovered candidate that never
        // triggers the disagreement gate because there is nothing to disagree with). Reject it
        // here too, so a validator that exists always has the final say on the returned value —
        // this only ever fires when a validator is present, so it is inert (behavior-neutral) for
        // every field whose ladder/override carries no validator.
        if (best.HasValue && validator is not null && !validator.IsValid(best.Value))
        {
            return Result<ExtractedField<TValue>>.WithSuccess(
                ExtractedField<TValue>.Missing(best.Locator, new ExtractionProvenance(best.Stage)));
        }

        if (HasDisagreement(candidates, ladder, validator))
        {
            // Honesty over recall: never pick a winner by fiat when stages disagree.
            return Result<ExtractedField<TValue>>.WithSuccess(
                ExtractedField<TValue>.Missing(best.Locator, new ExtractionProvenance(best.Stage)));
        }

        return Result<ExtractedField<TValue>>.WithSuccess(ToExtractedField(best));
    }

    private static bool ShouldEscalate<TValue>(
        EscalationTrigger trigger,
        FieldCandidate<TValue> current,
        IReadOnlyList<FieldCandidate<TValue>> candidatesSoFar,
        FieldEscalationLadder ladder,
        IFieldValidator? validator)
    {
        if (trigger.HasFlag(EscalationTrigger.StatusGate) && !current.HasValue)
            return true;

        if (trigger.HasFlag(EscalationTrigger.ConfidenceFloor) && current.HasValue && current.Score < ladder.ConfidenceFloor)
            return true;

        if (trigger.HasFlag(EscalationTrigger.ValidatorFailure)
            && current.HasValue
            && validator is not null
            && !validator.IsValid(current.Value))
        {
            return true;
        }

        if (trigger.HasFlag(EscalationTrigger.Disagreement) && HasDisagreement(candidatesSoFar, ladder, validator))
            return true;

        return false;
    }

    private static bool HasDisagreement<TValue>(
        IReadOnlyList<FieldCandidate<TValue>> candidates,
        FieldEscalationLadder ladder,
        IFieldValidator? validator)
    {
        // Only CREDIBLE candidates count as disagreeing peers. A stage-1 value we escalated
        // past precisely because it failed the validator, fell below the confidence floor, or
        // was self-reported invalid-format is a *rejected* candidate, not an opinion — including
        // it here would make every validator/confidence-triggered recovery abstain (it always
        // differs from the bad value it was recovering from). Excluding it lets a lone credible
        // recovery win, while genuine divergence between two credible reads still abstains
        // (honesty over recall, per the design's disagreement gate).
        var values = candidates.Where(c => IsCredible(c, ladder, validator)).Select(c => c.Value).ToList();
        if (values.Count < 2)
            return false;

        for (var i = 1; i < values.Count; i++)
        {
            if (!ValuesAgree(values[0], values[i], ladder.DisagreementTolerance))
                return true;
        }

        return false;
    }

    /// <summary>
    /// A candidate is "credible" — eligible to be treated as a genuine, competing opinion by the
    /// disagreement gate — only when it carries a value that clears the same bars the escalation
    /// triggers use to reject a value: it was found, it is not self-reported invalid-format, it
    /// meets the ladder's confidence floor, and it passes the ladder's validator (when present).
    /// </summary>
    private static bool IsCredible<TValue>(FieldCandidate<TValue> candidate, FieldEscalationLadder ladder, IFieldValidator? validator)
    {
        if (!candidate.HasValue)
            return false;
        if (candidate.Status == ExtractionStatus.ExtractedInvalidFormat)
            return false;
        if (candidate.Score < ladder.ConfidenceFloor)
            return false;
        if (validator is not null && !validator.IsValid(candidate.Value))
            return false;

        return true;
    }

    private static bool ValuesAgree<TValue>(TValue? a, TValue? b, double tolerance)
    {
        switch (a, b)
        {
            case (decimal da, decimal db):
                return Math.Abs(da - db) <= (decimal)tolerance;
            case (double dda, double ddb):
                return Math.Abs(dda - ddb) <= tolerance;
            case (int ia, int ib):
                return Math.Abs(ia - ib) <= tolerance;
            default:
                return EqualityComparer<TValue>.Default.Equals(a!, b!);
        }
    }

    private static ExtractedField<TValue> ToExtractedField<TValue>(FieldCandidate<TValue> candidate)
    {
        var provenance = new ExtractionProvenance(candidate.Stage);

        if (!candidate.HasValue)
            return ExtractedField<TValue>.Missing(candidate.Locator, provenance);

        if (candidate.Status == ExtractionStatus.ExtractedInvalidFormat)
        {
            return new ExtractedField<TValue>(
                candidate.Value, candidate.Score, candidate.Locator, ExtractionStatus.ExtractedInvalidFormat, provenance);
        }

        // Values reached via semantic search, LLM inference, or header-image OCR are marked
        // ExtractedByInference so a downstream consumer may require human confirmation before
        // they gate a verdict (design doc, "Provenance & honesty vocabulary"), and so
        // VerificationPipeline.CountExtractedFields' extraction-floor count — which only counts
        // Extracted/ExtractedInvalidFormat — excludes them (E7.S7.2/S7.3 owner ruling 4: the
        // floor stays a text-coverage measure, not inflated by a second-source OCR recovery).
        var status = candidate.Stage == StageId.SemanticSearch
            || candidate.Stage == StageId.LlmExtraction
            || candidate.Stage == StageId.HeaderImageOcr
            ? ExtractionStatus.ExtractedByInference
            : ExtractionStatus.Extracted;

        return new ExtractedField<TValue>(candidate.Value, candidate.Score, candidate.Locator, status, provenance);
    }
}
