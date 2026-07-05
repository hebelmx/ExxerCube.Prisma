using System;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// The gate(s) that must fire on the current best candidate before the orchestrator escalates a
/// field to the next rung of its <see cref="FieldEscalationLadder"/> (design doc, "Escalation
/// triggers"). Flags so a single rung transition can require any combination of gates.
/// </summary>
/// <remarks>
/// The <see cref="Disagreement"/> gate is evaluated across <em>all</em> candidates produced so
/// far (it needs at least two), not just the immediately-preceding one — see
/// <c>FieldResolutionOrchestrator</c>. Disagreement never picks a winner by fiat: when it fires
/// and there is no higher rung left to resolve it, the field abstains (honesty over recall).
/// </remarks>
[Flags]
public enum EscalationTrigger
{
    /// <summary>No gate — the rung never escalates (used only defensively; ladders should omit
    /// a rung entirely rather than declare it unreachable).</summary>
    None = 0,

    /// <summary>
    /// Fires when the current best candidate's status is
    /// <see cref="ExtractionStatus.NotExtracted"/> — nothing to lose by trying the next stage.
    /// </summary>
    StatusGate = 1 << 0,

    /// <summary>
    /// Fires when the current best candidate is
    /// <see cref="ExtractionStatus.Extracted"/> but its stage-native
    /// <see cref="FieldCandidate{TValue}.Score"/> is below the ladder's per-field
    /// <see cref="FieldEscalationLadder.ConfidenceFloor"/>.
    /// </summary>
    ConfidenceFloor = 1 << 1,

    /// <summary>
    /// Fires when the current best candidate has a value but fails the ladder's
    /// <see cref="FieldEscalationLadder.Validator"/> (when one is registered — inert in E1,
    /// which ships zero validators).
    /// </summary>
    ValidatorFailure = 1 << 2,

    /// <summary>
    /// Fires when two or more candidates produced so far for this field diverge beyond the
    /// field's tolerance. The orchestrator abstains rather than picking a winner when this fires
    /// and no further rung can resolve it.
    /// </summary>
    Disagreement = 1 << 3,
}
