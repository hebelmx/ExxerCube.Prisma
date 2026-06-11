namespace ExxerCube.Prisma.Infrastructure.BrowserAutomation.DependencyInjection;

/// <summary>
/// Extension methods for configuring browser automation dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds browser automation services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional action to configure browser automation options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBrowserAutomationServices(
        this IServiceCollection services,
        Action<BrowserAutomationOptions>? configureOptions = null)
    {
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<BrowserAutomationOptions>(_ => { });
        }

        services.AddScoped<IBrowserAutomationAgent, PlaywrightBrowserAutomationAdapter>();

        // The session-context capability is the SAME live adapter instance (same scope), so session
        // capture/restore/attach operate on the browser the navigation agent is driving.
        services.AddScoped<IBrowserSessionContext>(sp =>
            (IBrowserSessionContext)sp.GetRequiredService<IBrowserAutomationAgent>());

        // Register navigation targets with keyed services for runtime selection
        services.Configure<NavigationTargetOptions>(options =>
        {
            // Configure from appsettings.json - will be bound in Web UI
        });

        services.AddKeyedScoped<INavigationTarget, SiaraNavigationTarget>("siara");
        services.AddKeyedScoped<INavigationTarget, InternetArchiveNavigationTarget>("archive");
        services.AddKeyedScoped<INavigationTarget, GutenbergNavigationTarget>("gutenberg");

        // SIARA login service
        services.AddScoped<ISiaraLoginService, Services.SiaraLoginService>();

        return services;
    }
}

