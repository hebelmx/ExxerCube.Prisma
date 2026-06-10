using ExxerCube.Prisma.Application.Mapping;

namespace ExxerCube.Prisma.Tests.Application.Mapping;

/// <summary>
/// Mutation-killing tests for <see cref="LegalSubdivisionMapper"/> (pure static mapper).
/// Pins every numeric area-code switch arm and every text-based fallback branch.
/// </summary>
public class LegalSubdivisionMapperMutationTests
{
    // ---------- Numeric area-code switch (codes take precedence) ----------

    [Theory]
    [InlineData(1, "A_AS")]
    [InlineData(2, "A_DE")]
    [InlineData(3, "A_TF")]
    [InlineData(4, "A_IN")]
    [InlineData(5, "J_AS")]
    [InlineData(6, "J_DE")]
    [InlineData(7, "J_IN")]
    [InlineData(8, "H_IN")]
    [InlineData(9, "E_AS")]
    [InlineData(10, "E_DE")]
    [InlineData(11, "E_IN")]
    public void FromArea_KnownCode_MapsToExactKind(int areaClave, string expectedName)
    {
        // A description that would otherwise map elsewhere — proves the numeric code wins.
        var result = LegalSubdivisionMapper.FromArea(areaClave, "TRANSFERENCIA");

        result.Name.ShouldBe(expectedName);
    }

    // ---------- Text fallback (unknown code → MapByText) ----------

    [Fact]
    public void FromArea_UnknownCodeBlankDescription_ReturnsUnknown() =>
        LegalSubdivisionMapper.FromArea(0, "   ").ShouldBe(LegalSubdivisionKind.Unknown);

    [Fact]
    public void FromArea_UnknownCodeNullDescription_ReturnsUnknown() =>
        LegalSubdivisionMapper.FromArea(0, null).ShouldBe(LegalSubdivisionKind.Unknown);

    [Fact]
    public void FromArea_UnrecognizedDescription_ReturnsUnknown() =>
        LegalSubdivisionMapper.FromArea(99, "ALGO IRRELEVANTE").ShouldBe(LegalSubdivisionKind.Unknown);

    // Aseguramiento family
    [Fact]
    public void FromArea_AseguramientoPlain_ReturnsA_AS() =>
        LegalSubdivisionMapper.FromArea(0, "ASEGURAMIENTO").ShouldBe(LegalSubdivisionKind.A_AS);

    [Fact]
    public void FromArea_AseguramientoJudicial_ReturnsJ_AS() =>
        LegalSubdivisionMapper.FromArea(0, "ASEGURAMIENTO JUDICIAL").ShouldBe(LegalSubdivisionKind.J_AS);

    [Fact]
    public void FromArea_AseguramientoJSlash_ReturnsJ_AS() =>
        // "J/" alternative without the word "JUD".
        LegalSubdivisionMapper.FromArea(0, "ASEGURAMIENTO J/ALGO").ShouldBe(LegalSubdivisionKind.J_AS);

    [Fact]
    public void FromArea_AseguramientoIlicito_ReturnsE_AS() =>
        LegalSubdivisionMapper.FromArea(0, "ASEGURAMIENTO ILICITO").ShouldBe(LegalSubdivisionKind.E_AS);

    [Fact]
    public void FromArea_AseguramientoESlash_ReturnsE_AS() =>
        LegalSubdivisionMapper.FromArea(0, "ASEGURAMIENTO E/ALGO").ShouldBe(LegalSubdivisionKind.E_AS);

    // Desembargo family
    [Fact]
    public void FromArea_DesembargoPlain_ReturnsA_DE() =>
        LegalSubdivisionMapper.FromArea(0, "DESEMBARGO").ShouldBe(LegalSubdivisionKind.A_DE);

    [Fact]
    public void FromArea_DesembargoJudicial_ReturnsJ_DE() =>
        LegalSubdivisionMapper.FromArea(0, "DESEMBARGO JUDICIAL").ShouldBe(LegalSubdivisionKind.J_DE);

    [Fact]
    public void FromArea_DesembargoIlicito_ReturnsE_DE() =>
        LegalSubdivisionMapper.FromArea(0, "DESEMBARGO ILICITO").ShouldBe(LegalSubdivisionKind.E_DE);

    // Transfer
    [Fact]
    public void FromArea_Transferencia_ReturnsA_TF() =>
        LegalSubdivisionMapper.FromArea(0, "TRANSFERENCIA DE FONDOS").ShouldBe(LegalSubdivisionKind.A_TF);

    // Informativo / documentación family
    [Fact]
    public void FromArea_InformacionPlain_ReturnsA_IN() =>
        LegalSubdivisionMapper.FromArea(0, "INFORMACION GENERAL").ShouldBe(LegalSubdivisionKind.A_IN);

    [Fact]
    public void FromArea_DocumentacionPlain_ReturnsA_IN() =>
        // "DOCUMENT" alternative of the same branch.
        LegalSubdivisionMapper.FromArea(0, "DOCUMENTACION REQUERIDA").ShouldBe(LegalSubdivisionKind.A_IN);

    [Fact]
    public void FromArea_InformacionJudicial_ReturnsJ_IN() =>
        LegalSubdivisionMapper.FromArea(0, "INFORMACION JUDICIAL").ShouldBe(LegalSubdivisionKind.J_IN);

    [Fact]
    public void FromArea_InformacionHacendario_ReturnsH_IN() =>
        // "HAC" alternative, and proves JUD-before-HAC ordering (no JUD here).
        LegalSubdivisionMapper.FromArea(0, "INFORMACION HACENDARIO").ShouldBe(LegalSubdivisionKind.H_IN);

    [Fact]
    public void FromArea_InformacionHSlash_ReturnsH_IN() =>
        LegalSubdivisionMapper.FromArea(0, "INFORMACION H/ALGO").ShouldBe(LegalSubdivisionKind.H_IN);

    [Fact]
    public void FromArea_InformacionIlicito_ReturnsE_IN() =>
        // ILICIT after JUD and HAC in the ladder; isolate it.
        LegalSubdivisionMapper.FromArea(0, "INFORMACION ILICITO").ShouldBe(LegalSubdivisionKind.E_IN);
}
