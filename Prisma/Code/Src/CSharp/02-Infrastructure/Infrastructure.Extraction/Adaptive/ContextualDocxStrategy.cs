// <copyright file="ContextualDocxStrategy.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;

/// <summary>
/// Contextual extraction using label-value pairs (e.g., "Expediente: VALUE").
/// Works for semi-structured documents with clear labels but flexible formatting.
/// </summary>
/// <remarks>
/// More flexible than Structured strategy:
/// - Handles variations: "Expediente No.", "Número de Expediente"
/// - Tolerates spacing variations
/// - Works with inline text (not just forms).
/// </remarks>
public class ContextualDocxStrategy : IAdaptiveDocxStrategy
{
    private readonly ILogger<ContextualDocxStrategy> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextualDocxStrategy"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public ContextualDocxStrategy(ILogger<ContextualDocxStrategy> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public DocxExtractionStrategyType StrategyType => DocxExtractionStrategyType.Contextual;

    /// <inheritdoc/>
    public int CanHandle(string text)
    {
        var upperText = text.ToUpperInvariant();

        // Check for contextual label variations
        var contextualPatterns = new[]
        {
            "EXPEDIENTE NO",
            "NÚMERO DE EXPEDIENTE",
            "OFICIO NÚMERO",
            "NOMBRE COMPLETO",
            "NÚMERO DE CUENTA",
        };

        var patternCount = contextualPatterns.Count(pattern => upperText.Contains(pattern));

        // Medium-high confidence if contextual patterns present
        return patternCount >= 2 ? 75 : patternCount >= 1 ? 60 : 30;
    }

    /// <inheritdoc/>
    public ExtractedFields? Extract(string text)
    {
        try
        {
            var expediente = new Expediente
            {
                NumeroExpediente = ExtractExpedienteContextual(text),
                NumeroOficio = ExtractOficioContextual(text),
                Cuenta = ExtractCuentaContextual(text),
                NombreCompleto = ExtractNombreContextual(text),
                RFC = ExtractRFC(text),
                CLABE = ExtractCLABE(text),
                Monto = ExtractMontoContextual(text),
                Banco = ExtractBancoContextual(text),
            };

            _logger.LogDebug("Contextual strategy extracted: Expediente={Expediente}, Cuenta={Cuenta}",
                expediente.NumeroExpediente, expediente.Cuenta);

            return expediente;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ContextualDocxStrategy");
            return null;
        }
    }

    /// <summary>
    /// Extracts expediente using contextual patterns.
    /// Handles variations: "Expediente:", "Expediente No.", "Número de Expediente".
    /// </summary>
    private static string? ExtractExpedienteContextual(string text)
    {
        var patterns = new[]
        {
            @"EXPEDIENTE\s+N[OÚU]?[.:]?\s*([A-Z0-9/-]+)",      // Expediente No. XXX
            @"N[ÚU]MERO\s+DE\s+EXPEDIENTE[:]?\s*([A-Z0-9/-]+)", // Número de Expediente XXX
            @"EXP[.]?\s*([A-Z0-9/-]+)",                         // Exp. XXX
            @"[A-Z]/[A-Z]{1,2}\d+-\d+-\d+-[A-Z]+",             // A/AS1-2505-088637-PHM
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
    /// Extracts oficio using contextual patterns.
    /// </summary>
    private static string? ExtractOficioContextual(string text)
    {
        var patterns = new[]
        {
            @"OFICIO\s+N[ÚU]MERO[:]?\s*([A-Z0-9/-]+)",        // Oficio Número XXX
            @"OFICIO[:]?\s*([A-Z0-9/-]+)",                     // Oficio XXX
            @"OF[.]?\s*N[OÚU][.]?\s*([A-Z0-9/-]+)",           // Of. No. XXX
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
    /// Extracts cuenta using contextual patterns.
    /// </summary>
    private static string? ExtractCuentaContextual(string text)
    {
        var patterns = new[]
        {
            @"N[ÚU]MERO\s+DE\s+CUENTA[:]?\s*(\d+)",           // Número de cuenta 123456
            @"CUENTA\s+N[OÚU]?[.]?\s*(\d+)",                  // Cuenta No. 123456
            @"CUENTA[:]?\s*(\d+)",                             // Cuenta: 123456
            @"CTA[.]?\s*N?[OÚU]?[.]?\s*(\d+)",                // Cta. No. 123456
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
    /// Extracts nombre using contextual patterns.
    /// </summary>
    private static string? ExtractNombreContextual(string text)
    {
        var patterns = new[]
        {
            @"NOMBRE\s+COMPLETO[:]?\s*([A-ZÁÉÍÓÚÑ][a-záéíóúñA-ZÁÉÍÓÚÑ\s]+)",  // Nombre completo: XXX
            @"NOMBRE[:]?\s*([A-ZÁÉÍÓÚÑ][a-záéíóúñA-ZÁÉÍÓÚÑ\s]+)",           // Nombre: XXX
            @"TITULAR[:]?\s*([A-ZÁÉÍÓÚÑ][a-záéíóúñA-ZÁÉÍÓÚÑ\s]+)",          // Titular: XXX
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                var name = match.Groups[1].Value.Trim();
                if (name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2)
                {
                    return name;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts monto using contextual patterns.
    /// </summary>
    private static decimal? ExtractMontoContextual(string text)
    {
        var patterns = new[]
        {
            @"(?:MONTO|CANTIDAD)\s+(?:DE|POR)?[:]?\s*\$?[\s]*([\d,]+\.?\d*)", // Monto de: $1,234.56
            @"\$[\s]*([\d,]+\.?\d*)",                                          // $1,234.56
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
    /// Extracts banco using contextual patterns.
    /// </summary>
    private static string? ExtractBancoContextual(string text)
    {
        var patterns = new[]
        {
            @"BANCO[:]?\s*([A-ZÁÉÍÓÚÑ\s]+)",                  // Banco: XXX
            @"INSTITUCI[ÓO]N[:]?\s*([A-ZÁÉÍÓÚÑ\s]+)",        // Institución: XXX
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
    /// Extracts RFC.
    /// </summary>
    private static string? ExtractRFC(string text)
    {
        var pattern = @"\b[A-Z&Ñ]{3,4}\d{6}[A-Z0-9]{2,3}\b";
        var match = Regex.Match(text, pattern);
        return match.Success ? match.Value : null;
    }

    /// <summary>
    /// Extracts CLABE.
    /// </summary>
    private static string? ExtractCLABE(string text)
    {
        var pattern = @"CLABE[:]?\s*(\d{18})";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value;
        }

        // Fallback: any 18-digit number
        pattern = @"\b(\d{18})\b";
        match = Regex.Match(text, pattern);
        return match.Success ? match.Value : null;
    }
}
