using System;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

/// <summary>
/// Domain validator for a recovered <see cref="ExxerCube.Prisma.Veriqan.Domain.Extraction.FieldKind.PaymentDueDate"/>
/// value: rejects any date outside a bounded sanity window.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="FuzzyLabelStage{TValue}"/> only has the raw PDF corpus available — it does not see
/// the document's own <c>PeriodSummary.PeriodStart</c>/<c>PeriodCutDate</c> — so its own
/// plausibility gate always uses the static, document-independent <see cref="IsPlausible"/>
/// window. The orchestrator, however, resolves <c>PeriodCutDate</c> before <c>PaymentDueDate</c>
/// (<see cref="EscalatingStatementFieldExtractor"/>) and — when that cut date was itself
/// successfully extracted — passes a period-relative instance of this validator as the
/// PaymentDueDate call's <c>validatorOverride</c>, tightening the ladder's
/// <see cref="Resolution.EscalationTrigger.ValidatorFailure"/>/disagreement-credibility gate to
/// [cutDate, cutDate + <see cref="MaxDaysAfterCutDate"/>] (design doc, "due date within ~60 days
/// after the cut date"). When no cut date is available, the ladder's own instance (constructed
/// with the parameterless constructor) is used unchanged, so the static window remains the
/// fallback — this class is never *looser* than the static window, only ever tighter.
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
    /// Upper bound, in days after the statement's period cut date, for a period-relative
    /// plausible payment due date. Mexican bank card statements ("Fecha límite de pago") place
    /// the due date roughly 20-25 days after the cut date; 60 days is a deliberately generous
    /// ceiling — comfortably covers slower/irregular billing cycles while still being far
    /// tighter than the ~16-year static window, so it rejects a recovered value that landed on
    /// the wrong page/month (e.g. a mis-anchored fuzzy match) without risking a false reject of a
    /// genuine due date.
    /// </summary>
    public const int MaxDaysAfterCutDate = 60;

    private readonly DateOnly _minDate;
    private readonly DateOnly _maxDate;

    /// <summary>
    /// Initializes the validator with the static, document-independent sanity window
    /// [<see cref="MinPlausibleDate"/>, <see cref="MaxPlausibleDate"/>]. This is the fallback used
    /// whenever the statement's period cut date is unavailable.
    /// </summary>
    public PaymentDueDatePlausibilityValidator()
    {
        _minDate = MinPlausibleDate;
        _maxDate = MaxPlausibleDate;
    }

    /// <summary>
    /// Initializes the validator with a period-relative window
    /// [<paramref name="periodCutDate"/>, <paramref name="periodCutDate"/> + <see cref="MaxDaysAfterCutDate"/>
    /// days], tighter than the static window. Intended for use as a per-call
    /// <c>validatorOverride</c> once the statement's own period cut date has been resolved.
    /// </summary>
    /// <param name="periodCutDate">The statement's resolved period cut date.</param>
    public PaymentDueDatePlausibilityValidator(DateOnly periodCutDate)
    {
        _minDate = periodCutDate;
        _maxDate = periodCutDate.AddDays(MaxDaysAfterCutDate);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="date"/> falls within
    /// [<see cref="MinPlausibleDate"/>, <see cref="MaxPlausibleDate"/>] (inclusive) — the static,
    /// document-independent window used by <see cref="FuzzyLabelStage{TValue}"/>'s own gate
    /// regardless of which constructor built a given instance.
    /// </summary>
    /// <param name="date">The candidate due date.</param>
    public static bool IsPlausible(DateOnly date) => date >= MinPlausibleDate && date <= MaxPlausibleDate;

    /// <inheritdoc/>
    /// <remarks>
    /// Uses this instance's own window — the static window by default, or the period-relative
    /// window when this instance was built via the <see cref="PaymentDueDatePlausibilityValidator(DateOnly)"/>
    /// constructor.
    /// </remarks>
    public bool IsValid(object? value) => value is DateOnly date && date >= _minDate && date <= _maxDate;
}
