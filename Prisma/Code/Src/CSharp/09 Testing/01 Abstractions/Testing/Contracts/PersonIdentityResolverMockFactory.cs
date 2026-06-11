using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using IndQuestResults;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Builds the contract-conforming reference fake that backs the blueprint instance of
/// <see cref="PersonIdentityResolverContract"/>.
/// </summary>
/// <remarks>
/// The contract asserts real outcomes (RFC-variant generation, cross-format dedup, name normalisation,
/// the FindByRfc stub), which canned stubs cannot satisfy. Per ADR-005 §6 the factory is a
/// <em>reference fake</em>: a hand-written <see cref="IPersonIdentityResolver"/> implementing the
/// documented semantics (the same algorithm the production <c>PersonIdentityResolverService</c> uses,
/// minus logging). It is the executable design specification.
/// </remarks>
public static class PersonIdentityResolverMockFactory
{
    /// <summary>
    /// Creates an <see cref="IPersonIdentityResolver"/> reference fake that satisfies every test in
    /// <see cref="PersonIdentityResolverContract"/>.
    /// </summary>
    /// <returns>The configured reference fake.</returns>
    public static IPersonIdentityResolver CreateContractConformingMock()
        => new ReferencePersonIdentityResolver();

    private sealed class ReferencePersonIdentityResolver : IPersonIdentityResolver
    {
        public Task<Result<Persona>> ResolveIdentityAsync(Persona person, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(Result<Persona>.WithFailure("Operation was cancelled."));
            }

            if (person == null)
            {
                return Task.FromResult(Result<Persona>.WithFailure("Person cannot be null."));
            }

            if (!string.IsNullOrWhiteSpace(person.Rfc))
            {
                person.RfcVariants = GenerateRfcVariants(person.Rfc);
            }

            person.Nombre = NormalizeName(person.Nombre);
            person.Paterno = NormalizeName(person.Paterno);
            person.Materno = NormalizeName(person.Materno);

            return Task.FromResult(Result<Persona>.Success(person));
        }

        public Task<Result<List<Persona>>> DeduplicatePersonsAsync(List<Persona> persons, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(Result<List<Persona>>.WithFailure("Operation was cancelled."));
            }

            if (persons == null)
            {
                return Task.FromResult(Result<List<Persona>>.WithFailure("Persons list cannot be null."));
            }

            var deduplicated = new List<Persona>();
            var processedRfcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var person in persons)
            {
                var isDuplicate = false;

                if (!string.IsNullOrWhiteSpace(person.Rfc))
                {
                    var rfcVariants = GenerateRfcVariants(person.Rfc);
                    var normalizedRfc = NormalizeRfcForComparison(person.Rfc);

                    if (rfcVariants.Any(processedRfcs.Contains) || processedRfcs.Contains(normalizedRfc))
                    {
                        isDuplicate = true;
                    }
                    else
                    {
                        foreach (var variant in rfcVariants)
                        {
                            processedRfcs.Add(variant);
                        }

                        processedRfcs.Add(normalizedRfc);
                    }
                }

                if (!isDuplicate && string.IsNullOrWhiteSpace(person.Rfc))
                {
                    var normalizedName = GetNormalizedName(person);
                    if (!processedNames.Add(normalizedName))
                    {
                        isDuplicate = true;
                    }
                }

                if (!isDuplicate)
                {
                    deduplicated.Add(person);
                }
            }

            return Task.FromResult(Result<List<Persona>>.Success(deduplicated));
        }

        public Task<Result<Persona?>> FindByRfcAsync(string rfc, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(Result<Persona?>.WithFailure("Operation was cancelled."));
            }

            if (string.IsNullOrWhiteSpace(rfc))
            {
                return Task.FromResult(Result<Persona?>.WithFailure("RFC cannot be null or empty."));
            }

            // Documented stub: no database integration yet → success-with-null.
            return Task.FromResult(Result<Persona?>.Success(null));
        }

        public List<string> GenerateRfcVariants(string rfc)
        {
            if (string.IsNullOrWhiteSpace(rfc))
            {
                return new List<string>();
            }

            var variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rfc.Trim() };

            var cleaned = rfc.Replace("-", string.Empty)
                             .Replace(" ", string.Empty)
                             .Replace(".", string.Empty)
                             .Trim();

            if (!string.IsNullOrWhiteSpace(cleaned) && cleaned != rfc)
            {
                variants.Add(cleaned);
            }

            if (cleaned.Length == 12)
            {
                variants.Add($"{cleaned.Substring(0, 3)}-{cleaned.Substring(3, 6)}-{cleaned.Substring(9)}");
                variants.Add($"{cleaned.Substring(0, 3)} {cleaned.Substring(3, 6)} {cleaned.Substring(9)}");
            }
            else if (cleaned.Length == 13 && char.IsLetter(cleaned[3]))
            {
                variants.Add($"{cleaned.Substring(0, 3)}-{cleaned.Substring(4, 6)}-{cleaned.Substring(10)}");
                variants.Add($"{cleaned.Substring(0, 3)} {cleaned.Substring(4, 6)} {cleaned.Substring(10)}");
            }

            return variants.ToList();
        }

        private static string NormalizeRfcForComparison(string rfc)
        {
            if (string.IsNullOrWhiteSpace(rfc))
            {
                return string.Empty;
            }

            var cleaned = rfc.Replace("-", string.Empty)
                             .Replace(" ", string.Empty)
                             .Replace(".", string.Empty)
                             .Trim();

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

            return string.Join(" ", name.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
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
}
