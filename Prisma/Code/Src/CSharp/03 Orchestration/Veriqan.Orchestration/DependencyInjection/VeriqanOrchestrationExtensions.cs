using System;
using ExxerCube.Prisma.Veriqan.Application.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Reporting.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Visual.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Orchestration.Batch;
using ExxerCube.Prisma.Veriqan.Orchestration.InMemory;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ExxerCube.Prisma.Veriqan.Orchestration.DependencyInjection;

/// <summary>
/// Composite <see cref="IServiceCollection"/> extension that registers the entire Veriqan VEC
/// stack — application layer, all infrastructure adapters, pipeline, and batch processor — in
/// a single call suitable for use in worker hosts and integration tests.
/// </summary>
public static class VeriqanOrchestrationExtensions
{
    /// <summary>
    /// Registers all Veriqan VEC services required to run the end-to-end verification pipeline
    /// and batch processor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Persistence is conditional: when <c>"ConnectionStrings:VeriqanDb"</c> is present in
    /// <paramref name="config"/> the EF Core SQL Server adapter is registered; otherwise a
    /// lightweight in-memory fallback is used (suitable for tests and local dev without SQL).
    /// </para>
    /// <para>
    /// Reference-data is registered without a custom options action; callers that need to
    /// override the CSV root directory should call <c>AddVeriqanReferenceData(opts => ...)</c>
    /// directly before calling <c>AddVeriqan</c> (<c>TryAdd</c> semantics prevent double
    /// registration).
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="config">Application configuration (used for connection strings and SMTP options).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVeriqan(
        this IServiceCollection services,
        IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        // Application layer
        services.AddVeriqanIngestion();
        services.AddVeriqanBinding();
        services.AddVeriqanVerdict();

        // Infrastructure adapters
        services.AddVeriqanReferenceData();
        services.AddVeriqanExtraction();
        services.AddVeriqanValidation();
        services.AddVeriqanVisual();
        services.AddVeriqanReporting(config);

        // Persistence — real EF Core when connection string is present, otherwise in-memory
        var cs = config.GetConnectionString("VeriqanDb");
        if (!string.IsNullOrWhiteSpace(cs))
        {
            services.AddVeriqanPersistence(cs);
        }
        else
        {
            services.AddVeriqanInMemoryPersistence();
        }

        // Pipeline + batch
        services.AddScoped<IVerificationPipeline, VerificationPipeline>();
        services.AddSingleton<IBatchProcessor, BatchProcessor>();

        return services;
    }

    /// <summary>
    /// Registers lightweight in-memory stubs for <see cref="IVerificationJobRepository"/> and
    /// <see cref="IDispositionRepository"/>. Intended for use without a database connection
    /// (integration tests, local dev, unit test hosts).
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVeriqanInMemoryPersistence(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IVerificationJobRepository, InMemoryVerificationJobRepository>();
        services.TryAddScoped<IDispositionRepository, InMemoryDispositionRepository>();

        return services;
    }
}
