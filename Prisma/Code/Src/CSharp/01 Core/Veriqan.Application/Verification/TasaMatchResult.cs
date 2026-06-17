using System;

namespace ExxerCube.Prisma.Veriqan.Application.Verification;

/// <summary>
/// Full result of a TASA match check (FR-5 / CL-9).
/// </summary>
/// <remarks>
/// <para>
/// This is a pure value returned by <see cref="TasaMatcher.Match"/>; no I/O, no DI.
/// </para>
/// </remarks>
/// <param name="Outcome">Whether the rates matched, mismatched, or data was insufficient.</param>
/// <param name="ExtractedRate">
/// The TASA extracted from the statement as a decimal fraction (e.g. 0.1975 = 19.75%),
/// or <see langword="null"/> when the field was not extracted.
/// </param>
/// <param name="BundleRate">
/// The TASA from the reference bundle for the matched product + period as a decimal fraction,
/// or <see langword="null"/> when the bundle has no matching entry.
/// </param>
/// <param name="AbsoluteDifference">
/// Absolute difference <c>|ExtractedRate − BundleRate|</c>,
/// or <see langword="null"/> when either rate is unavailable.
/// </param>
/// <param name="ToleranceUsed">
/// The tolerance applied (as a decimal fraction) when comparing the rates.
/// </param>
/// <param name="MatchedPeriodLabel">
/// The period label from the matching <see cref="Domain.ReferenceData.RateByPeriod"/> entry,
/// or <see langword="null"/> when no period was matched.
/// </param>
/// <param name="Detail">
/// Human-readable explanation suitable for logging or a checklist finding detail.
/// </param>
public sealed record TasaMatchResult(
    TasaMatchOutcome Outcome,
    decimal? ExtractedRate,
    decimal? BundleRate,
    decimal? AbsoluteDifference,
    decimal ToleranceUsed,
    string? MatchedPeriodLabel,
    string Detail)
{
    /// <summary>
    /// Returns <see langword="true"/> when the outcome is <see cref="TasaMatchOutcome.Matched"/>.
    /// </summary>
    public bool IsMatch => Outcome == TasaMatchOutcome.Matched;

    /// <summary>
    /// Returns <see langword="true"/> when the outcome is <see cref="TasaMatchOutcome.InsufficientData"/>
    /// or <see cref="TasaMatchOutcome.ExtractedTasaMissing"/>.
    /// </summary>
    public bool IsInsufficientData =>
        Outcome is TasaMatchOutcome.InsufficientData or TasaMatchOutcome.ExtractedTasaMissing;
}
