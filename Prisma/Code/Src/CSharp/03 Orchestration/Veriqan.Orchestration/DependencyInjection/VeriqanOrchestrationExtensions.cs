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
using ExxerCube.Prisma.Veriqan.Orchestration.Observability;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using ExxerCube.Prisma.Veriqan.Orchestration.Reprocess;
using ExxerCube.Prisma.Veriqan.Orchestration.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ExxerCube.Prisma.Veriqan.Orchestration.DependencyInjection;

/// <summary>
/// Composite <see cref="IServiceCollection"/> extension that registers the entire Veriqan VEC
/// stack — application layer, all infrastructure adapters, pipeline, batch processor, and the
/// resume/reprocess services — in a single call suitable for use in worker hosts and integration
/// tests.
/// </summary>
public static class VeriqanOrchestrationExtensions
{
    /// <summary>
    /// Registers all Veriqan VEC services required to run the end-to-end verification pipeline,
    /// batch processor, resume-aware batching, and explicit reprocess.
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

        // Startup config validator — runs before persistence so the DB-absent warning is
        // emitted before any persistence-path decision.  Registered unconditionally (both
        // SQL and in-memory paths).  Does NOT crash the process; emits structured WARNINGs.
        services.AddHostedService<VeriqanConfigurationValidator>();

        // Persistence — real EF Core when connection string is present, otherwise in-memory.
        // When SQL persistence is selected the startup hosted service wires the encrypted
        // legal-baseline store (migrate → seed → InitialiseAsync) before traffic is served.
        var cs = config.GetConnectionString("VeriqanDb");
        if (!string.IsNullOrWhiteSpace(cs))
        {
            services.AddVeriqanPersistence(cs);
            // Startup hook: migrate DB → seed legal baseline → warm SqlLegalToleranceProvider.
            // Fail-loud: if the DB is unreachable or the cache is empty after seeding the host
            // aborts.  Never silently fall back to in-code defaults.
            services.AddHostedService<VeriqanLegalBaselineStartupService>();
        }
        else
        {
            services.AddVeriqanInMemoryPersistence();
        }

        // Metrics — singleton so the same Meter lives for the process lifetime.
        services.AddSingleton<VeriqanMetrics>();

        // Pipeline + batch + resume/reprocess
        services.AddScoped<IVerificationPipeline, VerificationPipeline>();
        services.AddSingleton<IBatchProcessor, BatchProcessor>();

        // Use TryAdd so that AddVeriqanInMemoryPersistence (called above when no connection
        // string is present) wins and a second descriptor is never registered.  Without Try*
        // semantics both the unconditional registration here and the TryAdd inside
        // AddVeriqanInMemoryPersistence produce two descriptors for the same interface,
        // leaving one singleton orphaned.
        services.TryAddSingleton<IVerificationResultStore, InMemoryVerificationResultStore>();
        services.TryAddSingleton<IReprocessAuditRepository, InMemoryReprocessAuditRepository>();
        services.AddScoped<IReprocessService, ReprocessService>();

        return services;
    }

    /// <summary>
    /// Registers lightweight in-memory stubs for <see cref="IVerificationJobRepository"/>,
    /// <see cref="IDispositionRepository"/>, <see cref="IVerificationResultStore"/>, and
    /// <see cref="IReprocessAuditRepository"/>.
    /// Intended for use without a database connection (integration tests, local dev, unit test
    /// hosts).
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVeriqanInMemoryPersistence(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IVerificationJobRepository, InMemoryVerificationJobRepository>();
        services.TryAddScoped<IDispositionRepository, InMemoryDispositionRepository>();
        services.TryAddSingleton<IVerificationResultStore, InMemoryVerificationResultStore>();
        services.TryAddSingleton<IReprocessAuditRepository, InMemoryReprocessAuditRepository>();

        return services;
    }
}
