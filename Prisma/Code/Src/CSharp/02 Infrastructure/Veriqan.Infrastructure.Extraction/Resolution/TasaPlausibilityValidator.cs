namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// Domain validator for a recovered <see cref="ExxerCube.Prisma.Veriqan.Domain.Extraction.FieldKind.Tasa"/>
/// value: rejects a magnitude that cannot plausibly be an annual interest rate.
/// </summary>
/// <remarks>
/// <para>
/// <c>PeriodSummary.Tasa</c> is stored as a decimal <b>fraction</b> — <c>0.2736</c> means
/// 27.36%, never <c>27.36</c>. This validator's bound is expressed in that same fraction space:
/// <see cref="MaxPlausibleFraction"/> (<c>2.0</c>, i.e. 200%) is a deliberately generous ceiling
/// for a Mexican card statement's annual rate, chosen to catch <b>gross</b> misreads without ever
/// rejecting a genuine (if unusually high) rate.
/// </para>
/// <para>
/// The bound exists specifically to catch a decimal-point drop: an OCR/positional misread that
/// turns <c>0.2736</c> into <c>27.36</c> reads as "2736%", which is far outside
/// [<see cref="MinPlausibleFraction"/>, <see cref="MaxPlausibleFraction"/>] and is abstained
/// rather than allowed to silently gate a compliance verdict on a garbage value. It equally
/// catches a sign flip (a negative rate) via <see cref="MinPlausibleFraction"/>.
/// </para>
/// <para>
/// This is a <b>plausibility</b> check only — a range test on the value's own magnitude. It never
/// inspects, recomputes, or second-guesses any regulatory identity (e.g. CAT vs. Tasa
/// relationships, interest recomputation). Coupling this validator to an identity would make the
/// field abstain exactly when a downstream compliance rule should instead fire a genuine
/// non-compliance verdict — the opposite of this validator's purpose.
/// </para>
/// </remarks>
public sealed class TasaPlausibilityValidator : IFieldValidator
{
    /// <summary>
    /// Lower bound (inclusive), in fraction space — a rate cannot be negative.
    /// </summary>
    public const decimal MinPlausibleFraction = 0m;

    /// <summary>
    /// Upper bound (inclusive), in fraction space — 200%. Generous for a card annual rate; chosen
    /// to sit comfortably below the ~10-100x inflation a dropped decimal point produces (e.g.
    /// 0.2736 misread as 27.36), so it never rejects a genuine rate while still catching that
    /// class of gross misread.
    /// </summary>
    public const decimal MaxPlausibleFraction = 2.0m;

    /// <inheritdoc/>
    /// <remarks>
    /// <see langword="true"/> only when <paramref name="value"/> is a <see cref="decimal"/> within
    /// [<see cref="MinPlausibleFraction"/>, <see cref="MaxPlausibleFraction"/>] (inclusive). Any
    /// other CLR type (including <see langword="null"/>) is treated as implausible rather than
    /// throwing — a validator is never asked to judge "no value" per <see cref="IFieldValidator"/>,
    /// but a wrong boxed type here would be a stage bug, and this validator abstains rather than
    /// crashes on it.
    /// </remarks>
    public bool IsValid(object? value) =>
        value is decimal fraction && fraction >= MinPlausibleFraction && fraction <= MaxPlausibleFraction;
}
