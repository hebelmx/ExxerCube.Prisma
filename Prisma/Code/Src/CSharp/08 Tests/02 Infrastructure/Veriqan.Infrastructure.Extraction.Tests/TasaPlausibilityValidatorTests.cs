using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Unit tests for the B2 <see cref="TasaPlausibilityValidator"/> fraction-magnitude gate.
/// </summary>
public sealed class TasaPlausibilityValidatorTests
{
    private readonly TasaPlausibilityValidator _validator = new();

    [Theory]
    [InlineData(0.2736, true)] // realistic annual rate (27.36%), in fraction space.
    [InlineData(0.0, true)] // lower boundary — inclusive.
    [InlineData(2.0, true)] // upper boundary — inclusive.
    [InlineData(1.5, true)] // unusually high but still inside the generous band.
    public void IsValid_FractionWithinBand_ReturnsTrue(double rawFraction, bool expected)
    {
        var fraction = (decimal)rawFraction;

        _validator.IsValid(fraction).ShouldBe(expected);
    }

    [Fact]
    public void IsValid_DecimalPointDropMisread_ReturnsFalse()
    {
        // 0.2736 (27.36%) misread with the decimal point dropped becomes 27.36 — a "2736%" rate.
        var misread = 27.36m;

        _validator.IsValid(misread).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_NegativeFraction_ReturnsFalse()
    {
        // A sign flip must not be accepted as a plausible rate.
        _validator.IsValid(-0.05m).ShouldBeFalse();
    }

    [Theory]
    [InlineData(-0.0001)] // just below the lower boundary.
    [InlineData(2.0001)] // just above the upper boundary.
    public void IsValid_JustOutsideBoundary_ReturnsFalse(double rawFraction)
    {
        var fraction = (decimal)rawFraction;

        _validator.IsValid(fraction).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_NonDecimalOrNull_ReturnsFalse_NeverThrows()
    {
        _validator.IsValid(null).ShouldBeFalse();
        _validator.IsValid("0.2736").ShouldBeFalse();
        _validator.IsValid(27).ShouldBeFalse();
        _validator.IsValid(0.2736d).ShouldBeFalse(); // boxed double, not decimal.
    }
}
