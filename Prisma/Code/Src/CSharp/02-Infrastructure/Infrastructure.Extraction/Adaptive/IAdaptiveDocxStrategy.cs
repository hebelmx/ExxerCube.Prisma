// <copyright file="IAdaptiveDocxStrategy.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;

/// <summary>
/// Interface for adaptive DOCX extraction strategies.
/// Each strategy uses a different approach to extract data from DOCX files.
/// </summary>
/// <remarks>
/// ADR-008: New interface for adaptive extraction (does NOT modify existing IFieldExtractor).
/// Uses ExtractedFields as return type to align with domain model.
/// </remarks>
public interface IAdaptiveDocxStrategy
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
