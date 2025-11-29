using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Classification;

/// <summary>
/// Service for resolving person identities by handling RFC variants and alias names.
/// </summary>
public class PersonIdentityResolverService : IPersonIdentityResolver
{
    private readonly ILogger<PersonIdentityResolverService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersonIdentityResolverService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public PersonIdentityResolverService(ILogger<PersonIdentityResolverService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<Result<Persona>> ResolveIdentityAsync(
        Persona person,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<Persona>.WithFailure("Operation was cancelled."));
        }

        if (person == null)
        {
            _logger.LogWarning("Person cannot be null for identity resolution");
            return Task.FromResult(Result<Persona>.WithFailure("Person cannot be null."));
        }

        try
        {
            _logger.LogDebug("Resolving identity for person: {Nombre} {Paterno} {Materno}, RFC: {Rfc}", 
                person.Nombre, person.Paterno, person.Materno, person.Rfc);

            // Generate RFC variants if RFC exists
            if (!string.IsNullOrWhiteSpace(person.Rfc))
            {
                person.RfcVariants = GenerateRfcVariants(person.Rfc);
                _logger.LogDebug("Generated {Count} RFC variants for {Rfc}", person.RfcVariants.Count, person.Rfc);
            }

            // Normalize name components
            person.Nombre = NormalizeName(person.Nombre);
            person.Paterno = NormalizeName(person.Paterno);
            person.Materno = NormalizeName(person.Materno);

            _logger.LogDebug("Identity resolved successfully for person: {Nombre}", person.Nombre);
            return Task.FromResult(Result<Persona>.Success(person));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving identity for person");
            return Task.FromResult(Result<Persona>.WithFailure($"Error resolving identity: {ex.Message}", default(Persona), ex));
        }
    }

    /// <inheritdoc />
    public Task<Result<List<Persona>>> DeduplicatePersonsAsync(
        List<Persona> persons,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<List<Persona>>.WithFailure("Operation was cancelled."));
        }

        if (persons == null)
        {
            _logger.LogWarning("Persons list cannot be null for deduplication");
            return Task.FromResult(Result<List<Persona>>.WithFailure("Persons list cannot be null."));
        }

        try
        {
            _logger.LogDebug("Deduplicating {Count} persons", persons.Count);

            var deduplicated = new List<Persona>();
            var processedRfcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var person in persons)
            {
                var isDuplicate = false;

                // Check RFC-based deduplication
                if (!string.IsNullOrWhiteSpace(person.Rfc))
                {
                    var rfcVariants = GenerateRfcVariants(person.Rfc);
                    var normalizedRfc = NormalizeRfcForComparison(person.Rfc);
                    
                    // Check if any variant or normalized RFC matches already processed RFCs
                    foreach (var variant in rfcVariants)
                    {
                        if (processedRfcs.Contains(variant))
                        {
                            _logger.LogDebug("Duplicate person found by RFC variant: {Rfc}", variant);
                            isDuplicate = true;
                            break;
                        }
                    }
                    
                    // Also check normalized RFC
                    if (!isDuplicate && processedRfcs.Contains(normalizedRfc))
                    {
                        _logger.LogDebug("Duplicate person found by normalized RFC: {Rfc}", normalizedRfc);
                        isDuplicate = true;
                    }

                    if (!isDuplicate)
                    {
                        foreach (var variant in rfcVariants)
                        {
                            processedRfcs.Add(variant);
                        }
                        processedRfcs.Add(normalizedRfc);
                    }
                }

                // Check name-based deduplication if RFC not available
                if (!isDuplicate && string.IsNullOrWhiteSpace(person.Rfc))
                {
                    var normalizedName = GetNormalizedName(person);
                    if (processedNames.Contains(normalizedName))
                    {
                        _logger.LogDebug("Duplicate person found by name: {Nombre}", normalizedName);
                        isDuplicate = true;
                    }
                    else
                    {
                        processedNames.Add(normalizedName);
                    }
                }

                if (!isDuplicate)
                {
                    deduplicated.Add(person);
                }
            }

            _logger.LogDebug("Deduplicated {OriginalCount} persons to {DeduplicatedCount}", persons.Count, deduplicated.Count);
            return Task.FromResult(Result<List<Persona>>.Success(deduplicated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deduplicating persons");
            return Task.FromResult(Result<List<Persona>>.WithFailure($"Error deduplicating persons: {ex.Message}", default(List<Persona>), ex));
        }
    }

    /// <inheritdoc />
    public Task<Result<Persona?>> FindByRfcAsync(
        string rfc,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<Persona?>.WithFailure("Operation was cancelled."));
        }

        if (string.IsNullOrWhiteSpace(rfc))
        {
            _logger.LogWarning("RFC cannot be null or empty for lookup");
            return Task.FromResult(Result<Persona?>.WithFailure("RFC cannot be null or empty."));
        }

        try
        {
            _logger.LogDebug("Finding person by RFC: {Rfc}", rfc);

            // Note: Database lookup implementation deferred to Infrastructure.Database layer
            // This method is designed to be extended with a repository pattern or database context injection
            // For now, returns null (not found) which is a valid result per the interface contract
            // TODO: Integrate with IPersonRepository or PrismaDbContext when database layer is ready
            _logger.LogDebug("RFC lookup completed (no database integration yet): {Rfc}", rfc);
            return Task.FromResult(Result<Persona?>.Success(null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding person by RFC: {Rfc}", rfc);
            return Task.FromResult(Result<Persona?>.WithFailure($"Error finding person by RFC: {ex.Message}", default(Persona?), ex));
        }
    }

    /// <inheritdoc />
    public List<string> GenerateRfcVariants(string rfc)
    {
        if (string.IsNullOrWhiteSpace(rfc))
        {
            return new List<string>();
        }

        var variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            rfc.Trim()
        };

        // Remove common separators and generate variants
        var cleaned = rfc.Replace("-", string.Empty)
                         .Replace(" ", string.Empty)
                         .Replace(".", string.Empty)
                         .Trim();

        if (!string.IsNullOrWhiteSpace(cleaned) && cleaned != rfc)
        {
            variants.Add(cleaned);
        }

        // Generate hyphenated variants for RFC format
        // Mexican RFC format: 3 letters + 6 digits (date) + 3 alphanumeric (homoclave)
        // Example: PEGJ850101ABC -> PEG-850101-ABC or PEGJ-850101-ABC
        if (cleaned.Length >= 12)
        {
            // Standard format: first 3-4 chars, then 6 digits, then remaining
            if (cleaned.Length == 12)
            {
                // 12 chars: 3 letters + 6 digits + 3 chars (e.g., PEG850101ABC)
                var hyphenated = $"{cleaned.Substring(0, 3)}-{cleaned.Substring(3, 6)}-{cleaned.Substring(9)}";
                variants.Add(hyphenated);
            }
            else if (cleaned.Length == 13)
            {
                // 13 chars: 3 letters + 1 letter + 6 digits + 3 chars (e.g., PEGJ850101ABC)
                // Generate format skipping middle letter: PEG-850101-ABC
                if (char.IsLetter(cleaned[3]))
                {
                    var hyphenated = $"{cleaned.Substring(0, 3)}-{cleaned.Substring(4, 6)}-{cleaned.Substring(10)}";
                    variants.Add(hyphenated);
                }
            }
        }

        // Generate space-separated variant
        if (cleaned.Length >= 12)
        {
            if (cleaned.Length == 12)
            {
                var spaced = $"{cleaned.Substring(0, 3)} {cleaned.Substring(3, 6)} {cleaned.Substring(9)}";
                variants.Add(spaced);
            }
            else if (cleaned.Length == 13 && char.IsLetter(cleaned[3]))
            {
                var spaced = $"{cleaned.Substring(0, 3)} {cleaned.Substring(4, 6)} {cleaned.Substring(10)}";
                variants.Add(spaced);
            }
        }

        return variants.ToList();
    }

    private static string NormalizeRfcForComparison(string rfc)
    {
        if (string.IsNullOrWhiteSpace(rfc))
        {
            return string.Empty;
        }

        // Remove all separators and normalize
        var cleaned = rfc.Replace("-", string.Empty)
                         .Replace(" ", string.Empty)
                         .Replace(".", string.Empty)
                         .Trim();

        // For 13-character RFCs, remove the middle letter (position 3) for comparison
        // This allows "PEGJ850101ABC" and "PEG-850101-ABC" to match
        if (cleaned.Length == 13 && char.IsLetter(cleaned[3]))
        {
            cleaned = cleaned.Substring(0, 3) + cleaned.Substring(4);
        }

        return cleaned;
    }

    private static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        // Remove extra whitespace and normalize
        return string.Join(" ", name.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            .Trim();
    }

    private static string GetNormalizedName(Persona person)
    {
        var parts = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(person.Nombre))
        {
            parts.Add(person.Nombre.Trim());
        }
        
        if (!string.IsNullOrWhiteSpace(person.Paterno))
        {
            parts.Add(person.Paterno.Trim());
        }
        
        if (!string.IsNullOrWhiteSpace(person.Materno))
        {
            parts.Add(person.Materno.Trim());
        }

        return string.Join(" ", parts).ToUpperInvariant();
    }
}

