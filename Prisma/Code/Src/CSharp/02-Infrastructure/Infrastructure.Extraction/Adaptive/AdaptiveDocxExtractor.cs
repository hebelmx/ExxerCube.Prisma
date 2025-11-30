// <copyright file="AdaptiveDocxExtractor.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;

/// <summary>
/// Adaptive DOCX extractor that intelligently selects the best extraction strategy.
/// </summary>
/// <remarks>
/// Strategy selection logic:
/// 1. Analyze document structure (DocxStructureAnalyzer)
/// 2. Query all strategies for CanHandle() confidence
/// 3. Select strategy with highest confidence
/// 4. If cross-references detected, also run SearchStrategy
/// 5. If used as complement, run ComplementStrategy
/// 6. Merge results from multiple strategies.
/// </remarks>
public class AdaptiveDocxExtractor
{
    private readonly DocxStructureAnalyzer _analyzer;
    private readonly IEnumerable<IDocxExtractionStrategy> _strategies;
    private readonly ILogger<AdaptiveDocxExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveDocxExtractor"/> class.
    /// </summary>
    /// <param name="analyzer">Document structure analyzer.</param>
    /// <param name="strategies">Available extraction strategies.</param>
    /// <param name="logger">Logger instance.</param>
    public AdaptiveDocxExtractor(
        DocxStructureAnalyzer analyzer,
        IEnumerable<IDocxExtractionStrategy> strategies,
        ILogger<AdaptiveDocxExtractor> logger)
    {
        _analyzer = analyzer;
        _strategies = strategies;
        _logger = logger;
    }

    /// <summary>
    /// Extracts expediente from DOCX text using adaptive strategy selection.
    /// </summary>
    /// <param name="text">The full DOCX text content.</param>
    /// <param name="mode">Extraction mode (Primary or Complement).</param>
    /// <returns>Extracted expediente data.</returns>
    public Expediente? Extract(string text, ExtractionMode mode = ExtractionMode.Primary)
    {
        try
        {
            // Analyze document structure
            var structure = _analyzer.Analyze(text);
            _logger.LogDebug("Document structure: Labels={LabelCount}, Tables={HasTables}, CrossRefs={HasCrossRefs}",
                structure.LabelCount, structure.HasTableStructure, structure.HasCrossReferences);

            // Select strategies based on mode
            if (mode == ExtractionMode.Complement)
            {
                // Complement mode: use ComplementStrategy only
                return ExtractWithComplement(text);
            }

            if (structure.HasCrossReferences)
            {
                // Cross-reference mode: use SearchStrategy + primary strategy
                return ExtractWithSearch(text, structure);
            }

            // Primary mode: select best strategy
            return ExtractWithBestStrategy(text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AdaptiveDocxExtractor");
            return null;
        }
    }

    /// <summary>
    /// Extracts using complement strategy (fills XML/OCR gaps).
    /// </summary>
    private Expediente? ExtractWithComplement(string text)
    {
        var complementStrategy = _strategies.FirstOrDefault(s => s.StrategyType == DocxExtractionStrategyType.Complement);
        if (complementStrategy == null)
        {
            _logger.LogWarning("ComplementStrategy not found");
            return null;
        }

        _logger.LogInformation("Using ComplementStrategy (filling XML/OCR gaps)");
        return complementStrategy.Extract(text);
    }

    /// <summary>
    /// Extracts using search strategy for cross-references + best primary strategy.
    /// </summary>
    private Expediente? ExtractWithSearch(string text, DocxStructureInfo structure)
    {
        var searchStrategy = _strategies.FirstOrDefault(s => s.StrategyType == DocxExtractionStrategyType.Search);
        var searchResult = searchStrategy?.Extract(text);

        // Also run best primary strategy
        var primaryResult = ExtractWithBestStrategy(text);

        // Merge results (search takes precedence for resolved cross-references)
        return MergeResults(searchResult, primaryResult);
    }

    /// <summary>
    /// Extracts using the best available strategy based on confidence scores.
    /// </summary>
    private Expediente? ExtractWithBestStrategy(string text)
    {
        // Query all strategies (except Complement and Search which are special)
        var strategyScores = _strategies
            .Where(s => s.StrategyType != DocxExtractionStrategyType.Complement &&
                       s.StrategyType != DocxExtractionStrategyType.Search)
            .Select(s => new { Strategy = s, Confidence = s.CanHandle(text) })
            .OrderByDescending(x => x.Confidence)
            .ToList();

        if (strategyScores.Count == 0)
        {
            _logger.LogWarning("No extraction strategies available");
            return null;
        }

        var best = strategyScores.First();
        _logger.LogInformation("Selected strategy: {StrategyType} (confidence: {Confidence}%)",
            best.Strategy.StrategyType, best.Confidence);

        return best.Strategy.Extract(text);
    }

    /// <summary>
    /// Merges results from multiple strategies.
    /// Priority: primary (non-null values), then fallback.
    /// </summary>
    private static Expediente? MergeResults(Expediente? primary, Expediente? fallback)
    {
        if (primary == null)
            return fallback;
        if (fallback == null)
            return primary;

        // Merge: primary takes precedence for non-null values
        return new Expediente
        {
            NumeroExpediente = primary.NumeroExpediente ?? fallback.NumeroExpediente,
            NumeroOficio = primary.NumeroOficio ?? fallback.NumeroOficio,
            Cuenta = primary.Cuenta ?? fallback.Cuenta,
            NombreCompleto = primary.NombreCompleto ?? fallback.NombreCompleto,
            RFC = primary.RFC ?? fallback.RFC,
            CLABE = primary.CLABE ?? fallback.CLABE,
            Monto = primary.Monto ?? fallback.Monto,
            Banco = primary.Banco ?? fallback.Banco,
        };
    }
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
    /// </summary>
    Complement,
}
