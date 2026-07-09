using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Unit tests for the E2.2 <see cref="PaymentDueDatePlausibilityValidator"/> sanity-window gate.
/// </summary>
public sealed class PaymentDueDatePlausibilityValidatorTests
{
    private readonly PaymentDueDatePlausibilityValidator _validator = new();

    [Theory]
    [InlineData(2025, 8, 25, true)]
    [InlineData(2020, 1, 1, true)]
    [InlineData(2035, 12, 31, true)]
    [InlineData(2019, 12, 31, false)]
    [InlineData(2036, 1, 1, false)]
    public void IsValid_DateWithinOrOutsideSanityWindow_ReturnsExpected(int year, int month, int day, bool expected)
    {
        var date = new DateOnly(year, month, day);

        _validator.IsValid(date).ShouldBe(expected);
        PaymentDueDatePlausibilityValidator.IsPlausible(date).ShouldBe(expected);
    }

    [Fact]
    public void IsValid_WrongTypeOrNull_ReturnsFalse()
    {
        _validator.IsValid("not a date").ShouldBeFalse();
        _validator.IsValid(null).ShouldBeFalse();
        _validator.IsValid(42).ShouldBeFalse();
    }

    // ── Period-relative window (S3.2) ──────────────────────────────────────

    [Fact]
    public void IsValid_PeriodRelativeConstructor_PlausibleDueDateShortlyAfterCutDate_ReturnsTrue()
    {
        var cutDate = new DateOnly(2025, 3, 15);
        var dueDate = new DateOnly(2025, 4, 5); // 21 days after cut — a realistic due date.
        var periodValidator = new PaymentDueDatePlausibilityValidator(cutDate);

        periodValidator.IsValid(dueDate).ShouldBeTrue();
    }

    [Fact]
    public void IsValid_PeriodRelativeConstructor_DateInStaticWindowButFarPastCutDate_ReturnsFalse()
    {
        var cutDate = new DateOnly(2025, 3, 15);
        var dueDate = new DateOnly(2025, 9, 1); // 170 days after cut — implausible for a due date.
        var periodValidator = new PaymentDueDatePlausibilityValidator(cutDate);

        // The static window alone would accept this date (still confirms it's a real tightening,
        // not a coincidentally-narrower static window).
        PaymentDueDatePlausibilityValidator.IsPlausible(dueDate).ShouldBeTrue();

        periodValidator.IsValid(dueDate).ShouldBeFalse();
    }

    [Theory]
    [InlineData(2025, 3, 15, true)] // exactly the cut date itself — inclusive lower bound.
    [InlineData(2025, 3, 14, false)] // one day before the cut date — outside the window.
    [InlineData(2025, 5, 14, true)] // cutDate + 60 days — inclusive upper bound.
    [InlineData(2025, 5, 15, false)] // cutDate + 61 days — one day past the upper bound.
    public void IsValid_PeriodRelativeConstructor_BoundaryDates_ReturnsExpected(int year, int month, int day, bool expected)
    {
        var cutDate = new DateOnly(2025, 3, 15);
        var date = new DateOnly(year, month, day);
        var periodValidator = new PaymentDueDatePlausibilityValidator(cutDate);

        periodValidator.IsValid(date).ShouldBe(expected);
    }

    [Fact]
    public void IsValid_ParameterlessConstructor_StaticWindowUnchanged_WhenNoPeriodCutDateAvailable()
    {
        // With no period cut date (the parameterless/default constructor), behavior must remain
        // exactly the static [MinPlausibleDate, MaxPlausibleDate] window — unaffected by the new
        // period-relative overload existing.
        var staticValidator = new PaymentDueDatePlausibilityValidator();

        staticValidator.IsValid(new DateOnly(2025, 9, 1)).ShouldBeTrue();
        staticValidator.IsValid(PaymentDueDatePlausibilityValidator.MinPlausibleDate).ShouldBeTrue();
        staticValidator.IsValid(PaymentDueDatePlausibilityValidator.MaxPlausibleDate).ShouldBeTrue();
        staticValidator.IsValid(PaymentDueDatePlausibilityValidator.MinPlausibleDate.AddDays(-1)).ShouldBeFalse();
        staticValidator.IsValid(PaymentDueDatePlausibilityValidator.MaxPlausibleDate.AddDays(1)).ShouldBeFalse();
    }
}
