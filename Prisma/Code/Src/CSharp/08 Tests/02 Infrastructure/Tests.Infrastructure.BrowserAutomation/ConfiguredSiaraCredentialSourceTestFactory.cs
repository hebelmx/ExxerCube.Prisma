using Microsoft.Extensions.Configuration;

namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Builds <see cref="ConfiguredSiaraCredentialSource"/> instances over a mocked <see cref="IConfiguration"/>
/// for the contract inheritor and the mechanics tests.
/// </summary>
internal static class ConfiguredSiaraCredentialSourceTestFactory
{
    public const string UsernameKey = "Siara:Credentials:Username";
    public const string PasswordKey = "Siara:Credentials:Password";

    /// <summary>Builds a configuration mock that serves the given credential values at the default keys.</summary>
    public static IConfiguration CreateConfigurationMock(string? username = "siara-bot", string? password = "vault-secret")
    {
        var configuration = Substitute.For<IConfiguration>();
        configuration[UsernameKey].Returns(username);
        configuration[PasswordKey].Returns(password);
        return configuration;
    }

    /// <summary>Wraps a configuration in a credential source with the given automated options.</summary>
    public static ConfiguredSiaraCredentialSource CreateSource(
        IConfiguration configuration,
        SiaraAutomatedOptions? automated = null)
    {
        var options = Options.Create(new SiaraAuthOptions
        {
            AuthMode = SiaraAuthMode.AutomatedLogin,
            Automated = automated ?? new SiaraAutomatedOptions(),
        });

        return new ConfiguredSiaraCredentialSource(
            configuration,
            options,
            Substitute.For<ILogger<ConfiguredSiaraCredentialSource>>());
    }

    /// <summary>Builds a source over a configuration mock that serves healthy default credentials.</summary>
    public static ConfiguredSiaraCredentialSource CreateSource() => CreateSource(CreateConfigurationMock());
}
