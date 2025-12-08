// <copyright file="FieldSanitizerContractTests.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using ExxerCube.Prisma.Domain.Sanitizers;

namespace ExxerCube.Prisma.Tests.Domain.Domain.Sanitizers;

/// <summary>
/// ITDD contract tests for <see cref="FieldSanitizer"/> static utility class.
/// These tests define the expected behavior for cleaning XML data quality issues.
/// </summary>
/// <remarks>
/// <para><strong>Contract Test Philosophy:</strong></para>
/// <list type="bullet">
/// <item>Tests focus on WHAT data quality issues should be cleaned, not HOW sanitization works</item>
/// <item>All assertions use real-world examples from XML analysis (original spec)</item>
/// <item>Covers all 9 data quality issues: whitespace, HTML entities, human annotations, typos, etc.</item>
/// <item>NEVER CRASH - Sanitizer must handle ANY input gracefully (null, empty, malformed)</item>
/// </list>
/// <para><strong>Reality of Chaos:</strong></para>
/// <para>
/// The law (R29 A-2911) says "no nulls allowed", but reality is chaotic:
/// - 14 fields don't even come in XML currently
/// - Human annotations everywhere ("NO SE CUENTA", "el monto mencionado en el texto")
/// - HTML entities (&amp;nbsp;) instead of null
/// - Typos and uncontrolled vocabularies
/// - Trailing whitespace, line breaks, collapsed text
/// </para>
/// <para><strong>Sanitizer Contract:</strong></para>
/// <para>
/// Given ANY input (null, garbage, malformed), sanitizer:
/// 1. NEVER throws exceptions
/// 2. Returns null for unparseable data
/// 3. Returns cleaned string for valid data
/// 4. Handles all edge cases gracefully
/// </para>
/// </remarks>
public class FieldSanitizerContractTests(ITestOutputHelper output)
{
    private readonly ILogger<FieldSanitizerContractTests> _logger =
        XUnitLogger.CreateLogger<FieldSanitizerContractTests>(output);

    #region Data Quality Issue 1: Trailing Whitespace

    [Fact]
    public void Sanitize_TrailingWhitespace_RemovesWhitespace()
    {
        _logger.LogInformation("=== TEST START: Sanitize_TrailingWhitespace ===");

        // Arrange - Real XML data quality issue (NumeroOficio from spec)
        var dirtyValue = "123/ABC/-4444444444/2025   ";

        _logger.LogInformation("Test data: Value='{Value}' (trailing spaces)", dirtyValue);

        // Act
        var result = FieldSanitizer.Sanitize(dirtyValue);

        // Assert
        _logger.LogInformation("Sanitized result: '{Result}' (expected: no trailing spaces)", result);
        result.ShouldBe("123/ABC/-4444444444/2025");
        result!.ShouldNotContain("   "); // No trailing whitespace

        _logger.LogInformation("=== TEST PASSED ===");
    }

    [Fact]
    public void Sanitize_LeadingWhitespace_RemovesWhitespace()
    {
        // Arrange
        var dirtyValue = "   A/AS1-1111-222222-AAA";

        _logger.LogInformation("Test data: Value='{Value}' (leading spaces)", dirtyValue);

        // Act
        var result = FieldSanitizer.Sanitize(dirtyValue);

        // Assert
        result.ShouldBe("A/AS1-1111-222222-AAA");
    }

    #endregion Data Quality Issue 1: Trailing Whitespace

    #region Data Quality Issue 2: HTML Entities (nbsp)

    [Fact]
    public void Sanitize_HtmlEntityNbsp_RemovesEntity()
    {
        _logger.LogInformation("=== TEST START: Sanitize_HtmlEntityNbsp ===");

        // Arrange - Real XML data quality issue: &nbsp; instead of null
        var dirtyValue = "&nbsp;&nbsp;&nbsp;";

        _logger.LogInformation("Test data: Value='{Value}' (HTML entities)", dirtyValue);

        // Act
        var result = FieldSanitizer.Sanitize(dirtyValue);

        // Assert - &nbsp; should be cleaned to null (no value)
        _logger.LogInformation("Sanitized result: '{Result}' (expected: null)", result ?? "null");
        result.ShouldBeNull();

        _logger.LogInformation("=== TEST PASSED ===");
    }

    [Fact]
    public void Sanitize_HtmlEntityAmpNbsp_RemovesEntity()
    {
        // Arrange - Double-encoded HTML entity: &amp;nbsp;
        var dirtyValue = "&amp;nbsp;&amp;nbsp;";

        _logger.LogInformation("Test data: Value='{Value}' (double-encoded entities)", dirtyValue);

        // Act
        var result = FieldSanitizer.Sanitize(dirtyValue);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Sanitize_HtmlEntityInText_RemovesEntityKeepsText()
    {
        // Arrange - HTML entity mixed with real text
        var dirtyValue = "SUBDELEGACION&nbsp;8&nbsp;SAN&nbsp;ANGEL";

        _logger.LogInformation("Test data: Value='{Value}' (entities in text)", dirtyValue);

        // Act
        var result = FieldSanitizer.Sanitize(dirtyValue);

        // Assert - Entities removed, spaces collapsed
        result.ShouldBe("SUBDELEGACION 8 SAN ANGEL");
    }

    #endregion Data Quality Issue 2: HTML Entities (nbsp)

    #region Data Quality Issue 3: Human Annotations

    [Theory]
    [InlineData("NO SE CUENTA")]
    [InlineData("el monto mencionado en el texto")]
    [InlineData("Se trata de la misma persona con variante en el RFC")]
    [InlineData("NO APLICA")]
    [InlineData("N/A")]
    [InlineData("no se cuenta")] // Lowercase variation
    [InlineData("NA")] // Short form
    public void Sanitize_HumanAnnotation_ReturnsNull(string annotation)
    {
        _logger.LogInformation("=== TEST: Sanitize_HumanAnnotation ===");
        _logger.LogInformation("Test data: Annotation='{Annotation}'", annotation);

        // Act
        var result = FieldSanitizer.Sanitize(annotation);

        // Assert - Human annotations should be treated as null
        _logger.LogInformation("Sanitized result: '{Result}' (expected: null)", result ?? "null");
        result.ShouldBeNull();
    }

    #endregion Data Quality Issue 3: Human Annotations

    #region Data Quality Issue 4: Line Breaks

    [Fact]
    public void Sanitize_LineBreaksCRLF_ReplacesWithSpace()
    {
        _logger.LogInformation("=== TEST START: Sanitize_LineBreaksCRLF ===");

        // Arrange - Real XML data quality issue: AutoridadNombre with line breaks
        var dirtyValue = "SUBDELEGACION 8\r\nSAN ANGEL";

        _logger.LogInformation("Test data: Value with CRLF line break");

        // Act
        var result = FieldSanitizer.Sanitize(dirtyValue);

        // Assert - Line breaks replaced with single space
        _logger.LogInformation("Sanitized result: '{Result}'", result);
        result.ShouldBe("SUBDELEGACION 8 SAN ANGEL");
        result!.ShouldNotContain("\r\n");

        _logger.LogInformation("=== TEST PASSED ===");
    }

    [Fact]
    public void Sanitize_LineBreaksLF_ReplacesWithSpace()
    {
        // Arrange - Unix-style line break
        var dirtyValue = "SUBDELEGACION 8\nSAN ANGEL";

        // Act
        var result = FieldSanitizer.Sanitize(dirtyValue);

        // Assert
        result.ShouldBe("SUBDELEGACION 8 SAN ANGEL");
        result!.ShouldNotContain("\n");
    }

    [Fact]
    public void Sanitize_LineBreaksCR_ReplacesWithSpace()
    {
        // Arrange - Old Mac-style line break
        var dirtyValue = "SUBDELEGACION 8\rSAN ANGEL";

        // Act
        var result = FieldSanitizer.Sanitize(dirtyValue);

        // Assert
        result.ShouldBe("SUBDELEGACION 8 SAN ANGEL");
        result!.ShouldNotContain("\r");
    }

    #endregion Data Quality Issue 4: Line Breaks

    #region Data Quality Issue 5: Multiple Spaces

    [Fact]
    public void Sanitize_MultipleSpaces_CollapsesToSingleSpace()
    {
        _logger.LogInformation("=== TEST START: Sanitize_MultipleSpaces ===");

        // Arrange - Multiple consecutive spaces
        var dirtyValue = "SUBDELEGACION    8    SAN    ANGEL";

        _logger.LogInformation("Test data: Value='{Value}' (multiple spaces)", dirtyValue);

        // Act
        var result = FieldSanitizer.Sanitize(dirtyValue);

        // Assert - Collapsed to single spaces
        _logger.LogInformation("Sanitized result: '{Result}'", result);
        result.ShouldBe("SUBDELEGACION 8 SAN ANGEL");
        result!.ShouldNotContain("  "); // No double spaces

        _logger.LogInformation("=== TEST PASSED ===");
    }

    #endregion Data Quality Issue 5: Multiple Spaces

    #region Data Quality Issue 6: All Spaces or Underscores

    [Theory]
    [InlineData("             ")] // 13 spaces (RFC field from spec)
    [InlineData("_____________")] // 13 underscores
    [InlineData("_ _ _ _ _")]     // Spaces and underscores
    [InlineData("   ___   ")]     // Mixed with whitespace
    public void Sanitize_AllSpacesOrUnderscores_ReturnsNull(string emptyField)
    {
        _logger.LogInformation("=== TEST: Sanitize_AllSpacesOrUnderscores ===");
        _logger.LogInformation("Test data: Field='{Field}' length={Len}", emptyField, emptyField.Length);

        // Act
        var result = FieldSanitizer.Sanitize(emptyField);

        // Assert - All spaces/underscores should be null
        _logger.LogInformation("Sanitized result: '{Result}' (expected: null)", result ?? "null");
        result.ShouldBeNull();
    }

    #endregion Data Quality Issue 6: All Spaces or Underscores

    #region Null/Empty Handling (NEVER CRASH)

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\t")]
    [InlineData("\r\n")]
    public void Sanitize_NullOrWhitespace_ReturnsNull(string? emptyValue)
    {
        _logger.LogInformation("=== TEST: Sanitize_NullOrWhitespace ===");
        _logger.LogInformation("Test data: Value='{Value}' (expected: null)", emptyValue ?? "null");

        // Act - MUST NOT CRASH
        var result = FieldSanitizer.Sanitize(emptyValue);

        // Assert
        _logger.LogInformation("Sanitized result: '{Result}' (expected: null)", result ?? "null");
        result.ShouldBeNull();
    }

    #endregion Null/Empty Handling (NEVER CRASH)

    #region Complex Real-World Examples

    [Fact]
    public void Sanitize_ComplexDirtyData_CleansCorrectly()
    {
        _logger.LogInformation("=== TEST START: Sanitize_ComplexDirtyData ===");

        // Arrange - Combination of multiple data quality issues
        var dirtyValue = "  SUBDELEGACION&nbsp;8\r\nSAN    ANGEL   ";

        _logger.LogInformation("Test data: Complex dirty value with multiple issues");

        // Act
        var result = FieldSanitizer.Sanitize(dirtyValue);

        // Assert - All issues cleaned
        _logger.LogInformation("Sanitized result: '{Result}'", result);
        result.ShouldBe("SUBDELEGACION 8 SAN ANGEL");

        _logger.LogInformation("=== TEST PASSED ===");
    }

    [Fact]
    public void Sanitize_ValidCleanData_ReturnsUnchanged()
    {
        // Arrange - Already clean data
        var cleanValue = "SUBDELEGACION 8 SAN ANGEL";

        _logger.LogInformation("Test data: Already clean value");

        // Act
        var result = FieldSanitizer.Sanitize(cleanValue);

        // Assert - Should return unchanged
        result.ShouldBe(cleanValue);
    }

    #endregion Complex Real-World Examples

    #region Monto Sanitization Tests

    [Fact]
    public void SanitizeMonto_ValidInteger_ReturnsFormatted()
    {
        _logger.LogInformation("=== TEST START: SanitizeMonto_ValidInteger ===");

        // Arrange - Clean integer amount
        var monto = "236570";

        _logger.LogInformation("Test data: Monto='{Monto}'", monto);

        // Act
        var result = FieldSanitizer.SanitizeMonto(monto);

        // Assert
        _logger.LogInformation("Sanitized result: '{Result}'", result);
        result.ShouldBe("236570");

        _logger.LogInformation("=== TEST PASSED ===");
    }

    [Fact]
    public void SanitizeMonto_WithCurrencySymbol_RemovesSymbol()
    {
        _logger.LogInformation("=== TEST START: SanitizeMonto_WithCurrencySymbol ===");

        // Arrange - Real XML data quality issue: amount with currency symbol
        var dirtyMonto = "$236,569.68";

        _logger.LogInformation("Test data: Monto='{Monto}' (with $, comma, decimals)", dirtyMonto);

        // Act
        var result = FieldSanitizer.SanitizeMonto(dirtyMonto);

        // Assert - Cleaned and rounded per R29 (>0.5 rounds up)
        _logger.LogInformation("Sanitized result: '{Result}' (expected: 236570)", result);
        result.ShouldBe("236570");

        _logger.LogInformation("=== TEST PASSED ===");
    }

    [Fact]
    public void SanitizeMonto_WithCommas_RemovesCommas()
    {
        // Arrange - Amount with thousands separators
        var dirtyMonto = "236,570";

        _logger.LogInformation("Test data: Monto='{Monto}' (with commas)", dirtyMonto);

        // Act
        var result = FieldSanitizer.SanitizeMonto(dirtyMonto);

        // Assert
        result.ShouldBe("236570");
    }

    [Fact]
    public void SanitizeMonto_WithDecimals_RoundsToNearestPeso()
    {
        // Arrange - R29 rounding rules: >0.5 rounds up, <0.5 rounds down
        var monto1 = "236569.68"; // Should round to 236570 (0.68 > 0.5)
        var monto2 = "236569.20"; // Should round to 236569 (0.20 < 0.5)
        var monto3 = "236569.50"; // Should round to 236570 (0.50 rounds away from zero)

        _logger.LogInformation("Testing R29 rounding rules");

        // Act & Assert
        FieldSanitizer.SanitizeMonto(monto1).ShouldBe("236570");
        FieldSanitizer.SanitizeMonto(monto2).ShouldBe("236569");
        FieldSanitizer.SanitizeMonto(monto3).ShouldBe("236570");
    }

    [Theory]
    [InlineData("MXN")]
    [InlineData("USD")]
    [InlineData("$")]
    [InlineData("MXN ")]
    [InlineData(" MXN")]
    public void SanitizeMonto_WithCurrencyCode_RemovesCurrency(string currencyCode)
    {
        // Arrange
        var dirtyMonto = $"236570 {currencyCode}";

        _logger.LogInformation("Test data: Monto='{Monto}' (with currency code)", dirtyMonto);

        // Act
        var result = FieldSanitizer.SanitizeMonto(dirtyMonto);

        // Assert - Currency code removed
        result.ShouldBe("236570");
    }

    [Fact]
    public void SanitizeMonto_Zero_ReturnsZero()
    {
        // Arrange - Zero is valid (toda la cuenta)
        var monto = "0";

        _logger.LogInformation("Test data: Monto='0' (toda la cuenta)");

        // Act
        var result = FieldSanitizer.SanitizeMonto(monto);

        // Assert
        result.ShouldBe("0");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("INVALID")]
    [InlineData("-236570")] // Negative (invalid)
    [InlineData("ABC")]
    public void SanitizeMonto_InvalidInput_ReturnsNull(string? invalidMonto)
    {
        _logger.LogInformation("=== TEST: SanitizeMonto_InvalidInput ===");
        _logger.LogInformation("Test data: Monto='{Monto}' (expected: null)", invalidMonto ?? "null");

        // Act - MUST NOT CRASH
        var result = FieldSanitizer.SanitizeMonto(invalidMonto);

        // Assert
        _logger.LogInformation("Sanitized result: '{Result}' (expected: null)", result ?? "null");
        result.ShouldBeNull();
    }

    #endregion Monto Sanitization Tests

    #region Idempotency Tests (Sanitize twice = same result)

    [Fact]
    public void Sanitize_Idempotent_SameResultTwice()
    {
        _logger.LogInformation("=== TEST START: Sanitize_Idempotent ===");

        // Arrange - Dirty data
        var dirtyValue = "  SUBDELEGACION&nbsp;8\r\nSAN    ANGEL   ";

        // Act - Sanitize twice
        var result1 = FieldSanitizer.Sanitize(dirtyValue);
        var result2 = FieldSanitizer.Sanitize(result1);

        // Assert - Should be identical (idempotent operation)
        _logger.LogInformation("First sanitization: '{Result1}'", result1);
        _logger.LogInformation("Second sanitization: '{Result2}'", result2);
        result1.ShouldBe(result2);

        _logger.LogInformation("=== TEST PASSED ===");
    }

    [Fact]
    public void SanitizeMonto_Idempotent_SameResultTwice()
    {
        // Arrange
        var dirtyMonto = "$236,569.68";

        // Act
        var result1 = FieldSanitizer.SanitizeMonto(dirtyMonto);
        var result2 = FieldSanitizer.SanitizeMonto(result1);

        // Assert
        result1.ShouldBe(result2);
    }

    #endregion Idempotency Tests
}
