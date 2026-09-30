using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using IndQuestResults.Operations;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Export;

/// <summary>
/// Implements criterion mapper service for mapping compliance requirements to SIRO regulatory criteria.
/// </summary>
public class CriterionMapperService : ICriterionMapper
{
    private readonly ILogger<CriterionMapperService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CriterionMapperService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public CriterionMapperService(ILogger<CriterionMapperService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, object>>> MapToSiroCriteriaAsync(
        List<ComplianceRequirement> requirements,
        CancellationToken cancellationToken = default)
    {
        // Early cancellation check
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Criterion mapping cancelled before starting");
            return ResultExtensions.Cancelled<Dictionary<string, object>>();
        }

        // Input validation
        if (requirements == null)
        {
            return Result<Dictionary<string, object>>.WithFailure("Requirements cannot be null");
        }

        try
        {
            _logger.LogInformation("Mapping {Count} compliance requirements to SIRO criteria", requirements.Count);

            var siroCriteria = new Dictionary<string, object>();

            // Map each requirement to SIRO criterion format
            foreach (var requirement in requirements)
            {
                var criterionKey = $"Criterion_{requirement.RequerimientoId}";
                var criterionValue = new Dictionary<string, object>
                {
                    { "RequerimientoId", requirement.RequerimientoId },
                    { "Descripcion", requirement.Descripcion },
                    { "Tipo", requirement.Tipo },
                    { "EsObligatorio", requirement.EsObligatorio }
                };

                siroCriteria[criterionKey] = criterionValue;
            }

            // Add summary information
            siroCriteria["TotalRequirements"] = requirements.Count;
            siroCriteria["MappedAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            _logger.LogInformation("Successfully mapped {Count} requirements to SIRO criteria", requirements.Count);
            return await Task.FromResult(Result<Dictionary<string, object>>.Success(siroCriteria)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Criterion mapping cancelled");
            return ResultExtensions.Cancelled<Dictionary<string, object>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mapping requirements to SIRO criteria");
            return Result<Dictionary<string, object>>.WithFailure($"Error mapping to SIRO criteria: {ex.Message}", default(Dictionary<string, object>), ex);
        }
    }
}

