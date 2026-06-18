using System;

namespace ExxerCube.Prisma.Veriqan.Domain.Tolerances;

/// <summary>
/// Typed, range-bounded tolerance specification for a single computation rule.
/// Carries the legal default and the permitted [Min, Max] override range.
/// </summary>
/// <remarks>
/// Invariant (enforced at construction): Min ≤ LegalDefault ≤ Max.
/// A violation of this invariant is a programming error in the legal spec and raises
/// <see cref="ArgumentException"/>.
/// </remarks>
public sealed record Tolerance
{
    /// <summary>
    /// The legally-mandated default value derived from the law's rounding math.
    /// Applied when no override is supplied or when the supplied override is rejected.
    /// </summary>
    public decimal LegalDefault { get; }

    /// <summary>
    /// Minimum permitted override value (inclusive).
    /// A tenant may TIGHTEN toward 0, but the floor prevents loosening beyond this value.
    /// </summary>
    public decimal Min { get; }

    /// <summary>
    /// Maximum permitted override value (inclusive).
    /// Caps how loosely a tenant may configure the tolerance — exceeding this is rejected.
    /// </summary>
    public decimal Max { get; }

    /// <summary>
    /// Initializes a <see cref="Tolerance"/> and validates that Min ≤ LegalDefault ≤ Max.
    /// </summary>
    /// <param name="legalDefault">
    /// The legally-mandated default value, derived from the law's rounding math.
    /// Applied when no override is supplied or when the supplied override is rejected.
    /// </param>
    /// <param name="min">
    /// Minimum permitted override value (inclusive). A tenant may TIGHTEN toward 0,
    /// but the floor prevents loosening beyond the rule's legal tolerance floor.
    /// </param>
    /// <param name="max">
    /// Maximum permitted override value (inclusive). Caps how loosely a tenant may
    /// configure the tolerance — exceeding the legal ceiling is rejected.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="min"/> &gt; <paramref name="legalDefault"/> or
    /// <paramref name="legalDefault"/> &gt; <paramref name="max"/>.
    /// This is a programming error in the legal spec, not a runtime data error.
    /// </exception>
    public Tolerance(decimal legalDefault, decimal min, decimal max)
    {
        if (min > legalDefault)
            throw new ArgumentException(
                $"Min ({min}) must be ≤ LegalDefault ({legalDefault}).",
                nameof(min));

        if (legalDefault > max)
            throw new ArgumentException(
                $"LegalDefault ({legalDefault}) must be ≤ Max ({max}).",
                nameof(legalDefault));

        LegalDefault = legalDefault;
        Min = min;
        Max = max;
    }

    /// <summary>
    /// Resolves the effective tolerance value, optionally applying a tenant/bundle override.
    /// </summary>
    /// <param name="requestedOverride">
    /// The override value from the bundle's <c>ToleranceConfig</c>, or <see langword="null"/>
    /// when no override was supplied.
    /// </param>
    /// <returns>
    /// A <see cref="ToleranceResolution"/> describing the effective value and whether any
    /// supplied override was rejected. Never throws.
    /// </returns>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>
    ///     <c>null</c> override → <see cref="LegalDefault"/> applied; not rejected.
    ///   </item>
    ///   <item>
    ///     Override within [<see cref="Min"/>, <see cref="Max"/>] → override applied; not rejected.
    ///   </item>
    ///   <item>
    ///     Override outside [<see cref="Min"/>, <see cref="Max"/>] →
    ///     <see cref="LegalDefault"/> applied; <see cref="ToleranceResolution.OverrideRejected"/> = <c>true</c>,
    ///     <see cref="ToleranceResolution.RejectionReason"/> describes the violation.
    ///   </item>
    /// </list>
    /// </remarks>
    public ToleranceResolution Resolve(decimal? requestedOverride)
    {
        if (requestedOverride is null)
            return new ToleranceResolution(LegalDefault, OverrideRejected: false, RejectionReason: null);

        var value = requestedOverride.Value;

        if (value >= Min && value <= Max)
            return new ToleranceResolution(value, OverrideRejected: false, RejectionReason: null);

        var reason = value < Min
            ? $"Override {value} is below the legal floor {Min}; legal default {LegalDefault} applied."
            : $"Override {value} exceeds the legal ceiling {Max}; legal default {LegalDefault} applied.";

        return new ToleranceResolution(LegalDefault, OverrideRejected: true, RejectionReason: reason);
    }
}
