using ExxerCube.Prisma.Application.Parsing;

namespace ExxerCube.Prisma.Tests.Application.Parsing;

/// <summary>
/// Mutation-killing tests for <see cref="IdentityParser"/> (pure static parser: RFC variants + CURP).
/// Pins the uppercasing, distinct-dedup, and match/no-match branches.
/// </summary>
public class IdentityParserMutationTests
{
    // ---------- ParseRfcVariants ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseRfcVariants_NullOrWhitespace_ReturnsEmpty(string? raw) =>
        IdentityParser.ParseRfcVariants(raw).ShouldBeEmpty();

    [Fact]
    public void ParseRfcVariants_NoRfc_ReturnsEmpty() =>
        IdentityParser.ParseRfcVariants("texto sin rfc valido 123").ShouldBeEmpty();

    [Fact]
    public void ParseRfcVariants_TwoDistinctRfcs_ReturnsBothUppercased()
    {
        var result = IdentityParser.ParseRfcVariants("RFC AAA010101AA1 y BBBB020202BB2 fin").ToList();

        result.Count.ShouldBe(2);
        result.ShouldContain("AAA010101AA1");
        result.ShouldContain("BBBB020202BB2");
    }

    [Fact]
    public void ParseRfcVariants_LowercaseInput_IsUppercased()
    {
        var result = IdentityParser.ParseRfcVariants("rfc aaa010101aa1 aqui").ToList();

        result.ShouldHaveSingleItem();
        // Kills removal of ToUpperInvariant in the Select.
        result[0].ShouldBe("AAA010101AA1");
    }

    [Fact]
    public void ParseRfcVariants_SameRfcDifferentCase_IsDeduplicated()
    {
        // Distinct(OrdinalIgnoreCase) collapses the two to one entry.
        var result = IdentityParser.ParseRfcVariants("AAA010101AA1 aaa010101aa1").ToList();

        result.ShouldHaveSingleItem();
        result[0].ShouldBe("AAA010101AA1");
    }

    // ---------- ParseCurp ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseCurp_NullOrWhitespace_ReturnsNull(string? raw) =>
        IdentityParser.ParseCurp(raw).ShouldBeNull();

    [Fact]
    public void ParseCurp_NoCurp_ReturnsNull() =>
        IdentityParser.ParseCurp("solo texto sin curp").ShouldBeNull();

    [Fact]
    public void ParseCurp_ValidCurp_ReturnsUppercased()
    {
        var result = IdentityParser.ParseCurp("CURP del solicitante: BADD110313HCMLNS09 vigente");

        // Kills the `match.Success ? ... : null` branch and the ToUpperInvariant.
        result.ShouldBe("BADD110313HCMLNS09");
    }

    [Fact]
    public void ParseCurp_LowercaseValidCurp_IsUppercased() =>
        IdentityParser.ParseCurp("badd110313hcmlns09").ShouldBe("BADD110313HCMLNS09");
}
