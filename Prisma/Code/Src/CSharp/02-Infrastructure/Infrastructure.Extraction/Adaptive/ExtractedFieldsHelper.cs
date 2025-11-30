// <copyright file="ExtractedFieldsHelper.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;

/// <summary>
/// Helper class for creating ExtractedFields instances from DOCX extraction results.
/// </summary>
public static class ExtractedFieldsHelper
{
    /// <summary>
    /// Creates an ExtractedFields instance with core and additional fields.
    /// </summary>
    /// <param name="expediente">Expediente number.</param>
    /// <param name="causa">Causa (cause).</param>
    /// <param name="accionSolicitada">Accion solicitada (requested action).</param>
    /// <param name="additionalFields">Additional fields dictionary.</param>
    /// <param name="montos">List of monetary amounts.</param>
    /// <returns>Populated ExtractedFields instance.</returns>
    public static ExtractedFields Create(
        string? expediente = null,
        string? causa = null,
        string? accionSolicitada = null,
        Dictionary<string, string?>? additionalFields = null,
        List<AmountData>? montos = null)
    {
        return new ExtractedFields
        {
            Expediente = expediente,
            Causa = causa,
            AccionSolicitada = accionSolicitada,
            AdditionalFields = additionalFields ?? new Dictionary<string, string?>(),
            Montos = montos ?? new List<AmountData>(),
        };
    }

    /// <summary>
    /// Adds a field to AdditionalFields if the value is not null or whitespace.
    /// </summary>
    /// <param name="fields">The ExtractedFields instance.</param>
    /// <param name="key">Field key.</param>
    /// <param name="value">Field value.</param>
    public static void AddIfNotEmpty(this ExtractedFields fields, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields.AdditionalFields[key] = value;
        }
    }

    /// <summary>
    /// Adds a monetary amount to the Montos list if the value has a value.
    /// </summary>
    /// <param name="fields">The ExtractedFields instance.</param>
    /// <param name="amount">The amount value.</param>
    /// <param name="originalText">Original text excerpt.</param>
    /// <param name="currency">Currency code (default: MXN).</param>
    public static void AddAmount(this ExtractedFields fields, decimal? amount, string originalText, string currency = "MXN")
    {
        if (amount.HasValue)
        {
            fields.Montos.Add(new AmountData
            {
                Value = amount.Value,
                Currency = currency,
                OriginalText = originalText.Length > 100 ? originalText[..100] : originalText,
            });
        }
    }
}
