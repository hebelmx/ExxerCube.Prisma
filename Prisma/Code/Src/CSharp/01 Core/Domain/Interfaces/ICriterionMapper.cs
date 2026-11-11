namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the criterion mapper service for mapping compliance requirements to SIRO regulatory criteria.
/// </summary>
public interface ICriterionMapper
{
    /// <summary>
    /// Maps compliance requirements to SIRO regulatory criteria.
    /// </summary>
    /// <param name="requirements">The list of compliance requirements to map.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the mapped SIRO criteria or an error.</returns>
    Task<Result<Dictionary<string, object>>> MapToSiroCriteriaAsync(
        List<ComplianceRequirement> requirements,
        CancellationToken cancellationToken = default);
}

