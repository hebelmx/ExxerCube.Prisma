// <copyright file="ComplementExtractionStrategy.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;

/// <summary>
/// CRITICAL: Complement strategy fills gaps when XML/OCR sources are missing data.
/// This is EXPECTED behavior in Mexican legal documents, not a failure mode.
/// </summary>
/// <remarks>
/// Common scenarios:
/// - XML has expediente but no cuenta → DOCX complements with cuenta
/// - OCR has oficio but no monto → DOCX complements with monto
/// - Both sources missing nombre → DOCX provides nombre
/// This is normal workflow, not error handling.
/// </remarks>
public class ComplementExtractionStrategy : IAdaptiveDocxStrategy
{
    private readonly MexicanNameFuzzyMatcher _nameMatcher;
    private readonly FuzzyMatchingPolicy _fuzzyPolicy;
    private readonly ILogger<ComplementExtractionStrategy> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplementExtractionStrategy"/> class.
    /// </summary>
    /// <param name="nameMatcher">Mexican name fuzzy matcher.</param>
    /// <param name="fuzzyPolicy">Fuzzy matching policy.</param>
    /// <param name="logger">Logger instance.</param>
    public ComplementExtractionStrategy(
        MexicanNameFuzzyMatcher nameMatcher,
        FuzzyMatchingPolicy fuzzyPolicy,
        ILogger<ComplementExtractionStrategy> logger)
    {
        _nameMatcher = nameMatcher;
        _fuzzyPolicy = fuzzyPolicy;
        _logger = logger;
    }

    /// <inheritdoc/>
    public DocxExtractionStrategyType StrategyType => DocxExtractionStrategyType.Complement;

    /// <inheritdoc/>
    public int CanHandle(string text)
    {
        // Complement strategy is always available but has lower priority
        // It should be used when other sources (XML/OCR) are missing data
        return 50; // Medium confidence - always available but not primary
    }

    /// <inheritdoc/>
    public ExtractedFields? Extract(string text)
    {
        try
        {
            var fields = new ExtractedFields
            {
                Expediente = ExtractExpediente(text),
                Causa = ExtractCausa(text),
                AccionSolicitada = ExtractAccionSolicitada(text),
                AdditionalFields = new Dictionary<string, string?>(),
            };

            // Extract additional fields
            var cuenta = ExtractCuenta(text);
            if (!string.IsNullOrWhiteSpace(cuenta))
                fields.AdditionalFields["Cuenta"] = cuenta;

            var nombre = ExtractNombre(text);
            if (!string.IsNullOrWhiteSpace(nombre))
                fields.AdditionalFields["Nombre"] = nombre;

            var rfc = ExtractRFC(text);
            if (!string.IsNullOrWhiteSpace(rfc))
                fields.AdditionalFields["RFC"] = rfc;

            var clabe = ExtractCLABE(text);
            if (!string.IsNullOrWhiteSpace(clabe))
                fields.AdditionalFields["CLABE"] = clabe;

            var banco = ExtractBanco(text);
            if (!string.IsNullOrWhiteSpace(banco))
                fields.AdditionalFields["Banco"] = banco;

            var oficio = ExtractOficio(text);
            if (!string.IsNullOrWhiteSpace(oficio))
                fields.AdditionalFields["Oficio"] = oficio;

            // Extract monetary amounts
            var monto = ExtractMonto(text);
            if (monto.HasValue)
            {
                fields.Montos.Add(new AmountData
                {
                    Value = monto.Value,
                    Currency = "MXN",
                    OriginalText = text.Length > 100 ? text[..100] : text,
                });
            }

            _logger.LogDebug("Complement strategy extracted fields: Expediente={Expediente}, Oficio={Oficio}, Cuenta={Cuenta}",
                fields.Expediente, fields.AdditionalFields.GetValueOrDefault("Oficio"), fields.AdditionalFields.GetValueOrDefault("Cuenta"));

            return fields;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ComplementExtractionStrategy");
            return null;
        }
    }

    /// <summary>
    /// Extracts expediente number from text.
    /// Pattern: A/AS1-2505-088637-PHM or similar CNBV format.
    /// </summary>
    private static string? ExtractExpediente(string text)
    {
        var patterns = new[]
        {
            @"[A-Z]/[A-Z]{1,2}\d+-\d+-\d+-[A-Z]+",           // A/AS1-2505-088637-PHM
            @"EXPEDIENTE[:\s]+([A-Z0-9/-]+)",                 // EXPEDIENTE: XXX
            @"EXP[.\s]+([A-Z0-9/-]+)",                        // EXP. XXX
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : match.Value.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts oficio number from text.
    /// </summary>
    private static string? ExtractOficio(string text)
    {
        var patterns = new[]
        {
            @"OFICIO[:\s]+([A-Z0-9/-]+)",                     // OFICIO: XXX
            @"OFICIO\s+N[ÚU]MERO[:\s]+([A-Z0-9/-]+)",         // OFICIO NÚMERO: XXX
            @"OF\.[:\s]+([A-Z0-9/-]+)",                       // OF.: XXX
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts cuenta (account number) from text.
    /// </summary>
    private static string? ExtractCuenta(string text)
    {
        var patterns = new[]
        {
            @"CUENTA[:\s]+(\d+)",                              // CUENTA: 123456
            @"N[ÚU]MERO\s+DE\s+CUENTA[:\s]+(\d+)",            // NÚMERO DE CUENTA: 123456
            @"CTA[.:\s]+(\d+)",                                // CTA. 123456
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts nombre completo (full name) from text.
    /// Uses fuzzy matching for Mexican name variations.
    /// </summary>
    private string? ExtractNombre(string text)
    {
        var patterns = new[]
        {
            @"NOMBRE[:\s]+([A-ZÁÉÍÓÚÑ\s]+)",                  // NOMBRE: XXX
            @"NOMBRE\s+COMPLETO[:\s]+([A-ZÁÉÍÓÚÑ\s]+)",      // NOMBRE COMPLETO: XXX
            @"TITULAR[:\s]+([A-ZÁÉÍÓÚÑ\s]+)",                // TITULAR: XXX
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                var name = match.Groups[1].Value.Trim();
                // Validate it's a reasonable name (at least 2 parts)
                if (name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2)
                {
                    return name;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts RFC from text.
    /// </summary>
    private static string? ExtractRFC(string text)
    {
        // RFC pattern: 12-13 alphanumeric characters
        var pattern = @"\b[A-Z&Ñ]{3,4}\d{6}[A-Z0-9]{2,3}\b";
        var match = Regex.Match(text, pattern);
        return match.Success ? match.Value : null;
    }

    /// <summary>
    /// Extracts CLABE (18-digit bank account) from text.
    /// </summary>
    private static string? ExtractCLABE(string text)
    {
        var patterns = new[]
        {
            @"CLABE[:\s]+(\d{18})",                            // CLABE: 123456789012345678
            @"\b(\d{18})\b",                                   // 18-digit number
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var clabe = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
                if (clabe.Length == 18 && clabe.All(char.IsDigit))
                {
                    return clabe;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts monto (amount) from text.
    /// </summary>
    private static decimal? ExtractMonto(string text)
    {
        var patterns = new[]
        {
            @"MONTO[:\s]+\$?[\s]*([\d,]+\.?\d*)",             // MONTO: $1,234.56
            @"CANTIDAD[:\s]+\$?[\s]*([\d,]+\.?\d*)",          // CANTIDAD: $1,234.56
            @"\$[\s]*([\d,]+\.?\d*)",                          // $1,234.56
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                var amountStr = match.Groups[1].Value.Replace(",", string.Empty);
                if (decimal.TryParse(amountStr, out var amount))
                {
                    return amount;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts banco (bank name) from text.
    /// </summary>
    private static string? ExtractBanco(string text)
    {
        var patterns = new[]
        {
            @"BANCO[:\s]+([A-ZÁÉÍÓÚÑ\s]+)",                   // BANCO: XXX
            @"INSTITUCIÓN[:\s]+([A-ZÁÉÍÓÚÑ\s]+)",           // INSTITUCIÓN: XXX
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts causa (cause) from text.
    /// </summary>
    private static string? ExtractCausa(string text)
    {
        var pattern = @"(?:CAUSA|Causa)[:\s]+([^\n\r]+)";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>
    /// Extracts accion solicitada (requested action) from text.
    /// </summary>
    private static string? ExtractAccionSolicitada(string text)
    {
        var pattern = @"(?:ACCI[ÓO]N\s+SOLICITADA|Accion\s+Solicitada)[:\s]+([^\n\r]+)";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : null;
    }
}