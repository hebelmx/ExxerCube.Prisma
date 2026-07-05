using System;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// Stage-local resolution result for one field — the raw output of a single rung of the
/// progressive fallback-extraction chain (see
/// <c>docs/planning-artifacts/veriqan-fallback-chain-design-2026-07-05.md</c>).
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="FieldCandidate{TValue}"/> is deliberately <em>not</em> an
/// <see cref="ExtractedField{T}"/> — it carries a stage-native confidence <see cref="Score"/>
/// that has not yet been mapped into the domain <c>Confidence</c>/<c>Status</c> vocabulary, and
/// it is not yet stamped with a final <see cref="ExtractionProvenance"/>. Only the
/// <c>FieldResolutionOrchestrator</c> is allowed to turn a winning candidate into a domain field.
/// </para>
/// <para>
/// <see cref="HasValue"/> represents "this stage produced nothing" (equivalent to
/// <see cref="ExtractionStatus.NotExtracted"/>) without forcing every stage to reason about a
/// domain <see cref="ExtractionStatus"/> — a stage only needs to say whether it found a value,
/// how confident it is, and where.
/// </para>
/// </remarks>
/// <typeparam name="TValue">The type of the candidate value (e.g. <see cref="string"/>, <see cref="decimal"/>).</typeparam>
public sealed class FieldCandidate<TValue>
{
    private FieldCandidate(
        TValue? value,
        bool hasValue,
        double score,
        StageId stage,
        FieldLocator locator,
        ExtractionStatus status)
    {
        Value = value;
        HasValue = hasValue;
        Score = score;
        Stage = stage;
        Locator = locator ?? throw new ArgumentNullException(nameof(locator));
        Status = status;
    }

    /// <summary>
    /// The candidate value, or <see langword="default"/> when <see cref="HasValue"/> is <see langword="false"/>.
    /// </summary>
    public TValue? Value { get; }

    /// <summary>
    /// <see langword="true"/> when this stage produced a value (whether or not it later clears
    /// validation); <see langword="false"/> when the stage found nothing at all.
    /// </summary>
    public bool HasValue { get; }

    /// <summary>
    /// Stage-native confidence score in the range [0.0, 1.0]. Each stage defines its own scoring
    /// scheme (e.g. positional = 1.0 always; fuzzy = match ratio; semantic = cosine similarity).
    /// The orchestrator — not the stage — is responsible for mapping this into the domain
    /// <see cref="ExtractedField{T}.Confidence"/> vocabulary.
    /// </summary>
    public double Score { get; }

    /// <summary>The resolution stage that produced this candidate.</summary>
    public StageId Stage { get; }

    /// <summary>Where the candidate value was found (or a best-effort hint when not found).</summary>
    public FieldLocator Locator { get; }

    /// <summary>
    /// The stage's raw extraction outcome (e.g. a stage may itself detect
    /// <see cref="ExtractionStatus.ExtractedInvalidFormat"/>). Validators re-evaluate this
    /// independently; a stage's self-reported status is advisory, not final.
    /// </summary>
    public ExtractionStatus Status { get; }

    /// <summary>
    /// Creates a candidate representing a value this stage found.
    /// </summary>
    /// <param name="value">The candidate value.</param>
    /// <param name="score">Stage-native confidence in [0.0, 1.0].</param>
    /// <param name="stage">The stage that produced this candidate.</param>
    /// <param name="locator">Where the value was found.</param>
    /// <param name="status">
    /// The stage's raw outcome; defaults to <see cref="ExtractionStatus.Extracted"/>.
    /// </param>
    public static FieldCandidate<TValue> Found(
        TValue value,
        double score,
        StageId stage,
        FieldLocator locator,
        ExtractionStatus status = ExtractionStatus.Extracted)
    {
        if (score is < 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(score), score, "Score must be in [0.0, 1.0].");

        return new FieldCandidate<TValue>(value, hasValue: true, score, stage, locator, status);
    }

    /// <summary>
    /// Creates a candidate representing "this stage found nothing" — the stage-local equivalent
    /// of <see cref="ExtractionStatus.NotExtracted"/>.
    /// </summary>
    /// <param name="stage">The stage that failed to find a value.</param>
    /// <param name="locator">A best-effort locator hint; defaults to a page-1 hint.</param>
    public static FieldCandidate<TValue> None(StageId stage, FieldLocator? locator = null) =>
        new(default, hasValue: false, score: 0.0, stage, locator ?? FieldLocator.PageHint(), ExtractionStatus.NotExtracted);
}
