using System;
using System.Threading;
using System.Threading.Tasks;
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

        // Register template seeder for database initialization
        services.AddScoped<TemplateSeeder>();

        // Register adapter for backward compatibility with IResponseExporter
        // This enables zero-downtime migration from SiroXmlExporter to AdaptiveExporter
        // OLD: services.AddScoped<IResponseExporter, SiroXmlExporter>();
        // NEW: services.AddScoped<IResponseExporter, AdaptiveResponseExporterAdapter>();
        services.AddScoped<IResponseExporter, AdaptiveResponseExporterAdapter>();

        return services;
    }

    /// <summary>
    /// Seeds initial templates (Excel, XML) if they don't already exist.
    /// Call this during application startup to ensure templates are available.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task SeedTemplatesAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<TemplateSeeder>();
        await seeder.SeedAllTemplatesAsync(cancellationToken);
    }
}
