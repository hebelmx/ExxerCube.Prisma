// <copyright file="IDocxExtractionStrategy.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Docx;

/// <summary>
/// Interface for DOCX extraction strategies.
/// Each strategy uses a different approach to extract data from DOCX files.
/// </summary>
/// <remarks>
/// NOTE: This interface will be superseded by IAdaptiveDocxStrategy (ADR-008).
/// Kept for reference but not actively used to avoid breaking existing code.
/// </remarks>
public interface IDocxExtractionStrategy
{
    /// <summary>
    /// Gets the strategy type identifier.
    /// </summary>
    DocxExtractionStrategyType StrategyType { get; }

    /// <summary>
    /// Extracts fields from DOCX text using this strategy.
    /// </summary>
    /// <param name="text">The full DOCX text content.</param>
    /// <returns>Extracted fields data, or null if strategy cannot extract.</returns>
    ExtractedFields? Extract(string text);

    /// <summary>
    /// Determines if this strategy can handle the given document structure.
    /// </summary>
    /// <param name="text">The full DOCX text content.</param>
    /// <returns>Confidence score (0-100) that this strategy can handle the document.</returns>
    int CanHandle(string text);
}
