namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;

/// <summary>
/// Adaptive DOCX extractor orchestrator that coordinates multiple extraction strategies.
/// </summary>
/// <remarks>
/// <para>
/// Orchestrates multiple <see cref="IAdaptiveDocxStrategy"/> implementations to provide
/// robust extraction across diverse document formats. Supports three extraction modes:
/// </para>
/// <list type="bullet">
///   <item><description><strong>BestStrategy:</strong> Selects highest confidence strategy and uses it exclusively</description></item>
///   <item><description><strong>MergeAll:</strong> Runs all capable strategies and merges their results</description></item>
///   <item><description><strong>Complement:</strong> Fills gaps in existing extraction using new extraction</description></item>
/// </list>
/// </remarks>
public sealed class AdaptiveDocxExtractor : IAdaptiveDocxExtractor
{
    private readonly IReadOnlyList<IAdaptiveDocxStrategy> _strategies;
    private readonly ILogger<AdaptiveDocxExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveDocxExtractor"/> class.
    /// </summary>
    /// <param name="strategies">Collection of extraction strategies to orchestrate.</param>
    /// <param name="logger">Logger instance for diagnostics.</param>
    public AdaptiveDocxExtractor(
        IReadOnlyList<IAdaptiveDocxStrategy> strategies,
        ILogger<AdaptiveDocxExtractor> logger)
    {
        _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_strategies.Count == 0)
        {
            throw new ArgumentException("At least one strategy must be provided.", nameof(strategies));
        }
    }

    /// <inheritdoc />
    public async Task<ExtractedFields?> ExtractAsync(
        string docxText,
        ExtractionMode mode = ExtractionMode.BestStrategy,
        ExtractedFields? existingFields = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(docxText))
        {
            _logger.LogDebug("AdaptiveDocxExtractor: Empty document text, returning null");
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _logger.LogDebug("AdaptiveDocxExtractor: Extracting from document ({Length} chars) using {Mode} mode",
                docxText.Length, mode);

            return mode switch
            {
                ExtractionMode.BestStrategy => await ExtractBestStrategyAsync(docxText, cancellationToken),
                ExtractionMode.MergeAll => await ExtractMergeAllAsync(docxText, cancellationToken),
                ExtractionMode.Complement => await ExtractComplementAsync(docxText, existingFields, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported extraction mode")
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("AdaptiveDocxExtractor: Extraction cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdaptiveDocxExtractor: Extraction error");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StrategyConfidence>> GetStrategyConfidencesAsync(
        string docxText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(docxText))
        {
            _logger.LogDebug("AdaptiveDocxExtractor: Empty document text, returning empty confidences");
            return _strategies.Select(s => new StrategyConfidence(s.StrategyName, 0)).ToList();
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _logger.LogDebug("AdaptiveDocxExtractor: Getting strategy confidences");

            var confidenceTasks = _strategies
                .Select(async strategy =>
                {
                    var confidence = await strategy.GetConfidenceAsync(docxText, cancellationToken);
                    return new StrategyConfidence(strategy.StrategyName, confidence);
                })
                .ToList();

            var confidences = await Task.WhenAll(confidenceTasks);

            // Order by confidence descending
            var ordered = confidences.OrderByDescending(c => c.Confidence).ToList();

            _logger.LogInformation("AdaptiveDocxExtractor: Strategy confidences - {Confidences}",
                string.Join(", ", ordered.Select(c => $"{c.StrategyName}:{c.Confidence}")));

            return ordered;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("AdaptiveDocxExtractor: GetStrategyConfidences cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdaptiveDocxExtractor: GetStrategyConfidences error");
            return _strategies.Select(s => new StrategyConfidence(s.StrategyName, 0)).ToList();
        }
    }

    //
    // Extraction Mode Implementations
    //

    private async Task<ExtractedFields?> ExtractBestStrategyAsync(string docxText, CancellationToken cancellationToken)
    {
        _logger.LogDebug("AdaptiveDocxExtractor: Using BestStrategy mode");

        // Get confidences and select best strategy
        var confidences = await GetStrategyConfidencesAsync(docxText, cancellationToken);

        var best = confidences.FirstOrDefault();
        if (best == null || best.Confidence == 0)
        {
            _logger.LogDebug("AdaptiveDocxExtractor: No strategy has confidence > 0");
            return null;
        }

        var bestStrategy = _strategies.First(s => s.StrategyName == best.StrategyName);
        _logger.LogInformation("AdaptiveDocxExtractor: Selected strategy '{StrategyName}' with confidence {Confidence}",
            best.StrategyName, best.Confidence);

        // Extract using best strategy
        var result = await bestStrategy.ExtractAsync(docxText, cancellationToken);

        return result;
    }

    private async Task<ExtractedFields?> ExtractMergeAllAsync(string docxText, CancellationToken cancellationToken)
    {
        _logger.LogDebug("AdaptiveDocxExtractor: Using MergeAll mode");

        // Get confidences to find capable strategies
        var confidences = await GetStrategyConfidencesAsync(docxText, cancellationToken);
        var capableStrategies = confidences
            .Where(c => c.Confidence > 0)
            .Select(c => _strategies.First(s => s.StrategyName == c.StrategyName))
            .ToList();

        if (capableStrategies.Count == 0)
        {
            _logger.LogDebug("AdaptiveDocxExtractor: No capable strategies found");
            return null;
        }

        _logger.LogInformation("AdaptiveDocxExtractor: Running {Count} capable strategies",
            capableStrategies.Count);

        // Extract from all capable strategies
        var extractionTasks = capableStrategies
            .Select(strategy => strategy.ExtractAsync(docxText, cancellationToken))
            .ToList();

        var results = await Task.WhenAll(extractionTasks);

        // Merge all non-null results
        var nonNullResults = results.Where(r => r != null).Cast<ExtractedFields>().ToList();

        if (nonNullResults.Count == 0)
        {
            _logger.LogDebug("AdaptiveDocxExtractor: All strategies returned null");
            return null;
        }

        var merged = MergeExtractedFields(nonNullResults);

        _logger.LogInformation("AdaptiveDocxExtractor: Merged {Count} extraction results",
            nonNullResults.Count);

        return merged;
    }

    private async Task<ExtractedFields?> ExtractComplementAsync(
        string docxText,
        ExtractedFields? existingFields,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("AdaptiveDocxExtractor: Using Complement mode");

        // If no existing fields, use BestStrategy
        if (existingFields == null)
        {
            _logger.LogDebug("AdaptiveDocxExtractor: No existing fields provided, using BestStrategy");
            return await ExtractBestStrategyAsync(docxText, cancellationToken);
        }

        // Extract new fields using BestStrategy
        var newExtraction = await ExtractBestStrategyAsync(docxText, cancellationToken);

        if (newExtraction == null)
        {
            _logger.LogDebug("AdaptiveDocxExtractor: No new extraction available, returning existing fields");
            return existingFields;
        }

        // Complement existing fields with new extraction (preserve existing, fill gaps)
        var complemented = ComplementFields(existingFields, newExtraction);

        _logger.LogInformation("AdaptiveDocxExtractor: Complemented existing fields with new extraction");

        return complemented;
    }

    //
    // Field Merging Logic
    //

    private ExtractedFields MergeExtractedFields(IReadOnlyList<ExtractedFields> fieldSets)
    {
        var merged = new ExtractedFields();

        foreach (var fieldSet in fieldSets)
        {
            // Merge core fields (first non-null wins)
            if (string.IsNullOrEmpty(merged.Expediente) && !string.IsNullOrEmpty(fieldSet.Expediente))
            {
                merged.Expediente = fieldSet.Expediente;
            }

            if (string.IsNullOrEmpty(merged.Causa) && !string.IsNullOrEmpty(fieldSet.Causa))
            {
                merged.Causa = fieldSet.Causa;
            }

            if (string.IsNullOrEmpty(merged.AccionSolicitada) && !string.IsNullOrEmpty(fieldSet.AccionSolicitada))
            {
                merged.AccionSolicitada = fieldSet.AccionSolicitada;
            }

            // Merge additional fields
            foreach (var kvp in fieldSet.AdditionalFields)
            {
                if (!merged.AdditionalFields.ContainsKey(kvp.Key))
                {
                    merged.AdditionalFields[kvp.Key] = kvp.Value;
                }
            }

            // Merge montos (combine unique amounts)
            foreach (var monto in fieldSet.Montos)
            {
                if (!merged.Montos.Any(m => m.Currency == monto.Currency && m.Value == monto.Value))
                {
                    merged.Montos.Add(monto);
                }
            }

            // Merge fechas (combine unique dates)
            foreach (var fecha in fieldSet.Fechas)
            {
                if (!merged.Fechas.Contains(fecha))
                {
                    merged.Fechas.Add(fecha);
                }
            }
        }

        return merged;
    }

    private ExtractedFields ComplementFields(ExtractedFields existing, ExtractedFields newExtraction)
    {
        var complemented = new ExtractedFields
        {
            // Preserve existing core fields
            Expediente = existing.Expediente ?? newExtraction.Expediente,
            Causa = existing.Causa ?? newExtraction.Causa,
            AccionSolicitada = existing.AccionSolicitada ?? newExtraction.AccionSolicitada
        };

        // Copy existing additional fields
        foreach (var kvp in existing.AdditionalFields)
        {
            complemented.AdditionalFields[kvp.Key] = kvp.Value;
        }

        // Add new additional fields (only if not already present)
        foreach (var kvp in newExtraction.AdditionalFields)
        {
            if (!complemented.AdditionalFields.ContainsKey(kvp.Key))
            {
                complemented.AdditionalFields[kvp.Key] = kvp.Value;
            }
        }

        // Copy existing montos
        foreach (var monto in existing.Montos)
        {
            complemented.Montos.Add(monto);
        }

        // Add new montos (only unique)
        foreach (var monto in newExtraction.Montos)
        {
            if (!complemented.Montos.Any(m => m.Currency == monto.Currency && m.Value == monto.Value))
            {
                complemented.Montos.Add(monto);
            }
        }

        // Copy existing fechas
        foreach (var fecha in existing.Fechas)
        {
            complemented.Fechas.Add(fecha);
        }

        // Add new fechas (only unique)
        foreach (var fecha in newExtraction.Fechas)
        {
            if (!complemented.Fechas.Contains(fecha))
            {
                complemented.Fechas.Add(fecha);
            }
        }

        return complemented;
    }
}
