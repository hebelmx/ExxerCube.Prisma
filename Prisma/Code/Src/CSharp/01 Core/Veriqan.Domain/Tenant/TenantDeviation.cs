namespace ExxerCube.Prisma.Veriqan.Domain.Tenant;

/// <summary>
/// Records a single rejected or adjusted tolerance override from a tenant profile.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="TenantDeviation"/> is produced for each entry in
/// <see cref="TenantProfile.ToleranceOverrides"/> that could not be applied:
/// either because the rule is <c>BaselineLocked</c>, the override would loosen a
/// <c>TenantTightenableOnly</c> rule, or the value falls outside the legal [Min, Max] range.
/// </para>
/// <para>
/// Deviations are purely diagnostic — the pipeline never fails because of them. They are
/// surfaced through <see cref="ResolvedTenantProfile.Deviations"/> and propagated to the
/// verdict summary so that compliance reviewers can see which overrides were silently reverted.
/// </para>
/// </remarks>
/// <param name="CheckId">
/// The <c>IVecValidationRule.CheckId</c> for which the override was requested, e.g. <c>"CL-10"</c>.
/// </param>
/// <param name="RequestedValue">
/// The override value submitted by the tenant that was rejected.
/// </param>
/// <param name="LegalDefaultUsed">
/// The legal-baseline value that was applied instead of the rejected override.
/// </param>
/// <param name="Reason">
/// Human-readable explanation of why the override was rejected.
/// </param>
public sealed record TenantDeviation(
    string CheckId,
    decimal RequestedValue,
    decimal LegalDefaultUsed,
    string Reason);
