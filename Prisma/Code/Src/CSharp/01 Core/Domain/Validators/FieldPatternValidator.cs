// <copyright file="FieldPatternValidator.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Validators;

using System.Text.RegularExpressions;

/// <summary>
/// Static utility class for validating R29 A-2911 field patterns.
/// Implements all pattern validation rules from CNBV SITI regulation.
/// </summary>
/// <remarks>
/// <para><strong>R29 Pattern Requirements:</strong></para>
/// <list type="bullet">
/// <item>RFC: Pattern for 13 chars física or underscore + 12 chars moral</item>
/// <item>CURP: Pattern for 18 chars with gender indicator (H=male, M=female)</item>
/// <item>NumeroExpediente: Pattern like A/AS1-1111-222222-AAA</item>
/// <item>CLABE: Exactly 18 digits (bank account number)</item>
/// <item>FechaSolicitud: Exactly 8 digits in YYYYMMDD format</item>
/// <item>Monto: Decimal, no decimals/commas, positive, rounded to nearest peso</item>
/// <item>NumeroOficio: Maximum 30 characters</item>
/// </list>
/// <para><strong>Usage in Fusion:</strong></para>
/// <para>
/// These validators are used by FusionExpedienteService to validate FieldCandidate values
/// before fusion. Candidates that fail pattern validation have reduced reliability scores.
/// </para>
/// </remarks>
public static partial class FieldPatternValidator
{
    // Compiled regex patterns for performance
    [GeneratedRegex(@"^(_)?[A-Z]{3,4}\d{6}[A-Z0-9]{3}$", RegexOptions.Compiled)]
    private static partial Regex RfcPattern();

    [GeneratedRegex(@"^[A-Z]{4}\d{6}[HM][A-Z]{5}[A-Z0-9]{2}$", RegexOptions.Compiled)]
    private static partial Regex CurpPattern();

    [GeneratedRegex(@"^[A-Z]/[A-Z0-9]+(-[A-Z0-9]+)+-[A-Z]+$", RegexOptions.Compiled)]
    private static partial Regex NumeroExpedientePattern();

    [GeneratedRegex(@"^\d{18}$", RegexOptions.Compiled)]
    private static partial Regex ClabePattern();

    [GeneratedRegex(@"^\d{8}$", RegexOptions.Compiled)]
    private static partial Regex DatePattern();

    /// <summary>
    /// Validates RFC (Registro Federal de Contribuyentes) pattern.
    /// </summary>
    /// <param name="value">RFC value to validate.</param>
    /// <returns>
    /// True if value matches RFC pattern:
    /// - Persona Física: XXXXAAMMDDABC (13 chars, 4 letters + 6 date + 3 alphanumeric)
    /// - Persona Moral: _XXXAAMMDDABC (underscore prefix + 12 chars)
    /// False if null, empty, whitespace, or invalid format.
    /// </returns>
    /// <remarks>
    /// R29 Requirement: "RFC debe anotarse exactamente como está registrado en SAT.
    /// Para personas morales usar guión bajo como primer carácter."
    /// </remarks>
    public static bool IsValidRFC(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return RfcPattern().IsMatch(value);
    }

    /// <summary>
    /// Validates CURP (Clave Única de Registro de Población) pattern.
    /// </summary>
    /// <param name="value">CURP value to validate.</param>
    /// <returns>
    /// True if value matches CURP pattern:
    /// - 18 characters total
    /// - 4 letters + 6 date + gender (H/M) + 5 letters + 2 alphanumeric
    /// False if null, empty, whitespace, or invalid format.
    /// </returns>
    /// <remarks>
    /// CURP format: XXXXAAMMDD[HM]XXXXX## where H=Hombre, M=Mujer.
    /// Example: LOMH850101HDFLRR01 (Male born 1985-01-01)
    /// </remarks>
    public static bool IsValidCURP(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return CurpPattern().IsMatch(value);
    }

    /// <summary>
    /// Validates NumeroExpediente pattern.
    /// </summary>
    /// <param name="value">NumeroExpediente value to validate.</param>
    /// <returns>
    /// True if value matches pattern: [A-Z]/[A-Z]{1,2}####-####-######-[A-Z]+
    /// Examples: "A/AS1-1111-222222-AAA", "H/H-123-456789-PENAL"
    /// False if null, empty, whitespace, or invalid format.
    /// </returns>
    /// <remarks>
    /// Format varies by authority and area:
    /// - Aseguramiento: A/AS1-####-######-AAA
    /// - Hacendario: H/H-###-######-PENAL
    /// Common pattern: Area/SubArea-Number1-Number2-Description
    /// </remarks>
    public static bool IsValidNumeroExpediente(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return NumeroExpedientePattern().IsMatch(value);
    }

    /// <summary>
    /// Validates CLABE (Clave Bancaria Estandarizada) pattern.
    /// </summary>
    /// <param name="value">CLABE value to validate.</param>
    /// <returns>
    /// True if value is exactly 18 digits.
    /// False if null, empty, whitespace, or not 18 digits.
    /// </returns>
    /// <remarks>
    /// CLABE is Mexico's standardized bank account number format.
    /// 18 digits: 3 (bank) + 3 (branch) + 11 (account) + 1 (check digit).
    /// Example: 012345678901234567
    /// </remarks>
    public static bool IsValidCLABE(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return ClabePattern().IsMatch(value);
    }

    /// <summary>
    /// Validates date pattern (YYYYMMDD format).
    /// </summary>
    /// <param name="value">Date value to validate.</param>
    /// <returns>
    /// True if value is 8 digits in YYYYMMDD format AND represents a valid calendar date.
    /// False if null, empty, whitespace, wrong format, or invalid date (e.g., Feb 30).
    /// </returns>
    /// <remarks>
    /// R29 Requirement: "Fechas deben reportarse en formato AAAAMMDD sin guiones ni diagonales."
    /// Validates:
    /// 1. Format: Exactly 8 digits
    /// 2. Value: Parseable as valid DateTime
    /// 3. Range: Reasonable dates (handles leap years)
    /// </remarks>
    public static bool IsValidDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!DatePattern().IsMatch(value))
        {
            return false;
        }

        // Additional validation: must be parseable as DateTime
        return DateTime.TryParseExact(
            value,
            "yyyyMMdd",
            null,
            System.Globalization.DateTimeStyles.None,
            out _);
    }

    /// <summary>
    /// Validates amount (monto) pattern.
    /// </summary>
    /// <param name="value">Amount value to validate.</param>
    /// <returns>
    /// True if value is a valid decimal number that:
    /// - Is positive (>= 0)
    /// - Has no decimal places (R29 requirement: rounded to nearest peso)
    /// - Contains no commas, currency symbols, or spaces
    /// False if null, empty, whitespace, negative, or has decimals.
    /// </returns>
    /// <remarks>
    /// R29 Requirement: "Montos sin decimales, sin comas, sin puntos, cifras positivas.
    /// Redondeo: mayor a 0.5 sube, menor a 0.5 baja. Ejemplo: $236,569.68 resulta en 236570"
    /// Valid: "236570", "0"
    /// Invalid: "236,570", "236570.50", "-236570", "$236570"
    /// </remarks>
    public static bool IsValidMonto(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // Must be a valid decimal (no commas, no spaces - should be sanitized first)
        // Use NumberStyles.None to reject commas and other formatting
        if (!decimal.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var amount))
        {
            return false;
        }

        // Must be positive (zero is valid for "toda la cuenta")
        if (amount < 0)
        {
            return false;
        }

        // Should not have decimal places (R29 requirement)
        return amount == Math.Round(amount);
    }

    /// <summary>
    /// Validates NumeroOficio pattern (max 30 characters).
    /// </summary>
    /// <param name="value">NumeroOficio value to validate.</param>
    /// <returns>
    /// True if value is not null/empty/whitespace AND length is at most 30.
    /// False if null, empty, whitespace, or exceeds 30 characters.
    /// </returns>
    /// <remarks>
    /// R29 Requirement: "NUMERO_OFICIO (30 chars max, unique per titular/cotitular)"
    /// If >2 titulares or >2 cotitulares, append "-XXX" (001-999) to NumeroOficio.
    /// </remarks>
    public static bool IsValidNumeroOficio(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Length <= 30;
    }

    /// <summary>
    /// Validates generic text field pattern (non-empty and within max length).
    /// </summary>
    /// <param name="value">Text field value to validate.</param>
    /// <param name="maxLength">Maximum allowed length.</param>
    /// <returns>
    /// True if value is not null/empty/whitespace AND length is within maxLength.
    /// False if null, empty, whitespace, or exceeds maxLength.
    /// </returns>
    /// <remarks>
    /// Used for text fields without specific format requirements:
    /// - AutoridadNombre (max 250)
    /// - Nombre (max 100)
    /// - ApellidoPaterno (max 100)
    /// - ApellidoMaterno (max 100)
    /// - RazonSocial (max 250)
    /// </remarks>
    public static bool IsValidTextField(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Length <= maxLength;
    }
}
