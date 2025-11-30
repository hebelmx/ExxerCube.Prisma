// <copyright file="StructuredDocxStrategy.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;

/// <summary>
/// Standard CNBV format extraction using regex patterns.
/// Works for well-formatted documents with predictable structure.
/// </summary>
/// <remarks>
/// Best for documents with:
/// - Clear label-value structure (EXPEDIENTE: VALUE)
/// - Standard CNBV formatting
/// - Consistent spacing and layout.
/// </remarks>
public class StructuredDocxStrategy : IAdaptiveDocxStrategy
{
    private readonly ILogger<StructuredDocxStrategy> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredDocxStrategy"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public StructuredDocxStrategy(ILogger<StructuredDocxStrategy> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public DocxExtractionStrategyType StrategyType => DocxExtractionStrategyType.Structured;

    /// <inheritdoc/>
    public int CanHandle(string text)
    {
        var upperText = text.ToUpperInvariant();

        // Check for standard CNBV labels
        var standardLabels = new[]
        {
            "EXPEDIENTE:",
            "OFICIO:",
            "NOMBRE:",
            "CUENTA:",
        };

        var labelCount = standardLabels.Count(label => upperText.Contains(label));

        // High confidence if multiple standard labels present
        return labelCount >= 3 ? 90 : labelCount >= 2 ? 70 : 40;
    }

    /// <inheritdoc/>
    public ExtractedFields? Extract(string text)
    {
        try
        {
            var expediente = new Expediente
            {
                NumeroExpediente = ExtractExpediente(text),
                NumeroOficio = ExtractOficio(text),
                Cuenta = ExtractCuenta(text),
                NombreCompleto = ExtractNombre(text),
                RFC = ExtractRFC(text),
                CLABE = ExtractCLABE(text),
                Monto = ExtractMonto(text),
                Banco = ExtractBanco(text),
            };

            _logger.LogDebug("Structured strategy extracted: Expediente={Expediente}, Oficio={Oficio}",
                expediente.NumeroExpediente, expediente.NumeroOficio);

            return expediente;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in StructuredDocxStrategy");
            return null;
        }
    }

    /// <summary>
    /// Extracts expediente number using standard CNBV patterns.
    /// </summary>
    private static string? ExtractExpediente(string text)
    {
        var patterns = new[]
        {
            @"[A-Z]/[A-Z]{1,2}\d+-\d+-\d+-[A-Z]+",           // A/AS1-2505-088637-PHM
            @"EXPEDIENTE[:\s]+([A-Z0-9/-]+)",                 // EXPEDIENTE: XXX
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
    /// Extracts oficio number.
    /// </summary>
    private static string? ExtractOficio(string text)
    {
        var pattern = @"OFICIO[:\s]+([A-Z0-9/-]+)";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>
    /// Extracts cuenta (account number).
    /// </summary>
    private static string? ExtractCuenta(string text)
    {
        var pattern = @"CUENTA[:\s]+(\d+)";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>
    /// Extracts nombre completo (full name).
    /// </summary>
    private static string? ExtractNombre(string text)
    {
        var pattern = @"NOMBRE[:\s]+([A-ZÁÉÍÓÚÑ][a-záéíóúñA-ZÁÉÍÓÚÑ\s]+)";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (match.Success && match.Groups.Count > 1)
        {
            var name = match.Groups[1].Value.Trim();
            // Validate reasonable name
            if (name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2)
            {
                return name;
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
    /// Extracts CLABE (18-digit account).
    /// </summary>
    private static string? ExtractCLABE(string text)
    {
        var pattern = @"CLABE[:\s]+(\d{18})";
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

    /// <summary>
    /// Extracts monto (amount).
    /// </summary>
    private static decimal? ExtractMonto(string text)
    {
        var pattern = @"(?:MONTO|CANTIDAD)[:\s]+\$?[\s]*([\d,]+\.?\d*)";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (match.Success && match.Groups.Count > 1)
        {
            var amountStr = match.Groups[1].Value.Replace(",", string.Empty);
            if (decimal.TryParse(amountStr, out var amount))
            {
                return amount;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts banco (bank name).
    /// </summary>
    private static string? ExtractBanco(string text)
    {
        var pattern = @"BANCO[:\s]+([A-ZÁÉÍÓÚÑ\s]+)";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : null;
    }
}
