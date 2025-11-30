using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive;
using ExxerCube.Prisma.Infrastructure.Export.Adaptive.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Infrastructure.Export.Adaptive.DependencyInjection;

/// <summary>
/// Extension methods for configuring adaptive export services dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds adaptive export services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The database connection string for template storage.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAdaptiveExportServices(
        this IServiceCollection services,
        string connectionString)
    {
        // Register TemplateDbContext with SQL Server
        services.AddDbContext<TemplateDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Register core adaptive export services
        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<ITemplateFieldMapper, TemplateFieldMapper>();
        services.AddScoped<IAdaptiveExporter, AdaptiveExporter>();

        // Register schema evolution detection services
        services.AddScoped<ISchemaEvolutionDetector, SchemaEvolutionDetector>();

        return services;
    }
}
