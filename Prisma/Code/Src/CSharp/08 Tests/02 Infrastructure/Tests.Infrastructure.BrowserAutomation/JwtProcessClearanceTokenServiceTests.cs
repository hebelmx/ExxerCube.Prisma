using ExxerCube.Prisma.Infrastructure.BrowserAutomation.ProcessIdentity;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Security-focused unit tests for the fail-closed rejection paths of
/// <see cref="JwtProcessClearanceTokenService.ValidateAsync"/>. Each test constructs real service instances
/// with controlled <see cref="ProcessIdentityOptions"/> — no mocks — to confirm that invalid tokens produce
/// a failure result rather than throwing (Railway-Oriented contract) and that no invalid token is accepted.
/// </summary>
/// <remarks>
/// JWT internals covered here complement the behavioral contract covered by
/// <see cref="JwtProcessClearanceTokenServiceContractTests"/>.
/// </remarks>
public sealed class JwtProcessClearanceTokenServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string ValidSecret = "test-secret-at-least-32-chars-long-for-hmac-sha256";
    private const string ValidIssuer = "prisma-pipeline-test";
    private const string ValidAudience = "prisma-pipeline-test";

    private static readonly SiaraActor SampleActor = new()
    {
        ActorId = "test-downloader",
        ActorType = SiaraActorType.ServiceAccount,
    };

    /// <summary>Builds a service with the supplied options; uses a null logger (tests are not log-sensitive).</summary>
    private static JwtProcessClearanceTokenService BuildService(ProcessIdentityOptions opts) =>
        new(Options.Create(opts), NullLogger<JwtProcessClearanceTokenService>.Instance);

    private static ProcessIdentityOptions ValidOptions() => new()
    {
        JwtSecret = ValidSecret,
        JwtIssuer = ValidIssuer,
        JwtAudience = ValidAudience,
        TokenLifetime = TimeSpan.FromMinutes(5),
    };

    // ── Helpers ──────────────────────────────────────────────────────────────────────────

    /// <summary>Mints a token using a signer built from <paramref name="signerOpts"/> and returns the token string.</summary>
    private static async Task<string> MintTokenAsync(
        ProcessIdentityOptions signerOpts,
        CancellationToken ct)
    {
        var signer = BuildService(signerOpts);
        var mintResult = await signer.MintAsync(SampleActor, ProcessClearance.Download, Guid.NewGuid(), ct);
        mintResult.IsSuccess.ShouldBeTrue("pre-condition: minting must succeed to run the validation path under test");
        return mintResult.Value!;
    }

    // ── Tests ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_TokenSignedWithDifferentSecret_ReturnsFailure()
    {
        // Token minted with "wrong-secret" must be rejected by the validator using the correct secret.
        var wrongSecretOpts = new ProcessIdentityOptions
        {
            JwtSecret = "wrong-secret-that-is-long-enough-for-hmac",
            JwtIssuer = ValidIssuer,
            JwtAudience = ValidAudience,
            TokenLifetime = TimeSpan.FromMinutes(5),
        };
        var token = await MintTokenAsync(wrongSecretOpts, Ct);

        var validator = BuildService(ValidOptions());
        var result = await validator.ValidateAsync(token, Ct);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_TokenWithWrongIssuer_ReturnsFailure()
    {
        // Token minted with issuer X must be rejected by a validator configured with issuer Y.
        var issuerXOpts = new ProcessIdentityOptions
        {
            JwtSecret = ValidSecret,
            JwtIssuer = "issuer-x",
            JwtAudience = ValidAudience,
            TokenLifetime = TimeSpan.FromMinutes(5),
        };
        var token = await MintTokenAsync(issuerXOpts, Ct);

        var issuerYOpts = new ProcessIdentityOptions
        {
            JwtSecret = ValidSecret,
            JwtIssuer = "issuer-y",
            JwtAudience = ValidAudience,
            TokenLifetime = TimeSpan.FromMinutes(5),
        };
        var validator = BuildService(issuerYOpts);
        var result = await validator.ValidateAsync(token, Ct);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_TokenWithWrongAudience_ReturnsFailure()
    {
        // Token minted for audience A must be rejected by a validator configured for audience B.
        var audienceAOpts = new ProcessIdentityOptions
        {
            JwtSecret = ValidSecret,
            JwtIssuer = ValidIssuer,
            JwtAudience = "audience-a",
            TokenLifetime = TimeSpan.FromMinutes(5),
        };
        var token = await MintTokenAsync(audienceAOpts, Ct);

        var audienceBOpts = new ProcessIdentityOptions
        {
            JwtSecret = ValidSecret,
            JwtIssuer = ValidIssuer,
            JwtAudience = "audience-b",
            TokenLifetime = TimeSpan.FromMinutes(5),
        };
        var validator = BuildService(audienceBOpts);
        var result = await validator.ValidateAsync(token, Ct);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ExpiredToken_ReturnsFailure()
    {
        // Mint a token with a lifetime that expired 5 minutes ago (comfortably outside the 30s ClockSkew).
        var expiredOpts = new ProcessIdentityOptions
        {
            JwtSecret = ValidSecret,
            JwtIssuer = ValidIssuer,
            JwtAudience = ValidAudience,
            TokenLifetime = TimeSpan.FromMinutes(-5),
        };
        var token = await MintTokenAsync(expiredOpts, Ct);

        var validator = BuildService(ValidOptions());
        var result = await validator.ValidateAsync(token, Ct);

        result.IsFailure.ShouldBeTrue();
    }
}
