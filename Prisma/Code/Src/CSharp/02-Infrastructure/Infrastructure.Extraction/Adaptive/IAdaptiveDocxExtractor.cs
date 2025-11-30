// <copyright file="IAdaptiveDocxExtractor.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;

/// <summary>
/// Interface for adaptive DOCX field extraction.
/// Uses multiple strategies to extract data from DOCX documents with varying formats.
/// </summary>
/// <remarks>
/// ADR-008: New interface for adaptive extraction.
/// Does NOT modify existing IFieldExtractor{DocxSource} - allows coexistence with zero breaking changes.
/// </remarks>
public interface IAdaptiveDocxExtractor
{
    /// <summary>
    /// Extracts fields from DOCX text using adaptive strategy selection.
    /// </summary>
    /// <param name="text">The full DOCX text content.</param>
    /// <param name="mode">Extraction mode (Primary or Complement).</param>
    /// <returns>Extracted fields, or null if extraction failed.</returns>
    ExtractedFields? Extract(string text, ExtractionMode mode = ExtractionMode.Primary);
}

/// <summary>
/// Extraction mode for adaptive DOCX extraction.
/// </summary>
public enum ExtractionMode
{
    /// <summary>
    /// Primary extraction (select best strategy).
    /// </summary>
    Primary,

    /// <summary>
    /// Complement mode (fill gaps from XML/OCR).
    /// This is EXPECTED workflow when XML/OCR sources are missing data.
    /// </summary>
    Complement,
}
