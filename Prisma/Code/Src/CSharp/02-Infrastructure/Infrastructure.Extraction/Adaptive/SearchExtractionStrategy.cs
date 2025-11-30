// <copyright file="SearchExtractionStrategy.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;

/// <summary>
/// CRITICAL: Search strategy resolves cross-references within documents.
/// Handles Mexican legal document patterns like:
/// - "cantidad arriba mencionada" (amount mentioned above)
/// - "anteriormente indicado" (previously indicated)
/// - "cuenta antes señalada" (account mentioned before).
/// </summary>
/// <remarks>
/// Common pattern in Mexican legal documents:
/// "Se ordena el aseguramiento de la cantidad arriba mencionada"
/// → Search upward in document for amount
/// "Desbloquear la cuenta anteriormente indicada"
/// → Search upward for account number.
/// </remarks>
public class SearchExtractionStrategy : IAdaptiveDocxStrategy
{
    private readonly ILogger<SearchExtractionStrategy> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchExtractionStrategy"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public SearchExtractionStrategy(ILogger<SearchExtractionStrategy> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public DocxExtractionStrategyType StrategyType => DocxExtractionStrategyType.Search;

    /// <inheritdoc/>
    public int CanHandle(string text)
    {
        var upperText = text.ToUpperInvariant();

        // Check for cross-reference indicators
        var crossRefPatterns = new[]
        {
            "ARRIBA MENCIONAD",
            "ANTERIORMENTE INDICAD",
            "PREVIAMENTE SEÑALAD",
            "ANTES MENCIONAD",
            "CUENTA ANTES",
            "MONTO ANTES",
            "CANTIDAD ARRIBA",
        };

        var hasReference = crossRefPatterns.Any(upperText.Contains);
        return hasReference ? 80 : 0; // High confidence if cross-references found
    }

    /// <inheritdoc/>
    public ExtractedFields? Extract(string text)
    {
        try
        {
            var fields = new ExtractedFields
            {
                Expediente = ExtractExpediente(text),
                AdditionalFields = new Dictionary<string, string?>(),
            };

            // Add oficio if found
            var oficio = ExtractOficio(text);
            if (!string.IsNullOrWhiteSpace(oficio))
                fields.AdditionalFields["Oficio"] = oficio;

            // Resolve cross-referenced fields
            var cuenta = ResolveCuentaReference(text);
            if (!string.IsNullOrWhiteSpace(cuenta))
                fields.AdditionalFields["Cuenta"] = cuenta;

            var nombre = ResolveNombreReference(text);
            if (!string.IsNullOrWhiteSpace(nombre))
                fields.AdditionalFields["Nombre"] = nombre;

            // Resolve monetary amounts
            var monto = ResolveMontoReference(text);
            fields.AddAmount(monto, text);

            _logger.LogDebug("Search strategy extracted fields: Cuenta={Cuenta}, Monto={Monto}",
                fields.AdditionalFields.GetValueOrDefault("Cuenta"), monto);

            return fields;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SearchExtractionStrategy");
            return null;
        }
    }

    /// <summary>
    /// Resolves cuenta (account) cross-references.
    /// Example: "desbloquear la cuenta anteriormente indicada" → search for account number.
    /// </summary>
    private string? ResolveCuentaReference(string text)
    {
        var crossRefPatterns = new[]
        {
            @"CUENTA\s+(?:ARRIBA\s+MENCIONAD|ANTERIORMENTE\s+INDICAD|ANTES\s+SEÑALAD|PREVIAMENTE\s+INDICAD)",
            @"(?:LA|DICHA)\s+CUENTA",
        };

        foreach (var pattern in crossRefPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                // Search backward from reference for account number
                var precedingText = text[..match.Index];
                var account = SearchBackwardForAccount(precedingText);
                if (account != null)
                {
                    _logger.LogDebug("Resolved cuenta cross-reference: {Account}", account);
                    return account;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves monto (amount) cross-references.
    /// Example: "la cantidad arriba mencionada" → search for amount.
    /// </summary>
    private decimal? ResolveMontoReference(string text)
    {
        var crossRefPatterns = new[]
        {
            @"(?:CANTIDAD|MONTO)\s+(?:ARRIBA\s+MENCIONAD|ANTERIORMENTE\s+INDICAD|ANTES\s+SEÑALAD)",
            @"(?:LA|DICHA)\s+(?:CANTIDAD|MONTO)",
        };

        foreach (var pattern in crossRefPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                // Search backward from reference for amount
                var precedingText = text[..match.Index];
                var amount = SearchBackwardForAmount(precedingText);
                if (amount.HasValue)
                {
                    _logger.LogDebug("Resolved monto cross-reference: {Amount}", amount);
                    return amount;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves nombre (name) cross-references.
    /// Example: "el titular anteriormente mencionado" → search for name.
    /// </summary>
    private string? ResolveNombreReference(string text)
    {
        var crossRefPatterns = new[]
        {
            @"(?:TITULAR|NOMBRE)\s+(?:ARRIBA\s+MENCIONAD|ANTERIORMENTE\s+INDICAD|ANTES\s+SEÑALAD)",
            @"(?:EL|DICHO)\s+TITULAR",
        };

        foreach (var pattern in crossRefPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                // Search backward from reference for name
                var precedingText = text[..match.Index];
                var name = SearchBackwardForName(precedingText);
                if (name != null)
                {
                    _logger.LogDebug("Resolved nombre cross-reference: {Name}", name);
                    return name;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Searches backward in text for account number.
    /// Looks for patterns like "CUENTA: 123456" or "CUENTA NÚMERO 123456".
    /// </summary>
    private static string? SearchBackwardForAccount(string text)
    {
        var accountPatterns = new[]
        {
            @"CUENTA[:\s]+(\d+)",
            @"N[ÚU]MERO\s+DE\s+CUENTA[:\s]+(\d+)",
            @"CTA[.:\s]+(\d+)",
            @"\b(\d{10,18})\b", // 10-18 digit account numbers
        };

        // Search in reverse order (most recent mention first)
        foreach (var pattern in accountPatterns)
        {
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);
            if (matches.Count > 0)
            {
                // Get last match (closest to reference)
                var lastMatch = matches[^1];
                return lastMatch.Groups.Count > 1 ? lastMatch.Groups[1].Value.Trim() : lastMatch.Value.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Searches backward in text for amount.
    /// Looks for patterns like "$1,234.56" or "MONTO: 1234.56".
    /// </summary>
    private static decimal? SearchBackwardForAmount(string text)
    {
        var amountPatterns = new[]
        {
            @"(?:MONTO|CANTIDAD)[:\s]+\$?[\s]*([\d,]+\.?\d*)",
            @"\$[\s]*([\d,]+\.?\d*)",
            @"\b([\d,]+\.?\d{2})\b", // Numbers with decimal
        };

        // Search in reverse order (most recent mention first)
        foreach (var pattern in amountPatterns)
        {
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);
            if (matches.Count > 0)
            {
                // Get last match (closest to reference)
                var lastMatch = matches[^1];
                var amountStr = lastMatch.Groups.Count > 1 ? lastMatch.Groups[1].Value : lastMatch.Value;
                amountStr = amountStr.Replace(",", string.Empty).Replace("$", string.Empty).Trim();

                if (decimal.TryParse(amountStr, out var amount))
                {
                    return amount;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Searches backward in text for name.
    /// Looks for patterns like "NOMBRE: José Pérez" or "TITULAR: María García".
    /// </summary>
    private static string? SearchBackwardForName(string text)
    {
        var namePatterns = new[]
        {
            @"(?:NOMBRE|TITULAR)[:\s]+([A-ZÁÉÍÓÚÑ][a-záéíóúñA-ZÁÉÍÓÚÑ\s]+)",
            @"NOMBRE\s+COMPLETO[:\s]+([A-ZÁÉÍÓÚÑ][a-záéíóúñA-ZÁÉÍÓÚÑ\s]+)",
        };

        // Search in reverse order (most recent mention first)
        foreach (var pattern in namePatterns)
        {
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);
            if (matches.Count > 0)
            {
                // Get last match (closest to reference)
                var lastMatch = matches[^1];
                if (lastMatch.Groups.Count > 1)
                {
                    var name = lastMatch.Groups[1].Value.Trim();
                    // Validate it's a reasonable name (at least 2 parts)
                    if (name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2)
                    {
                        return name;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts expediente number from text.
    /// </summary>
    private static string? ExtractExpediente(string text)
    {
        var pattern = @"[A-Z]/[A-Z]{1,2}\d+-\d+-\d+-[A-Z]+";
        var match = Regex.Match(text, pattern);
        return match.Success ? match.Value : null;
    }

    /// <summary>
    /// Extracts oficio number from text.
    /// </summary>
    private static string? ExtractOficio(string text)
    {
        var pattern = @"OFICIO[:\s]+([A-Z0-9/-]+)";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : null;
    }
}
