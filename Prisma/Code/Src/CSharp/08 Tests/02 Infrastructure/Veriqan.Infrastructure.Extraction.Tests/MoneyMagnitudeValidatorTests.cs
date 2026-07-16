using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// Unit tests for the B2 <see cref="MoneyMagnitudeValidator"/> — the single reusable validator
/// shared by the 11 RESUMEN/NIVEL/DESGLOSE money fields.
/// </summary>
public sealed class MoneyMagnitudeValidatorTests
{
    private readonly MoneyMagnitudeValidator _validator = new();

    [Theory]
    [InlineData(1234.56)] // typical statement line amount.
    [InlineData(0.0)]
    [InlineData(-987.65)] // legitimately signed (e.g. an abono) — must NOT be rejected.
    public void IsValid_InBandAmount_ReturnsTrue(double rawAmount)
    {
        var amount = (decimal)rawAmount;

        _validator.IsValid(amount).ShouldBeTrue();
    }

    [Fact]
    public void IsValid_TwelveDigitConcatenationMisread_ReturnsFalse()
    {
        // A gross digit-concatenation/run-together-fields misread — far beyond any plausible
        // single line item.
        var twelveDigits = 123456789012m;

        _validator.IsValid(twelveDigits).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_NegativeTwelveDigitConcatenationMisread_ReturnsFalse()
    {
        // The magnitude bound must also catch a signed gross misread — Math.Abs is applied first.
        var negativeTwelveDigits = -123456789012m;

        _validator.IsValid(negativeTwelveDigits).ShouldBeFalse();
    }

    [Theory]
    [InlineData(100_000_000.0)] // exactly the default ceiling — inclusive.
    [InlineData(-100_000_000.0)] // exactly the default ceiling, negative — inclusive.
    public void IsValid_ExactlyAtDefaultCeiling_ReturnsTrue(double rawAmount)
    {
        var amount = (decimal)rawAmount;

        _validator.IsValid(amount).ShouldBeTrue();
    }

    [Fact]
    public void IsValid_JustAboveDefaultCeiling_ReturnsFalse()
    {
        var justOver = MoneyMagnitudeValidator.DefaultCeiling + 0.01m;

        _validator.IsValid(justOver).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_CustomCeiling_UsesSuppliedBoundNotDefault()
    {
        var tightValidator = new MoneyMagnitudeValidator(ceiling: 1_000m);

        tightValidator.IsValid(999.99m).ShouldBeTrue();
        tightValidator.IsValid(1_000m).ShouldBeTrue();
        tightValidator.IsValid(1_000.01m).ShouldBeFalse();
        // A value well inside the default ceiling is still rejected by the tighter custom bound.
        tightValidator.IsValid(50_000m).ShouldBeFalse();
    }

    [Fact]
    public void Constructor_NegativeCeiling_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new MoneyMagnitudeValidator(ceiling: -1m));
    }

    [Fact]
    public void IsValid_NonDecimalOrNull_ReturnsFalse_NeverThrows()
    {
        _validator.IsValid(null).ShouldBeFalse();
        _validator.IsValid("1234.56").ShouldBeFalse();
        _validator.IsValid(1234).ShouldBeFalse();
        _validator.IsValid(1234.56d).ShouldBeFalse(); // boxed double, not decimal.
    }
}
