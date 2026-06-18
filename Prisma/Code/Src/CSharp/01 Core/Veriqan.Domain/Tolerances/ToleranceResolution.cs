namespace ExxerCube.Prisma.Veriqan.Domain.Tolerances;

/// <summary>
/// The result of resolving a <see cref="Tolerance"/> against an optional override value.
/// Carries the effective tolerance that the rule should use, plus a rejection flag and reason
/// so that callers can surface non-throwing override rejections as data.
/// </summary>
/// <param name="EffectiveValue">
/// The tolerance value that the rule must use for its comparison.
/// This is either the approved override or the legal default.
/// </param>
/// <param name="OverrideRejected">
/// <c>true</c> when a non-null override was supplied but fell outside the permitted
/// [Min, Max] range. The <see cref="EffectiveValue"/> in this case is the
/// <see cref="Tolerance.LegalDefault"/>.
/// </param>
/// <param name="RejectionReason">
/// Human-readable explanation of why the override was rejected, including the
/// offending value, the violated bound, and the fallback applied.
/// <see langword="null"/> when <see cref="OverrideRejected"/> is <c>false</c>.
/// </param>
public sealed record ToleranceResolution(
    decimal EffectiveValue,
    bool OverrideRejected,
    string? RejectionReason);
