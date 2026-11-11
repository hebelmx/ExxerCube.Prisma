using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.ProcessIdentity;

/// <summary>
/// Single source of truth for the set of HMAC signing keys a token <em>validator</em> should accept
/// (RC6 item 3.9 — zero-downtime rotation of the process-clearance JWT shared secret via an overlapping
/// key grace period).
/// </summary>
/// <remarks>
/// <para>
/// Both the per-message validation path (<c>JwtProcessClearanceTokenService.ValidateAsync</c>) and the
/// per-connection SignalR hub bearer auth (each worker's <c>AddJwtBearer</c> in <c>Program.cs</c>) must
/// accept exactly the same key set during a rotation, or one path would reject tokens the other accepts.
/// This type is the shared computation both call, so the two paths cannot drift apart.
/// </para>
/// <para>
/// Only <see cref="ProcessIdentityOptions.JwtSecret"/> is ever used to <em>mint</em> tokens — the
/// previous secrets are validate-only, so a token minted moments before a rotation completes is still
/// accepted until it naturally expires.
/// </para>
/// </remarks>
public static class ProcessIdentitySigningKeys
{
    /// <summary>
    /// Builds the ordered, de-duplicated set of <see cref="SecurityKey"/> instances a token validator
    /// should accept: the current <see cref="ProcessIdentityOptions.JwtSecret"/> first, followed by each
    /// non-blank entry in <see cref="ProcessIdentityOptions.PreviousJwtSecrets"/> in the order supplied.
    /// </summary>
    /// <param name="options">The process identity configuration.</param>
    /// <returns>
    /// The accepted signing keys. Blank/whitespace-only secrets (current or previous) are skipped, and
    /// secrets that resolve to the same key material are de-duplicated (first occurrence wins) so the
    /// same underlying key is never registered twice with the JWT handler.
    /// </returns>
    public static IReadOnlyList<SecurityKey> BuildAcceptedKeys(ProcessIdentityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var keys = new List<SecurityKey>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void AddIfNew(string? secret)
        {
            if (string.IsNullOrWhiteSpace(secret))
            {
                return;
            }

            if (!seen.Add(secret))
            {
                return;
            }

            keys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)));
        }

        AddIfNew(options.JwtSecret);

        foreach (var previous in options.PreviousJwtSecrets)
        {
            AddIfNew(previous);
        }

        return keys;
    }
}
