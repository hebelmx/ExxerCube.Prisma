using System;
using System.Collections.Generic;

namespace ExxerCube.Prisma.Veriqan.Domain.Tenant;

/// <summary>
/// Immutable value object representing a tenant's configuration profile, including any
/// tolerance overrides the tenant wishes to apply on top of the CONDUSEF legal baseline.
/// </summary>
/// <remarks>
/// <para>
/// When <see cref="ToleranceOverrides"/> is empty the profile is functionally equivalent to
/// <see cref="LegalBaseline"/>: all rules apply the legally-mandated defaults.
/// </para>
/// <para>
/// Overrides are validated and resolved by <c>TenantProfileResolver</c> (Story 9.3b).
/// Invalid or out-of-range overrides are rejected and surfaced as <see cref="TenantDeviation"/>
/// entries without aborting the resolution. The effective values live in the resulting
/// <see cref="ResolvedTenantProfile"/>.
/// </para>
/// </remarks>
public sealed record TenantProfile
{
    /// <summary>
    /// Gets the unique identifier for this tenant, e.g. <c>"BANCO-NORTE-001"</c>.
    /// Never null or white-space.
    /// </summary>
    public string TenantId { get; }

    /// <summary>
    /// Gets the human-readable display name for this tenant.
    /// Never null or white-space.
    /// </summary>
    public string TenantName { get; }

    /// <summary>
    /// Gets the per-rule tolerance override map where the key is the <c>CheckId</c>
    /// (e.g. <c>"CL-10"</c>) and the value is the requested tolerance value.
    /// An empty map means no overrides — the legal baseline applies for all rules.
    /// </summary>
    public IReadOnlyDictionary<string, decimal> ToleranceOverrides { get; }

    /// <summary>
    /// Initializes a <see cref="TenantProfile"/> with the specified identifiers and override map.
    /// </summary>
    /// <param name="tenantId">Unique tenant identifier. Must not be null or white-space.</param>
    /// <param name="tenantName">Human-readable tenant name. Must not be null or white-space.</param>
    /// <param name="toleranceOverrides">
    /// Per-rule override map. Pass an empty dictionary (or <see langword="null"/>
    /// to use an empty map automatically) when no overrides are desired.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="tenantId"/> or <paramref name="tenantName"/> is null or white-space.
    /// </exception>
    public TenantProfile(
        string tenantId,
        string tenantName,
        IReadOnlyDictionary<string, decimal>? toleranceOverrides = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantName);

        TenantId = tenantId;
        TenantName = tenantName;
        ToleranceOverrides = toleranceOverrides
            ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the CONDUSEF legal-baseline profile with no overrides.
    /// Use this when a caller has no tenant context and must apply pure legal defaults.
    /// </summary>
    /// <returns>
    /// A <see cref="TenantProfile"/> with <see cref="TenantId"/> = <c>"LEGAL-BASELINE"</c>,
    /// <see cref="TenantName"/> = <c>"Legal Baseline (CONDUSEF)"</c>, and an empty
    /// <see cref="ToleranceOverrides"/> map.
    /// </returns>
    public static TenantProfile LegalBaseline() =>
        new(
            tenantId: "LEGAL-BASELINE",
            tenantName: "Legal Baseline (CONDUSEF)",
            toleranceOverrides: new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase));
}
