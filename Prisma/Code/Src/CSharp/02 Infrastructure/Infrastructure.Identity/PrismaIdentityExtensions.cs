using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Infrastructure.Identity;

/// <summary>
/// Extension methods for registering Prisma Identity infrastructure.
/// </summary>
public static class PrismaIdentityExtensions
{
    /// <summary>
    /// Registers ASP.NET Core Identity using <see cref="PrismaApplicationUser"/> and
    /// <see cref="PrismaIdentityDbContext"/>, wires the domain auth interfaces, and
    /// configures cookie-based authentication.
    /// </summary>
    /// <remarks>
    /// The caller (Web.UI <c>Program.cs</c>) must also register the Web-layer scaffolding
    /// that cannot move into this project because it depends on Blazor/Razor abstractions:
    /// <code>
    /// services.AddCascadingAuthenticationState();
    /// services.AddScoped&lt;IdentityUserAccessor&gt;();
    /// services.AddScoped&lt;IdentityRedirectManager&gt;();
    /// services.AddScoped&lt;AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider&gt;();
    /// </code>
    /// See ADR-014 §7.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration (reads <c>ConnectionStrings:DefaultConnection</c>).</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddPrismaIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not found. " +
                "Supply it via dotnet user-secrets (key: ConnectionStrings:DefaultConnection) " +
                "or the CONNECTIONSTRINGS__DEFAULTCONNECTION environment variable.");

        // Identity DB context factory (SQL Server — PrismaID database)
        services.AddDbContextFactory<PrismaIdentityDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Surfaces schema migration errors as Razor pages instead of bare exceptions (dev only)
        services.AddDatabaseDeveloperPageExceptionFilter();

        // ASP.NET Core Identity core stack
        services.AddIdentityCore<PrismaApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<PrismaIdentityDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // Cookie-based authentication (MVP auth scheme per ADR-014)
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        // No-op email sender (replace with real SMTP when email confirmation is enabled)
        services.AddSingleton<Microsoft.AspNetCore.Identity.IEmailSender<PrismaApplicationUser>,
            PrismaNoOpEmailSender>();

        // IHttpContextAccessor — required by PrismaIdentityAdapter to read the current principal
        services.AddHttpContextAccessor();

        // Register domain auth interfaces against the new adapter
        services.AddScoped<IIdentityProvider, PrismaIdentityAdapter>();
        services.AddScoped<IUserContextAccessor, PrismaIdentityAdapter>();
        // ITokenService is intentionally NOT registered here; only needed in the worker
        // security path (see ADR-012). Add if per-user JWT issuance is required later.

        return services;
    }

    /// <summary>
    /// Applies pending EF Core migrations for the Identity schema.
    /// Fail-open: errors are logged but not rethrown.
    /// </summary>
    /// <param name="serviceProvider">Post-build root service provider.</param>
    /// <param name="logger">Logger for migration messages.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public static Task MigratePrismaIdentityAsync(
        this IServiceProvider serviceProvider,
        ILogger logger,
        CancellationToken cancellationToken = default)
        => PrismaIdentityMigrator.MigrateAsync(serviceProvider, logger, cancellationToken);

    /// <summary>
    /// Seeds the canonical roles and seed users idempotently.
    /// Fail-open: errors are logged but not rethrown.
    /// </summary>
    /// <param name="serviceProvider">Post-build root service provider.</param>
    /// <param name="configuration">Application configuration for reading seed passwords.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public static Task SeedPrismaIdentityAsync(
        this IServiceProvider serviceProvider,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
        => PrismaIdentitySeeder.SeedAsync(serviceProvider, configuration, cancellationToken);
}
