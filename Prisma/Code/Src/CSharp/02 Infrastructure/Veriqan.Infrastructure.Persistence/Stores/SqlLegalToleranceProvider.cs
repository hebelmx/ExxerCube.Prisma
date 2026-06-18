using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Tolerances;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Stores;

/// <summary>
/// SQL-backed <see cref="ILegalToleranceProvider"/> that loads tolerance specifications
/// from <see cref="ILegalBaselineStore"/> once per instance lifetime and answers
/// synchronous <see cref="For"/> / <see cref="Has"/> queries from the in-memory cache.
/// </summary>
/// <remarks>
/// <para>
/// This provider is registered in the SQL/worker composition path. Unit-test code that runs
/// without a database must use <c>DefaultLegalToleranceProvider</c> (the in-code fallback)
/// — the SQL provider must NOT be forced into test projects that have no DB.
/// </para>
/// <para>
/// <b>Usage:</b> call <see cref="InitialiseAsync"/> once at startup (or at integration-test
/// setup) before any synchronous <see cref="For"/> call is made. The DI registration in
/// <c>AddVeriqanPersistence</c> registers this as a <c>Scoped</c> service; callers that
/// need it pre-warmed should resolve and call <see cref="InitialiseAsync"/> during host
/// startup.
/// </para>
/// </remarks>
public sealed class SqlLegalToleranceProvider : ILegalToleranceProvider
{
    private readonly ILegalBaselineStore _store;
    private IReadOnlyDictionary<string, Tolerance>? _cache;

    /// <summary>
    /// Initializes a new instance of <see cref="SqlLegalToleranceProvider"/>.
    /// </summary>
    /// <param name="store">The legal-baseline store.</param>
    public SqlLegalToleranceProvider(ILegalBaselineStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// Loads tolerances from the SQL store into the in-memory cache.
    /// Call once at startup before any synchronous <see cref="For"/> query.
    /// </summary>
    /// <param name="cancellationToken">Propagated cancellation token.</param>
    public async Task InitialiseAsync(CancellationToken cancellationToken = default)
    {
        _cache = await _store.LoadTolerancesAsync(cancellationToken).ConfigureAwait(false);
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
