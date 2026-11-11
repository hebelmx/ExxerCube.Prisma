using ExxerCube.Prisma.Infrastructure.BrowserAutomation.ProcessIdentity;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Unit tests for <see cref="ProcessIdentitySigningKeys.BuildAcceptedKeys(ProcessIdentityOptions)"/> —
/// the shared accept-set computation used by both the per-message validation path
/// (<c>JwtProcessClearanceTokenService.ValidateAsync</c>) and each worker's connection-level
/// <c>AddJwtBearer</c> auth (RC6 item 3.9, HMAC secret rotation grace period).
/// </summary>
public sealed class ProcessIdentitySigningKeysTests
{
    private static string KeyMaterial(SecurityKey key) =>
        Convert.ToBase64String(((SymmetricSecurityKey)key).Key);

    [Fact]
    public void BuildAcceptedKeys_CurrentSecretOnly_ReturnsSingleKey()
    {
        var options = new ProcessIdentityOptions { JwtSecret = "current-secret-long-enough-for-hmac-sha256" };

        var keys = ProcessIdentitySigningKeys.BuildAcceptedKeys(options);

        keys.Count.ShouldBe(1);
        KeyMaterial(keys[0]).ShouldBe(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(options.JwtSecret)));
    }

    [Fact]
    public void BuildAcceptedKeys_CurrentSecretFirst_PreservesOrderingWithPreviousSecretsAfter()
    {
        var options = new ProcessIdentityOptions
        {
            JwtSecret = "current-secret-long-enough-for-hmac-sha256",
            PreviousJwtSecrets = ["previous-secret-one-long-enough-for-hmac", "previous-secret-two-long-enough-for-hmac"],
        };

        var keys = ProcessIdentitySigningKeys.BuildAcceptedKeys(options);

        keys.Count.ShouldBe(3);
        KeyMaterial(keys[0]).ShouldBe(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(options.JwtSecret)));
        KeyMaterial(keys[1]).ShouldBe(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(options.PreviousJwtSecrets[0])));
        KeyMaterial(keys[2]).ShouldBe(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(options.PreviousJwtSecrets[1])));
    }

    [Fact]
    public void BuildAcceptedKeys_BlankPreviousSecrets_AreFilteredOut()
    {
        var options = new ProcessIdentityOptions
        {
            JwtSecret = "current-secret-long-enough-for-hmac-sha256",
            PreviousJwtSecrets = ["", "   ", "real-previous-secret-long-enough-for-hmac"],
        };

        var keys = ProcessIdentitySigningKeys.BuildAcceptedKeys(options);

        keys.Count.ShouldBe(2);
    }

    [Fact]
    public void BuildAcceptedKeys_BlankCurrentSecret_IsFilteredOut()
    {
        var options = new ProcessIdentityOptions
        {
            JwtSecret = string.Empty,
            PreviousJwtSecrets = ["real-previous-secret-long-enough-for-hmac"],
        };

        var keys = ProcessIdentitySigningKeys.BuildAcceptedKeys(options);

        keys.Count.ShouldBe(1);
    }

    [Fact]
    public void BuildAcceptedKeys_DuplicateSecretAcrossCurrentAndPrevious_IsDeduplicated()
    {
        const string sharedSecret = "shared-secret-long-enough-for-hmac-sha256-signing";
        var options = new ProcessIdentityOptions
        {
            JwtSecret = sharedSecret,
            PreviousJwtSecrets = [sharedSecret, "distinct-previous-secret-long-enough-for-hmac"],
        };

        var keys = ProcessIdentitySigningKeys.BuildAcceptedKeys(options);

        keys.Count.ShouldBe(2);
    }

    [Fact]
    public void BuildAcceptedKeys_DuplicatesWithinPreviousSecrets_AreDeduplicated()
    {
        var options = new ProcessIdentityOptions
        {
            JwtSecret = "current-secret-long-enough-for-hmac-sha256",
            PreviousJwtSecrets = ["repeated-previous-secret-long-enough-for-hmac", "repeated-previous-secret-long-enough-for-hmac"],
        };

        var keys = ProcessIdentitySigningKeys.BuildAcceptedKeys(options);

        keys.Count.ShouldBe(2);
    }

    [Fact]
    public void BuildAcceptedKeys_NullOptions_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ProcessIdentitySigningKeys.BuildAcceptedKeys(null!));
    }
}
