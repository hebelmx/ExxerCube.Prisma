// <copyright file="DocxExtractionStrategyType.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Defines the extraction strategy types for DOCX document processing.
/// Multiple strategies can be used adaptively based on document structure.
/// </summary>
public enum DocxExtractionStrategyType
{
    /// <summary>
    /// Standard CNBV format extraction using regex patterns.
    /// Works for well-formatted documents with predictable structure.
    /// </summary>
    Structured = 0,

    /// <summary>
    /// Contextual extraction using label-value pairs (e.g., "Expediente: VALUE").
    /// Works for semi-structured documents with clear labels.
    /// </summary>
    Contextual = 1,

    /// <summary>
    /// Table-based extraction from DOCX tables.
    /// Uses column headers for field mapping.
    /// </summary>
    TableBased = 2,

    /// <summary>
    /// Fuzzy matching extraction with error tolerance.
    /// Handles typos, variations, and Mexican name spellings.
    /// </summary>
    Fuzzy = 3,

    /// <summary>
    /// Complement strategy - fills gaps when XML/OCR sources are missing data.
    /// This is EXPECTED behavior in Mexican legal documents, not a failure mode.
    /// </summary>
    Complement = 4,

    /// <summary>
    /// Search strategy - resolves cross-references within documents.
    /// Handles "cantidad arriba mencionada", "anteriormente indicado", etc.
    /// </summary>
    Search = 5,

    /// <summary>
    /// Hybrid strategy combining multiple approaches.
    /// Selects best result from all strategies.
    /// </summary>
    Hybrid = 99
}
