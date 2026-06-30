using ExxerCube.Prisma.Veriqan.Application.Ports;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests;

/// <summary>
/// Tests that <see cref="SecretValue"/> never leaks the plaintext secret via
/// <see cref="object.ToString"/> or the record's <c>PrintMembers</c> path (B1 fix, Story 6.3).
/// </summary>
public sealed class SecretValueTests
{
    /// <summary>
    /// <see cref="SecretValue.ToString"/> must not contain the plaintext secret value.
    /// It must include safe metadata (<see cref="SecretValue.KeyId"/>,
    /// <see cref="SecretValue.Version"/>) for diagnostic use.
    /// </summary>
    [Fact]
    public void ToString_DoesNotRevealPlaintextValue()
    {
        const string plaintext = "SUPER-SECRET-VALUE-12345";
        var value = new SecretValue(
            plaintext,
            KeyId: "config:Some:Key",
            Version: "abc12345",
            RetrievedAtUtc: DateTimeOffset.UtcNow);

        var str = value.ToString();

        str.Contains(plaintext).ShouldBeFalse(
            "ToString must never reveal the plaintext secret — accidental logging must be safe");
        str.Contains("config:Some:Key").ShouldBeTrue(
            "KeyId must be present in ToString output (safe to log)");
        str.Contains("abc12345").ShouldBeTrue(
            "Version must be present in ToString output (safe to log)");
    }

    /// <summary>
    /// String interpolation uses the record's <c>PrintMembers</c> path.
    /// The interpolated form must also redact the plaintext.
    /// </summary>
    [Fact]
    public void StringInterpolation_DoesNotRevealPlaintextValue()
    {
        const string plaintext = "ANOTHER-SECRET-98765";
        var value = new SecretValue(
            plaintext,
            KeyId: "config:Another:Key",
            Version: "deadbeef",
            RetrievedAtUtc: DateTimeOffset.UtcNow);

        // String interpolation calls ToString() on the record.
        var interpolated = $"{value}";

        interpolated.Contains(plaintext).ShouldBeFalse(
            "string interpolation must not reveal the plaintext secret");
    }
}
