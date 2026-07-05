using System;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// E3-stub domain validator for a recovered <see cref="ExxerCube.Prisma.Veriqan.Domain.Extraction.FieldKind.PaymentDueDate"/>
/// value: rejects any date outside a bounded sanity window.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="FuzzyLabelStage{TValue}"/> only has the raw PDF corpus available — it does not (yet)
/// see the document's own <c>PeriodSummary.PeriodStart</c>/<c>PeriodCutDate</c> — so this is a
/// static, document-independent sanity window rather than the tighter "due date within ~60 days
/// after the cut date" rule the design doc favors.
/// </para>
/// <para>
/// TODO(E3): tighten to a period-relative window once the period dates are threaded through the
/// ladder/validator seam (see <c>docs/planning-artifacts/veriqan-fallback-chain-design-2026-07-05.md</c>).
/// </para>
/// <para>
/// Used twice, belt-and-suspenders: as <see cref="FuzzyLabelStage{TValue}"/>'s own plausibility
/// gate (via the static <see cref="IsPlausible"/>, so the stage never emits an implausible value
/// in the first place) and as the ladder's <c>FieldEscalationLadder.Validator</c> (so the
/// orchestrator's <see cref="Resolution.EscalationTrigger.ValidatorFailure"/>/disagreement-credibility
/// machinery independently agrees).
/// </para>
/// </remarks>
public sealed class PaymentDueDatePlausibilityValidator : IFieldValidator
{
    /// <summary>Earliest date accepted as a plausible payment due date.</summary>
    public static readonly DateOnly MinPlausibleDate = new(2020, 1, 1);

    /// <summary>Latest date accepted as a plausible payment due date.</summary>
    public static readonly DateOnly MaxPlausibleDate = new(2035, 12, 31);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="date"/> falls within
    /// [<see cref="MinPlausibleDate"/>, <see cref="MaxPlausibleDate"/>] (inclusive).
    /// </summary>
    /// <param name="date">The candidate due date.</param>
    public static bool IsPlausible(DateOnly date) => date >= MinPlausibleDate && date <= MaxPlausibleDate;

    /// <inheritdoc/>
    public bool IsValid(object? value) => value is DateOnly date && IsPlausible(date);
}
