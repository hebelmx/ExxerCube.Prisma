using System.Collections.Concurrent;
using ExxerCube.Prisma.Domain.Interfaces;

namespace Prisma.Athena.Processing;

/// <summary>
/// In-memory implementation of <see cref="IClearanceReplayGuard"/> (issue #2.1).
/// Tracks seen <c>jti</c> values for at least the token's validatable lifetime, preventing a
/// captured clearance token from being replayed on the same process edge.
/// </summary>
/// <remarks>
/// <para>
/// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>. Each entry maps a <c>jti</c>
/// to its expiry instant (<see cref="DateTimeOffset.UtcNow"/> + <see cref="_retention"/>). On each
/// call to <see cref="TryRegister"/> expired entries are opportunistically evicted so the dictionary
/// does not grow without bound in long-running processes.
/// </para>
/// <para>
/// Registered as a singleton in both Athena Worker and Reconciliator Worker so the replay window is
/// scoped to a single process lifetime. A process restart resets the guard — this is acceptable because
/// clearance tokens are short-lived (design R5) and restarting a process is an operator action.
/// </para>
/// </remarks>
public sealed class InMemoryClearanceReplayGuard : IClearanceReplayGuard
{
    private static readonly TimeSpan DefaultRetention = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new(StringComparer.Ordinal);
    private readonly TimeSpan _retention;

    /// <summary>
    /// Initializes a new instance of <see cref="InMemoryClearanceReplayGuard"/>.
    /// </summary>
    /// <param name="retention">
    /// How long each <c>jti</c> is remembered after first registration. Must exceed the maximum
    /// token lifetime plus clock-skew tolerance. Defaults to 10 minutes when <see langword="null"/>.
    /// </param>
    public InMemoryClearanceReplayGuard(TimeSpan? retention = null)
    {
        _retention = retention ?? DefaultRetention;
    }

    /// <inheritdoc />
    public bool TryRegister(string jti)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            // Fail-closed: a token without a jti must never be accepted.
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var expiry = now + _retention;

        // Opportunistically evict expired entries to keep memory bounded in long-running processes.
        EvictExpired(now);

        // TryAdd returns true (first-seen) or false (already present).
        if (_seen.TryAdd(jti, expiry))
        {
            return true;
        }

        // Entry already exists — check whether it has expired (e.g. after a very long gap).
        if (_seen.TryGetValue(jti, out var existingExpiry) && existingExpiry < now)
        {
            // Expired: overwrite and treat as first-seen (the old token's lifetime is well past).
            _seen[jti] = expiry;
            return true;
        }

        // Non-expired duplicate: replay detected.
        return false;
    }

    private void EvictExpired(DateTimeOffset now)
    {
        foreach (var key in _seen.Keys)
        {
            if (_seen.TryGetValue(key, out var expiry) && expiry < now)
            {
                _seen.TryRemove(key, out _);
            }
        }
    }
}
