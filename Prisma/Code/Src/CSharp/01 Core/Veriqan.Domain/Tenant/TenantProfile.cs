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
    /// Gets the minimum extraction-confidence score a field must have before a rule
    /// uses it for arithmetic comparison. Fields below this floor cause the rule to
    /// abstain (<see cref="Domain.Enums.FindingVerdict.InsufficientData"/>) instead of
    /// producing a potentially incorrect Pass or Fail verdict.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Legal default: 0.8.</b> Clean <c>Found</c> fields have confidence 1.0 and always
    /// pass the guard. <c>InvalidFormat</c> fields have confidence 0.7 and abstain under the
    /// default floor, which is the correct preventive behaviour (a misread digit must yield
    /// "cannot verify", not "bank non-compliant"). A tenant may raise this value above 0.8
    /// to require even higher extraction quality before verdict, but may never lower it below
    /// zero or above one.
    /// </para>
    /// <para>
    /// Design choice (Story 9.5): kept as a single profile-level scalar rather than a per-rule
    /// map because confidence thresholds are an extraction-quality concern that applies uniformly
    /// across all field-reading rules. Per-rule overrides are reserved for tolerance bands
    /// (see <see cref="ToleranceOverrides"/>).
    /// </para>
    /// </remarks>
    public double MinFieldConfidence { get; }

    /// <summary>
    /// The legal minimum value for <see cref="MinFieldConfidence"/>.
    /// A tenant-supplied value below this is rejected.
    /// </summary>
    public const double MinFieldConfidenceLowerBound = 0.0;

    /// <summary>
    /// The legal maximum value for <see cref="MinFieldConfidence"/>.
    /// A tenant-supplied value above this is rejected.
    /// </summary>
    public const double MinFieldConfidenceUpperBound = 1.0;

    /// <summary>
    /// The CONDUSEF legal-floor default for <see cref="MinFieldConfidence"/> (Story 9.5).
    /// Clean <c>Found</c> fields (confidence 1.0) always pass; <c>InvalidFormat</c> fields
    /// (confidence 0.7) abstain, which is the intended preventive behaviour.
    /// </summary>
    public const double LegalMinFieldConfidenceDefault = 0.8;

    /// <summary>
    /// The CONDUSEF legal floor for <see cref="MinFieldConfidence"/> (Story 9.5 remediation).
    /// A tenant may RAISE this threshold (stricter extraction quality required before verdict),
    /// but may NEVER set it below this value — doing so would bypass the abstain guard on
    /// <c>InvalidFormat</c> fields (confidence 0.7) and allow a mis-read digit to produce a
    /// potentially incorrect Pass or Fail verdict.
    /// </summary>
    /// <remarks>
    /// When a tenant supplies a value below this floor, <c>TenantProfileResolver</c> rejects
    /// the override, records a <see cref="TenantDeviation"/> (CheckId = "MIN-FIELD-CONFIDENCE"),
    /// and resolves <see cref="MinFieldConfidence"/> to this floor (0.8).
    /// </remarks>
    public const double MinFieldConfidenceLegalFloor = 0.8;

    /// <summary>
    /// Initializes a <see cref="TenantProfile"/> with the specified identifiers, override map,
    /// and optional confidence threshold.
    /// </summary>
    /// <param name="tenantId">Unique tenant identifier. Must not be null or white-space.</param>
    /// <param name="tenantName">Human-readable tenant name. Must not be null or white-space.</param>
    /// <param name="toleranceOverrides">
    /// Per-rule override map. Pass an empty dictionary (or <see langword="null"/>
    /// to use an empty map automatically) when no overrides are desired.
    /// </param>
    /// <param name="minFieldConfidence">
    /// Minimum extraction-confidence threshold in [0.0, 1.0]. Defaults to
    /// <see cref="LegalMinFieldConfidenceDefault"/> (0.8) when omitted.
    /// A tenant may tighten (raise) this value; it is validated and clamped by the resolver.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="tenantId"/> or <paramref name="tenantName"/> is null or white-space.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="minFieldConfidence"/> is outside [0.0, 1.0].
    /// </exception>
    public TenantProfile(
        string tenantId,
        string tenantName,
        IReadOnlyDictionary<string, decimal>? toleranceOverrides = null,
        double minFieldConfidence = LegalMinFieldConfidenceDefault)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantName);
        if (minFieldConfidence is < MinFieldConfidenceLowerBound or > MinFieldConfidenceUpperBound)
            throw new ArgumentOutOfRangeException(
                nameof(minFieldConfidence),
                minFieldConfidence,
                $"MinFieldConfidence must be in [{MinFieldConfidenceLowerBound}, {MinFieldConfidenceUpperBound}].");

        TenantId = tenantId;
        TenantName = tenantName;
        ToleranceOverrides = toleranceOverrides
            ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        MinFieldConfidence = minFieldConfidence;
    }

    /// <summary>
    /// Returns the CONDUSEF legal-baseline profile with no overrides and the default
    /// confidence threshold (<see cref="LegalMinFieldConfidenceDefault"/> = 0.8).
    /// Use this when a caller has no tenant context and must apply pure legal defaults.
    /// </summary>
    /// <returns>
    /// A <see cref="TenantProfile"/> with <see cref="TenantId"/> = <c>"LEGAL-BASELINE"</c>,
    /// <see cref="TenantName"/> = <c>"Legal Baseline (CONDUSEF)"</c>, an empty
    /// <see cref="ToleranceOverrides"/> map, and <see cref="MinFieldConfidence"/> = 0.8.
    /// </returns>
    public static TenantProfile LegalBaseline() =>
        new(
            tenantId: "LEGAL-BASELINE",
            tenantName: "Legal Baseline (CONDUSEF)",
            toleranceOverrides: new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase),
            minFieldConfidence: LegalMinFieldConfidenceDefault);
}
