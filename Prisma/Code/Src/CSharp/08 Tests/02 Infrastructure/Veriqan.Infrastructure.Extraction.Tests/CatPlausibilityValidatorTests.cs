using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Unit tests for the B2 <see cref="CatPlausibilityValidator"/> fraction-magnitude gate.
/// </summary>
public sealed class CatPlausibilityValidatorTests
{
    private readonly CatPlausibilityValidator _validator = new();

    [Theory]
    [InlineData(0.35, true)] // realistic CAT (35%), in fraction space.
    [InlineData(0.0, true)] // lower boundary — inclusive.
    [InlineData(3.0, true)] // upper boundary — inclusive.
    [InlineData(2.5, true)] // higher than a typical Tasa but still inside the looser CAT band.
    public void IsValid_FractionWithinBand_ReturnsTrue(double rawFraction, bool expected)
    {
        var fraction = (decimal)rawFraction;

        _validator.IsValid(fraction).ShouldBe(expected);
    }

    [Fact]
    public void IsValid_PhoneNumberHomonymMagnitude_ReturnsFalse()
    {
        // A mis-anchored positional read landing on an adjacent "CAT:"-prefixed 10-digit phone
        // number parses as an astronomically large fraction — many orders of magnitude past the
        // band.
        var phoneNumberMagnitude = 5512345678m;

        _validator.IsValid(phoneNumberMagnitude).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_NegativeFraction_ReturnsFalse()
    {
        _validator.IsValid(-0.1m).ShouldBeFalse();
    }

    [Theory]
    [InlineData(-0.0001)] // just below the lower boundary.
    [InlineData(3.0001)] // just above the upper boundary.
    public void IsValid_JustOutsideBoundary_ReturnsFalse(double rawFraction)
    {
        var fraction = (decimal)rawFraction;

        _validator.IsValid(fraction).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_NonDecimalOrNull_ReturnsFalse_NeverThrows()
    {
        _validator.IsValid(null).ShouldBeFalse();
        _validator.IsValid("0.35").ShouldBeFalse();
        _validator.IsValid(35).ShouldBeFalse();
        _validator.IsValid(0.35d).ShouldBeFalse(); // boxed double, not decimal.
    }
}
