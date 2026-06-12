namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Mode-specific mechanics for <see cref="InteractiveLoginSiaraSessionProvider"/> that the universal
/// <see cref="SiaraSessionProviderContract"/> intentionally leaves to the implementation (ADR-005 §5):
/// the launch/navigate/human-wait sequence, the timeout fail-closed path, storage-state capture, and the
/// re-hydrate-and-re-probe of <c>EnsureValidAsync</c>.
/// </summary>
public sealed class InteractiveLoginSiaraSessionProviderTests
{
    private static SiaraSessionRequest RequestWith(TimeSpan? maxWait, string? requestedBy = "operator") =>
        new() { MaxWaitForHuman = maxWait, RequestedBy = requestedBy };

    [Fact]
    public void Mode_IsInteractiveLogin()
    {
        var sut = InteractiveLoginTestFactory.CreateProvider();

        sut.Mode.ShouldBe(SiaraAuthMode.InteractiveLogin);
    }

    [Fact]
    public async Task AcquireAsync_LaunchesNavigatesAndWaitsForPostLoginSelector()
    {
        var agent = InteractiveLoginTestFactory.CreateSuccessfulAgentMock();
        var context = InteractiveLoginTestFactory.CreateAuthenticatedContextMock();
        var sut = InteractiveLoginTestFactory.CreateProvider(
            agent,
            context,
            new SiaraInteractiveOptions { LoginUrl = "https://siara.example/login", PostLoginSelector = "#dashboard" });

        var result = await sut.AcquireAsync(RequestWith(TimeSpan.FromSeconds(30)), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        Received.InOrder(() =>
        {
            agent.LaunchBrowserAsync(Arg.Any<CancellationToken>());
            agent.NavigateToAsync("https://siara.example/login", Arg.Any<CancellationToken>());
            agent.WaitForSelectorAsync("#dashboard", Arg.Any<int?>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task AcquireAsync_UsesRequestMaxWaitForHumanAsTimeout()
    {
        var agent = InteractiveLoginTestFactory.CreateSuccessfulAgentMock();
        var sut = InteractiveLoginTestFactory.CreateProvider(agent, InteractiveLoginTestFactory.CreateAuthenticatedContextMock());

        var result = await sut.AcquireAsync(RequestWith(TimeSpan.FromSeconds(45)), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await agent.Received(1).WaitForSelectorAsync(Arg.Any<string>(), 45_000, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcquireAsync_WhenRequestMaxWaitNull_UsesConfiguredDefault()
    {
        var agent = InteractiveLoginTestFactory.CreateSuccessfulAgentMock();
        var sut = InteractiveLoginTestFactory.CreateProvider(
            agent,
            InteractiveLoginTestFactory.CreateAuthenticatedContextMock(),
            new SiaraInteractiveOptions { MaxWaitForHuman = TimeSpan.FromSeconds(90), PostLoginSelector = "#dashboard" });

        var result = await sut.AcquireAsync(RequestWith(maxWait: null), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await agent.Received(1).WaitForSelectorAsync(Arg.Any<string>(), 90_000, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcquireAsync_WhenHumanDoesNotCompleteLoginInTime_FailsClosed()
    {
        var agent = InteractiveLoginTestFactory.CreateSuccessfulAgentMock();
        agent.WaitForSelectorAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Result.WithFailure("timeout waiting for selector"));
        var context = InteractiveLoginTestFactory.CreateAuthenticatedContextMock();
        var sut = InteractiveLoginTestFactory.CreateProvider(agent, context);

        var result = await sut.AcquireAsync(RequestWith(TimeSpan.FromSeconds(1)), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
        // Fail-closed: no session is captured when the human did not finish logging in.
        await context.DidNotReceive().ExportStorageStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcquireAsync_WhenLaunchFails_ReturnsFailureWithoutNavigating()
    {
        var agent = InteractiveLoginTestFactory.CreateSuccessfulAgentMock();
        agent.LaunchBrowserAsync(Arg.Any<CancellationToken>()).Returns(Result.WithFailure("no display"));
        var sut = InteractiveLoginTestFactory.CreateProvider(agent, InteractiveLoginTestFactory.CreateAuthenticatedContextMock());

        var result = await sut.AcquireAsync(RequestWith(TimeSpan.FromSeconds(1)), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        await agent.DidNotReceive().NavigateToAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcquireAsync_WhenNavigateFails_ReturnsFailureWithoutWaiting()
    {
        var agent = InteractiveLoginTestFactory.CreateSuccessfulAgentMock();
        agent.NavigateToAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.WithFailure("bad url"));
        var sut = InteractiveLoginTestFactory.CreateProvider(agent, InteractiveLoginTestFactory.CreateAuthenticatedContextMock());

        var result = await sut.AcquireAsync(RequestWith(TimeSpan.FromSeconds(1)), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        await agent.DidNotReceive().WaitForSelectorAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcquireAsync_CapturesExportedStorageState()
    {
        var sut = InteractiveLoginTestFactory.CreateProvider();

        var result = await sut.AcquireAsync(RequestWith(TimeSpan.FromSeconds(1)), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.StorageStateRef.ShouldBe("interactive-captured-storage-state");
    }

    [Fact]
    public async Task AcquireAsync_WhenExportUnavailable_FailsClosed()
    {
        var agent = InteractiveLoginTestFactory.CreateSuccessfulAgentMock();
        var context = InteractiveLoginTestFactory.CreateAuthenticatedContextMock();
        context.ExportStorageStateAsync(Arg.Any<CancellationToken>())
            .Returns(Result<string>.WithFailure("no session to export"));
        var sut = InteractiveLoginTestFactory.CreateProvider(agent, context);

        var result = await sut.AcquireAsync(RequestWith(TimeSpan.FromSeconds(1)), TestContext.Current.CancellationToken);

        // Unlike passthrough, interactive has no imported reference to fall back to — it must fail closed.
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task AcquireAsync_RecordsTrustworthyAcquiringActor()
    {
        // The actor comes from the identity provider (the fake), not from the request's RequestedBy.
        var actorIdentity = new FakeSiaraActorIdentityProvider(actorId: "interactive-service-account");
        var sut = InteractiveLoginTestFactory.CreateProvider(actorIdentity: actorIdentity);

        var result = await sut.AcquireAsync(
            RequestWith(TimeSpan.FromSeconds(1), requestedBy: "reviewer-3"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.AcquiredBy.ActorId.ShouldBe("interactive-service-account");
        result.Value.Mode.ShouldBe(SiaraAuthMode.InteractiveLogin);
    }

    [Fact]
    public async Task EnsureValidAsync_WhenExpired_FailsClosedForReLogin()
    {
        var sut = InteractiveLoginTestFactory.CreateProvider();
        var expired = new SiaraSession
        {
            SessionId = "interactive-expired",
            Mode = SiaraAuthMode.InteractiveLogin,
            StorageStateRef = "expired-ref",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            AcquiredBy = new SiaraActor { ActorId = "test-actor", ActorType = SiaraActorType.ServiceAccount },
        };

        var result = await sut.EnsureValidAsync(expired, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task EnsureValidAsync_WhenStillAuthenticated_ReHydratesAndReturnsSession()
    {
        var agent = InteractiveLoginTestFactory.CreateSuccessfulAgentMock();
        var context = InteractiveLoginTestFactory.CreateAuthenticatedContextMock();
        var sut = InteractiveLoginTestFactory.CreateProvider(agent, context);
        var acquired = await sut.AcquireAsync(RequestWith(TimeSpan.FromSeconds(1)), TestContext.Current.CancellationToken);
        acquired.IsSuccess.ShouldBeTrue();

        var result = await sut.EnsureValidAsync(acquired.Value!, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.SessionId.ShouldBe(acquired.Value!.SessionId);
        await context.Received().LoadStorageStateAsync("interactive-captured-storage-state", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureValidAsync_WhenReprobeUnauthenticated_FailsClosed()
    {
        var agent = InteractiveLoginTestFactory.CreateSuccessfulAgentMock();
        var context = InteractiveLoginTestFactory.CreateAuthenticatedContextMock();
        var sut = InteractiveLoginTestFactory.CreateProvider(agent, context);
        var acquired = await sut.AcquireAsync(RequestWith(TimeSpan.FromSeconds(1)), TestContext.Current.CancellationToken);

        // The captured session has gone stale underneath us: the re-probe reports unauthenticated.
        context.IsAuthenticatedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));

        var result = await sut.EnsureValidAsync(acquired.Value!, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ReleaseAsync_ClosesOwnedBrowser()
    {
        var agent = InteractiveLoginTestFactory.CreateSuccessfulAgentMock();
        var context = InteractiveLoginTestFactory.CreateAuthenticatedContextMock();
        var sut = InteractiveLoginTestFactory.CreateProvider(agent, context);
        var acquired = await sut.AcquireAsync(RequestWith(TimeSpan.FromSeconds(1)), TestContext.Current.CancellationToken);

        var result = await sut.ReleaseAsync(acquired.Value!, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeFalse();
        await agent.Received(1).CloseBrowserAsync(Arg.Any<CancellationToken>());
    }
}
