namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Per-process anti-replay guard for clearance tokens (issue #2.1). Remembers each token's
/// <c>jti</c> for at least its validatable lifetime so a captured token cannot be replayed.
/// </summary>
public interface IClearanceReplayGuard
{
    /// <summary>
    /// Registers a first use of <paramref name="jti"/>. Returns <see langword="true"/> if it was
    /// previously unseen (caller may proceed); <see langword="false"/> if it has already been seen
    /// within the retention window (replay — caller must reject). A <see langword="null"/> or empty
    /// <paramref name="jti"/> returns <see langword="false"/> (fail-closed).
    /// </summary>
    /// <param name="jti">The JWT ID claim value extracted from the validated clearance token.</param>
    /// <returns>
    /// <see langword="true"/> on first registration; <see langword="false"/> on replay or missing jti.
    /// </returns>
    bool TryRegister(string jti);
}
