namespace Siara.Simulator.Configuration;

/// <summary>
/// Authentication settings for the SIARA simulator. Bound from the "Auth" configuration section.
/// </summary>
/// <remarks>
/// These are <strong>public fake credentials for the simulator only</strong> — they model the real
/// SIARA cookie-session login (username/password + max-attempt lockout) so the storage-state
/// capture/restore flow is testable end-to-end, without ever touching real SIARA credentials.
/// </remarks>
public sealed class SiaraAuthOptions
{
    /// <summary>The single valid simulator username. Default: <c>BANAMEX</c>.</summary>
    public string ValidUsername { get; set; } = "BANAMEX";

    /// <summary>The valid simulator password. Default: <c>password123</c>.</summary>
    public string ValidPassword { get; set; } = "password123";

    /// <summary>Failed attempts (per username) before the account locks. Default: 5.</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>How long an account stays locked after exceeding the attempt limit. Default: 5 minutes.</summary>
    public int LockoutMinutes { get; set; } = 5;
}
