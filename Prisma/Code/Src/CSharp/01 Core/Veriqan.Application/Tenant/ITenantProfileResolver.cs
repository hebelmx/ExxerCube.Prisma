using System.Collections.Generic;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;
using ExxerCube.Prisma.Veriqan.Domain.Tolerances;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Application.Tenant;

/// <summary>
/// Resolves a <see cref="TenantProfile"/> against the legal tolerance specifications and
/// the registered rule set, producing a <see cref="ResolvedTenantProfile"/> that contains
/// only the accepted effective tolerances together with a list of rejected override deviations.
/// </summary>
/// <remarks>
/// <para>
/// The resolver enforces the classification gating rules (Story 9.3b):
/// <list type="bullet">
///   <item><see cref="Domain.Verification.RuleClassification.BaselineLocked"/> rules never accept overrides.</item>
///   <item><see cref="Domain.Verification.RuleClassification.TenantTightenableOnly"/> rules accept an override
///     only when it tightens (i.e. the requested value ≤ legal default).</item>
///   <item><see cref="Domain.Verification.RuleClassification.TenantOverridable"/> rules accept any override
///     within the [Min, Max] range defined in the tolerance specification.</item>
/// </list>
/// </para>
/// <para>
/// Overrides for check IDs not found in the registered rule list are silently skipped.
/// Overrides for check IDs not registered in the tolerance provider produce
/// a deviation with a sentinel legal default of <c>0</c>.
/// </para>
/// </remarks>
public interface ITenantProfileResolver
{
    /// <summary>
    /// Resolves <paramref name="profile"/>'s tolerance overrides against the legal specification,
    /// returning the accepted effective tolerances and any rejected override deviations.
    /// </summary>
    /// <param name="profile">
    /// The source tenant profile whose <see cref="TenantProfile.ToleranceOverrides"/> are to
    /// be validated and applied. Must not be null.
    /// </param>
    /// <param name="toleranceProvider">
    /// Provides the legally-mandated <see cref="Tolerance"/> specification for each check ID.
    /// Must not be null.
    /// </param>
    /// <param name="rules">
    /// The full list of registered <see cref="IVecValidationRule"/> instances from the current
    /// validation assembly. Used to look up each rule's
    /// <see cref="IVecValidationRule.Classification"/>. Must not be null.
    /// </param>
    /// <param name="ct">
    /// Propagated cancellation token. Returns a cancelled result immediately when cancellation
    /// is already requested on entry.
    /// </param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> wrapping a <see cref="ResolvedTenantProfile"/>;
    /// a cancelled result if <paramref name="ct"/> was cancelled.
    /// </returns>
    Result<ResolvedTenantProfile> Resolve(
        TenantProfile profile,
        ILegalToleranceProvider toleranceProvider,
        IReadOnlyList<IVecValidationRule> rules,
        CancellationToken ct = default);
}
