using System;
using System.Collections.Generic;

namespace ExxerCube.Prisma.Veriqan.Domain.Tenant;

/// <summary>
/// Immutable result of resolving a <see cref="TenantProfile"/> against the legal tolerance
/// specifications. Contains the effective per-rule tolerances after all override validation
/// has been applied, plus a list of rejected overrides.
/// </summary>
/// <remarks>
/// <para>
/// Produced exclusively by <c>TenantProfileResolver.Resolve</c> (Story 9.3b). Callers should
/// not construct this type directly; obtain it through the resolver.
/// </para>
/// <para>
/// <b>Effective tolerance lookup:</b> use <see cref="GetEffectiveTolerance"/> to retrieve the
/// tolerance that a rule should apply. If no tenant override was accepted for a given check,
/// the method falls back to the legal default supplied to that method.
/// </para>
/// </remarks>
public sealed record ResolvedTenantProfile
{
    /// <summary>
    /// Gets the unique identifier of the tenant this profile belongs to.
    /// Matches <see cref="TenantProfile.TenantId"/> of the source profile.
    /// </summary>
    public string TenantId { get; }

    /// <summary>
    /// Gets the human-readable display name of the tenant.
    /// Matches <see cref="TenantProfile.TenantName"/> of the source profile.
    /// </summary>
    public string TenantName { get; }

    /// <summary>
    /// Gets the map of accepted effective tolerance values, keyed by <c>CheckId</c>.
    /// Only contains entries where the tenant's override was accepted and applied.
    /// Rules absent from this map use the legal default.
    /// </summary>
    public IReadOnlyDictionary<string, decimal> EffectiveTolerances { get; }

    /// <summary>
    /// Gets the list of rejected overrides (deviations) from the source profile.
    /// Empty when all overrides were accepted or when no overrides were supplied.
    /// </summary>
    public IReadOnlyList<TenantDeviation> Deviations { get; }

    /// <summary>
    /// Gets <see langword="true"/> when at least one override in the source profile
    /// was rejected and a deviation was recorded; <see langword="false"/> otherwise.
    /// </summary>
    public bool HasDeviations => Deviations.Count > 0;

    /// <summary>
    /// Initializes a <see cref="ResolvedTenantProfile"/>.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="tenantName">Tenant display name.</param>
    /// <param name="effectiveTolerances">Accepted effective tolerances keyed by CheckId.</param>
    /// <param name="deviations">Rejected override records. Pass an empty list when there are none.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="effectiveTolerances"/> or <paramref name="deviations"/> is null.
    /// </exception>
    public ResolvedTenantProfile(
        string tenantId,
        string tenantName,
        IReadOnlyDictionary<string, decimal> effectiveTolerances,
        IReadOnlyList<TenantDeviation> deviations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantName);

        TenantId = tenantId;
        TenantName = tenantName;
        EffectiveTolerances = effectiveTolerances
            ?? throw new ArgumentNullException(nameof(effectiveTolerances));
        Deviations = deviations
            ?? throw new ArgumentNullException(nameof(deviations));
    }

    /// <summary>
    /// Returns the effective tolerance value for <paramref name="checkId"/>, or
    /// <paramref name="legalDefault"/> when no accepted override exists in this profile.
    /// </summary>
    /// <param name="checkId">
    /// The rule check identifier to look up, e.g. <c>"CL-10"</c>.
    /// </param>
    /// <param name="legalDefault">
    /// The legal-baseline default to fall back to when no effective override is present.
    /// </param>
    /// <returns>
    /// The accepted tenant override value when present; otherwise <paramref name="legalDefault"/>.
    /// </returns>
    public decimal GetEffectiveTolerance(string checkId, decimal legalDefault) =>
        EffectiveTolerances.TryGetValue(checkId, out var effective)
            ? effective
            : legalDefault;
}
