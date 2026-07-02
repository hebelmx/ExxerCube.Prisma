using ExxerCube.Prisma.Domain.Entities;

namespace ExxerCube.Prisma.Domain.Llm;

/// <summary>
/// The outcome of reconciling multiple <see cref="LabelledExtraction"/> candidates into a
/// single authoritative <see cref="Expediente"/>.
/// </summary>
/// <param name="Best">
/// The assembled <see cref="Expediente"/> built from the winning per-field values.
/// Never <see langword="null"/>; the reconciler always returns a best-effort result.
/// </param>
/// <param name="Candidates">
/// All candidates that were submitted, in original order.  Callers may inspect this for
/// tracing and audit.
/// </param>
/// <param name="ReviewFlags">
/// Human-readable conflict descriptions (one per field where candidates disagreed and
/// the deterministic track was absent).  An empty list means no conflicts were detected.
/// </param>
public sealed record ReconciliationResult(
    Expediente Best,
    IReadOnlyList<LabelledExtraction> Candidates,
    IReadOnlyList<string> ReviewFlags);
