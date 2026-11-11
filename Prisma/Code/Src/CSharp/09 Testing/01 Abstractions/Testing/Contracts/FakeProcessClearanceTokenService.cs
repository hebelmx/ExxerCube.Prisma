using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Hand-written, stateful reference fake of <see cref="IProcessClearanceTokenService"/> (ADR-005 §6,
/// MVP-PATH 1.5 A5): honest logic, no mocking framework, modelling an in-memory token store.
/// </summary>
/// <remarks>
/// <para>
/// Stores minted tokens as a simple dictionary keyed by the opaque token string. A mint call writes a
/// new entry; a validate call looks it up and returns the stored claims. This models the round-trip
/// contract without depending on JWT or any signing infrastructure. Construct one per test for isolation.
/// </para>
/// <para>
/// Invalid token strings (those not previously minted by this instance) return a failure result, exactly
/// as a real implementation must. The fake never accepts "any" token — it only returns success for tokens
/// it itself minted.
/// </para>
/// <para>
/// Both methods genuinely honor <see cref="CancellationToken"/>: a pre-cancelled token returns
/// <see cref="ResultExtensions.Cancelled{T}()"/> immediately — the team deleted a fake-green
/// <c>await Task.CompletedTask</c> no-op in Phase 6 and this fake must not repeat that pattern.
/// </para>
/// </remarks>
public sealed class FakeProcessClearanceTokenService : IProcessClearanceTokenService
{
    private readonly Dictionary<string, ClearanceTokenClaims> _store = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    /// <summary>Gets the number of tokens minted by this fake instance.</summary>
    public int MintCount
    {
        get { lock (_gate) { return _store.Count; } }
    }

    /// <inheritdoc />
    public Task<Result<string>> MintAsync(
        SiaraActor actor,
        ProcessClearance clearance,
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<string>());
        }

        ArgumentNullException.ThrowIfNull(actor);

        var token = $"fake-token:{actor.ActorId}:{clearance}:{fileId:D}:{Guid.NewGuid():D}";
        var claims = new ClearanceTokenClaims
        {
            ActorId = actor.ActorId,
            ActorType = actor.ActorType,
            Clearance = clearance,
            FileId = fileId,
            // Stable per-token jti so the fake round-trips a unique id — keeps it usable in front of a
            // forwarder (whose replay guard fail-closes on a missing jti) without dropping the event.
            Jti = Guid.NewGuid().ToString("D"),
        };

        lock (_gate)
        {
            _store[token] = claims;
        }

        return Task.FromResult(Result<string>.Success(token));
    }

    /// <inheritdoc />
    public Task<Result<ClearanceTokenClaims>> ValidateAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<ClearanceTokenClaims>());
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(Result<ClearanceTokenClaims>.WithFailure("Token is null or empty."));
        }

        ClearanceTokenClaims? claims;
        lock (_gate)
        {
            _store.TryGetValue(token, out claims);
        }

        if (claims is null)
        {
            return Task.FromResult(Result<ClearanceTokenClaims>.WithFailure(
                $"Token not recognised by this fake instance: '{token}'."));
        }

        return Task.FromResult(Result<ClearanceTokenClaims>.Success(claims));
    }
}
