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
/// With an empty ladder — the case for every <see cref="FieldKind"/> as of E1 — this returns the
/// stage-1 field completely unchanged without constructing any candidate machinery, so E1 is
/// behavior-neutral by construction rather than by coincidence.
/// </remarks>
public sealed class FieldResolutionOrchestrator
{
    private readonly IFieldEscalationLadderRegistry _ladderRegistry;
    private readonly ILogger<FieldResolutionOrchestrator> _logger;

    /// <summary>
    /// Initializes a <see cref="FieldResolutionOrchestrator"/>.
    /// </summary>
    /// <param name="ladderRegistry">Per-field ladder lookup.</param>
    /// <param name="logger">Logger used for escalation-stop diagnostics (missing stage, exhausted budget).</param>
    public FieldResolutionOrchestrator(
        IFieldEscalationLadderRegistry ladderRegistry,
        ILogger<FieldResolutionOrchestrator> logger)
    {
        _ladderRegistry = ladderRegistry ?? throw new ArgumentNullException(nameof(ladderRegistry));
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
    /// <see cref="IFieldResolutionStage{TValue}.Stage"/>. Defaults to none — the case for every
    /// call in E1, since no higher stage exists yet.
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
            return Result<ExtractedField<TValue>>.WithSuccess(positionalField);
        }

        var stages = higherStages ?? Array.Empty<IFieldResolutionStage<TValue>>();

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
            if (!ShouldEscalate(rung.Trigger, current, candidates, ladder))
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
        if (HasDisagreement(candidates, ladder))
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
        FieldEscalationLadder ladder)
    {
        if (trigger.HasFlag(EscalationTrigger.StatusGate) && !current.HasValue)
            return true;

        if (trigger.HasFlag(EscalationTrigger.ConfidenceFloor) && current.HasValue && current.Score < ladder.ConfidenceFloor)
            return true;

        if (trigger.HasFlag(EscalationTrigger.ValidatorFailure)
            && current.HasValue
            && ladder.Validator is not null
            && !ladder.Validator.IsValid(current.Value))
        {
            return true;
        }

        if (trigger.HasFlag(EscalationTrigger.Disagreement) && HasDisagreement(candidatesSoFar, ladder))
            return true;

        return false;
    }

    private static bool HasDisagreement<TValue>(IReadOnlyList<FieldCandidate<TValue>> candidates, FieldEscalationLadder ladder)
    {
        var values = candidates.Where(c => c.HasValue).Select(c => c.Value).ToList();
        if (values.Count < 2)
            return false;

        for (var i = 1; i < values.Count; i++)
        {
            if (!ValuesAgree(values[0], values[i], ladder.DisagreementTolerance))
                return true;
        }

        return false;
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

        // Values reached via semantic search or LLM inference are marked ExtractedByInference so
        // a downstream consumer may require human confirmation before they gate a verdict
        // (design doc, "Provenance & honesty vocabulary"). No stage produces these yet in E1.
        var status = candidate.Stage == StageId.SemanticSearch || candidate.Stage == StageId.LlmExtraction
            ? ExtractionStatus.ExtractedByInference
            : ExtractionStatus.Extracted;

        return new ExtractedField<TValue>(candidate.Value, candidate.Score, candidate.Locator, status, provenance);
    }
}
