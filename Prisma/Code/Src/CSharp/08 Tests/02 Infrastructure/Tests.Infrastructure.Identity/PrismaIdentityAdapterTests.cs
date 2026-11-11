using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Tests.Infrastructure.Identity;

/// <summary>
/// Unit tests for <see cref="PrismaIdentityAdapter"/> (ADR-014 §D).
/// </summary>
public sealed class PrismaIdentityAdapterTests
{
    // ── Helper ───────────────────────────────────────────────────────────────

    private static PrismaIdentityAdapter CreateAdapter(IHttpContextAccessor accessor)
    {
        var logger = Substitute.For<ILogger<PrismaIdentityAdapter>>();
        return new PrismaIdentityAdapter(accessor, logger);
    }

    private static IHttpContextAccessor WithNoContext()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        return accessor;
    }

    private static IHttpContextAccessor WithAuthenticatedUser(string userId, string userName, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userName)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns(principal);

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return accessor;
    }

    private static IHttpContextAccessor WithUnauthenticatedUser()
    {
        var identity = new ClaimsIdentity(); // no authenticationType → IsAuthenticated = false
        var principal = new ClaimsPrincipal(identity);

        var httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns(principal);

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return accessor;
    }

    // ── GetCurrentAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentAsync_ReturnsNull_WhenNoHttpContext()
    {
        var adapter = CreateAdapter(WithNoContext());

        var result = await adapter.GetCurrentAsync(TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetCurrentAsync_ReturnsIdentity_WhenAuthenticated()
    {
        var accessor = WithAuthenticatedUser("user-123", "alice@prisma.local", "Reviewer");
        var adapter = CreateAdapter(accessor);

        var result = await adapter.GetCurrentAsync(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.UserId.ShouldBe("user-123");
        result.UserName.ShouldBe("alice@prisma.local");
        result.Roles.ShouldContain("Reviewer");
    }

    // ── Current (synchronous) ─────────────────────────────────────────────────

    [Fact]
    public void Current_ReturnsNull_WhenNotAuthenticated()
    {
        var adapter = CreateAdapter(WithUnauthenticatedUser());

        adapter.Current.ShouldBeNull();
    }

    [Fact]
    public void Current_ReturnsIdentity_WhenAuthenticated()
    {
        var accessor = WithAuthenticatedUser("user-999", "bob@prisma.local", "Admin", "Administrator");
        var adapter = CreateAdapter(accessor);

        var identity = adapter.Current;

        identity.ShouldNotBeNull();
        identity.UserId.ShouldBe("user-999");
        identity.Roles.ShouldContain("Admin");
        identity.Roles.ShouldContain("Administrator");
    }
}
