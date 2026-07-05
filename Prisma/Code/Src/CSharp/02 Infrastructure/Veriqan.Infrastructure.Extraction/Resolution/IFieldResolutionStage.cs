using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// One rung of the per-field progressive fallback-extraction chain (see
/// <c>docs/planning-artifacts/veriqan-fallback-chain-design-2026-07-05.md</c>). Implementations
/// are stateless with respect to a single document — all per-document state (budget, corpus,
/// prior candidates) is supplied via <see cref="FieldResolutionContext{TValue}"/>.
/// </summary>
/// <typeparam name="TValue">The type of the field this stage attempts to resolve.</typeparam>
public interface IFieldResolutionStage<TValue>
{
    /// <summary>
    /// The <see cref="StageId"/> this implementation represents. Used by the orchestrator to
    /// look up which registered stage implements a given ladder rung.
    /// </summary>
    StageId Stage { get; }

    /// <summary>
    /// Attempts to resolve the field described by <paramref name="context"/>.
    /// </summary>
    /// <param name="context">Everything this stage needs for this field of this document.</param>
    /// <param name="cancellationToken">Cancellation token propagated to all async operations.</param>
    /// <returns>
    /// <see cref="Result{T}.IsSuccess"/> with a <see cref="FieldCandidate{TValue}"/> — including a
    /// <see cref="FieldCandidate{TValue}.None"/> candidate when the stage legitimately found
    /// nothing; a cancelled result when <paramref name="cancellationToken"/> is triggered; a
    /// failure result only for genuine infrastructure errors (never for "value not found," which
    /// is a successful <see cref="FieldCandidate{TValue}.None"/> result, not a failure).
    /// </returns>
    Task<Result<FieldCandidate<TValue>>> TryResolveAsync(
        FieldResolutionContext<TValue> context,
        CancellationToken cancellationToken = default);
}
