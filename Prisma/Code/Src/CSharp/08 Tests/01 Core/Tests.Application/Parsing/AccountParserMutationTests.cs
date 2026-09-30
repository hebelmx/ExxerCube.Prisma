using ExxerCube.Prisma.Application.Parsing;

namespace ExxerCube.Prisma.Tests.Application.Parsing;

/// <summary>
/// Mutation-killing tests for <see cref="AccountParser"/> (pure static parser, regex <c>\b\d{6,}\b</c>).
/// </summary>
public class AccountParserMutationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_NullOrWhitespace_ReturnsNull(string? raw) =>
        AccountParser.Parse(raw).ShouldBeNull();

    [Fact]
    public void Parse_NoDigitRun_ReturnsNull() =>
        AccountParser.Parse("sin numeros aqui").ShouldBeNull();

    [Fact]
    public void Parse_FiveDigits_BelowBoundary_ReturnsNull() =>
        // 5 digits fails `\d{6,}` (kills a `{5,}` / `+` widening of the quantifier).
        AccountParser.Parse("abc 12345 def").ShouldBeNull();

    [Fact]
    public void Parse_SixDigits_ReturnsFirstMatch()
    {
        var cuenta = AccountParser.Parse("ref 123456 fin");

        cuenta.ShouldNotBeNull();
        cuenta!.Numero.ShouldBe("123456");
    }

    [Fact]
    public void Parse_EmbeddedLongAccount_ExtractsExactValue()
    {
        var cuenta = AccountParser.Parse("Cuenta no. 0123456789 a nombre de");

        cuenta.ShouldNotBeNull();
        cuenta!.Numero.ShouldBe("0123456789");
    }
}
