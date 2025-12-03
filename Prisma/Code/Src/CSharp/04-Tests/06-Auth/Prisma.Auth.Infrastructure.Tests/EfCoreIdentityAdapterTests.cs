namespace Prisma.Auth.Infrastructure.Tests;

/// <summary>
/// TDD tests for EF Core Identity adapter wrapping UserManager/SignInManager.
/// </summary>
/// <remarks>
/// Stage 6 Requirements:
/// - Adapt EF Core Identity (UserManager/SignInManager) to clean auth interfaces
/// - Separate UI layer from concrete Identity implementation
/// - Enable swappable auth providers (in-memory, EF, Azure AD, etc.)
/// - Maintain SRP: UI depends on abstractions, Infrastructure provides implementations
/// </remarks>
public sealed class EfCoreIdentityAdapterTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateTokenAsync_ValidUser_ReturnsJwtToken()
    {
        // Arrange
        var (adapter, userManager, _) = CreateAdapter();
        var user = new TestIdentityUser { Id = "user-123", UserName = "testuser", Email = "test@example.com" };
        userManager.FindByIdAsync("user-123").Returns(user);
        userManager.GetRolesAsync(user).Returns(new List<string> { "Admin", "User" });

        var identity = new UserIdentity("user-123", "testuser", new[] { "Admin", "User" });

        // Act
        var token = await adapter.CreateTokenAsync(identity, TestContext.Current.CancellationToken);

        // Assert
        token.ShouldNotBeNullOrEmpty();
        token.ShouldStartWith("eyJ"); // JWT tokens start with "eyJ" (base64 encoded JSON header)
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateTokenAsync_ValidToken_ReturnsSuccessWithIdentity()
    {
        // Arrange
        var (adapter, userManager, _) = CreateAdapter();
        var user = new TestIdentityUser { Id = "user-123", UserName = "testuser" };
        var identity = new UserIdentity("user-123", "testuser", new[] { "Admin" });

        userManager.FindByIdAsync("user-123").Returns(user);
        userManager.GetRolesAsync(user).Returns(new List<string> { "Admin" });

        // Create a valid token first
        var token = await adapter.CreateTokenAsync(identity, TestContext.Current.CancellationToken);

        // Act
        var result = await adapter.ValidateTokenAsync(token, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Identity.ShouldNotBeNull();
        result.Identity.UserId.ShouldBe("user-123");
        result.Identity.UserName.ShouldBe("testuser");
        result.Identity.Roles.ShouldContain("Admin");
        result.Error.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateTokenAsync_InvalidToken_ReturnsFailure()
    {
        // Arrange
        var (adapter, _, _) = CreateAdapter();
        var invalidToken = "invalid-token-12345";

        // Act
        var result = await adapter.ValidateTokenAsync(invalidToken, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Identity.ShouldBeNull();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateTokenAsync_ExpiredToken_ReturnsFailure()
    {
        // Arrange
        var (adapter, userManager, _) = CreateAdapter(tokenLifetime: TimeSpan.FromMilliseconds(1));
        var user = new TestIdentityUser { Id = "user-123", UserName = "testuser" };
        var identity = new UserIdentity("user-123", "testuser", Array.Empty<string>());

        userManager.FindByIdAsync("user-123").Returns(user);
        userManager.GetRolesAsync(user).Returns(new List<string>());

        var token = await adapter.CreateTokenAsync(identity, TestContext.Current.CancellationToken);

        // Wait for token to expire
        await Task.Delay(10);

        // Act
        var result = await adapter.ValidateTokenAsync(token, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("expired", Case.Insensitive);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCurrentAsync_UserSignedIn_ReturnsUserIdentity()
    {
        // Arrange
        var (adapter, userManager, signInManager) = CreateAdapter();
        var user = new TestIdentityUser { Id = "user-456", UserName = "currentuser", Email = "current@example.com" };

        // Simulate user is signed in
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        var claimsPrincipal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "user-456"),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "currentuser")
            }, "TestAuth"));
        httpContext.User = claimsPrincipal;

        userManager.GetUserAsync(claimsPrincipal).Returns(user);
        userManager.GetRolesAsync(user).Returns(new List<string> { "User" });

        adapter.SetHttpContext(httpContext);

        // Act
        var identity = await adapter.GetCurrentAsync(TestContext.Current.CancellationToken);

        // Assert
        identity.ShouldNotBeNull();
        identity.UserId.ShouldBe("user-456");
        identity.UserName.ShouldBe("currentuser");
        identity.Roles.ShouldContain("User");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCurrentAsync_NoUserSignedIn_ReturnsNull()
    {
        // Arrange
        var (adapter, _, _) = CreateAdapter();
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(); // No claims = not authenticated

        adapter.SetHttpContext(httpContext);

        // Act
        var identity = await adapter.GetCurrentAsync(TestContext.Current.CancellationToken);

        // Assert
        identity.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Current_UserSignedIn_ReturnsUserIdentity()
    {
        // Arrange
        var (adapter, userManager, _) = CreateAdapter();
        var user = new TestIdentityUser { Id = "user-789", UserName = "syncuser" };

        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        var claimsPrincipal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "user-789"),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "syncuser")
            }, "TestAuth"));
        httpContext.User = claimsPrincipal;

        userManager.GetUserAsync(claimsPrincipal).Returns(user);
        userManager.GetRolesAsync(user).Returns(new List<string>());

        adapter.SetHttpContext(httpContext);

        // Act
        var identity = adapter.Current;

        // Assert
        identity.ShouldNotBeNull();
        identity.UserId.ShouldBe("user-789");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Current_NoUserSignedIn_ReturnsNull()
    {
        // Arrange
        var (adapter, _, _) = CreateAdapter();
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        adapter.SetHttpContext(httpContext);

        // Act
        var identity = adapter.Current;

        // Assert
        identity.ShouldBeNull();
    }

    // Helper method to create adapter with mocked dependencies
    private static (EfCoreIdentityAdapter<TestIdentityUser> adapter, UserManager<TestIdentityUser> userManager, SignInManager<TestIdentityUser> signInManager)
        CreateAdapter(TimeSpan? tokenLifetime = null)
    {
        var userStore = Substitute.For<IUserStore<TestIdentityUser>>();
        var userManager = Substitute.For<UserManager<TestIdentityUser>>(
            userStore, null, null, null, null, null, null, null, null);

        var contextAccessor = Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var userPrincipalFactory = Substitute.For<IUserClaimsPrincipalFactory<TestIdentityUser>>();
        var signInManager = Substitute.For<SignInManager<TestIdentityUser>>(
            userManager, contextAccessor, userPrincipalFactory, null, null, null, null);

        var config = new EfCoreIdentityConfiguration
        {
            JwtSecret = "ThisIsAVerySecureSecretKeyForTestingPurposesOnly12345!",
            JwtIssuer = "PrismaTests",
            JwtAudience = "PrismaTestUsers",
            TokenLifetime = tokenLifetime ?? TimeSpan.FromHours(1)
        };

        var adapter = new EfCoreIdentityAdapter<TestIdentityUser>(
            userManager,
            signInManager,
            config,
            NullLogger<EfCoreIdentityAdapter<TestIdentityUser>>.Instance);

        return (adapter, userManager, signInManager);
    }
}

/// <summary>
/// Test identity user for testing purposes.
/// </summary>
public class TestIdentityUser : IdentityUser
{
}
