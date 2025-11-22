namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the person identity resolver service for resolving person identities by handling RFC variants and alias names.
/// </summary>
public interface IPersonIdentityResolver
{
    /// <summary>
    /// Resolves person identity by handling RFC variants and alias names.
    /// </summary>
    /// <param name="person">The person entity to resolve.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the resolved person with normalized identity or an error.</returns>
    Task<Result<Persona>> ResolveIdentityAsync(
        Persona person,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deduplicates person records across multiple documents for the same case.
    /// </summary>
    /// <param name="persons">The list of persons to deduplicate.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the deduplicated list of persons or an error.</returns>
    Task<Result<List<Persona>>> DeduplicatePersonsAsync(
        List<Persona> persons,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds existing person by RFC or RFC variants.
    /// </summary>
    /// <param name="rfc">The RFC to search for.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the found person or null if not found, or an error.</returns>
    Task<Result<Persona?>> FindByRfcAsync(
        string rfc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates RFC variants for identity resolution (e.g., "ABC123456789" → ["ABC123456789", "ABC-123456-789", "ABC 123456 789"]).
    /// </summary>
    /// <param name="rfc">The RFC to generate variants for.</param>
    /// <returns>A list of RFC variants.</returns>
    List<string> GenerateRfcVariants(string rfc);
}

