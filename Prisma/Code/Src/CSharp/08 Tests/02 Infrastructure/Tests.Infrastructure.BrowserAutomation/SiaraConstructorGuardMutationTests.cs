using ExxerCube.Prisma.Infrastructure.BrowserAutomation.Services;
using Microsoft.Extensions.Configuration;

namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Pins every <c>ArgumentNullException.ThrowIfNull</c> constructor guard across the SIARA auth surface so a
/// "remove the guard" (statement) mutation is observable. Each case passes <c>null</c> for exactly one
/// dependency and asserts the constructor throws.
/// </summary>
public sealed class SiaraConstructorGuardMutationTests
{
    private static IOptions<SiaraAuthOptions> Opts() => Options.Create(new SiaraAuthOptions());
    private static ILogger<T> Log<T>() => Substitute.For<ILogger<T>>();
    private static SiaraHostPolicy HostPolicy() =>
        new(Opts(), Substitute.For<ILogger<SiaraHostPolicy>>());

    [Fact]
    public void SiaraLoginService_NullArgs_Throw()
    {
        Should.Throw<ArgumentNullException>(() => new SiaraLoginService(null!, Log<SiaraLoginService>()));
        Should.Throw<ArgumentNullException>(() => new SiaraLoginService(HostPolicy(), null!));
    }

    [Fact]
    public void SiaraHostPolicy_NullArgs_Throw()
    {
        Should.Throw<ArgumentNullException>(() => new SiaraHostPolicy(null!, Substitute.For<ILogger<SiaraHostPolicy>>()));
        Should.Throw<ArgumentNullException>(() => new SiaraHostPolicy(Opts(), null!));
    }

    [Fact]
    public void SiaraLoginCircuitBreaker_NullArgs_Throw()
    {
        Should.Throw<ArgumentNullException>(() => new SiaraLoginCircuitBreaker(null!, TimeProvider.System, Log<SiaraLoginCircuitBreaker>()));
        Should.Throw<ArgumentNullException>(() => new SiaraLoginCircuitBreaker(Opts(), null!, Log<SiaraLoginCircuitBreaker>()));
        Should.Throw<ArgumentNullException>(() => new SiaraLoginCircuitBreaker(Opts(), TimeProvider.System, null!));
    }

    [Fact]
    public void SiaraSessionProviderResolver_NullArgs_Throw()
    {
        var sp = Substitute.For<IServiceProvider>();
        Should.Throw<ArgumentNullException>(() => new SiaraSessionProviderResolver(null!, Opts(), Log<SiaraSessionProviderResolver>()));
        Should.Throw<ArgumentNullException>(() => new SiaraSessionProviderResolver(sp, null!, Log<SiaraSessionProviderResolver>()));
        Should.Throw<ArgumentNullException>(() => new SiaraSessionProviderResolver(sp, Opts(), null!));
    }

    [Fact]
    public void ConfiguredSiaraCredentialSource_NullArgs_Throw()
    {
        var config = Substitute.For<IConfiguration>();
        Should.Throw<ArgumentNullException>(() => new ConfiguredSiaraCredentialSource(null!, Opts(), Log<ConfiguredSiaraCredentialSource>()));
        Should.Throw<ArgumentNullException>(() => new ConfiguredSiaraCredentialSource(config, null!, Log<ConfiguredSiaraCredentialSource>()));
        Should.Throw<ArgumentNullException>(() => new ConfiguredSiaraCredentialSource(config, Opts(), null!));
    }

    [Fact]
    public void ConfiguredSiaraActorIdentityProvider_NullArgs_Throw()
    {
        Should.Throw<ArgumentNullException>(() => new ConfiguredSiaraActorIdentityProvider(null!, Log<ConfiguredSiaraActorIdentityProvider>()));
        Should.Throw<ArgumentNullException>(() => new ConfiguredSiaraActorIdentityProvider(Opts(), null!));
    }

    [Fact]
    public void SessionPassthroughSiaraSessionProvider_NullArgs_Throw()
    {
        var ctx = Substitute.For<IBrowserSessionContext>();
        var actor = new FakeSiaraActorIdentityProvider();
        Should.Throw<ArgumentNullException>(() => new SessionPassthroughSiaraSessionProvider(null!, actor, Opts(), Log<SessionPassthroughSiaraSessionProvider>()));
        Should.Throw<ArgumentNullException>(() => new SessionPassthroughSiaraSessionProvider(ctx, null!, Opts(), Log<SessionPassthroughSiaraSessionProvider>()));
        Should.Throw<ArgumentNullException>(() => new SessionPassthroughSiaraSessionProvider(ctx, actor, null!, Log<SessionPassthroughSiaraSessionProvider>()));
        Should.Throw<ArgumentNullException>(() => new SessionPassthroughSiaraSessionProvider(ctx, actor, Opts(), null!));
    }

    [Fact]
    public void InteractiveLoginSiaraSessionProvider_NullArgs_Throw()
    {
        var agent = Substitute.For<IBrowserAutomationAgent>();
        var ctx = Substitute.For<IBrowserSessionContext>();
        var actor = new FakeSiaraActorIdentityProvider();
        Should.Throw<ArgumentNullException>(() => new InteractiveLoginSiaraSessionProvider(null!, ctx, actor, Opts(), Log<InteractiveLoginSiaraSessionProvider>()));
        Should.Throw<ArgumentNullException>(() => new InteractiveLoginSiaraSessionProvider(agent, null!, actor, Opts(), Log<InteractiveLoginSiaraSessionProvider>()));
        Should.Throw<ArgumentNullException>(() => new InteractiveLoginSiaraSessionProvider(agent, ctx, null!, Opts(), Log<InteractiveLoginSiaraSessionProvider>()));
        Should.Throw<ArgumentNullException>(() => new InteractiveLoginSiaraSessionProvider(agent, ctx, actor, null!, Log<InteractiveLoginSiaraSessionProvider>()));
        Should.Throw<ArgumentNullException>(() => new InteractiveLoginSiaraSessionProvider(agent, ctx, actor, Opts(), null!));
    }

    [Fact]
    public void AutomatedLoginSiaraSessionProvider_NullArgs_Throw()
    {
        var agent = Substitute.For<IBrowserAutomationAgent>();
        var ctx = Substitute.For<IBrowserSessionContext>();
        var actor = new FakeSiaraActorIdentityProvider();
        var login = Substitute.For<ISiaraLoginService>();
        var source = new FakeSiaraCredentialSource();
        var breaker = AutomatedLoginTestFactory.CreateCircuitBreaker();
        var log = Log<AutomatedLoginSiaraSessionProvider>();

        Should.Throw<ArgumentNullException>(() => new AutomatedLoginSiaraSessionProvider(null!, ctx, actor, login, source, breaker, Opts(), log));
        Should.Throw<ArgumentNullException>(() => new AutomatedLoginSiaraSessionProvider(agent, null!, actor, login, source, breaker, Opts(), log));
        Should.Throw<ArgumentNullException>(() => new AutomatedLoginSiaraSessionProvider(agent, ctx, null!, login, source, breaker, Opts(), log));
        Should.Throw<ArgumentNullException>(() => new AutomatedLoginSiaraSessionProvider(agent, ctx, actor, null!, source, breaker, Opts(), log));
        Should.Throw<ArgumentNullException>(() => new AutomatedLoginSiaraSessionProvider(agent, ctx, actor, login, null!, breaker, Opts(), log));
        Should.Throw<ArgumentNullException>(() => new AutomatedLoginSiaraSessionProvider(agent, ctx, actor, login, source, null!, Opts(), log));
        Should.Throw<ArgumentNullException>(() => new AutomatedLoginSiaraSessionProvider(agent, ctx, actor, login, source, breaker, null!, log));
        Should.Throw<ArgumentNullException>(() => new AutomatedLoginSiaraSessionProvider(agent, ctx, actor, login, source, breaker, Opts(), null!));
    }
}
