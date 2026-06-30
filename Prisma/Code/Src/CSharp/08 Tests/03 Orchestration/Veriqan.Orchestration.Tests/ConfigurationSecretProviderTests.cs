using System.Collections.Generic;
using System.Threading;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Configuration;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests;

/// <summary>
/// Unit tests for <see cref="ConfigurationSecretProvider"/> (Story 6.3).
/// </summary>
/// <remarks>
/// Covers the contract for the default configuration-backed ISecretProvider implementation:
/// returns a success result for present secrets, failure (not throw) for absent ones, and
/// ensures the KeyId never contains the raw secret value.
/// </remarks>
public sealed class ConfigurationSecretProviderTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // 1. Present secret → success result with value and stable KeyId
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When the logical name is present in configuration the provider must return
    /// <c>IsSuccess = true</c> with the correct plaintext value.
    /// </summary>
    [Fact]
    public async Task GetSecretAsync_SecretPresent_ReturnsSuccessWithCorrectValue()
    {
        var ct = TestContext.Current.CancellationToken;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MySecret"] = "super-secret-value",
            })
            .Build();

        var sut = new ConfigurationSecretProvider(config);

        var result = await sut.GetSecretAsync("MySecret", ct);

        result.IsSuccess.ShouldBeTrue("Provider must return success for a present secret.");
        result.Value!.Value.ShouldBe("super-secret-value",
            "The returned Value must match the configured secret.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. Absent secret → Result.WithFailure (no throw)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When the logical name is not present in configuration the provider must return a
    /// failure result — it must NEVER throw an exception for a missing secret.
    /// </summary>
    [Fact]
    public async Task GetSecretAsync_SecretAbsent_ReturnsFailureWithoutThrowing()
    {
        var ct = TestContext.Current.CancellationToken;

        var config = new ConfigurationBuilder().Build(); // empty
        var sut = new ConfigurationSecretProvider(config);

        var result = await sut.GetSecretAsync("NonExistent:Key", ct);

        result.IsFailure.ShouldBeTrue("Provider must return failure for an absent secret.");
        result.Error.ShouldNotBeNullOrEmpty("Failure result must carry a descriptive error message.");
    }

    /// <summary>
    /// An empty (whitespace) configuration value is treated as absent — not a valid secret.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task GetSecretAsync_SecretEmptyOrWhitespace_ReturnsFailure(string emptyValue)
    {
        var ct = TestContext.Current.CancellationToken;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SomeKey"] = emptyValue,
            })
            .Build();

        var sut = new ConfigurationSecretProvider(config);

        var result = await sut.GetSecretAsync("SomeKey", ct);

        result.IsFailure.ShouldBeTrue(
            $"Empty/whitespace value '{emptyValue}' must be treated as absent.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. KeyId does NOT contain the raw secret (security invariant)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The <see cref="SecretValue.KeyId"/> must never embed the raw secret value.
    /// Logs the KeyId freely — it must be safe to log.
    /// </summary>
    [Fact]
    public async Task GetSecretAsync_KeyIdDoesNotContainRawSecret()
    {
        var ct = TestContext.Current.CancellationToken;

        const string secretValue = "top-secret-api-key-xyz123";
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Api:Key"] = secretValue,
            })
            .Build();

        var sut = new ConfigurationSecretProvider(config);

        var result = await sut.GetSecretAsync("Api:Key", ct);

        result.IsSuccess.ShouldBeTrue();

        var keyId = result.Value!.KeyId;
        keyId.Contains(secretValue).ShouldBeFalse(
            "KeyId must never embed the raw secret value — it must be safe to log.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. KeyId is stable; Version fingerprint changes with value
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Calling <c>GetSecretAsync</c> twice with the same logical name and value must return
    /// the same <see cref="SecretValue.KeyId"/> and the same <see cref="SecretValue.Version"/>
    /// (the fingerprint is deterministic).
    /// </summary>
    [Fact]
    public async Task GetSecretAsync_SameValue_ProducesSameKeyIdAndVersion()
    {
        var ct = TestContext.Current.CancellationToken;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Stable:Key"] = "same-value",
            })
            .Build();

        var sut = new ConfigurationSecretProvider(config);

        var r1 = await sut.GetSecretAsync("Stable:Key", ct);
        var r2 = await sut.GetSecretAsync("Stable:Key", ct);

        r1.IsSuccess.ShouldBeTrue();
        r2.IsSuccess.ShouldBeTrue();

        r1.Value!.KeyId.ShouldBe(r2.Value!.KeyId,
            "KeyId must be stable for the same logical name.");
        r1.Value.Version.ShouldBe(r2.Value.Version,
            "Version fingerprint must be deterministic for the same value.");
    }

    /// <summary>
    /// Two different secrets must produce different <see cref="SecretValue.Version"/> fingerprints.
    /// This is the rotation-detection invariant: a changed value signals a rotation.
    /// </summary>
    [Fact]
    public async Task GetSecretAsync_DifferentValues_ProduceDifferentVersionFingerprints()
    {
        var ct = TestContext.Current.CancellationToken;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Key:A"] = "value-one",
                ["Key:B"] = "value-two",
            })
            .Build();

        var sut = new ConfigurationSecretProvider(config);

        var rA = await sut.GetSecretAsync("Key:A", ct);
        var rB = await sut.GetSecretAsync("Key:B", ct);

        rA.IsSuccess.ShouldBeTrue();
        rB.IsSuccess.ShouldBeTrue();

        rA.Value!.Version.ShouldNotBe(rB.Value!.Version,
            "Different values must produce different Version fingerprints (rotation detection).");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. Cancellation → Cancelled result
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When the cancellation token is already cancelled the provider must return a Cancelled
    /// result without attempting to read configuration.
    /// </summary>
    [Fact]
    public async Task GetSecretAsync_CancelledToken_ReturnsCancelledResult()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Some:Key"] = "value",
            })
            .Build();

        var sut = new ConfigurationSecretProvider(config);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await sut.GetSecretAsync("Some:Key", cts.Token);

        result.IsCancelled().ShouldBeTrue(
            "Pre-cancelled token must produce a Cancelled result.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. KeyId format: "config:<logicalName>" (documented contract)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The <see cref="SecretValue.KeyId"/> must follow the documented format
    /// <c>"config:&lt;logicalName&gt;"</c>, enabling callers to identify the backing store.
    /// </summary>
    [Fact]
    public async Task GetSecretAsync_KeyIdFollowsDocumentedFormat()
    {
        var ct = TestContext.Current.CancellationToken;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Veriqan:Auth:Jwt:SigningKey"] = "some-key",
            })
            .Build();

        var sut = new ConfigurationSecretProvider(config);

        var result = await sut.GetSecretAsync("Veriqan:Auth:Jwt:SigningKey", ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.KeyId.ShouldBe(
            "config:Veriqan:Auth:Jwt:SigningKey",
            "KeyId must be 'config:<logicalName>' for the configuration-backed provider.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. RetrievedAtUtc is populated
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The <see cref="SecretValue.RetrievedAtUtc"/> must be set and be close to the current UTC time.
    /// </summary>
    [Fact]
    public async Task GetSecretAsync_SecretPresent_RetrievedAtUtcIsSet()
    {
        var ct = TestContext.Current.CancellationToken;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ts:Key"] = "value",
            })
            .Build();

        var sut = new ConfigurationSecretProvider(config);
        var before = System.DateTimeOffset.UtcNow;

        var result = await sut.GetSecretAsync("Ts:Key", ct);

        var after = System.DateTimeOffset.UtcNow;

        result.IsSuccess.ShouldBeTrue();
        result.Value!.RetrievedAtUtc.ShouldBeGreaterThanOrEqualTo(before,
            "RetrievedAtUtc must be >= the time before the call.");
        result.Value.RetrievedAtUtc.ShouldBeLessThanOrEqualTo(after,
            "RetrievedAtUtc must be <= the time after the call.");
    }
}
