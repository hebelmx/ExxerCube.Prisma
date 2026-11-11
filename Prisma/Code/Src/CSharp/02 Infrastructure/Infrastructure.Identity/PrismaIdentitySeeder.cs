namespace ExxerCube.Prisma.Infrastructure.Identity;

/// <summary>
/// Idempotent startup seeder for Prisma Identity roles and seed users.
/// </summary>
/// <remarks>
/// Called from <c>Program.cs</c> via the extension method
/// <see cref="PrismaIdentityExtensions.SeedPrismaIdentityAsync"/> after
/// <c>app.Build()</c> and before <c>app.Run()</c>.
///
/// Seeded roles: <see cref="PrismaIdentityRoles.Reviewer"/>,
/// <see cref="PrismaIdentityRoles.Admin"/>, <see cref="PrismaIdentityRoles.Administrator"/>.
///
/// Seeded users:
/// <list type="bullet">
///   <item><c>reviewer@prisma.local</c> — role Reviewer; password from <c>Seeding:ReviewerPassword</c>.</item>
///   <item><c>admin@prisma.local</c>    — roles Admin + Administrator; password from <c>Seeding:AdminPassword</c>.</item>
/// </list>
///
/// If a config key is absent the seeder logs a Warning and skips that user.
/// The app continues to boot regardless (fail-open pattern, matching template seeder).
/// </remarks>
public static class PrismaIdentitySeeder
{
    private const string ReviewerEmail = "reviewer@prisma.local";
    private const string AdminEmail = "admin@prisma.local";

    /// <summary>
    /// Default dev/staging password used when <c>Seeding:ReviewerPassword</c> is not configured.
    /// CHANGE before production deployment. Override via env var
    /// <c>Seeding__ReviewerPassword</c> or dotnet user-secrets.
    /// </summary>
    private const string DefaultReviewerPassword = "Reviewer@Prisma1!";

    /// <summary>
    /// Default dev/staging password used when <c>Seeding:AdminPassword</c> is not configured.
    /// CHANGE before production deployment. Override via env var
    /// <c>Seeding__AdminPassword</c> or dotnet user-secrets.
    /// </summary>
    private const string DefaultAdminPassword = "Admin@Prisma1!";

    /// <summary>
    /// Seeds roles and users idempotently. Safe to call on every startup.
    /// </summary>
    /// <param name="serviceProvider">Root service provider (post-build).</param>
    /// <param name="configuration">Application configuration for reading seed passwords.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public static async Task SeedAsync(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PrismaApplicationUser>>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(PrismaIdentitySeeder));

        // ── 1. Ensure all roles exist ─────────────────────────────────────────
        await EnsureRoleAsync(roleManager, logger, PrismaIdentityRoles.Reviewer, cancellationToken).ConfigureAwait(false);
        await EnsureRoleAsync(roleManager, logger, PrismaIdentityRoles.Admin, cancellationToken).ConfigureAwait(false);
        await EnsureRoleAsync(roleManager, logger, PrismaIdentityRoles.Administrator, cancellationToken).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested)
            return;

        // ── 2. Seed reviewer user ─────────────────────────────────────────────
        var reviewerPassword = configuration["Seeding:ReviewerPassword"];
        if (string.IsNullOrWhiteSpace(reviewerPassword))
        {
            logger.LogWarning(
                "Seeding:ReviewerPassword is not configured. Using built-in dev default for {Email}. " +
                "Override via env var Seeding__ReviewerPassword or dotnet user-secrets before staging deployment.",
                ReviewerEmail);
            reviewerPassword = DefaultReviewerPassword;
        }

        await EnsureUserAsync(
            userManager, logger,
            ReviewerEmail, reviewerPassword, "Reviewer User",
            [PrismaIdentityRoles.Reviewer],
            cancellationToken).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested)
            return;

        // ── 3. Seed admin user (holds Admin + Administrator) ──────────────────
        var adminPassword = configuration["Seeding:AdminPassword"];
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning(
                "Seeding:AdminPassword is not configured. Using built-in dev default for {Email}. " +
                "Override via env var Seeding__AdminPassword or dotnet user-secrets before staging deployment.",
                AdminEmail);
            adminPassword = DefaultAdminPassword;
        }

        await EnsureUserAsync(
            userManager, logger,
            AdminEmail, adminPassword, "Admin User",
            [PrismaIdentityRoles.Admin, PrismaIdentityRoles.Administrator],
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation("PrismaIdentitySeeder completed successfully");
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static async Task EnsureRoleAsync(
        RoleManager<IdentityRole> roleManager,
        ILogger logger,
        string roleName,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        if (await roleManager.RoleExistsAsync(roleName).ConfigureAwait(false))
        {
            logger.LogDebug("Role {Role} already exists — skipping create", roleName);
            return;
        }

        var result = await roleManager.CreateAsync(new IdentityRole(roleName)).ConfigureAwait(false);
        if (result.Succeeded)
        {
            logger.LogInformation("Created Identity role {Role}", roleName);
        }
        else
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            logger.LogError("Failed to create role {Role}: {Errors}", roleName, errors);
        }
    }

    private static async Task EnsureUserAsync(
        UserManager<PrismaApplicationUser> userManager,
        ILogger logger,
        string email,
        string password,
        string fullName,
        IEnumerable<string> roles,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        var existing = await userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (existing is null)
        {
            var user = new PrismaApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName
            };

            var createResult = await userManager.CreateAsync(user, password).ConfigureAwait(false);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                logger.LogError("Failed to create seed user {Email}: {Errors}", email, errors);
                return;
            }

            logger.LogInformation("Created seed user {Email}", email);
            existing = user;
        }
        else
        {
            logger.LogDebug("Seed user {Email} already exists — ensuring roles only", email);
        }

        // Ensure roles
        foreach (var role in roles)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            if (await userManager.IsInRoleAsync(existing, role).ConfigureAwait(false))
            {
                logger.LogDebug("User {Email} already in role {Role}", email, role);
                continue;
            }

            var addResult = await userManager.AddToRoleAsync(existing, role).ConfigureAwait(false);
            if (addResult.Succeeded)
            {
                logger.LogInformation("Added user {Email} to role {Role}", email, role);
            }
            else
            {
                var errors = string.Join("; ", addResult.Errors.Select(e => e.Description));
                logger.LogError("Failed to add user {Email} to role {Role}: {Errors}", email, role, errors);
            }
        }
    }
}
