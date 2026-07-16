namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// Domain validator for a recovered <see cref="ExxerCube.Prisma.Veriqan.Domain.Extraction.FieldKind.Cat"/>
/// value: rejects a magnitude that cannot plausibly be a CAT (Costo Anual Total).
/// </summary>
/// <remarks>
/// <para>
/// Like <c>Tasa</c>, <c>PeriodSummary.Cat</c> is stored as a decimal <b>fraction</b> —
/// <c>0.35</c> means 35%, never <c>35</c>. CAT is regulatorily required to be at or above the
/// nominal Tasa and can legitimately run substantially higher (it folds in commissions, IVA, and
/// other costs), so this validator's ceiling — <see cref="MaxPlausibleFraction"/> (<c>3.0</c>,
/// i.e. 300%) — is deliberately looser than <see cref="TasaPlausibilityValidator.MaxPlausibleFraction"/>.
/// </para>
/// <para>
/// This is a <b>single-field magnitude band only</b>: it has no visibility into the statement's
/// resolved Tasa value, so it cannot and does not enforce the literal "CAT ≥ Tasa" regulatory
/// relationship — that cross-field check belongs to a compliance rule, not this extraction-side
/// plausibility gate. What it does catch is the CAT-field label's known homonym trap: a
/// mis-anchored positional read that lands on an adjacent "CAT:"-prefixed phone number or account
/// digit string parses as an astronomically large fraction (e.g. a 10-digit phone number parsed
/// as a raw number is many orders of magnitude past 3.0), which this bound rejects outright.
/// </para>
/// <para>
/// This is a plausibility check only — orthogonal to any regulatory identity. It never recomputes
/// or second-guesses a compliance rule's own math; coupling this validator to an identity would
/// make the field abstain exactly when a downstream rule should instead fire a genuine
/// non-compliance verdict.
/// </para>
/// </remarks>
public sealed class CatPlausibilityValidator : IFieldValidator
{
    /// <summary>
    /// Lower bound (inclusive), in fraction space — a CAT cannot be negative.
    /// </summary>
    public const decimal MinPlausibleFraction = 0m;

    /// <summary>
    /// Upper bound (inclusive), in fraction space — 300%. Looser than
    /// <see cref="TasaPlausibilityValidator.MaxPlausibleFraction"/> because CAT legitimately runs
    /// higher than the nominal rate, while still comfortably rejecting the CAT/phone-number
    /// homonym misread (which parses many orders of magnitude past this bound).
    /// </summary>
    public const decimal MaxPlausibleFraction = 3.0m;

    /// <inheritdoc/>
    /// <remarks>
    /// <see langword="true"/> only when <paramref name="value"/> is a <see cref="decimal"/> within
    /// [<see cref="MinPlausibleFraction"/>, <see cref="MaxPlausibleFraction"/>] (inclusive). Any
    /// other CLR type (including <see langword="null"/>) is treated as implausible rather than
    /// throwing.
    /// </remarks>
    public bool IsValid(object? value) =>
        value is decimal fraction && fraction >= MinPlausibleFraction && fraction <= MaxPlausibleFraction;
}
