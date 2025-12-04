// <copyright file="FieldPatternValidatorContractTests.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using ExxerCube.Prisma.Domain.Validators;

namespace ExxerCube.Prisma.Tests.Domain.Domain.Validators;

/// <summary>
/// ITDD contract tests for <see cref="FieldPatternValidator"/> static utility class.
/// These tests define the expected behavior for all R29 A-2911 field pattern validations.
/// </summary>
/// <remarks>
/// Contract Test Philosophy:
/// - Tests focus on WHAT patterns should be validated, not HOW validation works internally
/// - All assertions use R29 specification examples
/// - Covers all field types: RFC, CURP, NumeroExpediente, CLABE, dates, amounts
/// - Edge cases: null, empty, whitespace, invalid formats, valid formats
/// </remarks>
public class FieldPatternValidatorContractTests(ITestOutputHelper output)
{
    private readonly ILogger<FieldPatternValidatorContractTests> _logger =
        XUnitLogger.CreateLogger<FieldPatternValidatorContractTests>(output);

    #region RFC Pattern Validation Tests

    [Fact]
    public void IsValidRFC_ValidPersonaFisica_ReturnsTrue()
    {
        _logger.LogInformation("=== TEST START: IsValidRFC_ValidPersonaFisica ===");

        // Arrange - Valid RFC for Persona Física (13 chars)
        var rfc = "LOMH850101ABC";

        _logger.LogInformation("Test data: RFC='{RFC}' (Persona Física format: XXXXAAMMDDABC)", rfc);

        // Act
        var result = FieldPatternValidator.IsValidRFC(rfc);

        // Assert
        _logger.LogInformation("Validation result: {Result} (expected: true)", result);
        result.ShouldBeTrue();

        _logger.LogInformation("=== TEST PASSED ===");
    }

    [Fact]
    public void IsValidRFC_ValidPersonaMoral_ReturnsTrue()
    {
        _logger.LogInformation("=== TEST START: IsValidRFC_ValidPersonaMoral ===");

        // Arrange - Valid RFC for Persona Moral (underscore prefix + 12 chars)
        var rfc = "_ABC850101XY1";

        _logger.LogInformation("Test data: RFC='{RFC}' (Persona Moral format: _XXXAAMMDDXY1)", rfc);

        // Act
        var result = FieldPatternValidator.IsValidRFC(rfc);

        // Assert
        _logger.LogInformation("Validation result: {Result} (expected: true)", result);
        result.ShouldBeTrue();

        _logger.LogInformation("=== TEST PASSED ===");
    }

    [Fact]
    public void IsValidRFC_ValidWith3LetterName_ReturnsTrue()
    {
        // Arrange - Valid RFC with 3-letter name (rare but valid)
        var rfc = "LOM850101ABC";

        _logger.LogInformation("Test data: RFC='{RFC}' (3-letter name format)", rfc);

        // Act
        var result = FieldPatternValidator.IsValidRFC(rfc);

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("INVALID")]
    [InlineData("LOMH85010")]              // Too short
    [InlineData("LOMH850101ABCDEF")]       // Too long
    [InlineData("lomh850101abc")]          // Lowercase (invalid)
    [InlineData("LOMH8501OIABC")]          // Invalid date (month=O1)
    [InlineData("1234567890123")]          // All numbers
    public void IsValidRFC_InvalidFormats_ReturnsFalse(string? invalidRfc)
    {
        _logger.LogInformation("=== TEST: IsValidRFC_InvalidFormats ===");
        _logger.LogInformation("Test data: RFC='{RFC}' (expected: false)", invalidRfc ?? "null");

        // Act
        var result = FieldPatternValidator.IsValidRFC(invalidRfc);

        // Assert
        _logger.LogInformation("Validation result: {Result} (expected: false)", result);
        result.ShouldBeFalse();
    }

    #endregion RFC Pattern Validation Tests

    #region CURP Pattern Validation Tests

    [Fact]
    public void IsValidCURP_ValidMale_ReturnsTrue()
    {
        _logger.LogInformation("=== TEST START: IsValidCURP_ValidMale ===");

        // Arrange - Valid CURP for male (H = Hombre)
        var curp = "LOMH850101HDFLRR01";

        _logger.LogInformation("Test data: CURP='{CURP}' (Male format with H)", curp);

        // Act
        var result = FieldPatternValidator.IsValidCURP(curp);

        // Assert
        _logger.LogInformation("Validation result: {Result} (expected: true)", result);
        result.ShouldBeTrue();

        _logger.LogInformation("=== TEST PASSED ===");
    }

    [Fact]
    public void IsValidCURP_ValidFemale_ReturnsTrue()
    {
        _logger.LogInformation("=== TEST START: IsValidCURP_ValidFemale ===");

        // Arrange - Valid CURP for female (M = Mujer)
        var curp = "LOMA850101MDFLRR01";

        _logger.LogInformation("Test data: CURP='{CURP}' (Female format with M)", curp);

        // Act
        var result = FieldPatternValidator.IsValidCURP(curp);

        // Assert
        _logger.LogInformation("Validation result: {Result} (expected: true)", result);
        result.ShouldBeTrue();

        _logger.LogInformation("=== TEST PASSED ===");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("LOMH850101HDFLRR")]        // Too short
    [InlineData("LOMH850101HDFLRR012")]     // Too long
    [InlineData("lomh850101hdflrr01")]      // Lowercase
    [InlineData("LOMH850101XDFLRR01")]      // Invalid gender (X)
    [InlineData("12345678901234567")]       // All numbers
    public void IsValidCURP_InvalidFormats_ReturnsFalse(string? invalidCurp)
    {
        _logger.LogInformation("=== TEST: IsValidCURP_InvalidFormats ===");
        _logger.LogInformation("Test data: CURP='{CURP}' (expected: false)", invalidCurp ?? "null");

        // Act
        var result = FieldPatternValidator.IsValidCURP(invalidCurp);

        // Assert
        _logger.LogInformation("Validation result: {Result} (expected: false)", result);
        result.ShouldBeFalse();
    }

    #endregion CURP Pattern Validation Tests

    #region NumeroExpediente Pattern Validation Tests

    [Fact]
    public void IsValidNumeroExpediente_ValidAseguramiento_ReturnsTrue()
    {
        _logger.LogInformation("=== TEST START: IsValidNumeroExpediente_ValidAseguramiento ===");

        // Arrange - Valid Numero Expediente (Aseguramiento format)
        var numeroExpediente = "A/AS1-1111-222222-AAA";

        _logger.LogInformation("Test data: NumeroExpediente='{Num}' (Format: A/AS1-1111-222222-AAA)", numeroExpediente);

        // Act
        var result = FieldPatternValidator.IsValidNumeroExpediente(numeroExpediente);

        // Assert
        _logger.LogInformation("Validation result: {Result} (expected: true)", result);
        result.ShouldBeTrue();

        _logger.LogInformation("=== TEST PASSED ===");
    }

    [Fact]
    public void IsValidNumeroExpediente_ValidHacendario_ReturnsTrue()
    {
        // Arrange - Valid Numero Expediente (Hacendario format)
        var numeroExpediente = "H/H123-456789-PENAL";

        _logger.LogInformation("Test data: NumeroExpediente='{Num}'", numeroExpediente);

        // Act
        var result = FieldPatternValidator.IsValidNumeroExpediente(numeroExpediente);

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("INVALID")]
    [InlineData("A-AS1-1111-222222-AAA")]   // Missing slash
    [InlineData("A/AS1/1111/222222/AAA")]   // Slashes instead of hyphens
    [InlineData("a/as1-1111-222222-aaa")]   // Lowercase
    [InlineData("123/AS1-1111-222222-AAA")] // Number prefix
    public void IsValidNumeroExpediente_InvalidFormats_ReturnsFalse(string? invalidNum)
    {
        _logger.LogInformation("=== TEST: IsValidNumeroExpediente_InvalidFormats ===");
        _logger.LogInformation("Test data: NumeroExpediente='{Num}' (expected: false)", invalidNum ?? "null");

        // Act
        var result = FieldPatternValidator.IsValidNumeroExpediente(invalidNum);

        // Assert
        _logger.LogInformation("Validation result: {Result} (expected: false)", result);
        result.ShouldBeFalse();
    }

    #endregion NumeroExpediente Pattern Validation Tests

    #region CLABE Pattern Validation Tests

    [Fact]
    public void IsValidCLABE_Valid18Digits_ReturnsTrue()
    {
        _logger.LogInformation("=== TEST START: IsValidCLABE_Valid18Digits ===");

        // Arrange - Valid CLABE (18 digits)
        var clabe = "012345678901234567";

        _logger.LogInformation("Test data: CLABE='{CLABE}' (18 digits)", clabe);

        // Act
        var result = FieldPatternValidator.IsValidCLABE(clabe);

        // Assert
        _logger.LogInformation("Validation result: {Result} (expected: true)", result);
        result.ShouldBeTrue();

        _logger.LogInformation("=== TEST PASSED ===");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345678901234567")]       // 17 digits (too short)
    [InlineData("1234567890123456789")]     // 19 digits (too long)
    [InlineData("01234567890123456A")]      // Contains letter
    [InlineData("012 345 678 901 234 567")] // Contains spaces
    [InlineData("012-345-678-901-234-567")] // Contains hyphens
    public void IsValidCLABE_InvalidFormats_ReturnsFalse(string? invalidClabe)
    {
        _logger.LogInformation("=== TEST: IsValidCLABE_InvalidFormats ===");
        _logger.LogInformation("Test data: CLABE='{CLABE}' (expected: false)", invalidClabe ?? "null");

        // Act
        var result = FieldPatternValidator.IsValidCLABE(invalidClabe);

        // Assert
        _logger.LogInformation("Validation result: {Result} (expected: false)", result);
        result.ShouldBeFalse();
    }

    #endregion CLABE Pattern Validation Tests

    #region Date Pattern Validation Tests

    [Fact]
    public void IsValidDate_ValidYYYYMMDD_ReturnsTrue()
    {
        _logger.LogInformation("=== TEST START: IsValidDate_ValidYYYYMMDD ===");

        // Arrange - Valid date in YYYYMMDD format
        var date = "20250101";

        _logger.LogInformation("Test data: Date='{Date}' (Format: YYYYMMDD = 2025-01-01)", date);

        // Act
        var result = FieldPatternValidator.IsValidDate(date);

        // Assert
        _logger.LogInformation("Validation result: {Result} (expected: true)", result);
        result.ShouldBeTrue();

        _logger.LogInformation("=== TEST PASSED ===");
    }

    [Fact]
    public void IsValidDate_ValidLeapYear_ReturnsTrue()
    {
        // Arrange - Valid leap year date (Feb 29)
        var date = "20240229";

        _logger.LogInformation("Test data: Date='{Date}' (Leap year: 2024-02-29)", date);

        // Act
        var result = FieldPatternValidator.IsValidDate(date);

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2025-01-01")]              // Hyphens (invalid format)
    [InlineData("01/01/2025")]              // Slashes (invalid format)
    [InlineData("20250132")]                // Invalid day (32)
    [InlineData("20251301")]                // Invalid month (13)
    [InlineData("20230229")]                // Invalid leap year (2023 not leap)
    [InlineData("2025010")]                 // Too short (7 digits)
    [InlineData("202501011")]               // Too long (9 digits)
    [InlineData("ABCD0101")]                // Contains letters
    public void IsValidDate_InvalidFormats_ReturnsFalse(string? invalidDate)
    {
        _logger.LogInformation("=== TEST: IsValidDate_InvalidFormats ===");
        _logger.LogInformation("Test data: Date='{Date}' (expected: false)", invalidDate ?? "null");

        // Act
        var result = FieldPatternValidator.IsValidDate(invalidDate);

        // Assert
        _logger.LogInformation("Validation result: {Result} (expected: false)", result);
        result.ShouldBeFalse();
    }

    #endregion Date Pattern Validation Tests

    #region Monto Pattern Validation Tests

    [Fact]
    public void IsValidMonto_ValidInteger_ReturnsTrue()
    {
        _logger.LogInformation("=== TEST START: IsValidMonto_ValidInteger ===");

        // Arrange - Valid amount (R29 requirement: no decimals, no commas)
        var monto = "236570";

        _logger.LogInformation("Test data: Monto='{Monto}' (Integer, no decimals, no commas)", monto);

        // Act
        var result = FieldPatternValidator.IsValidMonto(monto);

        // Assert
        _logger.LogInformation("Validation result: {Result} (expected: true)", result);
        result.ShouldBeTrue();

        _logger.LogInformation("=== TEST PASSED ===");
    }

    [Fact]
    public void IsValidMonto_ValidZero_ReturnsTrue()
    {
        // Arrange - Zero amount (valid for "toda la cuenta")
        var monto = "0";

        _logger.LogInformation("Test data: Monto='{Monto}' (Zero = toda la cuenta)", monto);

        // Act
        var result = FieldPatternValidator.IsValidMonto(monto);

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("236,570")]                 // Contains comma (invalid for R29)
    [InlineData("236570.50")]               // Contains decimal (invalid for R29)
    [InlineData("-236570")]                 // Negative (invalid)
    [InlineData("$236570")]                 // Contains currency symbol
    [InlineData("236570 MXN")]              // Contains currency code
    [InlineData("ABC")]                     // Non-numeric
    [InlineData("236.570,50")]              // European format
    public void IsValidMonto_InvalidFormats_ReturnsFalse(string? invalidMonto)
    {
        _logger.LogInformation("=== TEST: IsValidMonto_InvalidFormats ===");
        _logger.LogInformation("Test data: Monto='{Monto}' (expected: false)", invalidMonto ?? "null");

        // Act
        var result = FieldPatternValidator.IsValidMonto(invalidMonto);

        // Assert
        _logger.LogInformation("Validation result: {Result} (expected: false)", result);
        result.ShouldBeFalse();
    }

    #endregion Monto Pattern Validation Tests

    #region NumeroOficio Pattern Validation Tests

    [Fact]
    public void IsValidNumeroOficio_ValidUnder30Chars_ReturnsTrue()
    {
        _logger.LogInformation("=== TEST START: IsValidNumeroOficio_ValidUnder30Chars ===");

        // Arrange - Valid NumeroOficio (<= 30 chars)
        var numeroOficio = "123/ABC/-4444444444/2025";

        _logger.LogInformation("Test data: NumeroOficio='{Num}' (Length: {Len} <= 30)",
            numeroOficio, numeroOficio.Length);

        // Act
        var result = FieldPatternValidator.IsValidNumeroOficio(numeroOficio);

        // Assert
        _logger.LogInformation("Validation result: {Result} (expected: true)", result);
        result.ShouldBeTrue();

        _logger.LogInformation("=== TEST PASSED ===");
    }

    [Fact]
    public void IsValidNumeroOficio_ValidExactly30Chars_ReturnsTrue()
    {
        // Arrange - Exactly 30 characters (boundary test)
        var numeroOficio = "123456789012345678901234567890"; // 30 chars

        _logger.LogInformation("Test data: NumeroOficio length={Len} (boundary: exactly 30)", numeroOficio.Length);

        // Act
        var result = FieldPatternValidator.IsValidNumeroOficio(numeroOficio);

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234567890123456789012345678901")] // 31 chars (too long)
    public void IsValidNumeroOficio_InvalidFormats_ReturnsFalse(string? invalidNum)
    {
        _logger.LogInformation("=== TEST: IsValidNumeroOficio_InvalidFormats ===");
        _logger.LogInformation("Test data: NumeroOficio='{Num}' length={Len} (expected: false)",
            invalidNum ?? "null", invalidNum?.Length ?? 0);

        // Act
        var result = FieldPatternValidator.IsValidNumeroOficio(invalidNum);

        // Assert
        _logger.LogInformation("Validation result: {Result} (expected: false)", result);
        result.ShouldBeFalse();
    }

    #endregion NumeroOficio Pattern Validation Tests

    #region TextField Pattern Validation Tests

    [Fact]
    public void IsValidTextField_ValidWithinMaxLength_ReturnsTrue()
    {
        _logger.LogInformation("=== TEST START: IsValidTextField_ValidWithinMaxLength ===");

        // Arrange
        var textField = "SUBDELEGACION 8 SAN ANGEL";
        var maxLength = 100;

        _logger.LogInformation("Test data: TextField='{Text}' (Length: {Len} <= {Max})",
            textField, textField.Length, maxLength);

        // Act
        var result = FieldPatternValidator.IsValidTextField(textField, maxLength);

        // Assert
        _logger.LogInformation("Validation result: {Result} (expected: true)", result);
        result.ShouldBeTrue();

        _logger.LogInformation("=== TEST PASSED ===");
    }

    [Theory]
    [InlineData(null, 100)]
    [InlineData("", 100)]
    [InlineData("   ", 100)]
    [InlineData("This text is way too long for the field", 10)] // Exceeds max
    public void IsValidTextField_InvalidFormats_ReturnsFalse(string? invalidText, int maxLength)
    {
        _logger.LogInformation("=== TEST: IsValidTextField_InvalidFormats ===");
        _logger.LogInformation("Test data: TextField='{Text}' length={Len}, maxLength={Max} (expected: false)",
            invalidText ?? "null", invalidText?.Length ?? 0, maxLength);

        // Act
        var result = FieldPatternValidator.IsValidTextField(invalidText, maxLength);

        // Assert
        _logger.LogInformation("Validation result: {Result} (expected: false)", result);
        result.ShouldBeFalse();
    }

    #endregion TextField Pattern Validation Tests
}
