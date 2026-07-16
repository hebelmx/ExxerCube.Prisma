using System;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// Domain validator for a recovered money-valued <c>PeriodSummary</c> field (the 11 RESUMEN /
/// NIVEL / DESGLOSE fields — e.g. <c>MontoIntereses</c>, <c>TotalCargos</c>): rejects a magnitude
/// so large it cannot plausibly be a real peso amount on a card statement.
/// </summary>
/// <remarks>
/// <para>
/// <b>Magnitude only, deliberately no sign constraint.</b> Unlike <see cref="TasaPlausibilityValidator"/>
/// and <see cref="CatPlausibilityValidator"/>, this validator does not reject negative values —
/// several of these summary lines can legitimately be signed (e.g. a credit/abono reducing a
/// balance), and a validator that rejected negatives here would turn a correct, plausible signed
/// read into a false abstain (a false <c>InsufficientData</c>), which is exactly as harmful to
/// honesty as a false compliance verdict. <see cref="IsValid"/> therefore only ever bounds
/// <c>Math.Abs(value)</c>.
/// </para>
/// <para>
/// <b>This deliberately only catches GROSS garbage misreads</b> — a 12-digit digit-concatenation
/// artifact, a stray account/card number parsed as an amount, an OCR run-together of two
/// adjacent fields — not small in-range misreads (e.g. a single transposed digit that still lands
/// under the ceiling). That narrower class of error is explicitly out of scope for this
/// validator; the owner ruled the full RESUMEN/NIVEL/DESGLOSE sweep knowing this limitation, in
/// exchange for a validator simple enough to be trusted not to false-abstain a real, if unusual,
/// amount.
/// </para>
/// <para>
/// This is a plausibility check only — orthogonal to any regulatory identity (e.g. the §20
/// waterfall, §19 interest, or CL-21 recomputation rules). It never inspects or second-guesses
/// arithmetic correctness between fields; coupling this validator to an identity would make the
/// field abstain exactly when a downstream compliance rule should instead fire a genuine
/// non-compliance verdict — the opposite of this validator's purpose (see design note "never
/// abstain iff the compliance identity fails").
/// </para>
/// </remarks>
public sealed class MoneyMagnitudeValidator : IFieldValidator
{
    /// <summary>
    /// Default inclusive magnitude ceiling in pesos — MXN $100,000,000. Comfortably above any
    /// plausible individual card-statement line item while still comfortably below the magnitude
    /// a 12-digit (or longer) digit-concatenation misread produces.
    /// </summary>
    public const decimal DefaultCeiling = 100_000_000m;

    private readonly decimal _ceiling;

    /// <summary>
    /// Initializes a <see cref="MoneyMagnitudeValidator"/> with the <see cref="DefaultCeiling"/>.
    /// </summary>
    public MoneyMagnitudeValidator()
        : this(DefaultCeiling)
    {
    }

    /// <summary>
    /// Initializes a <see cref="MoneyMagnitudeValidator"/> with an explicit magnitude ceiling.
    /// </summary>
    /// <param name="ceiling">
    /// Inclusive magnitude ceiling in pesos. Must be non-negative; the validator compares
    /// <c>Math.Abs(value)</c> against this bound.
    /// </param>
    public MoneyMagnitudeValidator(decimal ceiling)
    {
        if (ceiling < 0m)
            throw new ArgumentOutOfRangeException(nameof(ceiling), ceiling, "Ceiling must be non-negative.");

        _ceiling = ceiling;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <see langword="true"/> only when <paramref name="value"/> is a <see cref="decimal"/> whose
    /// absolute value does not exceed this instance's ceiling. No sign constraint — see the
    /// class-level remarks. Any other CLR type (including <see langword="null"/>) is treated as
    /// implausible rather than throwing.
    /// </remarks>
    public bool IsValid(object? value) =>
        value is decimal amount && Math.Abs(amount) <= _ceiling;
}
