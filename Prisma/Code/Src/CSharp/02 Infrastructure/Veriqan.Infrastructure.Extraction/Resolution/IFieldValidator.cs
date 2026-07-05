using ExxerCube.Prisma.Veriqan.Domain.Extraction;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// Domain validator seam for the validator-failure escalation gate (design doc, "Escalation
/// triggers" — e.g. Product must alias-resolve via the reference catalog; Tasa must fall in
/// <c>[0, 200]%</c>; a due date must fall inside the statement period window).
/// </summary>
/// <remarks>
/// <para>
/// A stage's candidate is not "settled" merely by being <see cref="FieldCandidate{TValue}.HasValue"/>
/// — it must also clear the field's validator (when one is registered) before the orchestrator
/// treats it as a non-escalating result. E1 ships <b>zero</b> validators: every
/// <see cref="FieldKind"/>'s <see cref="FieldEscalationLadder"/> has a
/// <see langword="null"/> <see cref="FieldEscalationLadder.Validator"/>, so this gate is inert —
/// it is plumbed so E3 can add real validators as a pure registration change.
/// </para>
/// <para>
/// Deliberately non-generic (<see cref="object"/> value) so a single
/// <see cref="FieldEscalationLadder"/> record can carry a validator without the ladder itself
/// needing to be generic over the field's value type — the ladder is looked up by
/// <see cref="FieldKind"/> alone, independent of <c>TValue</c>.
/// </para>
/// </remarks>
public interface IFieldValidator
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="value"/> passes this field's domain
    /// validation rule. Called only when the candidate already has a value
    /// (<see cref="FieldCandidate{TValue}.HasValue"/> is <see langword="true"/>) — a validator is
    /// never asked to judge "no value."
    /// </summary>
    /// <param name="value">The candidate value, boxed if it is a value type.</param>
    bool IsValid(object? value);
}
