using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Application.Validation;

/// <summary>
/// Contract for a single VEC checklist validation rule.
/// </summary>
/// <remarks>
/// <para>
/// <b>Authoring a new rule (Story 4.2+):</b> implement this interface in
/// <c>Veriqan.Infrastructure.Validation</c> and it will be discovered automatically
/// by the Scrutor assembly scan registered in <c>AddVeriqanValidation</c>.
/// </para>
/// <para>
/// <b>Evaluate contract (NFR-5/6):</b>
/// <list type="bullet">
///   <item>
///     Return <c>Result&lt;RuleFinding&gt;.WithSuccess(...)</c> for any conclusive verdict
///     (Pass, Fail, or InsufficientData). <see cref="RuleFinding.InsufficientData"/>
///     is the correct path when a required reference-data capability is absent (FR-20).
///   </item>
///   <item>
///     Return <c>Result&lt;RuleFinding&gt;.WithFailure(...)</c> only for unexpected internal
///     errors (e.g. a required field threw unexpectedly). The engine will capture this
///     as an InsufficientData finding for the <see cref="CheckId"/> and continue; it will
///     NOT abort the batch (NFR-6).
///   </item>
///   <item>Do NOT throw for control flow; throw only for programming errors.</item>
///   <item>Check the <c>ct</c> token early and return a cancelled <c>Result</c> via
///     <c>ResultExtensions.Cancelled&lt;RuleFinding&gt;()</c> when cancellation is
///     requested.</item>
/// </list>
/// </para>
/// <para>
/// <b>Determinism (NFR-5):</b> given the same <see cref="VerificationContext"/>, a rule
/// must always return the same <see cref="RuleFinding"/>. Rules must not depend on
/// wall-clock time, random state, or any mutable shared state.
/// </para>
/// </remarks>
public interface IVecValidationRule
{
    /// <summary>
    /// Gets the unique compliance check identifier for this rule, e.g. <c>"CL-21"</c>.
    /// Must be stable across deployments; it is used as the persistence key.
    /// </summary>
    string CheckId { get; }

    /// <summary>
    /// Gets the algorithmic technique this rule uses to evaluate the check.
    /// Used to populate <see cref="RuleFinding.Technique"/> without each rule
    /// having to repeat it inside <see cref="Evaluate"/>.
    /// </summary>
    TechniqueClass Technique { get; }

    /// <summary>
    /// Evaluates this rule against the provided verification context.
    /// </summary>
    /// <param name="ctx">
    /// The fully-bound verification context containing the reference bundle,
    /// resolved product, availability map, and (optionally) the extracted statement model.
    /// </param>
    /// <param name="ct">
    /// Propagated cancellation token. Rules must check this early and return
    /// <c>ResultExtensions.Cancelled&lt;RuleFinding&gt;()</c> if cancellation is requested.
    /// </param>
    /// <returns>
    /// A <see cref="Result{T}"/> wrapping a <see cref="RuleFinding"/>.
    /// The result is synchronous (no I/O inside a rule); the method signature is kept
    /// non-async intentionally (architecture section 5: rules are pure evaluators).
    /// </returns>
    Result<RuleFinding> Evaluate(VerificationContext ctx, CancellationToken ct = default);
}
