namespace ExxerCube.Prisma.Infrastructure.Extraction.Adaptive;

/// <summary>
/// Enhanced field merge strategy with conflict detection and resolution.
/// </summary>
/// <remarks>
/// <para>
/// Merges extracted fields from multiple sources with sophisticated conflict detection,
/// resolution tracking, and comprehensive metadata. Supports both multi-source merging
/// and two-source (primary/secondary) merging.
/// </para>
/// <para>
/// <strong>Merge Semantics:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>First non-null value wins for core fields (Expediente, Causa, AccionSolicitada)</description></item>
///   <item><description>Collections are combined (Montos, Fechas, AdditionalFields)</description></item>
///   <item><description>Conflicts detected when same field has different non-null values</description></item>
///   <item><description>Two-parameter overload: Primary source wins on conflicts</description></item>
/// </list>
/// </remarks>
public sealed class EnhancedFieldMergeStrategy : IFieldMergeStrategy
{
    private readonly ILogger<EnhancedFieldMergeStrategy> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnhancedFieldMergeStrategy"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostics.</param>
    public EnhancedFieldMergeStrategy(ILogger<EnhancedFieldMergeStrategy> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<MergeResult> MergeAsync(
        IReadOnlyList<ExtractedFields?> fieldSets,
        CancellationToken cancellationToken = default)
    {
        if (fieldSets == null)
        {
            throw new ArgumentNullException(nameof(fieldSets));
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _logger.LogDebug("EnhancedFieldMerge: Merging {Count} field sets", fieldSets.Count);

            var result = new MergeResult();
            var nonNullSets = fieldSets.Where(f => f != null).Cast<ExtractedFields>().ToList();

            result.SourceCount = nonNullSets.Count;

            if (nonNullSets.Count == 0)
            {
                _logger.LogDebug("EnhancedFieldMerge: All field sets are null");
                return Task.FromResult(result);
            }

            // Merge core fields with conflict detection
            MergeCoreFieldsWithConflicts(nonNullSets, result);

            cancellationToken.ThrowIfCancellationRequested();

            // Merge collections
            MergeCollections(nonNullSets, result);

            _logger.LogInformation("EnhancedFieldMerge: Merged {SourceCount} sources, detected {ConflictCount} conflicts",
                result.SourceCount, result.Conflicts.Count);

            return Task.FromResult(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("EnhancedFieldMerge: Merge cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EnhancedFieldMerge: Merge error");
            throw;
        }
    }

    /// <inheritdoc />
    public Task<MergeResult> MergeAsync(
        ExtractedFields? primary,
        ExtractedFields? secondary,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _logger.LogDebug("EnhancedFieldMerge: Merging primary and secondary sources");

            var result = new MergeResult();

            // Build list with primary first (so it wins on conflicts)
            var fieldSets = new List<ExtractedFields?>();

            if (primary != null)
            {
                fieldSets.Add(primary);
                result.SourceCount++;
            }

            if (secondary != null)
            {
                fieldSets.Add(secondary);
                result.SourceCount++;
            }

            if (fieldSets.Count == 0)
            {
                _logger.LogDebug("EnhancedFieldMerge: Both primary and secondary are null");
                return Task.FromResult(result);
            }

            var nonNullSets = fieldSets.Where(f => f != null).Cast<ExtractedFields>().ToList();

            // Merge with primary preference
            MergeCoreFieldsWithPrimaryPreference(primary, secondary, result);

            cancellationToken.ThrowIfCancellationRequested();

            // Merge collections
            MergeCollections(nonNullSets, result);

            _logger.LogInformation("EnhancedFieldMerge: Merged primary+secondary, detected {ConflictCount} conflicts",
                result.Conflicts.Count);

            return Task.FromResult(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("EnhancedFieldMerge: Merge cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EnhancedFieldMerge: Merge error");
            throw;
        }
    }

    //
    // Core Field Merging
    //

    private void MergeCoreFieldsWithConflicts(List<ExtractedFields> fieldSets, MergeResult result)
    {
        // Expediente
        var expedienteValues = fieldSets
            .Select(f => f.Expediente)
            .Where(v => !string.IsNullOrEmpty(v))
            .Distinct()
            .ToList();

        if (expedienteValues.Count > 0)
        {
            result.MergedFields.Expediente = expedienteValues[0];
            result.MergedFieldNames.Add("Expediente");

            if (expedienteValues.Count > 1)
            {
                result.Conflicts.Add(new FieldConflict
                {
                    FieldName = "Expediente",
                    ConflictingValues = expedienteValues!,
                    ResolvedValue = expedienteValues[0],
                    ResolutionStrategy = "First non-null value"
                });
            }
        }

        // Causa
        var causaValues = fieldSets
            .Select(f => f.Causa)
            .Where(v => !string.IsNullOrEmpty(v))
            .Distinct()
            .ToList();

        if (causaValues.Count > 0)
        {
            result.MergedFields.Causa = causaValues[0];
            result.MergedFieldNames.Add("Causa");

            if (causaValues.Count > 1)
            {
                result.Conflicts.Add(new FieldConflict
                {
                    FieldName = "Causa",
                    ConflictingValues = causaValues!,
                    ResolvedValue = causaValues[0],
                    ResolutionStrategy = "First non-null value"
                });
            }
        }

        // AccionSolicitada
        var accionValues = fieldSets
            .Select(f => f.AccionSolicitada)
            .Where(v => !string.IsNullOrEmpty(v))
            .Distinct()
            .ToList();

        if (accionValues.Count > 0)
        {
            result.MergedFields.AccionSolicitada = accionValues[0];
            result.MergedFieldNames.Add("AccionSolicitada");

            if (accionValues.Count > 1)
            {
                result.Conflicts.Add(new FieldConflict
                {
                    FieldName = "AccionSolicitada",
                    ConflictingValues = accionValues!,
                    ResolvedValue = accionValues[0],
                    ResolutionStrategy = "First non-null value"
                });
            }
        }
    }

    private void MergeCoreFieldsWithPrimaryPreference(
        ExtractedFields? primary,
        ExtractedFields? secondary,
        MergeResult result)
    {
        // Expediente (primary wins)
        if (!string.IsNullOrEmpty(primary?.Expediente))
        {
            result.MergedFields.Expediente = primary.Expediente;
            result.MergedFieldNames.Add("Expediente");

            if (!string.IsNullOrEmpty(secondary?.Expediente) && primary.Expediente != secondary.Expediente)
            {
                result.Conflicts.Add(new FieldConflict
                {
                    FieldName = "Expediente",
                    ConflictingValues = new List<string> { primary.Expediente, secondary.Expediente },
                    ResolvedValue = primary.Expediente,
                    ResolutionStrategy = "Primary source preference"
                });
            }
        }
        else if (!string.IsNullOrEmpty(secondary?.Expediente))
        {
            result.MergedFields.Expediente = secondary.Expediente;
            result.MergedFieldNames.Add("Expediente");
        }

        // Causa (primary wins)
        if (!string.IsNullOrEmpty(primary?.Causa))
        {
            result.MergedFields.Causa = primary.Causa;
            result.MergedFieldNames.Add("Causa");

            if (!string.IsNullOrEmpty(secondary?.Causa) && primary.Causa != secondary.Causa)
            {
                result.Conflicts.Add(new FieldConflict
                {
                    FieldName = "Causa",
                    ConflictingValues = new List<string> { primary.Causa, secondary.Causa },
                    ResolvedValue = primary.Causa,
                    ResolutionStrategy = "Primary source preference"
                });
            }
        }
        else if (!string.IsNullOrEmpty(secondary?.Causa))
        {
            result.MergedFields.Causa = secondary.Causa;
            result.MergedFieldNames.Add("Causa");
        }

        // AccionSolicitada (primary wins)
        if (!string.IsNullOrEmpty(primary?.AccionSolicitada))
        {
            result.MergedFields.AccionSolicitada = primary.AccionSolicitada;
            result.MergedFieldNames.Add("AccionSolicitada");

            if (!string.IsNullOrEmpty(secondary?.AccionSolicitada) && primary.AccionSolicitada != secondary.AccionSolicitada)
            {
                result.Conflicts.Add(new FieldConflict
                {
                    FieldName = "AccionSolicitada",
                    ConflictingValues = new List<string> { primary.AccionSolicitada, secondary.AccionSolicitada },
                    ResolvedValue = primary.AccionSolicitada,
                    ResolutionStrategy = "Primary source preference"
                });
            }
        }
        else if (!string.IsNullOrEmpty(secondary?.AccionSolicitada))
        {
            result.MergedFields.AccionSolicitada = secondary.AccionSolicitada;
            result.MergedFieldNames.Add("AccionSolicitada");
        }
    }

    //
    // Collection Merging
    //

    private void MergeCollections(List<ExtractedFields> fieldSets, MergeResult result)
    {
        foreach (var fieldSet in fieldSets)
        {
            // Merge AdditionalFields
            foreach (var kvp in fieldSet.AdditionalFields)
            {
                if (!result.MergedFields.AdditionalFields.ContainsKey(kvp.Key))
                {
                    result.MergedFields.AdditionalFields[kvp.Key] = kvp.Value;
                    result.MergedFieldNames.Add($"AdditionalFields.{kvp.Key}");
                }
            }

            // Merge Montos (combine unique amounts)
            foreach (var monto in fieldSet.Montos)
            {
                if (!result.MergedFields.Montos.Any(m => m.Currency == monto.Currency && m.Value == monto.Value))
                {
                    result.MergedFields.Montos.Add(monto);
                }
            }

            // Merge Fechas (combine unique dates)
            foreach (var fecha in fieldSet.Fechas)
            {
                if (!result.MergedFields.Fechas.Contains(fecha))
                {
                    result.MergedFields.Fechas.Add(fecha);
                }
            }
        }
    }
}
