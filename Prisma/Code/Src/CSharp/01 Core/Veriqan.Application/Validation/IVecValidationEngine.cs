using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Application.Validation;

/// <summary>
/// Runs all registered <see cref="IVecValidationRule"/> implementations against a
/// <see cref="VerificationContext"/> and returns the aggregated collection of findings.
/// </summary>
/// <remarks>
/// <para>
/// <b>Batch isolation (NFR-6):</b> a rule that returns a failure <c>Result</c>
/// (or throws unexpectedly) must NOT abort the batch.  The engine captures the error
/// as an <see cref="Domain.Enums.FindingVerdict.InsufficientData"/> finding for that
/// <see cref="IVecValidationRule.CheckId"/> and continues with the remaining rules.
/// </para>
/// <para>
/// <b>Determinism (NFR-5):</b> the returned list is sorted by
/// <see cref="RuleFinding.CheckId"/> (ordinal) so that identical inputs always produce
/// the same output order, regardless of DI registration order.
/// </para>
/// <para>
/// <b>No exceptions for control flow (NFR-6):</b> the engine never throws; all
/// outcomes are expressed as <see cref="Result{T}"/> values.
/// The outer result is a failure only if the engine itself encounters an unexpected
/// internal error (not a rule failure); cancellation is returned as a cancelled result.
/// </para>
/// </remarks>
public interface IVecValidationEngine
{
    /// <summary>
    /// Runs all registered rules against <paramref name="ctx"/> and returns the
    /// full, deterministically-ordered list of rule findings.
    /// </summary>
    /// <param name="ctx">The bound verification context for this run.</param>
    /// <param name="ct">
    /// Propagated cancellation token.  If cancellation is requested before the run
    /// begins the engine returns a cancelled <c>Result</c> immediately.
    /// Mid-run cancellation is checked between rules.
    /// </param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> wrapping the read-only list of findings
    /// (one per rule); or a cancelled result if <paramref name="ct"/> was cancelled;
    /// or a failure result for unexpected engine-level errors.
    /// </returns>
    Task<Result<IReadOnlyList<RuleFinding>>> RunAsync(
        VerificationContext ctx,
        CancellationToken ct = default);
}
