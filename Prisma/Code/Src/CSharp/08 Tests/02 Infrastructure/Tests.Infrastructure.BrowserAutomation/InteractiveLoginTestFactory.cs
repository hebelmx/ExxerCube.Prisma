namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Builds <see cref="InteractiveLoginSiaraSessionProvider"/> instances over a mocked
/// <see cref="IBrowserAutomationAgent"/> and <see cref="IBrowserSessionContext"/> for the contract
/// inheritor and the mode-specific mechanics tests.
/// </summary>
/// <remarks>
/// The default mocks model a successful interactive login: the browser launches and navigates, the human
/// completes login (the post-login wait succeeds), the context probes as authenticated, and capture returns
/// a portable storage-state. The mechanics tests override individual behaviors to exercise the timeout,
/// launch/navigate-failure, and fail-closed paths.
/// </remarks>
internal static class InteractiveLoginTestFactory
{
    /// <summary>Builds a browser-agent mock that behaves as a successful headed login session.</summary>
    public static IBrowserAutomationAgent CreateSuccessfulAgentMock()
    {
        var agent = Substitute.For<IBrowserAutomationAgent>();

        agent.LaunchBrowserAsync(Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<CancellationToken>(0).IsCancellationRequested
                ? ResultExtensions.Cancelled()
                : Result.Success());

        agent.NavigateToAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<CancellationToken>(1).IsCancellationRequested
                ? ResultExtensions.Cancelled()
                : Result.Success());

        agent.WaitForSelectorAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<CancellationToken>(2).IsCancellationRequested
                ? ResultExtensions.Cancelled()
                : Result.Success());

        agent.CloseBrowserAsync(Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<CancellationToken>(0).IsCancellationRequested
                ? ResultExtensions.Cancelled()
                : Result.Success());

        return agent;
    }

    /// <summary>Builds a session-context mock that behaves as a healthy, authenticated context.</summary>
    public static IBrowserSessionContext CreateAuthenticatedContextMock()
    {
        var mock = Substitute.For<IBrowserSessionContext>();

        mock.LoadStorageStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<CancellationToken>(1).IsCancellationRequested
                ? ResultExtensions.Cancelled()
                : Result.Success());

        mock.IsAuthenticatedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<CancellationToken>(1).IsCancellationRequested
                ? ResultExtensions.Cancelled<bool>()
                : Result<bool>.Success(true));

        mock.ExportStorageStateAsync(Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<CancellationToken>(0).IsCancellationRequested
                ? ResultExtensions.Cancelled<string>()
                : Result<string>.Success("interactive-captured-storage-state"));

        return mock;
    }

    /// <summary>Wraps an agent + session-context in a provider with the given interactive options.</summary>
    /// <param name="agent">The browser agent mock to use.</param>
    /// <param name="sessionContext">The browser session context mock to use.</param>
    /// <param name="interactive">Optional interactive options; defaults to a sensible test setup.</param>
    /// <param name="actorIdentity">Optional actor identity provider; defaults to a fresh fake.</param>
    public static InteractiveLoginSiaraSessionProvider CreateProvider(
        IBrowserAutomationAgent agent,
        IBrowserSessionContext sessionContext,
        SiaraInteractiveOptions? interactive = null,
        ISiaraActorIdentityProvider? actorIdentity = null)
    {
        var options = Options.Create(new SiaraAuthOptions
        {
            AuthMode = SiaraAuthMode.InteractiveLogin,
            Interactive = interactive ?? new SiaraInteractiveOptions
            {
                LoginUrl = "https://siara.example/login",
                PostLoginSelector = "#dashboard",
            },
        });

        return new InteractiveLoginSiaraSessionProvider(
            agent,
            sessionContext,
            actorIdentity ?? new FakeSiaraActorIdentityProvider(),
            options,
            Substitute.For<ILogger<InteractiveLoginSiaraSessionProvider>>());
    }

    /// <summary>Builds a provider over the default successful agent + authenticated context mocks.</summary>
    /// <param name="interactive">Optional interactive options.</param>
    /// <param name="actorIdentity">Optional actor identity provider; defaults to a fresh fake.</param>
    public static InteractiveLoginSiaraSessionProvider CreateProvider(
        SiaraInteractiveOptions? interactive = null,
        ISiaraActorIdentityProvider? actorIdentity = null) =>
        CreateProvider(CreateSuccessfulAgentMock(), CreateAuthenticatedContextMock(), interactive, actorIdentity);
}
