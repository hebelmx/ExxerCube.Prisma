using Microsoft.Extensions.Configuration;

namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Mode-specific mechanics for <see cref="ConfiguredSiaraCredentialSource"/> that the universal
/// <see cref="SiaraCredentialSourceContract"/> leaves to the implementation (ADR-005 §5): reading the
/// configured keys, surfacing the configured values, and failing closed on missing secrets.
/// </summary>
public sealed class ConfiguredSiaraCredentialSourceTests
{
    [Fact]
    public async Task GetCredentialsAsync_RevealsConfiguredValues()
    {
        var configuration = ConfiguredSiaraCredentialSourceTestFactory.CreateConfigurationMock("operator-1", "s3cr3t");
        var sut = ConfiguredSiaraCredentialSourceTestFactory.CreateSource(configuration);

        var result = await sut.GetCredentialsAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        using var credential = result.Value!;
        var (username, password) = await credential.UseAsync((u, p) => Task.FromResult((u, p)));
        username.ShouldBe("operator-1");
        password.ShouldBe("s3cr3t");
    }

    [Fact]
    public async Task GetCredentialsAsync_ReadsConfiguredKeyNames()
    {
        var configuration = Substitute.For<IConfiguration>();
        configuration["Vault:Siara:User"].Returns("vault-user");
        configuration["Vault:Siara:Pass"].Returns("vault-pass");
        var sut = ConfiguredSiaraCredentialSourceTestFactory.CreateSource(
            configuration,
            new SiaraAutomatedOptions { UsernameConfigKey = "Vault:Siara:User", PasswordConfigKey = "Vault:Siara:Pass" });

        var result = await sut.GetCredentialsAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _ = configuration.Received(1)["Vault:Siara:User"];
        _ = configuration.Received(1)["Vault:Siara:Pass"];
    }

    [Fact]
    public async Task GetCredentialsAsync_MissingUsername_FailsClosed()
    {
        var configuration = ConfiguredSiaraCredentialSourceTestFactory.CreateConfigurationMock(username: null);
        var sut = ConfiguredSiaraCredentialSourceTestFactory.CreateSource(configuration);

        var result = await sut.GetCredentialsAsync(TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetCredentialsAsync_MissingPassword_FailsClosed()
    {
        var configuration = ConfiguredSiaraCredentialSourceTestFactory.CreateConfigurationMock(password: "");
        var sut = ConfiguredSiaraCredentialSourceTestFactory.CreateSource(configuration);

        var result = await sut.GetCredentialsAsync(TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
    }
}
