namespace ExxerCube.Prisma.Tests.Domain.Domain.Entities;

/// <summary>
/// Best-effort validation for identity data (RFC/CURP) should surface warnings without blocking processing.
/// </summary>
public class PersonaSolicitudValidationTests
{
    [Fact]
    public void FlagIdentityIssues_ShouldWarnWhenRfcMissing()
    {
        var persona = new PersonaSolicitud
        {
            Nombre = "Juan",
            Paterno = "Perez",
            Materno = "Lopez",
            Rfc = null,
            Curp = "PEMJ800101HDFLLN04" // valid CURP
        };

        IdentityValidator.FlagIdentityIssues(persona);

        persona.Validation.Warnings.ShouldContain("RFCMissing");
        persona.Validation.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void FlagIdentityIssues_ShouldWarnWhenRfcInvalid()
    {
        var persona = new PersonaSolicitud
        {
            Nombre = "Maria",
            Paterno = "Gomez",
            Rfc = "INVALIDRFC123",
            Curp = "GOHM800101MDFLRR07" // valid pattern
        };

        IdentityValidator.FlagIdentityIssues(persona);

        persona.Validation.Warnings.ShouldContain("RFCInvalid");
        persona.Validation.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void FlagIdentityIssues_ShouldWarnWhenCurpInvalid()
    {
        var persona = new PersonaSolicitud
        {
            Nombre = "Luis",
            Paterno = "Martinez",
            Rfc = "MARL800101XXX",
            Curp = "INVALIDCURP"
        };

        IdentityValidator.FlagIdentityIssues(persona);

        persona.Validation.Warnings.ShouldContain("CURPInvalid");
        persona.Validation.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void FlagIdentityIssues_ShouldStayCleanWhenIdentityIsValid()
    {
        var persona = new PersonaSolicitud
        {
            Nombre = "Ana",
            Paterno = "Lopez",
            Rfc = "LOPA800101ABC",
            Curp = "LOPA800101MDFZRN05"
        };

        IdentityValidator.FlagIdentityIssues(persona);

        persona.Validation.Warnings.ShouldBeEmpty();
        persona.Validation.IsValid.ShouldBeTrue();
    }
}
