using ExxerCube.Prisma.Infrastructure.Extraction.Txt.Llm;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction.Txt;

/// <summary>
/// Pure unit tests for <see cref="LlmExtractionGate"/>.
/// No I/O; all tests deterministic and fast.
/// </summary>
public sealed class LlmExtractionGateTests
{
    // -----------------------------------------------------------------------
    // Happy path
    // -----------------------------------------------------------------------

    [Fact]
    public void IsValid_ValidDto_ReturnsTrue()
    {
        var dto = new LlmExpedienteDto(
            Expediente: "123/2024",
            Solicitante: "Juan Pérez",
            Monto: "5000.00",
            Cuenta: null,
            Rfc: null,
            Curp: null,
            Partes: null);

        LlmExtractionGate.IsValid(dto).ShouldBeTrue();
    }

    [Fact]
    public void Validate_ValidDto_ReturnsNull()
    {
        var dto = new LlmExpedienteDto(
            Expediente: "1234/2025",
            Solicitante: null,
            Monto: null,
            Cuenta: null,
            Rfc: null,
            Curp: null,
            Partes: null);

        LlmExtractionGate.Validate(dto).ShouldBeNull();
    }

    // -----------------------------------------------------------------------
    // Null DTO
    // -----------------------------------------------------------------------

    [Fact]
    public void IsValid_NullDto_ReturnsFalse()
    {
        LlmExtractionGate.IsValid(null).ShouldBeFalse();
    }

    [Fact]
    public void Validate_NullDto_ReturnsReason()
    {
        var reason = LlmExtractionGate.Validate(null);
        reason.ShouldNotBeNull();
        reason.ShouldContain("null");
    }

    // -----------------------------------------------------------------------
    // All-null reject
    // -----------------------------------------------------------------------

    [Fact]
    public void IsValid_AllNullFields_ReturnsFalse()
    {
        var dto = new LlmExpedienteDto(
            Expediente: null,
            Solicitante: null,
            Monto: null,
            Cuenta: null,
            Rfc: null,
            Curp: null,
            Partes: null);

        LlmExtractionGate.IsValid(dto).ShouldBeFalse();
    }

    [Fact]
    public void Validate_AllNullFields_ReturnsReason()
    {
        var dto = new LlmExpedienteDto(null, null, null, null, null, null, null);
        var reason = LlmExtractionGate.Validate(dto);
        reason.ShouldNotBeNull();
        reason.ShouldContain("all-null");
    }

    [Fact]
    public void IsValid_EmptyPartes_AllOtherNull_ReturnsFalse()
    {
        var dto = new LlmExpedienteDto(null, null, null, null, null, null, Partes: []);
        LlmExtractionGate.IsValid(dto).ShouldBeFalse();
    }

    // -----------------------------------------------------------------------
    // Bad Expediente format
    // -----------------------------------------------------------------------

    [Fact]
    public void IsValid_BadExpedienteFormat_ReturnsFalse()
    {
        var dto = new LlmExpedienteDto(
            Expediente: "A/AS1-2505-088637",  // old-format, not ddd/yyyy
            Solicitante: "Test",
            Monto: null,
            Cuenta: null,
            Rfc: null,
            Curp: null,
            Partes: null);

        LlmExtractionGate.IsValid(dto).ShouldBeFalse();
    }

    [Theory]
    [InlineData("123/24")]       // year too short
    [InlineData("1234567/2024")] // too many digits
    [InlineData("abc/2024")]     // letters instead of digits
    [InlineData("2024/123")]     // reversed
    public void IsValid_InvalidExpedienteFormats_ReturnsFalse(string expediente)
    {
        var dto = new LlmExpedienteDto(expediente, "Solicitante", null, null, null, null, null);
        LlmExtractionGate.IsValid(dto).ShouldBeFalse();
    }

    [Theory]
    [InlineData("123/2024")]
    [InlineData("123456/2025")]
    [InlineData("001/1999")]
    public void IsValid_ValidExpedienteFormats_ReturnsTrue(string expediente)
    {
        var dto = new LlmExpedienteDto(expediente, null, null, null, null, null, null);
        LlmExtractionGate.IsValid(dto).ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // Bad RFC
    // -----------------------------------------------------------------------

    [Fact]
    public void IsValid_BadRfc_ReturnsFalse()
    {
        var dto = new LlmExpedienteDto(
            Expediente: null,
            Solicitante: "Test",
            Monto: null,
            Cuenta: null,
            Rfc: "RFC-INVALIDO",
            Curp: null,
            Partes: null);

        LlmExtractionGate.IsValid(dto).ShouldBeFalse();
    }

    [Theory]
    [InlineData("XAXX010101000")]  // valid RFC (generic)
    [InlineData("GODE561231GR8")]  // valid RFC
    [InlineData("&ABC123456789")]   // valid symbol start
    public void IsValid_ValidRfc_ReturnsTrue(string rfc)
    {
        var dto = new LlmExpedienteDto(null, "Solicitante", null, null, rfc, null, null);
        LlmExtractionGate.IsValid(dto).ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // Monto out of bounds
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("0")]
    [InlineData("-1000")]
    [InlineData("100000000")]
    [InlineData("999999999.99")]
    public void IsValid_MontoOutOfBounds_ReturnsFalse(string monto)
    {
        var dto = new LlmExpedienteDto(null, "Solicitante", monto, null, null, null, null);
        LlmExtractionGate.IsValid(dto).ShouldBeFalse();
    }

    // -----------------------------------------------------------------------
    // Monto non-numeric
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("abc")]
    [InlineData("$1,234")]
    [InlineData("mil pesos")]
    public void IsValid_MontoNonNumeric_ReturnsFalse(string monto)
    {
        var dto = new LlmExpedienteDto(null, "Solicitante", monto, null, null, null, null);
        LlmExtractionGate.IsValid(dto).ShouldBeFalse();
    }

    [Theory]
    [InlineData("1")]
    [InlineData("5000.50")]
    [InlineData("99999999.99")]
    public void IsValid_ValidMonto_ReturnsTrue(string monto)
    {
        var dto = new LlmExpedienteDto(null, "Solicitante", monto, null, null, null, null);
        LlmExtractionGate.IsValid(dto).ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // IsValid with out-param reason
    // -----------------------------------------------------------------------

    [Fact]
    public void IsValid_OutParam_ValidDto_ReasonIsNull()
    {
        var dto = new LlmExpedienteDto(null, "Solicitante", null, null, null, null, null);
        var valid = LlmExtractionGate.IsValid(dto, out var reason);
        valid.ShouldBeTrue();
        reason.ShouldBeNull();
    }

    [Fact]
    public void IsValid_OutParam_InvalidDto_ReasonIsSet()
    {
        var valid = LlmExtractionGate.IsValid(null, out var reason);
        valid.ShouldBeFalse();
        reason.ShouldNotBeNull();
    }

    // -----------------------------------------------------------------------
    // Partes with content counts as having core field
    // -----------------------------------------------------------------------

    [Fact]
    public void IsValid_NullCoreFieldsButNonEmptyPartes_ReturnsTrue()
    {
        var parte = new LlmParteDto("Juan Pérez", null, null, null, null);
        var dto = new LlmExpedienteDto(null, null, null, null, null, null, [parte]);

        LlmExtractionGate.IsValid(dto).ShouldBeTrue();
    }
}
