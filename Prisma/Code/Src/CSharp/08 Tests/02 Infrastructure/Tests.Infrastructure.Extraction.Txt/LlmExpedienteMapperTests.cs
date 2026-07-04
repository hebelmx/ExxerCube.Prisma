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

    // -----------------------------------------------------------------------
    // Per-field abstention (design-change contract) — a malformed field abstains without
    // discarding usable siblings; abstentions are recorded in AdditionalFields["_AbstainedFields"]
    // and the malformed raw value never appears anywhere in the mapped Expediente.
    // -----------------------------------------------------------------------

    [Fact]
    public void ToExpediente_OnlyExpedienteMalformed_AbstainsExpedienteKeepsSiblings()
    {
        var dto = new LlmExpedienteDto(
            Expediente: "A/AS1-2505-088637",  // malformed CNBV shape
            Solicitante: "Juan Pérez",
            Monto: "5000.00",
            Cuenta: "1234567890",
            Rfc: null,
            Curp: null,
            Partes: null);

        var expediente = LlmExpedienteMapper.ToExpediente(dto);

        expediente.NumeroExpediente.ShouldBe(string.Empty);
        expediente.NombreSolicitante.ShouldBe("Juan Pérez");
        expediente.AdditionalFields["Monto"].ShouldBe("5000.00");
        expediente.AdditionalFields["Cuenta"].ShouldBe("1234567890");
        expediente.AdditionalFields["_AbstainedFields"].ShouldBe("NumeroExpediente");
    }

    [Fact]
    public void ToExpediente_MalformedRfcButOficioAndAuthorityPresent_KeepsOficioAndAuthorityAbstainsRfc()
    {
        var dto = new LlmExpedienteDto(
            Expediente: null,
            Solicitante: null,
            Monto: null,
            Cuenta: null,
            Rfc: "RFC-INVALIDO",
            Curp: null,
            Partes: null,
            NumeroOficio: "AGAFADAFSON2/2025/000084",
            AutoridadNombre: "Comisión Nacional Bancaria y de Valores");

        var expediente = LlmExpedienteMapper.ToExpediente(dto);

        expediente.NumeroOficio.ShouldBe("AGAFADAFSON2/2025/000084");
        expediente.AutoridadNombre.ShouldBe("Comisión Nacional Bancaria y de Valores");
        expediente.AdditionalFields.ContainsKey("Rfc").ShouldBeFalse();
        expediente.AdditionalFields["_AbstainedFields"].ShouldBe("Rfc");
    }

    [Fact]
    public void ToExpediente_AllCoreFieldsMalformedButPartesPresent_KeepsPartesOnlyAbstainsRest()
    {
        var parte = new LlmParteDto("Juan Pérez", null, null, null, null);
        var dto = new LlmExpedienteDto(
            Expediente: "AB/AS1-1111-222222-AAA",  // boundary-shifted, implausible
            Solicitante: null,
            Monto: "abc",                            // unparseable
            Cuenta: null,
            Rfc: "RFC-INVALIDO",
            Curp: "CURP-HALLUCINATED",
            Partes: [parte],
            NumeroOficio: "BADSHAPE",
            AutoridadNombre: "XZ");

        var expediente = LlmExpedienteMapper.ToExpediente(dto);

        expediente.SolicitudPartes.Count.ShouldBe(1);
        expediente.SolicitudPartes[0].Nombre.ShouldBe("Juan Pérez");

        expediente.NumeroExpediente.ShouldBe(string.Empty);
        expediente.NumeroOficio.ShouldBe(string.Empty);
        expediente.AutoridadNombre.ShouldBe(string.Empty);
        expediente.AdditionalFields.ContainsKey("Rfc").ShouldBeFalse();
        expediente.AdditionalFields.ContainsKey("Curp").ShouldBeFalse();
        expediente.AdditionalFields.ContainsKey("Monto").ShouldBeFalse();

        var abstained = expediente.AdditionalFields["_AbstainedFields"].Split(',');
        abstained.ShouldContain("NumeroExpediente");
        abstained.ShouldContain("Rfc");
        abstained.ShouldContain("Curp");
        abstained.ShouldContain("Monto");
        abstained.ShouldContain("NumeroOficio");
        abstained.ShouldContain("AutoridadNombre");
    }

    [Fact]
    public void ToExpediente_MalformedCurp_NeverSurfacesRawCurpValueAnywhere()
    {
        const string maliciousLookingCurp = "CURP-HALLUCINATED-DO-NOT-LEAK";
        var dto = new LlmExpedienteDto(
            Expediente: "A/AS1-1111-222222-AAA",
            Solicitante: "Juan Pérez",
            Monto: null,
            Cuenta: null,
            Rfc: null,
            Curp: maliciousLookingCurp,
            Partes: null);

        var expediente = LlmExpedienteMapper.ToExpediente(dto);

        expediente.AdditionalFields.ContainsKey("Curp").ShouldBeFalse();
        foreach (var kv in expediente.AdditionalFields)
        {
            kv.Value.ShouldNotContain(maliciousLookingCurp);
        }
    }

    [Fact]
    public void ToExpediente_FieldsAbstained_RecordsAbstainedFieldsProvenanceKey()
    {
        var dto = new LlmExpedienteDto(
            Expediente: null,
            Solicitante: "Juan Pérez",
            Monto: "abc",  // unparseable → abstain
            Cuenta: null,
            Rfc: null,
            Curp: null,
            Partes: null);

        var expediente = LlmExpedienteMapper.ToExpediente(dto);

        expediente.AdditionalFields.ContainsKey("_AbstainedFields").ShouldBeTrue();
        expediente.AdditionalFields["_AbstainedFields"].ShouldBe("Monto");
        expediente.AdditionalFields["_ExtractionSource"].ShouldBe("llm-text");
    }
}
