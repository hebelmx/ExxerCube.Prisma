namespace ExxerCube.Prisma.Application.Services;

/// <summary>
/// Represents the result of decision logic processing.
/// </summary>
public class DecisionLogicResult
{
    /// <summary>
    /// Gets or sets the resolved and deduplicated list of persons.
    /// </summary>
    public List<Persona> ResolvedPersons { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of compliance actions identified from legal directives.
    /// </summary>
    public List<ComplianceAction> ComplianceActions { get; set; } = new();
}