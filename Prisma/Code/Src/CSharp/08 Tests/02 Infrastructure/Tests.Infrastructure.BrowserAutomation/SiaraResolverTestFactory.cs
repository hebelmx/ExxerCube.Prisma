using ExxerCube.Prisma.Infrastructure.BrowserAutomation.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Builds a real <see cref="IServiceProvider"/> wired by <c>AddSiaraAuthentication</c> over mocked browser
/// dependencies, so the resolver and the keyed-provider registration are exercised end-to-end without a
/// live browser or real secret store.
/// </summary>
internal static class SiaraResolverTestFactory
{
    /// <summary>Builds a service provider with the SIARA auth seam configured for the given mode.</summary>
    public static ServiceProvider BuildProvider(SiaraAuthMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // The browser/login collaborators the providers depend on (mocked — no live browser).
        services.AddSingleton(Substitute.For<IBrowserAutomationAgent>());
        services.AddSingleton(Substitute.For<IBrowserSessionContext>());
        services.AddSingleton(Substitute.For<ISiaraLoginService>());
        services.AddSingleton(Substitute.For<IConfiguration>());

        services.AddSiaraAuthentication(configure: options => options.AuthMode = mode);

        return services.BuildServiceProvider();
    }

    /// <summary>Builds the resolver from a provider configured for the given mode.</summary>
    public static ISiaraSessionProviderResolver CreateResolver(SiaraAuthMode mode) =>
        BuildProvider(mode).GetRequiredService<ISiaraSessionProviderResolver>();
}
