using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Models;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Service for comparing extracted data from different sources (XML vs OCR).
/// Implements exact match with fuzzy fallback strategy.
/// </summary>
public interface IDocumentComparisonService
{
    /// <summary>
    /// Compares two Expediente objects and returns detailed field-level comparison results.
    /// </summary>
    /// <param name="xmlExpediente">The expediente extracted from XML.</param>
    /// <param name="ocrExpediente">The expediente extracted from OCR.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A comparison result with field-level details and overall similarity.</returns>
    Task<ComparisonResult> CompareExpedientesAsync(
        Expediente xmlExpediente,
        Expediente ocrExpediente,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares a single field value from XML and OCR sources.
    /// Uses exact match first, then falls back to fuzzy matching.
    /// </summary>
    /// <param name="fieldName">The name of the field being compared.</param>
    /// <param name="xmlValue">The value from XML source.</param>
    /// <param name="ocrValue">The value from OCR source.</param>
    /// <param name="ocrConfidence">Optional OCR confidence for this field.</param>
    /// <returns>A field comparison result with similarity score and status.</returns>
    FieldComparison CompareField(
        string fieldName,
        string? xmlValue,
        string? ocrValue,
        float? ocrConfidence = null);
}
