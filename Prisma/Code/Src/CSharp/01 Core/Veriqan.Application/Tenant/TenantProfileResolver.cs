using System;
using System.Collections.Generic;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Validation;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;
using ExxerCube.Prisma.Veriqan.Domain.Tolerances;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Veriqan.Application.Tenant;

/// <summary>
/// Default implementation of <see cref="ITenantProfileResolver"/>.
/// Pure deterministic logic — no I/O, no mutable state, no exceptions for control flow.
/// </summary>
/// <remarks>
/// <para>
/// <b>Classification gating (Story 9.3b):</b>
/// <list type="bullet">
///   <item>
///     <see cref="RuleClassification.BaselineLocked"/> — override is always rejected.
///     Reason: <c>"BaselineLocked rule does not permit tenant overrides."</c>
///   </item>
///   <item>
///     <see cref="RuleClassification.TenantTightenableOnly"/> — override is accepted only
///     when it tightens (override ≤ legal default) and is within [Min, Max].
///     Loosening (override &gt; legal default) is rejected even if within [Min, Max].
///   </item>
///   <item>
///     <see cref="RuleClassification.TenantOverridable"/> — any override within [Min, Max]
///     is accepted.
///   </item>
/// </list>
/// </para>
/// </remarks>
public sealed class TenantProfileResolver : ITenantProfileResolver
{
    /// <inheritdoc />
    public Result<ResolvedTenantProfile> Resolve(
        TenantProfile profile,
        ILegalToleranceProvider toleranceProvider,
        IReadOnlyList<IVecValidationRule> rules,
        CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<ResolvedTenantProfile>();

        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(toleranceProvider);
        ArgumentNullException.ThrowIfNull(rules);

        // Build a fast CheckId → rule lookup
        var ruleMap = new Dictionary<string, IVecValidationRule>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules)
            ruleMap[rule.CheckId] = rule;

        var effectiveTolerances = new Dictionary<string, decimal>(
            StringComparer.OrdinalIgnoreCase);
        var deviations = new List<TenantDeviation>();

        foreach (var (checkId, requestedValue) in profile.ToleranceOverrides)
        {
            // Rule not found in the registered list — skip (another rule may use it later)
            if (!ruleMap.TryGetValue(checkId, out var rule))
                continue;

            var classification = rule.Classification;

            // ----------------------------------------------------------------
            // BaselineLocked: ALWAYS reject
            // ----------------------------------------------------------------
            if (classification == RuleClassification.BaselineLocked)
            {
                // We still need a fallback value for the deviation record.
                // If the provider has a spec, use its LegalDefault; otherwise use 0.
                var legalFallback = toleranceProvider.Has(checkId)
                    ? toleranceProvider.For(checkId).LegalDefault
                    : 0m;

                deviations.Add(new TenantDeviation(
                    CheckId: checkId,
                    RequestedValue: requestedValue,
                    LegalDefaultUsed: legalFallback,
                    Reason: "BaselineLocked rule does not permit tenant overrides."));
                continue;
            }

            // ----------------------------------------------------------------
            // No tolerance spec registered — deviation with sentinel LegalDefault=0
            // ----------------------------------------------------------------
            if (!toleranceProvider.Has(checkId))
            {
                deviations.Add(new TenantDeviation(
                    CheckId: checkId,
                    RequestedValue: requestedValue,
                    LegalDefaultUsed: 0m,
                    Reason: "No legal tolerance spec for checkId; override rejected."));
                continue;
            }

            var tolerance = toleranceProvider.For(checkId);
            var legalDefault = tolerance.LegalDefault;

            // ----------------------------------------------------------------
            // TenantTightenableOnly: accept ONLY when override ≤ LegalDefault AND within range
            // ----------------------------------------------------------------
            if (classification == RuleClassification.TenantTightenableOnly)
            {
                // First check if override exceeds the legal default (loosening)
                if (requestedValue > legalDefault)
                {
                    deviations.Add(new TenantDeviation(
                        CheckId: checkId,
                        RequestedValue: requestedValue,
                        LegalDefaultUsed: legalDefault,
                        Reason: "TenantTightenableOnly rule does not permit loosening; override exceeds legal default."));
                    continue;
                }

                // Tightening — but still must be within [Min, Max]
                var resolution = tolerance.Resolve(requestedValue);
                if (resolution.OverrideRejected)
                {
                    deviations.Add(new TenantDeviation(
                        CheckId: checkId,
                        RequestedValue: requestedValue,
                        LegalDefaultUsed: legalDefault,
                        Reason: resolution.RejectionReason ?? "Override rejected by tolerance spec."));
                    continue;
                }

                // Tightening accepted
                effectiveTolerances[checkId] = resolution.EffectiveValue;
                continue;
            }

            // ----------------------------------------------------------------
            // TenantOverridable: accept within [Min, Max]
            // ----------------------------------------------------------------
            {
                var resolution = tolerance.Resolve(requestedValue);
                if (resolution.OverrideRejected)
                {
                    deviations.Add(new TenantDeviation(
                        CheckId: checkId,
                        RequestedValue: requestedValue,
                        LegalDefaultUsed: legalDefault,
                        Reason: resolution.RejectionReason ?? "Override rejected by tolerance spec."));
                    continue;
                }

                effectiveTolerances[checkId] = resolution.EffectiveValue;
            }
        }

        var resolved = new ResolvedTenantProfile(
            tenantId: profile.TenantId,
            tenantName: profile.TenantName,
            effectiveTolerances: effectiveTolerances,
            deviations: deviations);

        return Result<ResolvedTenantProfile>.WithSuccess(resolved);
    }
}
