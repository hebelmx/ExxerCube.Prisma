using System.Reflection;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults.Operations;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="ISiaraCredentialSource"/> — every implementation (the mock
/// blueprint, the reference fake, and the production config/env source) must pass these tests unchanged
/// (ADR-005, ADR-010 P5).
/// </summary>
/// <remarks>
/// Uses the ADR-005 §3 default <strong>injected-<see cref="Sut"/></strong> mechanism. Scope (ADR-005 §5):
/// Result semantics (failure not throw), cancellation, that a yielded credential is usable, self-clearing,
/// non-leaking, and that the <see cref="SiaraCredential"/> type structurally exposes no string credential
/// members. The specifics of <em>where</em> the secret comes from stay in each implementation's own tests.
/// </remarks>
public abstract class SiaraCredentialSourceContract
{
    /// <summary>Initializes the contract with the source under test.</summary>
    /// <param name="sut">The <see cref="ISiaraCredentialSource"/> implementation to verify.</param>
    protected SiaraCredentialSourceContract(ISiaraCredentialSource sut)
    {
        ArgumentNullException.ThrowIfNull(sut);
        Sut = sut;
    }

    /// <summary>Gets the source under test.</summary>
    protected ISiaraCredentialSource Sut { get; }

    /// <summary>Contract: a pre-cancelled token yields a Cancelled Result — never an exception.</summary>
    [Fact]
    public async Task GetCredentialsAsync_PreCancelledToken_ReturnsCancelled()
    {
        var cancelled = new CancellationToken(canceled: true);

        var result = await Sut.GetCredentialsAsync(cancelled);

        result.ShouldNotBeNull();
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>Contract: a valid read yields a non-empty, usable credential.</summary>
    [Fact]
    public async Task GetCredentialsAsync_ReturnsUsableCredential()
    {
        var result = await Sut.GetCredentialsAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        using var credential = result.Value!;
        credential.IsEmpty.ShouldBeFalse();

        var (username, password) = await RevealAsync(credential);
        username.ShouldNotBeNullOrEmpty();
        password.ShouldNotBeNullOrEmpty();
    }

    /// <summary>Contract: the credential never renders its secret — <see cref="object.ToString"/> is redacted.</summary>
    [Fact]
    public async Task Credential_ToStringDoesNotLeakSecret()
    {
        var result = await Sut.GetCredentialsAsync(TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        using var credential = result.Value!;

        var (username, password) = await RevealAsync(credential);
        var rendered = credential.ToString();

        rendered.ShouldNotContain(username);
        rendered.ShouldNotContain(password);
    }

    /// <summary>Contract: once disposed, the credential zeroes its buffers and refuses further reveals.</summary>
    [Fact]
    public async Task Credential_AfterDispose_CannotBeRevealed()
    {
        var result = await Sut.GetCredentialsAsync(TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        var credential = result.Value!;

        credential.Dispose();

        await Should.ThrowAsync<ObjectDisposedException>(
            async () => await RevealAsync(credential));
    }

    /// <summary>
    /// Contract: each read yields its own usable credential, supporting the transient-per-login model
    /// (acquire, use once, dispose).
    /// </summary>
    [Fact]
    public async Task GetCredentialsAsync_MultipleReads_EachYieldUsableCredential()
    {
        var first = await Sut.GetCredentialsAsync(TestContext.Current.CancellationToken);
        var second = await Sut.GetCredentialsAsync(TestContext.Current.CancellationToken);

        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
        using var firstCredential = first.Value!;
        using var secondCredential = second.Value!;
        firstCredential.IsEmpty.ShouldBeFalse();
        secondCredential.IsEmpty.ShouldBeFalse();
    }

    /// <summary>
    /// Contract: the <see cref="SiaraCredential"/> type exposes no string credential <em>properties</em>
    /// — the secret can only be revealed through the scoped <c>UseAsync</c> callback, never read off a
    /// property a logger or serializer could pick up (ADR-010 P5).
    /// </summary>
    [Fact]
    public void CredentialType_ExposesNoStringCredentialProperties()
    {
        string[] forbidden = ["password", "passwd", "pwd", "username", "userid", "credential", "secret"];

        var stringProperties = typeof(SiaraCredential)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => p.Name);

        foreach (var name in stringProperties)
        {
            foreach (var token in forbidden)
            {
                name.Contains(token, StringComparison.OrdinalIgnoreCase).ShouldBeFalse(
                    $"SiaraCredential must not expose a string credential property ('{name}' matched '{token}').");
            }
        }
    }

    private static async Task<(string Username, string Password)> RevealAsync(SiaraCredential credential) =>
        await credential.UseAsync((username, password) => Task.FromResult((username, password)));
}
