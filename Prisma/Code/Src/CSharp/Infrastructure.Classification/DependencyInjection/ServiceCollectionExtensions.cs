using Microsoft.Extensions.DependencyInjection;
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
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddClassificationServices(this IServiceCollection services)
    {
        services.AddScoped<IFileClassifier, FileClassifierService>();

        return services;
    }
}

