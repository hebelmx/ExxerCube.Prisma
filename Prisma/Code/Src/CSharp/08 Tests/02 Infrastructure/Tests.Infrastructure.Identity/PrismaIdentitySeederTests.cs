using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Tests.Infrastructure.Identity;

/// <summary>
/// Unit tests for <see cref="PrismaIdentitySeeder"/> (ADR-014 §D).
/// Uses an in-memory Identity store; no real database is needed.
/// </summary>
public sealed class PrismaIdentitySeederTests
{
    // ── DI Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a service provider with in-memory Identity stores.
    /// This avoids Testcontainers/SQL for unit tests of the seeder logic.
    /// </summary>
    private static ServiceProvider BuildInMemoryIdentityProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

        // In-memory EF Core store for Identity — unique DB name per test invocation
        var dbName = $"PrismaIdentityTest_{Guid.NewGuid()}";
        services.AddDbContext<PrismaIdentityDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        services.AddIdentityCore<PrismaApplicationUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<PrismaIdentityDbContext>();

        return services.BuildServiceProvider();
    }

    private static IConfiguration BuildConfig(
        string? reviewerPwd = "Reviewer@Prisma1!",
        string? adminPwd = "Admin@Prisma1!")
    {
        var dict = new Dictionary<string, string?>();
        if (reviewerPwd is not null) dict["Seeding:ReviewerPassword"] = reviewerPwd;
        if (adminPwd is not null) dict["Seeding:AdminPassword"] = adminPwd;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SeedAsync_CreatesRolesAndUsers_WhenDatabaseIsEmpty()
    {
        await using var sp = BuildInMemoryIdentityProvider();
        var config = BuildConfig();

        await PrismaIdentitySeeder.SeedAsync(sp, config, TestContext.Current.CancellationToken);

        // Assertions run in a new scope so they use a fresh-but-same-InMemory-database
        await using var scope = sp.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PrismaApplicationUser>>();

        // Roles created
        (await roleManager.RoleExistsAsync(PrismaIdentityRoles.Reviewer)).ShouldBeTrue();
        (await roleManager.RoleExistsAsync(PrismaIdentityRoles.Admin)).ShouldBeTrue();
        (await roleManager.RoleExistsAsync(PrismaIdentityRoles.Administrator)).ShouldBeTrue();

        // Reviewer user created and in Reviewer role
        var reviewer = await userManager.FindByEmailAsync("reviewer@prisma.local");
        reviewer.ShouldNotBeNull();
        (await userManager.IsInRoleAsync(reviewer, PrismaIdentityRoles.Reviewer)).ShouldBeTrue();

        // Admin user created and in Admin + Administrator roles
        var admin = await userManager.FindByEmailAsync("admin@prisma.local");
        admin.ShouldNotBeNull();
        (await userManager.IsInRoleAsync(admin, PrismaIdentityRoles.Admin)).ShouldBeTrue();
        (await userManager.IsInRoleAsync(admin, PrismaIdentityRoles.Administrator)).ShouldBeTrue();
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent_WhenCalledTwice()
    {
        await using var sp = BuildInMemoryIdentityProvider();
        var config = BuildConfig();

        // First call
        await PrismaIdentitySeeder.SeedAsync(sp, config, TestContext.Current.CancellationToken);
        // Second call — must not throw or create duplicates
        await PrismaIdentitySeeder.SeedAsync(sp, config, TestContext.Current.CancellationToken);

        await using var scope = sp.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PrismaApplicationUser>>();
        var users = userManager.Users.ToList();

        // Exactly one of each seed user
        users.Count(u => u.Email == "reviewer@prisma.local").ShouldBe(1);
        users.Count(u => u.Email == "admin@prisma.local").ShouldBe(1);
    }

    [Fact]
    public async Task SeedAsync_SkipsUser_WhenPasswordConfigMissing()
    {
        await using var sp = BuildInMemoryIdentityProvider();
        // Config has neither password key — seeder should use dev defaults and NOT throw
        var config = BuildConfig(reviewerPwd: null, adminPwd: null);

        // Seeder is fail-open: logs Warning but uses built-in dev default (does NOT skip creation)
        Exception? ex = null;
        try
        {
            await PrismaIdentitySeeder.SeedAsync(sp, config, TestContext.Current.CancellationToken);
        }
        catch (Exception caught)
        {
            ex = caught;
        }

        // Must not throw
        ex.ShouldBeNull();

        // Users are still created via dev defaults
        await using var scope = sp.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PrismaApplicationUser>>();
        var reviewer = await userManager.FindByEmailAsync("reviewer@prisma.local");
        reviewer.ShouldNotBeNull("Seeder should fall back to dev default when config key absent");
    }
}
