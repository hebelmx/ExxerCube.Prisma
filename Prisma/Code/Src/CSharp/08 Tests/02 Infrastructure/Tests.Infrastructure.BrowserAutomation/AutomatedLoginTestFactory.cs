namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Builds <see cref="AutomatedLoginSiaraSessionProvider"/> instances and its collaborators over mocks/fakes
/// for the contract inheritor and the mode-specific mechanics tests.
/// </summary>
/// <remarks>
/// The defaults model a successful unattended login: the circuit is closed, the credential source yields
/// usable credentials, the browser launches and navigates, the form driver accepts, the context probes as
/// authenticated, and capture returns a portable storage-state. Mechanics tests override individual pieces
/// to exercise the circuit-gating, fail-closed, and credential-disposal paths.
/// </remarks>
internal static class AutomatedLoginTestFactory
{
    /// <summary>Builds a browser-agent mock that launches/navigates/closes successfully.</summary>
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
                : Result<string>.Success("automated-captured-storage-state"));

        return mock;
    }

    /// <summary>Builds a login-service mock that accepts the submitted credentials.</summary>
    public static ISiaraLoginService CreateAcceptingLoginServiceMock()
    {
        var login = Substitute.For<ISiaraLoginService>();
        login.LoginAsync(Arg.Any<IBrowserAutomationAgent>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<CancellationToken>(3).IsCancellationRequested
                ? ResultExtensions.Cancelled()
                : Result.Success());
        return login;
    }

    /// <summary>Builds a circuit-breaker with a real clock and the given (or default) automated options.</summary>
    public static SiaraLoginCircuitBreaker CreateCircuitBreaker(SiaraAutomatedOptions? automated = null)
    {
        var options = Options.Create(new SiaraAuthOptions
        {
            AuthMode = SiaraAuthMode.AutomatedLogin,
            Automated = automated ?? new SiaraAutomatedOptions(),
        });
        return new SiaraLoginCircuitBreaker(
            options, TimeProvider.System, Substitute.For<ILogger<SiaraLoginCircuitBreaker>>());
    }

    /// <summary>Wraps the supplied collaborators in a provider.</summary>
    public static AutomatedLoginSiaraSessionProvider CreateProvider(
        IBrowserAutomationAgent agent,
        IBrowserSessionContext sessionContext,
        ISiaraLoginService loginService,
        ISiaraCredentialSource credentialSource,
        SiaraLoginCircuitBreaker circuitBreaker,
        SiaraAutomatedOptions? automated = null)
    {
        var options = Options.Create(new SiaraAuthOptions
        {
            AuthMode = SiaraAuthMode.AutomatedLogin,
            Automated = automated ?? new SiaraAutomatedOptions { LoginUrl = "https://siara.example/login", PostLoginSelector = "#dashboard" },
        });

        return new AutomatedLoginSiaraSessionProvider(
            agent,
            sessionContext,
            loginService,
            credentialSource,
            circuitBreaker,
            options,
            Substitute.For<ILogger<AutomatedLoginSiaraSessionProvider>>());
    }

    /// <summary>Builds a provider over the default successful collaborators.</summary>
    public static AutomatedLoginSiaraSessionProvider CreateProvider() => CreateProvider(
        CreateSuccessfulAgentMock(),
        CreateAuthenticatedContextMock(),
        CreateAcceptingLoginServiceMock(),
        new FakeSiaraCredentialSource(),
        CreateCircuitBreaker());
}
