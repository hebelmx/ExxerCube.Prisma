using ExxerCube.Prisma.Infrastructure.Extraction.Txt.Llm;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Txt;

/// <summary>
/// Pure unit tests for <see cref="LlmExpedienteMapper"/>.
/// No I/O; all tests deterministic and fast.
/// </summary>
public sealed class LlmExpedienteMapperTests
{
    // -----------------------------------------------------------------------
    // S4-B — NumeroOficio / AutoridadNombre mapping
    // -----------------------------------------------------------------------

    [Fact]
    public void ToExpediente_SetsNumeroOficioAndAutoridad_WhenPresent()
    {
        var dto = new LlmExpedienteDto(
            Expediente: "A/AS1-1111-222222-AAA",
            Solicitante: "Juan Pérez",
            Monto: null,
            Cuenta: null,
            Rfc: null,
            Curp: null,
            Partes: null,
            NumeroOficio: "AGAFADAFSON2/2025/000084",
            AutoridadNombre: "Comisión Nacional Bancaria y de Valores");

        var expediente = LlmExpedienteMapper.ToExpediente(dto);

        expediente.NumeroOficio.ShouldBe("AGAFADAFSON2/2025/000084");
        expediente.AutoridadNombre.ShouldBe("Comisión Nacional Bancaria y de Valores");
    }

    [Fact]
    public void ToExpediente_OficioNotMatchingShape_LeavesNumeroOficioAbsent()
    {
        // "BADSHAPE" does not match the anchored deterministic oficio regex — abstain
        // (a plausible WRONG value is worse than an abstention).
        var dto = new LlmExpedienteDto(
            Expediente: null,
            Solicitante: null,
            Monto: null,
            Cuenta: null,
            Rfc: null,
            Curp: null,
            Partes: null,
            NumeroOficio: "BADSHAPE",
            AutoridadNombre: null);

        var expediente = LlmExpedienteMapper.ToExpediente(dto);

        expediente.NumeroOficio.ShouldBe(string.Empty);
    }

    [Fact]
    public void ToExpediente_AuthoritySingleToken_LeavesAutoridadNombreAbsent()
    {
        // Single-token garbage ("XZ") must not be trusted as an authority name — abstain.
        var dto = new LlmExpedienteDto(
            Expediente: null,
            Solicitante: null,
            Monto: null,
            Cuenta: null,
            Rfc: null,
            Curp: null,
            Partes: null,
            NumeroOficio: null,
            AutoridadNombre: "XZ");

        var expediente = LlmExpedienteMapper.ToExpediente(dto);

        expediente.AutoridadNombre.ShouldBe(string.Empty);
    }

    [Fact]
    public void ToExpediente_OficioAndAutoridadNull_LeavesBothAbsent()
    {
        var dto = new LlmExpedienteDto(
            Expediente: "A/AS1-1111-222222-AAA",
            Solicitante: "Juan Pérez",
            Monto: null,
            Cuenta: null,
            Rfc: null,
            Curp: null,
            Partes: null);

        var expediente = LlmExpedienteMapper.ToExpediente(dto);

        expediente.NumeroOficio.ShouldBe(string.Empty);
        expediente.AutoridadNombre.ShouldBe(string.Empty);
    }
}
