using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Mechanics for the production <see cref="SiaraSessionProviderResolver"/> and the
/// <c>AddSiaraAuthentication</c> wiring (ADR-010 S7): each configured mode resolves to its strategy, and
/// an unknown/unregistered mode fails closed.
/// </summary>
public sealed class SiaraSessionProviderResolverTests
{
    [Fact]
    public void Resolve_ForSessionPassthrough_ReturnsPassthroughProvider()
    {
        var result = SiaraResolverTestFactory.CreateResolver(SiaraAuthMode.SessionPassthrough).Resolve();

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeOfType<SessionPassthroughSiaraSessionProvider>();
    }

    [Fact]
    public void Resolve_ForInteractiveLogin_ReturnsInteractiveProvider()
    {
        var result = SiaraResolverTestFactory.CreateResolver(SiaraAuthMode.InteractiveLogin).Resolve();

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeOfType<InteractiveLoginSiaraSessionProvider>();
    }

    [Fact]
    public void Resolve_ForAutomatedLogin_ReturnsAutomatedProvider()
    {
        var result = SiaraResolverTestFactory.CreateResolver(SiaraAuthMode.AutomatedLogin).Resolve();

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeOfType<AutomatedLoginSiaraSessionProvider>();
    }

    [Fact]
    public void Resolve_ForUndefinedMode_FailsClosed()
    {
        var result = SiaraResolverTestFactory.CreateResolver((SiaraAuthMode)999).Resolve();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Resolve_WhenNoProviderRegisteredForMode_FailsClosed()
    {
        using var empty = new ServiceCollection().BuildServiceProvider();
        var resolver = new SiaraSessionProviderResolver(
            empty,
            Options.Create(new SiaraAuthOptions { AuthMode = SiaraAuthMode.SessionPassthrough }),
            Substitute.For<ILogger<SiaraSessionProviderResolver>>());

        var result = resolver.Resolve();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void AddSiaraAuthentication_RegistersResolverAndSupportingServices()
    {
        using var provider = SiaraResolverTestFactory.BuildProvider(SiaraAuthMode.SessionPassthrough);

        provider.GetService<ISiaraSessionProviderResolver>().ShouldNotBeNull();
        provider.GetService<ISiaraCredentialSource>().ShouldNotBeNull();
        provider.GetService<SiaraLoginCircuitBreaker>().ShouldNotBeNull();
    }

    [Fact]
    public void AddSiaraAuthentication_RegistersAllThreeKeyedProviders()
    {
        using var provider = SiaraResolverTestFactory.BuildProvider(SiaraAuthMode.SessionPassthrough);

        provider.GetKeyedService<ISiaraSessionProvider>(SiaraAuthMode.SessionPassthrough).ShouldNotBeNull();
        provider.GetKeyedService<ISiaraSessionProvider>(SiaraAuthMode.InteractiveLogin).ShouldNotBeNull();
        provider.GetKeyedService<ISiaraSessionProvider>(SiaraAuthMode.AutomatedLogin).ShouldNotBeNull();
    }
}
