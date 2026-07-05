using System.Collections.Generic;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// One step beyond stage 1 in a field's escalation ladder: which stage to try next, and which
/// <see cref="EscalationTrigger"/> gate(s) must fire on the current best candidate before the
/// orchestrator runs it.
/// </summary>
/// <param name="Stage">
/// The <see cref="StageId"/> to run when <paramref name="Trigger"/> fires. The orchestrator looks
/// up the concrete <c>IFieldResolutionStage&lt;TValue&gt;</c> implementation for this id among the
/// stages registered for the field being resolved.
/// </param>
/// <param name="Trigger">
/// The gate(s) — combined with bitwise OR — any one of which is sufficient to escalate to this
/// rung.
/// </param>
public sealed record FieldEscalationRung(StageId Stage, EscalationTrigger Trigger);

/// <summary>
/// Declarative, per-<see cref="FieldKind"/> description of a field's progressive
/// fallback-extraction chain (design doc, "Architecture — per-field resolver pipeline").
/// </summary>
/// <remarks>
/// Adding, reordering, or tightening a field's ladder is meant to be a <em>registration</em>
/// change — editing the table built by <see cref="FieldEscalationLadderRegistry"/> — not new
/// stage or orchestrator code. As of E1 every field's ladder is
/// <see cref="PositionalOnly(FieldKind)"/>: zero rungs, so no field ever escalates.
/// </remarks>
/// <param name="FieldKind">The field this ladder governs.</param>
/// <param name="ConfidenceFloor">
/// Per-field confidence floor used by the <see cref="EscalationTrigger.ConfidenceFloor"/> gate.
/// Ignored (and irrelevant) when no rung declares that gate.
/// </param>
/// <param name="Rungs">
/// Ordered list of rungs beyond stage 1. Empty means positional-only — the field never escalates.
/// </param>
/// <param name="Validator">
/// Optional domain validator used by the <see cref="EscalationTrigger.ValidatorFailure"/> gate.
/// <see langword="null"/> for every field in E1 (no validators exist yet — E3 adds them).
/// </param>
/// <param name="DisagreementTolerance">
/// Per-field tolerance used by the <see cref="EscalationTrigger.Disagreement"/> gate to decide
/// whether two numeric candidates "diverge." Unused for non-numeric fields and when no rung
/// declares the <see cref="EscalationTrigger.Disagreement"/> gate. Defaults to 0 (exact match
/// required).
/// </param>
public sealed record FieldEscalationLadder(
    FieldKind FieldKind,
    double ConfidenceFloor,
    IReadOnlyList<FieldEscalationRung> Rungs,
    IFieldValidator? Validator = null,
    double DisagreementTolerance = 0.0)
{
    /// <summary>
    /// Creates the E1 default ladder for a field: stage 1 (positional) only, no confidence floor,
    /// no validator, no higher rungs — the field's positional result is always returned unchanged.
    /// </summary>
    /// <param name="fieldKind">The field this ladder governs.</param>
    public static FieldEscalationLadder PositionalOnly(FieldKind fieldKind) =>
        new(fieldKind, ConfidenceFloor: 0.0, Rungs: []);
}
