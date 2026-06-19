using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Tolerances;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Stores;

/// <summary>
/// SQL-backed singleton <see cref="ILegalToleranceProvider"/> that loads tolerance specifications
/// from <see cref="ILegalBaselineStore"/> once at startup (via a transient scope) and answers
/// synchronous <see cref="For"/> / <see cref="Has"/> queries from the in-memory cache.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lifetime design:</b> registered as a <c>Singleton</c> so that it outlives any individual
/// request scope. <see cref="ILegalBaselineStore"/> is scoped (EF Core
/// <c>VeriqanDbContext</c>), so this provider receives an <see cref="IServiceScopeFactory"/>
/// and creates a short-lived scope only during <see cref="InitialiseAsync"/> — never
/// during <see cref="For"/> or <see cref="Has"/>, which are pure memory reads.
/// </para>
/// <para>
/// <b>Usage:</b> call <see cref="InitialiseAsync"/> exactly once at host startup (from
/// <c>VeriqanLegalBaselineStartupService</c>) before any synchronous <see cref="For"/>
/// call is made. If the store cannot be loaded the startup service throws, aborting host
/// startup — never fall back to in-code defaults silently.
/// </para>
/// <para>
/// Unit-test code that runs without a database must use <c>DefaultLegalToleranceProvider</c>
/// (the in-code fallback). The SQL provider must NOT be forced into test projects that have
/// no DB. <c>AddVeriqanValidation</c> alone registers <c>DefaultLegalToleranceProvider</c>;
/// <c>AddVeriqanPersistence</c> replaces it with this provider.
/// </para>
/// </remarks>
public sealed class SqlLegalToleranceProvider : ILegalToleranceProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private IReadOnlyDictionary<string, Tolerance>? _cache;

    /// <summary>
    /// Initializes a new instance of <see cref="SqlLegalToleranceProvider"/>.
    /// </summary>
    /// <param name="scopeFactory">
    /// The scope factory used to create a short-lived scope during <see cref="InitialiseAsync"/>.
    /// </param>
    public SqlLegalToleranceProvider(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    /// <summary>
    /// Gets a value indicating whether the in-memory tolerance cache has been initialised.
    /// Returns <c>false</c> until <see cref="InitialiseAsync"/> completes successfully.
    /// Exposed so health-check probes can report warm/cold state without relying on <see cref="Has"/>.
    /// </summary>
    public bool IsWarm => _cache is not null;

    /// <summary>
    /// Loads tolerances from the SQL store into the in-memory cache.
    /// Call exactly once at startup (from <c>VeriqanLegalBaselineStartupService</c>)
    /// before any synchronous <see cref="For"/> query.
    /// </summary>
    /// <param name="cancellationToken">Propagated cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the loaded cache is empty after seeding — this means the DB is reachable
    /// but contains no tolerance rows, which is a fatal configuration error.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Propagated when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    public async Task InitialiseAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ILegalBaselineStore>();

        // LoadTolerancesAsync propagates cancellation (throws OCE) — callers must not
        // catch OCE and must let it bubble to abort host startup cleanly.
        var loaded = await store.LoadTolerancesAsync(cancellationToken).ConfigureAwait(false);

        if (loaded.Count == 0)
            throw new InvalidOperationException(
                $"{nameof(SqlLegalToleranceProvider)} initialised with an empty tolerance " +
                "cache. The legal baseline table is empty after seeding — check that " +
                "LegalBaselineSeeder ran successfully and that the migration was applied.");

        _cache = loaded;
    }

    /// <inheritdoc />
    public Tolerance For(string checkId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkId);

        EnsureInitialised();

        if (_cache!.TryGetValue(checkId, out var tolerance))
            return tolerance;

        throw new ArgumentException(
            $"No legal tolerance specification is registered for check '{checkId}'.",
            nameof(checkId));
    }

    /// <inheritdoc />
    public bool Has(string checkId) =>
        !string.IsNullOrWhiteSpace(checkId)
        && _cache is not null
        && _cache.ContainsKey(checkId);

    private void EnsureInitialised()
    {
        if (_cache is null)
            throw new InvalidOperationException(
                $"{nameof(SqlLegalToleranceProvider)} has not been initialised. " +
                $"Call {nameof(InitialiseAsync)} at startup before using this provider.");
    }
}
