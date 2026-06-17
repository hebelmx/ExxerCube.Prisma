using System;

namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Result of verifying the printed day count against the computed date span (CL-1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Day-count convention used here:</b>
/// <list type="bullet">
///   <item><description>
///     <c>ComputedSpanDays</c> = <c>(PeriodCutDate − PeriodStart).TotalDays</c>
///     — the <em>exclusive-end</em> span (whole calendar days from start up to but not including cut date).
///     Example: 2025-07-05 to 2025-08-04 → 30 days.
///   </description></item>
///   <item><description>
///     The VEC statement uses the <em>inclusive-end</em> convention:
///     <c>PrintedDays = ComputedSpanDays + 1</c>.
///     Example: fixture prints 31 for the same period.
///   </description></item>
///   <item><description>
///     <see cref="IsConsistent"/> is <see langword="true"/> when
///     <c>PrintedDays == ComputedSpanDays + 1</c>  (i.e. the printed value matches
///     the inclusive-end interpretation of the extracted dates).
///   </description></item>
/// </list>
/// </para>
/// <para>
/// When either date is not available (extraction failed), <see cref="IsConsistent"/> is
/// <see langword="false"/> and <see cref="ComputedSpanDays"/> is <see langword="null"/>.
/// </para>
/// </remarks>
/// <param name="PrintedDays">
/// Day count as literally printed in the statement ("Número de días en el periodo: N").
/// <see langword="null"/> when that field was not extracted.
/// </param>
/// <param name="ComputedSpanDays">
/// Day count computed as <c>(PeriodCutDate − PeriodStart).TotalDays</c> (exclusive-end).
/// <see langword="null"/> when either date was not extracted.
/// </param>
/// <param name="IsConsistent">
/// <see langword="true"/> when <c>PrintedDays == ComputedSpanDays + 1</c>
/// (inclusive-end reconciliation succeeds).
/// <see langword="false"/> when a mismatch is detected or when inputs are unavailable.
/// </param>
public sealed record DayCountVerification(
    int? PrintedDays,
    int? ComputedSpanDays,
    bool IsConsistent)
{
    /// <summary>
    /// Computes a <see cref="DayCountVerification"/> from extracted date fields and the printed count.
    /// </summary>
    /// <param name="periodStart">Extracted period-start field.</param>
    /// <param name="periodCutDate">Extracted cut-date field.</param>
    /// <param name="dayCountPrinted">Extracted printed day-count field.</param>
    /// <returns>A <see cref="DayCountVerification"/> with computed span and consistency flag.</returns>
    public static DayCountVerification Compute(
        ExtractedField<DateOnly> periodStart,
        ExtractedField<DateOnly> periodCutDate,
        ExtractedField<int> dayCountPrinted)
    {
        ArgumentNullException.ThrowIfNull(periodStart);
        ArgumentNullException.ThrowIfNull(periodCutDate);
        ArgumentNullException.ThrowIfNull(dayCountPrinted);

        var printed = dayCountPrinted.Status == ExtractionStatus.Extracted
            ? dayCountPrinted.Value
            : (int?)null;

        if (periodStart.Status != ExtractionStatus.Extracted
            || periodCutDate.Status != ExtractionStatus.Extracted)
        {
            // Dates not available — cannot compute span.
            return new DayCountVerification(printed, null, false);
        }

        var start = periodStart.Value;
        var cut = periodCutDate.Value;

        // Exclusive-end span: number of whole calendar days between start and cut.
        var spanDays = cut.DayNumber - start.DayNumber;

        // Inclusive-end reconciliation: printed should equal spanDays + 1.
        var consistent = printed.HasValue && printed.Value == spanDays + 1;

        return new DayCountVerification(printed, spanDays, consistent);
    }
}
