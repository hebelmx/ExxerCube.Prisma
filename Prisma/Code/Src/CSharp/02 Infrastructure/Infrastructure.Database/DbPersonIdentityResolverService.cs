namespace ExxerCube.Prisma.Infrastructure.Database;

/// <summary>
/// DB-backed implementation of <see cref="IPersonIdentityResolver"/> that persists and deduplicates
/// <see cref="Persona"/> records by RFC across documents (FR10 / G-H5).
/// </summary>
/// <remarks>
/// <para>
/// RFC normalisation: separators (hyphens, spaces, dots) are stripped and casing is folded to
/// upper-case before any comparison. A 13-char RFC with a name-discriminant letter at position 3
/// (e.g. "PEGJ850101ABC") is also stored alongside a 12-char canonical form ("PEG850101ABC") in
/// <see cref="Persona.RfcVariants"/> so that documents presenting either format resolve to the
/// same row.
/// </para>
/// <para>
/// Dedup persistence contract: <see cref="FindOrCreateAsync"/> is the authoritative
/// "find or persist" entry point. Concurrent callers targeting the same RFC are protected by an
/// optimistic retry — if a second caller races in between the "not found" check and the INSERT,
/// the unique-index violation is caught and the winner's row is returned instead.
/// </para>
/// <para>
/// Scoped lifetime: this service is registered as <c>Scoped</c> (same scope as
/// <see cref="PrismaDbContext"/>). The Athena Worker's composition root resolves it per audit call
/// via <see cref="IServiceScopeFactory"/>.
/// </para>
/// </remarks>
public sealed class DbPersonIdentityResolverService : IPersonIdentityResolver
{
    private readonly IPrismaDbContext _db;
    private readonly ILogger<DbPersonIdentityResolverService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="DbPersonIdentityResolverService"/>.
    /// </summary>
    /// <param name="db">The EF Core database context (scoped).</param>
    /// <param name="logger">Logger instance.</param>
    public DbPersonIdentityResolverService(
        IPrismaDbContext db,
        ILogger<DbPersonIdentityResolverService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IPersonIdentityResolver — ResolveIdentityAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<Result<Persona>> ResolveIdentityAsync(
        Persona person,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(Result<Persona>.WithFailure("Operation was cancelled."));

        if (person is null)
        {
            _logger.LogWarning("ResolveIdentityAsync: person argument is null");
            return Task.FromResult(Result<Persona>.WithFailure("Person cannot be null."));
        }

        try
        {
            _logger.LogDebug(
                "Resolving identity for person: {Nombre} {Paterno} {Materno}, RFC: {Rfc}",
                person.Nombre, person.Paterno, person.Materno, person.Rfc);

            if (!string.IsNullOrWhiteSpace(person.Rfc))
            {
                person.RfcVariants = GenerateRfcVariants(person.Rfc);
                _logger.LogDebug(
                    "Generated {Count} RFC variants for {Rfc}",
                    person.RfcVariants.Count, person.Rfc);
            }

            person.Nombre = NormalizeName(person.Nombre);
            person.Paterno = NormalizeName(person.Paterno);
            person.Materno = NormalizeName(person.Materno);

            return Task.FromResult(Result<Persona>.Success(person));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving identity for person");
            return Task.FromResult(Result<Persona>.WithFailure(
                $"Error resolving identity: {ex.Message}", default(Persona), ex));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IPersonIdentityResolver — DeduplicatePersonsAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<Result<List<Persona>>> DeduplicatePersonsAsync(
        List<Persona> persons,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(Result<List<Persona>>.WithFailure("Operation was cancelled."));

        if (persons is null)
        {
            _logger.LogWarning("DeduplicatePersonsAsync: persons list is null");
            return Task.FromResult(Result<List<Persona>>.WithFailure("Persons list cannot be null."));
        }

        try
        {
            _logger.LogDebug("Deduplicating {Count} persons (in-memory pass)", persons.Count);

            var deduplicated = new List<Persona>();
            var seenRfcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var person in persons)
            {
                var isDuplicate = false;

                if (!string.IsNullOrWhiteSpace(person.Rfc))
                {
                    var canonical = NormalizeRfcForComparison(person.Rfc);
                    var variants = GenerateRfcVariants(person.Rfc);

                    if (seenRfcs.Contains(canonical) ||
                        variants.Any(v => seenRfcs.Contains(v)))
                    {
                        isDuplicate = true;
                        _logger.LogDebug("Duplicate by RFC: {Rfc}", person.Rfc);
                    }
                    else
                    {
                        seenRfcs.Add(canonical);
                        foreach (var v in variants)
                            seenRfcs.Add(v);
                    }
                }

                if (!isDuplicate && string.IsNullOrWhiteSpace(person.Rfc))
                {
                    var name = BuildNormalizedName(person);
                    if (seenNames.Contains(name))
                    {
                        isDuplicate = true;
                        _logger.LogDebug("Duplicate by name: {Nombre}", name);
                    }
                    else
                    {
                        seenNames.Add(name);
                    }
                }

                if (!isDuplicate)
                    deduplicated.Add(person);
            }

            _logger.LogDebug(
                "Deduplicated {Original} persons → {Unique}",
                persons.Count, deduplicated.Count);

            return Task.FromResult(Result<List<Persona>>.Success(deduplicated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deduplicating persons");
            return Task.FromResult(Result<List<Persona>>.WithFailure(
                $"Error deduplicating persons: {ex.Message}", default(List<Persona>), ex));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IPersonIdentityResolver — FindByRfcAsync (DB-backed)
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    /// <remarks>
    /// Queries the <c>Persona</c> table using all RFC variants produced by
    /// <see cref="GenerateRfcVariants"/> so that "PEGJ850101ABC" and "PEG-850101-ABC"
    /// resolve to the same persisted row. Returns <c>success(null)</c> when no match exists
    /// (caller may then persist via <see cref="FindOrCreateAsync"/>).
    /// </remarks>
    public async Task<Result<Persona?>> FindByRfcAsync(
        string rfc,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Result<Persona?>.WithFailure("Operation was cancelled.");

        if (string.IsNullOrWhiteSpace(rfc))
        {
            _logger.LogWarning("FindByRfcAsync: RFC is null or empty");
            return Result<Persona?>.WithFailure("RFC cannot be null or empty.");
        }

        try
        {
            _logger.LogDebug("FindByRfcAsync: querying DB for RFC {Rfc}", rfc);

            // Build the full variant set (covers hyphenated, spaced, and canonical forms).
            var variants = GenerateRfcVariants(rfc);
            var canonical = NormalizeRfcForComparison(rfc);
            if (!variants.Contains(canonical, StringComparer.OrdinalIgnoreCase))
                variants.Add(canonical);

            // A single query: match on Rfc column OR any element of the JSON RfcVariants list.
            // Because RfcVariants is stored as a JSON nvarchar we perform an EF client evaluation
            // via FirstOrDefaultAsync with an in-memory predicate after fetching candidates by
            // the Rfc index. This is acceptable for the low cardinality of the Persona table and
            // avoids full-table JSON scans in hot paths — the Rfc index narrows the set first.
            var variantSet = new HashSet<string>(variants, StringComparer.OrdinalIgnoreCase);

            // Primary lookup: indexed Rfc column covers exact matches including all variants
            // whose 13-char or 12-char form was stored as the canonical Rfc value.
            var byRfc = await _db.Persona
                .FirstOrDefaultAsync(
                    p => p.Rfc != null && variantSet.Contains(p.Rfc),
                    cancellationToken)
                .ConfigureAwait(false);

            if (byRfc is not null)
            {
                _logger.LogDebug(
                    "FindByRfcAsync: found Persona ParteId={ParteId} by Rfc column",
                    byRfc.ParteId);
                return Result<Persona?>.Success(byRfc);
            }

            // Secondary lookup: scan RfcVariants JSON for any cross-format match.
            // Loaded into memory; the Persona table is expected to be small per legal case.
            var candidates = await _db.Persona
                .Where(p => p.RfcVariants != null)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var byVariant = candidates.FirstOrDefault(p =>
                p.RfcVariants.Any(v => variantSet.Contains(v)));

            if (byVariant is not null)
            {
                _logger.LogDebug(
                    "FindByRfcAsync: found Persona ParteId={ParteId} by RfcVariants",
                    byVariant.ParteId);
                return Result<Persona?>.Success(byVariant);
            }

            _logger.LogDebug("FindByRfcAsync: no Persona found for RFC {Rfc}", rfc);
            return Result<Persona?>.Success(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FindByRfcAsync: error querying Persona for RFC {Rfc}", rfc);
            return Result<Persona?>.WithFailure(
                $"Error finding person by RFC: {ex.Message}", default(Persona?), ex);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FindOrCreateAsync — dedup persistence (not on IPersonIdentityResolver interface)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the existing <see cref="Persona"/> matching <paramref name="rfc"/>, or persists a
    /// new one and returns it.  Exactly ONE row is created per canonical RFC across all documents.
    /// </summary>
    /// <param name="prototype">
    /// A <see cref="Persona"/> populated with the data extracted from the current document.
    /// Its <see cref="Persona.Rfc"/> must match <paramref name="rfc"/>. If a pre-existing row is
    /// returned, the prototype's fields are NOT merged — the caller receives the canonical row.
    /// </param>
    /// <param name="rfc">The RFC to look up (any format; normalisation is applied internally).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing the deduplicated <see cref="Persona"/> (always non-null
    /// on success).
    /// </returns>
    public async Task<Result<Persona>> FindOrCreateAsync(
        Persona prototype,
        string rfc,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Result<Persona>.WithFailure("Operation was cancelled.");

        if (prototype is null)
            return Result<Persona>.WithFailure("Prototype persona cannot be null.");

        if (string.IsNullOrWhiteSpace(rfc))
            return Result<Persona>.WithFailure("RFC cannot be null or empty.");

        try
        {
            // 1. Resolve: generate variants + normalise.
            var resolveResult = await ResolveIdentityAsync(prototype, cancellationToken)
                .ConfigureAwait(false);
            if (resolveResult.IsFailure)
                return resolveResult;

            var resolved = resolveResult.Value!;

            // 2. Look up by RFC (all variants).
            var findResult = await FindByRfcAsync(rfc, cancellationToken).ConfigureAwait(false);
            if (findResult.IsFailure)
                return Result<Persona>.WithFailure(findResult.Error);

            if (findResult.Value is not null)
            {
                _logger.LogDebug(
                    "FindOrCreateAsync: returning existing Persona ParteId={ParteId} for RFC {Rfc}",
                    findResult.Value.ParteId, rfc);
                return Result<Persona>.Success(findResult.Value);
            }

            // 3. Not found → persist.
            // ParteId = 0 (default) lets SQL Server IDENTITY assign it.
            resolved.ParteId = 0;
            _db.Persona.Add(resolved);

            try
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "FindOrCreateAsync: created Persona ParteId={ParteId} for RFC {Rfc}",
                    resolved.ParteId, rfc);
                return Result<Persona>.Success(resolved);
            }
            catch (DbUpdateException dbEx)
                when (dbEx.InnerException?.Message.Contains("IX_Persona_Rfc",
                    StringComparison.OrdinalIgnoreCase) == true)
            {
                // Optimistic-concurrency race: another caller persisted first.
                // Detach the failed entity and fall back to a fresh lookup.
                _db.Entry(resolved).State = EntityState.Detached;

                _logger.LogWarning(
                    "FindOrCreateAsync: unique-index race on RFC {Rfc}; retrying lookup",
                    rfc);

                var retryResult = await FindByRfcAsync(rfc, cancellationToken)
                    .ConfigureAwait(false);
                if (retryResult.IsFailure)
                    return Result<Persona>.WithFailure(retryResult.Error);

                if (retryResult.Value is null)
                    return Result<Persona>.WithFailure(
                        $"Concurrent insert race: could not locate Persona for RFC {rfc} after retry.");

                return Result<Persona>.Success(retryResult.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FindOrCreateAsync: unexpected error for RFC {Rfc}", rfc);
            return Result<Persona>.WithFailure(
                $"Error in FindOrCreateAsync for RFC {rfc}: {ex.Message}", default(Persona), ex);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IPersonIdentityResolver — GenerateRfcVariants
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public List<string> GenerateRfcVariants(string rfc)
    {
        if (string.IsNullOrWhiteSpace(rfc))
            return new List<string>();

        var variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            rfc.Trim()
        };

        var cleaned = rfc
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();

        if (!string.IsNullOrWhiteSpace(cleaned))
            variants.Add(cleaned);

        if (cleaned.Length == 12)
        {
            // 12 chars: 3 letters + 6 digits + 3 chars  → PEG-850101-ABC
            variants.Add($"{cleaned[..3]}-{cleaned.Substring(3, 6)}-{cleaned[9..]}");
            variants.Add($"{cleaned[..3]} {cleaned.Substring(3, 6)} {cleaned[9..]}");
        }
        else if (cleaned.Length == 13 && char.IsLetter(cleaned[3]))
        {
            // 13 chars: name-discriminant at position 3  → PEGJ850101ABC
            // Cross-format variant dropping the discriminant: PEG-850101-ABC
            var without = cleaned[..3] + cleaned[4..]; // 12 chars
            variants.Add(without);
            variants.Add($"{cleaned[..3]}-{cleaned.Substring(4, 6)}-{cleaned[10..]}");
            variants.Add($"{cleaned[..3]} {cleaned.Substring(4, 6)} {cleaned[10..]}");
            // Also the full 13-char hyphenated: PEGJ-850101-ABC
            variants.Add($"{cleaned[..4]}-{cleaned.Substring(4, 6)}-{cleaned[10..]}");
        }

        return variants.ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string NormalizeRfcForComparison(string rfc)
    {
        if (string.IsNullOrWhiteSpace(rfc))
            return string.Empty;

        var cleaned = rfc
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();

        // For 13-char RFC: drop the name-discriminant at position 3 to get the 12-char canonical.
        if (cleaned.Length == 13 && char.IsLetter(cleaned[3]))
            cleaned = cleaned[..3] + cleaned[4..];

        return cleaned;
    }

    private static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        return string.Join(" ",
            name.Split(new[] { ' ', '\t', '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    private static string BuildNormalizedName(Persona person)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(person.Nombre))
            parts.Add(person.Nombre.Trim());
        if (!string.IsNullOrWhiteSpace(person.Paterno))
            parts.Add(person.Paterno.Trim());
        if (!string.IsNullOrWhiteSpace(person.Materno))
            parts.Add(person.Materno.Trim());

        return string.Join(" ", parts).ToUpperInvariant();
    }
}
