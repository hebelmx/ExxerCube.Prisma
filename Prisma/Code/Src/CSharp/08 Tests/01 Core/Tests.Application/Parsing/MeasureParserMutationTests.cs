using ExxerCube.Prisma.Application.Parsing;

namespace ExxerCube.Prisma.Tests.Application.Parsing;

/// <summary>
/// Mutation-killing tests for <see cref="MeasureParser"/> (pure static parser).
/// Pins every <see cref="ComplianceActionKind"/> keyword branch and the account-length boundary so
/// string/Contains/relational/ToUpper mutants on the deterministic surface die.
/// </summary>
public class MeasureParserMutationTests
{
    // ---------- ParseActionKind ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseActionKind_NullOrWhitespace_ReturnsUnknown(string? raw) =>
        MeasureParser.ParseActionKind(raw).ShouldBe(ComplianceActionKind.Unknown);

    [Fact]
    public void ParseActionKind_Bloq_ReturnsBlock() =>
        MeasureParser.ParseActionKind("BLOQUEAR CUENTA").ShouldBe(ComplianceActionKind.Block);

    [Fact]
    public void ParseActionKind_Desblo_ReturnsUnblock() =>
        // "DESBLO" (not "DESBLOQUEO" which embeds "BLOQ" and would map to Block first).
        MeasureParser.ParseActionKind("DESBLO ORDEN").ShouldBe(ComplianceActionKind.Unblock);

    [Fact]
    public void ParseActionKind_Liber_ReturnsUnblock() =>
        MeasureParser.ParseActionKind("LIBERAR FONDOS").ShouldBe(ComplianceActionKind.Unblock);

    [Fact]
    public void ParseActionKind_Trans_ReturnsTransfer() =>
        MeasureParser.ParseActionKind("TRANSFERIR SALDO").ShouldBe(ComplianceActionKind.Transfer);

    [Fact]
    public void ParseActionKind_Trasp_ReturnsTransfer() =>
        MeasureParser.ParseActionKind("TRASPASO INTERBANCARIO").ShouldBe(ComplianceActionKind.Transfer);

    [Fact]
    public void ParseActionKind_Info_ReturnsInformation() =>
        MeasureParser.ParseActionKind("SOLICITO INFORMACION").ShouldBe(ComplianceActionKind.Information);

    [Fact]
    public void ParseActionKind_Doc_ReturnsDocument() =>
        MeasureParser.ParseActionKind("ADJUNTAR DOCUMENTO").ShouldBe(ComplianceActionKind.Document);

    [Fact]
    public void ParseActionKind_Ignor_ReturnsIgnore() =>
        MeasureParser.ParseActionKind("IGNORAR ESTE OFICIO").ShouldBe(ComplianceActionKind.Ignore);

    [Fact]
    public void ParseActionKind_Unmatched_ReturnsOther() =>
        MeasureParser.ParseActionKind("CUALQUIER COSA").ShouldBe(ComplianceActionKind.Other);

    [Fact]
    public void ParseActionKind_LowercaseInput_StillMatches_KillsToUpperInvariant() =>
        // If ToUpperInvariant is removed, "bloquear" no longer matches "BLOQ" → would fall to Other.
        MeasureParser.ParseActionKind("bloquear").ShouldBe(ComplianceActionKind.Block);

    // ---------- ParseCuenta ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseCuenta_NullOrWhitespace_ReturnsNull(string? raw) =>
        MeasureParser.ParseCuenta(raw).ShouldBeNull();

    [Fact]
    public void ParseCuenta_BelowSixChars_ReturnsNull() =>
        // 5 chars → length < 6 → null (pins the `< 6` boundary; a `<=` would still null here).
        MeasureParser.ParseCuenta("12345").ShouldBeNull();

    [Fact]
    public void ParseCuenta_ExactlySixChars_ReturnsCuenta()
    {
        // 6 chars → passes the boundary (kills `< 6` → `<= 6`, which would null this).
        var cuenta = MeasureParser.ParseCuenta("123456");

        cuenta.ShouldNotBeNull();
        cuenta!.Numero.ShouldBe("123456");
    }

    [Fact]
    public void ParseCuenta_RemovesSpaces_AndPinsNumero()
    {
        // "12 34 56" → spaces stripped → "123456" (len 6). Kills the Replace(" ", "") removal,
        // which would leave length 8 and Numero "12 34 56".
        var cuenta = MeasureParser.ParseCuenta("12 34 56");

        cuenta.ShouldNotBeNull();
        cuenta!.Numero.ShouldBe("123456");
    }
}
