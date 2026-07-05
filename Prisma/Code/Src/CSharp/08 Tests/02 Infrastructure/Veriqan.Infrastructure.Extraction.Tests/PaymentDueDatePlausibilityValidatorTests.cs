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
}
