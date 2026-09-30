using ExxerCube.Prisma.Domain.Llm;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Merges a set of extraction candidates (deterministic + LLM tracks) into a single
/// authoritative <see cref="ReconciliationResult"/>.
/// </summary>
/// <remarks>
/// The policy is: (1) a deterministic non-null value wins over any LLM value;
/// (2) if deterministic is absent/empty the first agreeing LLM fills the field;
/// (3) if LLM candidates disagree and deterministic is absent, the field is left
/// unresolved and a <see cref="ReconciliationResult.ReviewFlags"/> entry is emitted.
/// </remarks>
public interface IExtractionReconciler
{
    /// <summary>
    /// Reconciles <paramref name="candidates"/> into a best-effort <see cref="ReconciliationResult"/>.
    /// </summary>
    /// <param name="candidates">
    /// All extraction candidates for a single document, each labelled with its source
    /// (<c>"deterministic"</c>, <c>"llm-text"</c>, <c>"llm-vision"</c>, …).
    /// Must not be null.  May be empty (will return a failure result).
    /// </param>
    /// <param name="cancellationToken">Propagated to any downstream async work.</param>
    /// <returns>
    /// A successful <see cref="ReconciliationResult"/> on the happy path, or a failure
    /// result when the input is invalid (e.g. empty candidate list).
    /// </returns>
    Task<Result<ReconciliationResult>> ReconcileAsync(
        IReadOnlyList<LabelledExtraction> candidates,
        CancellationToken cancellationToken = default);
}
