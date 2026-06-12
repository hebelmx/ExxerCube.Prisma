namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Builds <see cref="SessionPassthroughSiaraSessionProvider"/> instances over a mocked
/// <see cref="IBrowserSessionContext"/> for the contract inheritor and the mode-specific mechanics tests.
/// </summary>
/// <remarks>
/// The default mock models a fully-authenticated external context (attach succeeds, the probe reports
/// authenticated, capture returns a portable storage-state), which is what the universal contract
/// requires. The mechanics tests override individual behaviors to exercise the fail-closed and
/// transport-selection paths.
/// </remarks>
internal static class SessionPassthroughTestFactory
{
    /// <summary>Builds a session-context mock that behaves as a healthy, authenticated external context.</summary>
    public static IBrowserSessionContext CreateAuthenticatedContextMock()
    {
        var mock = Substitute.For<IBrowserSessionContext>();

        mock.LoadStorageStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<CancellationToken>(1).IsCancellationRequested
                ? ResultExtensions.Cancelled()
                : Result.Success());

        mock.ConnectToExistingContextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
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
                : Result<string>.Success("passthrough-captured-storage-state"));

        return mock;
    }

    /// <summary>Wraps a session-context in a provider with the given passthrough options.</summary>
    /// <param name="sessionContext">The browser session context to use.</param>
    /// <param name="passthrough">Optional passthrough options; defaults to a sensible test setup.</param>
    /// <param name="actorIdentity">Optional actor identity provider; defaults to a fresh fake.</param>
    public static SessionPassthroughSiaraSessionProvider CreateProvider(
        IBrowserSessionContext sessionContext,
        SiaraPassthroughOptions? passthrough = null,
        ISiaraActorIdentityProvider? actorIdentity = null)
    {
        var options = Options.Create(new SiaraAuthOptions
        {
            AuthMode = SiaraAuthMode.SessionPassthrough,
            Passthrough = passthrough ?? new SiaraPassthroughOptions { PostLoginSelector = "#dashboard" },
        });

        return new SessionPassthroughSiaraSessionProvider(
            sessionContext,
            actorIdentity ?? new FakeSiaraActorIdentityProvider(),
            options,
            Substitute.For<ILogger<SessionPassthroughSiaraSessionProvider>>());
    }
}
