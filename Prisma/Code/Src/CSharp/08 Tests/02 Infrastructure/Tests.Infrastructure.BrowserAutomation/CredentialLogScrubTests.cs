using ExxerCube.Prisma.Infrastructure.BrowserAutomation.Services;

namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Verifies that the AutomatedLogin acquisition path never emits raw credentials
/// (username or password) to any logger — ADR-010 precondition P5.
/// </summary>
public sealed class CredentialLogScrubTests
{
    private const string SecretUsername = "super-secret-user";
    private const string SecretPassword = "super-secret-pass";

    // -----------------------------------------------------------------------
    // Test 1 — full AcquireAsync path: neither logger leaks either secret
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AcquireAsync_NeverLogsUsernameOrPassword_AcrossProviderAndLoginServiceLoggers()
    {
        var providerLogger = new CapturingLogger<AutomatedLoginSiaraSessionProvider>();
        var loginLogger = new CapturingLogger<SiaraLoginService>();

        var agent = BuildLoginCapableAgentMock();
        var loginService = new SiaraLoginService(loginLogger);
        var context = AutomatedLoginTestFactory.CreateAuthenticatedContextMock();
        var source = new FakeSiaraCredentialSource(SecretUsername, SecretPassword);
        var breaker = AutomatedLoginTestFactory.CreateCircuitBreaker();

        var options = Options.Create(new SiaraAuthOptions
        {
            AuthMode = SiaraAuthMode.AutomatedLogin,
            Automated = new SiaraAutomatedOptions
            {
                LoginUrl = "https://siara.example/login",
                PostLoginSelector = "#dashboard",
            },
        });

        var sut = new AutomatedLoginSiaraSessionProvider(
            agent,
            context,
            new FakeSiaraActorIdentityProvider(),
            loginService,
            source,
            breaker,
            options,
            providerLogger);

        var result = await sut.AcquireAsync(
            new SiaraSessionRequest { RequestedBy = "test-runner" },
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("AcquireAsync must succeed for the log-scrub assertion to be meaningful");

        var allMessages = providerLogger.Messages.Concat(loginLogger.Messages).ToList();

        allMessages
            .Any(m => m.Contains(SecretUsername, StringComparison.OrdinalIgnoreCase))
            .ShouldBeFalse("username must never appear in any log message");

        allMessages
            .Any(m => m.Contains(SecretPassword, StringComparison.OrdinalIgnoreCase))
            .ShouldBeFalse("password must never appear in any log message");
    }

    // -----------------------------------------------------------------------
    // Test 2 — SiaraLoginService in isolation: LoginAsync never logs either secret
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LoginAsync_NeverLogsUsernameOrPassword()
    {
        var loginLogger = new CapturingLogger<SiaraLoginService>();
        var agent = BuildLoginCapableAgentMock();
        var sut = new SiaraLoginService(loginLogger);

        var result = await sut.LoginAsync(
            agent,
            SecretUsername,
            SecretPassword,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("LoginAsync must succeed for the log-scrub assertion to be meaningful");

        loginLogger.Messages
            .Any(m => m.Contains(SecretUsername, StringComparison.OrdinalIgnoreCase))
            .ShouldBeFalse("username must never appear in any SiaraLoginService log message");

        loginLogger.Messages
            .Any(m => m.Contains(SecretPassword, StringComparison.OrdinalIgnoreCase))
            .ShouldBeFalse("password must never appear in any SiaraLoginService log message");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a browser-agent mock that succeeds at all selectors SiaraLoginService touches,
    /// so the full login path executes (and has the opportunity to log credentials).
    /// </summary>
    private static IBrowserAutomationAgent BuildLoginCapableAgentMock()
    {
        var agent = Substitute.For<IBrowserAutomationAgent>();

        agent.LaunchBrowserAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Result.Success());

        agent.NavigateToAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Result.Success());

        agent.WaitForSelectorAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Result.Success());

        agent.FillInputAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Result.Success());

        agent.ClickElementAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Result.Success());

        agent.CloseBrowserAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Result.Success());

        return agent;
    }
}

/// <summary>
/// Minimal <see cref="ILogger{T}"/> implementation that captures every formatted log message into a
/// thread-safe list so tests can assert that no sensitive value was emitted.
/// </summary>
/// <typeparam name="T">The category type.</typeparam>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<string> _messages = [];
    private readonly object _gate = new();

    /// <summary>Gets all captured, fully-formatted log messages.</summary>
    public IReadOnlyList<string> Messages
    {
        get
        {
            lock (_gate)
            {
                return [.. _messages];
            }
        }
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        lock (_gate)
        {
            _messages.Add(message);
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
