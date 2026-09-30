using System.Globalization;
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
            Expediente: "A/AS1-1111-222222-AAA",
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
            Expediente: "H/IN1-2222-333333-BBB",
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
        reason.ShouldContain("no usable fields");
    }

    [Fact]
    public void Validate_AllCoreMalformedNoPartes_ReturnsRejectReason()
    {
        // Every core field is present but malformed, and there is no partes/solicitante/cuenta
        // to fall back on — nothing usable survives plausibility gating, so the gate still
        // hard-rejects the whole DTO.
        var dto = new LlmExpedienteDto(
            Expediente: "AB/AS1-1111-222222-AAA",   // boundary-shifted, implausible
            Solicitante: null,
            Monto: "abc",                            // unparseable
            Cuenta: null,
            Rfc: "RFC-INVALIDO",                     // implausible shape
            Curp: "CURP-HALLUCINATED",                // implausible shape
            Partes: null,
            NumeroOficio: "BADSHAPE",                 // implausible
            AutoridadNombre: "XZ");                   // implausible (too short / no space)

        var reason = LlmExtractionGate.Validate(dto);

        reason.ShouldNotBeNull();
        reason!.ShouldContain("no usable fields");
    }

    [Fact]
    public void IsValid_EmptyPartes_AllOtherNull_ReturnsFalse()
    {
        var dto = new LlmExpedienteDto(null, null, null, null, null, null, Partes: []);
        LlmExtractionGate.IsValid(dto).ShouldBeFalse();
    }

    // -----------------------------------------------------------------------
    // Field-level abstention guard — IsPlausibleExpediente
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("A/AS1-2505-088637")]      // missing final letter segment
    [InlineData("123/24")]                 // year too short (legacy ddd/yyyy shape)
    [InlineData("1234567/2024")]           // too many digits
    [InlineData("abc/2024")]               // letters instead of digits
    [InlineData("2024/123")]               // reversed
    [InlineData("123/2024")]               // legacy ddd/yyyy shape
    [InlineData("123456/2025")]            // legacy ddd/yyyy shape
    [InlineData("001/1999")]               // legacy ddd/yyyy shape
    [InlineData("AB/AS1-1111-222222-AAA")] // boundary-shifted (two letters before slash)
    public void IsPlausibleExpediente_MalformedCnbvShape_ReturnsFalse(string expediente)
    {
        LlmExtractionGate.IsPlausibleExpediente(expediente).ShouldBeFalse();
    }

    // -----------------------------------------------------------------------
    // Per-field abstention (narrowed gate) — a malformed Expediente no longer discards a
    // DTO that has other usable signals (e.g. Solicitante). The mapper abstains just that
    // one field; the gate only hard-rejects when NOTHING usable survives.
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_OnlyExpedienteMalformedSiblingsPresent_ReturnsNull()
    {
        var dto = new LlmExpedienteDto(
            Expediente: "A/AS1-2505-088637",  // malformed CNBV format (missing final letter segment)
            Solicitante: "Test",
            Monto: null,
            Cuenta: null,
            Rfc: null,
            Curp: null,
            Partes: null);

        LlmExtractionGate.Validate(dto).ShouldBeNull();
    }

    // -----------------------------------------------------------------------
    // CNBV expediente format (S4-B) — anchored parity with the deterministic extractor
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("A/AS1-1111-222222-AAA")]
    [InlineData("H/IN1-2222-333333-BBB")]
    [InlineData("E/DE-3333-4444444-AAA")]
    [InlineData("A/AS1-4444-5555555-HHHH")]
    public void Validate_CnbvExpediente_Accepted(string expediente)
    {
        var dto = new LlmExpedienteDto(expediente, "Solicitante", null, null, null, null, null);
        LlmExtractionGate.Validate(dto).ShouldBeNull();
    }

    // -----------------------------------------------------------------------
    // Field-level abstention guard — IsPlausibleRfc
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("RFC-INVALIDO")]
    [InlineData("12345")]
    [InlineData("")]
    public void IsPlausibleRfc_MalformedShape_ReturnsFalse(string rfc)
    {
        LlmExtractionGate.IsPlausibleRfc(rfc).ShouldBeFalse();
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
    // Field-level abstention guard — IsPlausibleMonto
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("0")]
    [InlineData("-1000")]
    [InlineData("100000000")]
    [InlineData("999999999.99")]
    [InlineData("abc")]
    [InlineData("INVALID")]
    [InlineData("mil pesos")]
    public void IsPlausibleMonto_OutOfRangeOrUnparseable_ReturnsFalse(string monto)
    {
        LlmExtractionGate.IsPlausibleMonto(monto).ShouldBeFalse();
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

    // -----------------------------------------------------------------------
    // CURP structural validation (adversarial review F1)
    // -----------------------------------------------------------------------

    [Fact]
    public void IsValid_WellFormedCurp_ReturnsTrue()
    {
        var dto = new LlmExpedienteDto("A/AS1-1111-222222-AAA", null, null, null, null, "MAHJ920101HDFRLM09", null);

        LlmExtractionGate.IsValid(dto).ShouldBeTrue();
    }

    [Theory]
    [InlineData("UCM444444ABCDEF")]      // OCR-mangled fragment (too short)
    [InlineData("CURP-HALLUCINATED")]    // hallucinated garbage
    [InlineData("MAHJ920101XDFRLM09")]   // invalid sex char (X)
    public void IsPlausibleCurp_MalformedShape_ReturnsFalse(string badCurp)
    {
        LlmExtractionGate.IsPlausibleCurp(badCurp).ShouldBeFalse();
    }

    [Fact]
    public void Validate_MalformedCurpButPlausibleExpedienteSibling_ReturnsNull()
    {
        // A malformed CURP no longer discards the whole DTO when a plausible Expediente
        // sibling is present — the mapper abstains only the Curp field.
        var dto = new LlmExpedienteDto("A/AS1-1111-222222-AAA", null, null, null, null, "CURP-HALLUCINATED", null);

        LlmExtractionGate.Validate(dto).ShouldBeNull();
    }

    // -----------------------------------------------------------------------
    // TryParseMonto — currency-formatted Monto (bug fix: LLM emits "$9,976,691.72"-style
    // strings; the gate must accept them instead of rejecting the whole DTO).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("$9,976,691.72", 9976691.72)]
    [InlineData("9976691.72", 9976691.72)]
    [InlineData("$236,569.68", 236569.68)]
    [InlineData("1234.56 MXN", 1234.56)]
    [InlineData("236570 MXN", 236570)]
    public void TryParseMonto_CurrencyFormattedValues_AcceptedAndCentsPreserved(string raw, decimal expected)
    {
        var accepted = LlmExtractionGate.TryParseMonto(raw, out var amount);

        accepted.ShouldBeTrue();
        amount.ShouldBe(expected);
    }

    [Theory]
    [InlineData("-100")]
    [InlineData("0")]
    [InlineData("100000000")]
    public void TryParseMonto_OutOfRangeValues_ParsesButIsPlausibleMontoRejectsRange(string raw)
    {
        // TryParseMonto itself only parses — the (0, 100,000,000) exclusive range guard now
        // lives in LlmExtractionGate.IsPlausibleMonto (used by the mapper for per-field
        // abstention), not in Validate (which only hard-rejects when nothing is usable).
        var parsed = LlmExtractionGate.TryParseMonto(raw, out var amount);

        parsed.ShouldBeTrue();
        LlmExtractionGate.IsPlausibleMonto(raw).ShouldBeFalse();
        amount.ShouldBe(decimal.Parse(raw, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TryParseMonto_Invalid_ReturnsFalseAndIsPlausibleMontoRejects()
    {
        LlmExtractionGate.TryParseMonto("INVALID", out var amount).ShouldBeFalse();
        amount.ShouldBe(0m);
        LlmExtractionGate.IsPlausibleMonto("INVALID").ShouldBeFalse();
    }

    [Fact]
    public void TryParseMonto_NullOrWhitespace_ReturnsFalse()
    {
        LlmExtractionGate.TryParseMonto(null, out var amount1).ShouldBeFalse();
        amount1.ShouldBe(0m);

        LlmExtractionGate.TryParseMonto("   ", out var amount2).ShouldBeFalse();
        amount2.ShouldBe(0m);
    }

    [Fact]
    public void Validate_FullDtoWithCurrencyFormattedMonto_PreviouslyRejected_NowAccepted()
    {
        // Regression: this exact shape ("$9,976,691.72") was silently discarding 17/20
        // golden-corpus documents before the TryParseMonto fix — decimal.TryParse with
        // NumberStyles.Number rejects a leading "$".
        var dto = new LlmExpedienteDto(
            Expediente: "A/AS1-1111-222222-AAA",
            Solicitante: "Juan Pérez",
            Monto: "$9,976,691.72",
            Cuenta: null,
            Rfc: null,
            Curp: null,
            Partes: null);

        var valid = LlmExtractionGate.IsValid(dto, out var reason);

        valid.ShouldBeTrue();
        reason.ShouldBeNull();
    }
}
