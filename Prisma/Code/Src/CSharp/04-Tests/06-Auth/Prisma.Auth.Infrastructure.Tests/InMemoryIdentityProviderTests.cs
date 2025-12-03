namespace Prisma.Auth.Infrastructure.Tests;

/// <summary>
/// TDD tests for in-memory identity provider (dev/testing).
/// </summary>
public sealed class InMemoryIdentityProviderTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateTokenAsync_CreatesSimpleDevToken()
    {
        // Arrange
        var provider = new InMemoryIdentityProvider();
        var identity = new UserIdentity("dev-user-1", "devuser", new[] { "Admin" });

        // Act
        var token = await provider.CreateTokenAsync(identity, TestContext.Current.CancellationToken);

        // Assert
        token.ShouldBe("DEV-TOKEN-dev-user-1");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateTokenAsync_ValidToken_ReturnsSuccess()
    {
        // Arrange
        var provider = new InMemoryIdentityProvider();
        var identity = new UserIdentity("dev-user-2", "devuser2", new[] { "User" });
        var token = await provider.CreateTokenAsync(identity, TestContext.Current.CancellationToken);

        // Act
        var result = await provider.ValidateTokenAsync(token, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Identity.ShouldNotBeNull();
        result.Identity.UserId.ShouldBe("dev-user-2");
        result.Identity.UserName.ShouldBe("devuser2");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateTokenAsync_InvalidToken_ReturnsFailure()
    {
        // Arrange
        var provider = new InMemoryIdentityProvider();
        var identity = new UserIdentity("dev-user-3", "devuser3", Array.Empty<string>());
        await provider.CreateTokenAsync(identity, TestContext.Current.CancellationToken);

        // Act
        var result = await provider.ValidateTokenAsync("WRONG-TOKEN", TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Identity.ShouldBeNull();
        result.Error.ShouldBe("Invalid token");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCurrentAsync_AfterTokenCreation_ReturnsIdentity()
    {
        // Arrange
        var provider = new InMemoryIdentityProvider();
        var identity = new UserIdentity("dev-user-4", "devuser4", new[] { "Admin", "User" });
        await provider.CreateTokenAsync(identity, TestContext.Current.CancellationToken);

        // Act
        var current = await provider.GetCurrentAsync(TestContext.Current.CancellationToken);

        // Assert
        current.ShouldNotBeNull();
        current.UserId.ShouldBe("dev-user-4");
        current.UserName.ShouldBe("devuser4");
        current.Roles.ShouldContain("Admin");
        current.Roles.ShouldContain("User");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Current_AfterTokenCreation_ReturnsIdentity()
    {
        // Arrange
        var provider = new InMemoryIdentityProvider();
        var identity = new UserIdentity("dev-user-5", "devuser5", Array.Empty<string>());
        await provider.CreateTokenAsync(identity, TestContext.Current.CancellationToken);

        // Act
        var current = provider.Current;

        // Assert
        current.ShouldNotBeNull();
        current.UserId.ShouldBe("dev-user-5");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCurrentAsync_BeforeAnyToken_ReturnsNull()
    {
        // Arrange
        var provider = new InMemoryIdentityProvider();

        // Act
        var current = await provider.GetCurrentAsync(TestContext.Current.CancellationToken);

        // Assert
        current.ShouldBeNull();
    }
}
