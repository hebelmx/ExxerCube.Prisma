// <copyright file="FieldCandidate.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.ValueObjects;

using ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Represents a candidate field value from a single source during multi-source fusion.
/// Contains the value, source reliability, and validation flags used for fusion decision-making.
/// </summary>
public class FieldCandidate
{
    /// <summary>
    /// Gets or sets the field value extracted from this source (may be null).
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Gets or sets the source type (XML, PDF, DOCX) that provided this value.
    /// </summary>
    public SourceType Source { get; set; } = SourceType.XML_HandFilled;

    /// <summary>
    /// Gets or sets the calculated reliability score for this source (0.0-1.0).
    /// Dynamically calculated from base reliability + OCR quality + image quality + extraction success.
    /// </summary>
    public double SourceReliability { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this value matches the expected regex pattern for the field type.
    /// Examples: RFC pattern, CURP pattern, NumeroExpediente pattern, date format, etc.
    /// </summary>
    public bool MatchesPattern { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this value exists in the appropriate CNBV catalog.
    /// Applicable to fields like AreaDescripcion, AutoridadNombre, Caracter, etc.
    /// </summary>
    public bool MatchesCatalog { get; set; }

    /// <summary>
    /// Gets or sets the OCR confidence for this specific field (0.0-1.0).
    /// Available for PDF/DOCX sources with word-level OCR confidence. Null for XML sources.
    /// </summary>
    public double? OcrConfidence { get; set; }

    /// <summary>
    /// Gets the validation state for this candidate.
    /// </summary>
    public ValidationState Validation { get; } = new();
}
