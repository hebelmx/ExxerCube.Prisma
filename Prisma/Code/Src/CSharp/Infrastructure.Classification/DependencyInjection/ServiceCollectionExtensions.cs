using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ExxerCube.Prisma.Domain.Interfaces;

namespace ExxerCube.Prisma.Infrastructure.Classification;

/// <summary>
/// Extension methods for registering classification services in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds classification services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration (optional, for matching policy options).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddClassificationServices(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.AddScoped<IFileClassifier, FileClassifierService>();
        
        // Register matching policy service
        services.AddScoped<IMatchingPolicy, MatchingPolicyService>();
        
        // Register matching policy options from configuration
        if (configuration != null)
        {
            services.Configure<MatchingPolicyOptions>(configuration.GetSection("MatchingPolicy"));
        }
        else
        {
            // Use default options if no configuration provided
            services.Configure<MatchingPolicyOptions>(_ => { });
        }

        return services;
    }
}

