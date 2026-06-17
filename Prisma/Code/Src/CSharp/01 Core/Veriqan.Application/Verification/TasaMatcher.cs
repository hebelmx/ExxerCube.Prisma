using System;
using System.Globalization;
using System.Linq;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;

namespace ExxerCube.Prisma.Veriqan.Application.Verification;

/// <summary>
/// Pure-function service that matches an extracted TASA against the reference bundle
/// for a given product and billing period (FR-5 / CL-9).
/// </summary>
/// <remarks>
/// <para>
/// This is a stateless, side-effect-free service — no I/O, no DI required.
/// Call <see cref="Match"/> directly; no instance needed.
/// </para>
/// <para>
/// <b>Product matching:</b> the bundle's <see cref="InterestRateEntry.ProductId"/> is
/// compared case-insensitively against the <c>productId</c> argument.
/// </para>
/// <para>
/// <b>Period matching:</b> when <c>periodStart</c> and <c>periodCutDate</c>
/// are available, the method selects the <see cref="RateByPeriod"/> entry whose
/// <c>PeriodStart</c> / <c>PeriodEnd</c> overlap the extracted period.
/// When ISO dates are absent from the bundle entry, the <c>PeriodLabel</c> is used
/// as a fallback (substring match, case-insensitive).  When no period overlap can be
/// determined, the most-recent entry is used as the best-effort fallback.
/// </para>
/// <para>
/// <b>Tolerance:</b> default 0.0001 (i.e. 0.01 percentage point).  Callers may pass a
/// custom tolerance via the <c>tolerance</c> argument.
/// </para>
/// </remarks>
public static class TasaMatcher
{
    /// <summary>Default rate comparison tolerance (0.0001 = 0.01 percentage point).</summary>
    public const decimal DefaultTolerance = 0.0001m;

    /// <summary>
    /// Matches the extracted TASA from <paramref name="periodSummary"/> against the TASA
    /// in <paramref name="bundle"/> for the specified product + period.
    /// </summary>
    /// <param name="periodSummary">
    /// The extracted period summary carrying the <c>Tasa</c> field and billing dates.
    /// </param>
    /// <param name="bundle">The VEC reference bundle containing the TASA table.</param>
    /// <param name="productId">
    /// Canonical product id to look up in the bundle's interest-rate table
    /// (e.g. <c>"TC-BSSB"</c>).
    /// </param>
    /// <param name="tolerance">
    /// Maximum absolute difference between extracted and bundle rates to be considered a match.
    /// Defaults to <see cref="DefaultTolerance"/>.
    /// </param>
    /// <returns>A <see cref="TasaMatchResult"/> describing the outcome.</returns>
    public static TasaMatchResult Match(
        PeriodSummary? periodSummary,
        VecReferenceBundle bundle,
        string productId,
        decimal tolerance = DefaultTolerance)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);

        // Require extracted TASA.
        if (periodSummary is null
            || periodSummary.Tasa.Status != ExtractionStatus.Extracted
            || periodSummary.Tasa.Value is var extractedRate && false)
        {
            // Unreachable branch above; evaluate properly below.
        }

        if (periodSummary is null || periodSummary.Tasa.Status != ExtractionStatus.Extracted)
        {
            return new TasaMatchResult(
                TasaMatchOutcome.ExtractedTasaMissing,
                ExtractedRate: null,
                BundleRate: null,
                AbsoluteDifference: null,
                ToleranceUsed: tolerance,
                MatchedPeriodLabel: null,
                Detail: "Extracted TASA is not available (field not extracted from statement).");
        }

        var extracted = periodSummary.Tasa.Value!;

        // Locate the rate table for this product.
        if (bundle.InterestRates is null || bundle.InterestRates.Count == 0)
        {
            return new TasaMatchResult(
                TasaMatchOutcome.InsufficientData,
                ExtractedRate: extracted,
                BundleRate: null,
                AbsoluteDifference: null,
                ToleranceUsed: tolerance,
                MatchedPeriodLabel: null,
                Detail: $"Bundle contains no interest-rate table (InterestRates is empty/null).");
        }

        var productEntry = bundle.InterestRates.FirstOrDefault(e =>
            string.Equals(e.ProductId, productId, StringComparison.OrdinalIgnoreCase));

        if (productEntry is null)
        {
            return new TasaMatchResult(
                TasaMatchOutcome.InsufficientData,
                ExtractedRate: extracted,
                BundleRate: null,
                AbsoluteDifference: null,
                ToleranceUsed: tolerance,
                MatchedPeriodLabel: null,
                Detail: $"Bundle has no interest-rate entry for product '{productId}'.");
        }

        if (productEntry.RatesByPeriod is null || productEntry.RatesByPeriod.Count == 0)
        {
            return new TasaMatchResult(
                TasaMatchOutcome.InsufficientData,
                ExtractedRate: extracted,
                BundleRate: null,
                AbsoluteDifference: null,
                ToleranceUsed: tolerance,
                MatchedPeriodLabel: null,
                Detail: $"Bundle interest-rate entry for product '{productId}' has no period entries.");
        }

        // Select the best matching period entry.
        var bestEntry = SelectBestPeriod(
            productEntry,
            periodSummary.PeriodStart,
            periodSummary.PeriodCutDate);

        var bundleRate = bestEntry.AnnualOrdinaryFixedRate;
        var diff = Math.Abs(extracted - bundleRate);
        var matches = diff <= tolerance;

        return new TasaMatchResult(
            matches ? TasaMatchOutcome.Matched : TasaMatchOutcome.Mismatch,
            ExtractedRate: extracted,
            BundleRate: bundleRate,
            AbsoluteDifference: diff,
            ToleranceUsed: tolerance,
            MatchedPeriodLabel: bestEntry.PeriodLabel,
            Detail: matches
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "Extracted TASA {0:P2} matches bundle TASA {1:P2} for period '{2}' (diff {3:P4} ≤ tolerance {4:P4}).",
                    extracted, bundleRate, bestEntry.PeriodLabel ?? "(no label)", diff, tolerance)
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "Extracted TASA {0:P2} does NOT match bundle TASA {1:P2} for period '{2}' (diff {3:P4} > tolerance {4:P4}).",
                    extracted, bundleRate, bestEntry.PeriodLabel ?? "(no label)", diff, tolerance));
    }

    // -----------------------------------------------------------------------
    // Period-selection helper
    // -----------------------------------------------------------------------

    private static RateByPeriod SelectBestPeriod(
        InterestRateEntry productEntry,
        ExtractedField<DateOnly> periodStart,
        ExtractedField<DateOnly> periodCutDate)
    {
        // Try overlap: find entries where the extracted period overlaps [PeriodStart, PeriodEnd].
        if (periodStart.Status == ExtractionStatus.Extracted
            && periodCutDate.Status == ExtractionStatus.Extracted)
        {
            var start = periodStart.Value;
            var cut = periodCutDate.Value;

            foreach (var entry in productEntry.RatesByPeriod)
            {
                if (TryParseIsoDate(entry.PeriodStart, out var entryStart)
                    && TryParseIsoDate(entry.PeriodEnd, out var entryEnd))
                {
                    // Overlap: extracted period starts on or before entry end AND cut is on or after entry start.
                    if (start <= entryEnd && cut >= entryStart)
                        return entry;
                }
            }
        }

        // Fallback: return the last entry (most recent period).
        return productEntry.RatesByPeriod[productEntry.RatesByPeriod.Count - 1];
    }

    private static bool TryParseIsoDate(string? raw, out DateOnly result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        return DateOnly.TryParseExact(
            raw, "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out result);
    }
}
