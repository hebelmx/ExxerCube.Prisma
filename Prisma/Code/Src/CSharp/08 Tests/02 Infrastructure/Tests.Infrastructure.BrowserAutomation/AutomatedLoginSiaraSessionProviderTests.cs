namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Mode-specific mechanics for <see cref="AutomatedLoginSiaraSessionProvider"/> that the universal
/// <see cref="SiaraSessionProviderContract"/> leaves to the implementation (ADR-005 §5): the credential
/// path (transient use + disposal, P5), the P3 circuit-gating/fail-recording, and the fail-closed paths.
/// </summary>
public sealed class AutomatedLoginSiaraSessionProviderTests
{
    private static SiaraSessionRequest RequestBy(string? requestedBy = "orion-watcher") =>
        new() { RequestedBy = requestedBy };

    [Fact]
    public void Mode_IsAutomatedLogin()
    {
        AutomatedLoginTestFactory.CreateProvider().Mode.ShouldBe(SiaraAuthMode.AutomatedLogin);
    }

    [Fact]
    public async Task AcquireAsync_DrivesLoginWithSourcedCredentialsAndCaptures()
    {
        var agent = AutomatedLoginTestFactory.CreateSuccessfulAgentMock();
        var login = AutomatedLoginTestFactory.CreateAcceptingLoginServiceMock();
        var sut = AutomatedLoginTestFactory.CreateProvider(
            agent,
            AutomatedLoginTestFactory.CreateAuthenticatedContextMock(),
            login,
            new FakeSiaraCredentialSource("siara-bot", "vault-secret"),
            AutomatedLoginTestFactory.CreateCircuitBreaker());

        var result = await sut.AcquireAsync(RequestBy(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.StorageStateRef.ShouldBe("automated-captured-storage-state");
        result.Value.Mode.ShouldBe(SiaraAuthMode.AutomatedLogin);
        await login.Received(1).LoginAsync(agent, "siara-bot", "vault-secret", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcquireAsync_DisposesCredentialAfterLogin()
    {
        // A credential we keep a reference to, so we can prove the provider disposed it (P5).
        var credential = new SiaraCredential("siara-bot".ToCharArray(), "vault-secret".ToCharArray());
        var source = Substitute.For<ISiaraCredentialSource>();
        source.GetCredentialsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<SiaraCredential>.Success(credential));
        var sut = AutomatedLoginTestFactory.CreateProvider(
            AutomatedLoginTestFactory.CreateSuccessfulAgentMock(),
            AutomatedLoginTestFactory.CreateAuthenticatedContextMock(),
            AutomatedLoginTestFactory.CreateAcceptingLoginServiceMock(),
            source,
            AutomatedLoginTestFactory.CreateCircuitBreaker());

        var result = await sut.AcquireAsync(RequestBy(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await Should.ThrowAsync<ObjectDisposedException>(
            async () => await credential.UseAsync((u, p) => Task.FromResult(u)));
    }

    [Fact]
    public async Task AcquireAsync_WhenCircuitOpen_FailsWithoutTouchingCredentialsOrForm()
    {
        var breaker = AutomatedLoginTestFactory.CreateCircuitBreaker(
            new SiaraAutomatedOptions { MaxConsecutiveFailures = 1 });
        breaker.RecordFailure(); // hard stop

        var source = Substitute.For<ISiaraCredentialSource>();
        var login = AutomatedLoginTestFactory.CreateAcceptingLoginServiceMock();
        var sut = AutomatedLoginTestFactory.CreateProvider(
            AutomatedLoginTestFactory.CreateSuccessfulAgentMock(),
            AutomatedLoginTestFactory.CreateAuthenticatedContextMock(),
            login,
            source,
            breaker);

        var result = await sut.AcquireAsync(RequestBy(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        await source.DidNotReceive().GetCredentialsAsync(Arg.Any<CancellationToken>());
        await login.DidNotReceive().LoginAsync(
            Arg.Any<IBrowserAutomationAgent>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcquireAsync_WhenCredentialsMissing_FailsClosedWithoutTrippingBreaker()
    {
        var breaker = AutomatedLoginTestFactory.CreateCircuitBreaker();
        var source = Substitute.For<ISiaraCredentialSource>();
        source.GetCredentialsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<SiaraCredential>.WithFailure("no secret configured"));
        var login = AutomatedLoginTestFactory.CreateAcceptingLoginServiceMock();
        var sut = AutomatedLoginTestFactory.CreateProvider(
            AutomatedLoginTestFactory.CreateSuccessfulAgentMock(),
            AutomatedLoginTestFactory.CreateAuthenticatedContextMock(),
            login,
            source,
            breaker);

        var result = await sut.AcquireAsync(RequestBy(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        await login.DidNotReceive().LoginAsync(
            Arg.Any<IBrowserAutomationAgent>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        // A configuration fault is not a SIARA rejection: the breaker stays closed.
        breaker.CheckCanAttempt().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task AcquireAsync_WhenLoginRejected_RecordsFailureOnBreaker()
    {
        var breaker = AutomatedLoginTestFactory.CreateCircuitBreaker(); // default MaxConsecutiveFailures = 3
        var login = Substitute.For<ISiaraLoginService>();
        login.LoginAsync(Arg.Any<IBrowserAutomationAgent>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.WithFailure("contraseña incorrecta"));
        var context = AutomatedLoginTestFactory.CreateAuthenticatedContextMock();
        var sut = AutomatedLoginTestFactory.CreateProvider(
            AutomatedLoginTestFactory.CreateSuccessfulAgentMock(),
            context,
            login,
            new FakeSiaraCredentialSource(),
            breaker);

        var result = await sut.AcquireAsync(RequestBy(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        // The rejection tripped the breaker into a backoff window (1 < 3, so not a hard stop yet).
        breaker.CheckCanAttempt().IsFailure.ShouldBeTrue();
        // Fail-closed: a rejected login captures nothing.
        await context.DidNotReceive().ExportStorageStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcquireAsync_WhenProbeUnauthenticated_RecordsFailureAndFailsClosed()
    {
        var breaker = AutomatedLoginTestFactory.CreateCircuitBreaker();
        var context = AutomatedLoginTestFactory.CreateAuthenticatedContextMock();
        context.IsAuthenticatedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        var sut = AutomatedLoginTestFactory.CreateProvider(
            AutomatedLoginTestFactory.CreateSuccessfulAgentMock(),
            context,
            AutomatedLoginTestFactory.CreateAcceptingLoginServiceMock(),
            new FakeSiaraCredentialSource(),
            breaker);

        var result = await sut.AcquireAsync(RequestBy(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        breaker.CheckCanAttempt().IsFailure.ShouldBeTrue();
        await context.DidNotReceive().ExportStorageStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcquireAsync_WhenLaunchFails_FailsClosedWithoutTrippingBreaker()
    {
        var breaker = AutomatedLoginTestFactory.CreateCircuitBreaker();
        var agent = AutomatedLoginTestFactory.CreateSuccessfulAgentMock();
        agent.LaunchBrowserAsync(Arg.Any<CancellationToken>()).Returns(Result.WithFailure("no display"));
        var login = AutomatedLoginTestFactory.CreateAcceptingLoginServiceMock();
        var sut = AutomatedLoginTestFactory.CreateProvider(
            agent,
            AutomatedLoginTestFactory.CreateAuthenticatedContextMock(),
            login,
            new FakeSiaraCredentialSource(),
            breaker);

        var result = await sut.AcquireAsync(RequestBy(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        await login.DidNotReceive().LoginAsync(
            Arg.Any<IBrowserAutomationAgent>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        // Infrastructure fault, not a SIARA rejection: the breaker stays closed.
        breaker.CheckCanAttempt().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task AcquireAsync_RecordsTrustworthyAcquiringActor()
    {
        // The actor comes from the identity provider (the fake), not from the request's RequestedBy.
        var actorIdentity = new FakeSiaraActorIdentityProvider(actorId: "automated-service-account");
        var sut = AutomatedLoginTestFactory.CreateProvider(
            AutomatedLoginTestFactory.CreateSuccessfulAgentMock(),
            AutomatedLoginTestFactory.CreateAuthenticatedContextMock(),
            AutomatedLoginTestFactory.CreateAcceptingLoginServiceMock(),
            new FakeSiaraCredentialSource(),
            AutomatedLoginTestFactory.CreateCircuitBreaker(),
            actorIdentity: actorIdentity);

        var result = await sut.AcquireAsync(RequestBy("scheduler-7"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.AcquiredBy.ActorId.ShouldBe("automated-service-account");
    }

    [Fact]
    public async Task EnsureValidAsync_WhenExpired_FailsClosed()
    {
        var sut = AutomatedLoginTestFactory.CreateProvider();
        var expired = new SiaraSession
        {
            SessionId = "automated-expired",
            Mode = SiaraAuthMode.AutomatedLogin,
            StorageStateRef = "expired-ref",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            AcquiredBy = new SiaraActor { ActorId = "test-actor", ActorType = SiaraActorType.ServiceAccount },
        };

        var result = await sut.EnsureValidAsync(expired, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task EnsureValidAsync_WhenStillAuthenticated_ReHydratesAndReturnsSession()
    {
        var context = AutomatedLoginTestFactory.CreateAuthenticatedContextMock();
        var sut = AutomatedLoginTestFactory.CreateProvider(
            AutomatedLoginTestFactory.CreateSuccessfulAgentMock(),
            context,
            AutomatedLoginTestFactory.CreateAcceptingLoginServiceMock(),
            new FakeSiaraCredentialSource(),
            AutomatedLoginTestFactory.CreateCircuitBreaker());
        var acquired = await sut.AcquireAsync(RequestBy(), TestContext.Current.CancellationToken);

        var result = await sut.EnsureValidAsync(acquired.Value!, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.SessionId.ShouldBe(acquired.Value!.SessionId);
        await context.Received().LoadStorageStateAsync("automated-captured-storage-state", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReleaseAsync_ClosesOwnedBrowser()
    {
        var agent = AutomatedLoginTestFactory.CreateSuccessfulAgentMock();
        var sut = AutomatedLoginTestFactory.CreateProvider(
            agent,
            AutomatedLoginTestFactory.CreateAuthenticatedContextMock(),
            AutomatedLoginTestFactory.CreateAcceptingLoginServiceMock(),
            new FakeSiaraCredentialSource(),
            AutomatedLoginTestFactory.CreateCircuitBreaker());
        var acquired = await sut.AcquireAsync(RequestBy(), TestContext.Current.CancellationToken);

        var result = await sut.ReleaseAsync(acquired.Value!, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeFalse();
        await agent.Received(1).CloseBrowserAsync(Arg.Any<CancellationToken>());
    }
}
