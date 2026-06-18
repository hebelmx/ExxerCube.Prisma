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
    /// Gets the CONDUSEF DOF <i>Acuerdo … estado de cuenta estandarizado</i> section
    /// or form-rule reference that this rule enforces (NFR-7 — auditability).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Must be a non-empty string in one of the canonical forms:
    /// <list type="bullet">
    ///   <item><c>"Acuerdo §N"</c> — for a numbered section of the Acuerdo (e.g. <c>"Acuerdo §9"</c>).</item>
    ///   <item><c>"Acuerdo §M/§N"</c> — when the check spans two sections (e.g. <c>"Acuerdo §14/§17"</c>).</item>
    ///   <item><c>"Acuerdo Anexo — &lt;description&gt;"</c> — for global form / typography rules in the
    ///     guía de llenado annex (e.g. <c>"Acuerdo Anexo — Tipografía"</c>).</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Enforcement:</b> the registry/architecture test in
    /// <c>Veriqan.Infrastructure.Validation.Tests</c> asserts that every registered
    /// <see cref="IVecValidationRule"/> exposes a non-empty <c>DofNumeral</c>.
    /// A rule with an empty or whitespace numeral will cause that test to fail.
    /// </para>
    /// <para>
    /// <b>Consumer:</b> <c>VecValidationEngine</c> stamps this value onto every
    /// <see cref="RuleFinding"/> it produces, providing a CheckId → numeral evidence chain
    /// without requiring each rule's <c>Pass</c>/<c>Fail</c>/<c>InsufficientData</c>
    /// call sites to carry the numeral explicitly.
    /// </para>
    /// </remarks>
    string DofNumeral { get; }

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
