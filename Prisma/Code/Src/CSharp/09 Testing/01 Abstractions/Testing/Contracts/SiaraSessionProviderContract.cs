using System.Reflection;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults.Operations;
using Shouldly;
using Xunit;


namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="ISiaraSessionProvider"/> — every implementation (the mock
/// blueprint, the reference fake, and both production strategies) must pass these tests unchanged
/// (ADR-005, ADR-010).
/// </summary>
/// <remarks>
/// <para>
/// Uses the ADR-005 §3 default <strong>injected-<see cref="Sut"/></strong> mechanism. The class is
/// <c>abstract</c>, so xUnit does not discover it; each inherited <c>[Fact]</c> runs once per deriving
/// class.
/// </para>
/// <para>
/// Scope rule (ADR-005 §5): only behavior <em>any</em> correct provider must exhibit — Result
/// semantics (failure not throw), null-input handling, cancellation (<see cref="ResultExtensions"/>
/// cancelled on a pre-cancelled token), the fail-closed-on-expiry invariant, release idempotency, and
/// the ADR-010 security invariant that a session exposes no credentials. Mode-specific acquisition
/// mechanics (CDP attach, headed-login wait, storage-state export) stay in each implementation's own
/// test project.
/// </para>
/// </remarks>
public abstract class SiaraSessionProviderContract
{
    /// <summary>
    /// A shared trustworthy actor used when the contract needs to construct a SiaraSession directly
    /// (for example, for the expired-session test). All contract-created sessions use this actor so that
    /// the required AcquiredBy field is always satisfied.
    /// </summary>
    protected static readonly SiaraActor ContractActor = new()
    {
        ActorId = "contract-actor",
        ActorType = SiaraActorType.ServiceAccount,
    };

    /// <summary>Initializes the contract with the provider under test.</summary>
    /// <param name="sut">The <see cref="ISiaraSessionProvider"/> implementation to verify.</param>
    protected SiaraSessionProviderContract(ISiaraSessionProvider sut)
    {
        ArgumentNullException.ThrowIfNull(sut);
        Sut = sut;
    }

    /// <summary>Gets the provider under test.</summary>
    protected ISiaraSessionProvider Sut { get; }

    /// <summary>
    /// Builds a request that should succeed for the provider under test. Both mode-relevant fields are
    /// populated so the default works for either strategy; an implementation may override to supply a
    /// request tailored to its mode.
    /// </summary>
    /// <returns>A valid acquisition request.</returns>
    protected virtual SiaraSessionRequest ValidRequest() => new()
    {
        ExistingContextEndpoint = "ws://localhost:9222/devtools/browser/contract-fixture",
        MaxWaitForHuman = TimeSpan.FromSeconds(1),
        RequestedBy = "contract-test",
    };

    //
    // Mode
    //

    /// <summary>Contract: the provider reports a defined <see cref="SiaraAuthMode"/>.</summary>
    [Fact]
    public void Mode_IsDefinedAuthMode()
    {
        System.Enum.IsDefined(Sut.Mode).ShouldBeTrue();
    }

    //
    // AcquireAsync
    //

    /// <summary>Contract: a null request yields a failure Result — never a throw, never a null Result.</summary>
    [Fact]
    public async Task AcquireAsync_NullRequest_ReturnsFailure()
    {
        var result = await Sut.AcquireAsync(null!, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    /// <summary>Contract: a pre-cancelled token yields a Cancelled Result — never an exception.</summary>
    [Fact]
    public async Task AcquireAsync_PreCancelledToken_ReturnsCancelled()
    {
        var cancelled = new CancellationToken(canceled: true);

        var result = await Sut.AcquireAsync(ValidRequest(), cancelled);

        result.ShouldNotBeNull();
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Contract: a valid request yields a success Result wrapping an authenticated session whose mode
    /// matches the provider and whose correlation id and storage-state reference are populated.
    /// </summary>
    [Fact]
    public async Task AcquireAsync_ValidRequest_ReturnsAuthenticatedSession()
    {
        var result = await Sut.AcquireAsync(ValidRequest(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Mode.ShouldBe(Sut.Mode);
        result.Value.SessionId.ShouldNotBeNullOrEmpty();
        result.Value.StorageStateRef.ShouldNotBeNullOrEmpty();
    }

    //
    // EnsureValidAsync
    //

    /// <summary>Contract: a null session yields a failure Result.</summary>
    [Fact]
    public async Task EnsureValidAsync_NullSession_ReturnsFailure()
    {
        var result = await Sut.EnsureValidAsync(null!, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>Contract: a pre-cancelled token yields a Cancelled Result.</summary>
    [Fact]
    public async Task EnsureValidAsync_PreCancelledToken_ReturnsCancelled()
    {
        var session = await AcquireValidSessionAsync();
        var cancelled = new CancellationToken(canceled: true);

        var result = await Sut.EnsureValidAsync(session, cancelled);

        result.ShouldNotBeNull();
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>Contract: a freshly-acquired session validates successfully (keep-warm).</summary>
    [Fact]
    public async Task EnsureValidAsync_FreshlyAcquiredSession_ReturnsSuccess()
    {
        var session = await AcquireValidSessionAsync();

        var result = await Sut.EnsureValidAsync(session, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
    }

    /// <summary>
    /// Contract: an expired session must not validate as still-good — the provider either fails closed
    /// or returns a refreshed session with a future (or unknown) expiry. It must never return success
    /// while keeping a past expiry.
    /// </summary>
    [Fact]
    public async Task EnsureValidAsync_ExpiredSession_FailsClosedOrRefreshes()
    {
        var expired = new SiaraSession
        {
            SessionId = "contract-expired",
            Mode = Sut.Mode,
            StorageStateRef = "contract-expired-ref",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            AcquiredBy = ContractActor,
        };

        var result = await Sut.EnsureValidAsync(expired, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        if (result.IsSuccess)
        {
            // Accepted only if genuinely refreshed: expiry is now unknown or in the future.
            result.Value.ShouldNotBeNull();
            var refreshed = result.Value!.ExpiresAt;
            (refreshed is null || refreshed > DateTimeOffset.UtcNow).ShouldBeTrue(
                "EnsureValidAsync must not report an expired session as still valid.");
        }
        else
        {
            result.IsFailure.ShouldBeTrue();
        }
    }

    //
    // ReleaseAsync
    //

    /// <summary>Contract: releasing a null session yields a failure Result.</summary>
    [Fact]
    public async Task ReleaseAsync_NullSession_ReturnsFailure()
    {
        var result = await Sut.ReleaseAsync(null!, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>Contract: a pre-cancelled token yields a Cancelled Result.</summary>
    [Fact]
    public async Task ReleaseAsync_PreCancelledToken_ReturnsCancelled()
    {
        var session = await AcquireValidSessionAsync();
        var cancelled = new CancellationToken(canceled: true);

        var result = await Sut.ReleaseAsync(session, cancelled);

        result.ShouldNotBeNull();
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>Contract: releasing the same session twice is idempotent (the second release is not a failure).</summary>
    [Fact]
    public async Task ReleaseAsync_AcquiredSession_IsIdempotent()
    {
        var session = await AcquireValidSessionAsync();

        var first = await Sut.ReleaseAsync(session, TestContext.Current.CancellationToken);
        var second = await Sut.ReleaseAsync(session, TestContext.Current.CancellationToken);

        first.IsFailure.ShouldBeFalse();
        second.IsFailure.ShouldBeFalse();
    }

    //
    // Security invariant (ADR-010 §9)
    //

    /// <summary>
    /// Contract: the <see cref="SiaraSession"/> type exposes no raw-credential members — the
    /// never-store-credentials guarantee, enforced by reflection.
    /// </summary>
    [Fact]
    public void Session_TypeExposesNoCredentialMembers()
    {
        string[] forbidden = ["password", "passwd", "pwd", "username", "userid", "credential", "secret"];

        var members = typeof(SiaraSession)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name);

        foreach (var name in members)
        {
            foreach (var token in forbidden)
            {
                name.Contains(token, StringComparison.OrdinalIgnoreCase).ShouldBeFalse(
                    $"SiaraSession must not expose a credential-like member ('{name}' matched '{token}').");
            }
        }
    }

    private async Task<SiaraSession> AcquireValidSessionAsync()
    {
        var result = await Sut.AcquireAsync(ValidRequest(), TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue("contract setup requires a successful acquisition");
        result.Value.ShouldNotBeNull();
        return result.Value!;
    }
}
