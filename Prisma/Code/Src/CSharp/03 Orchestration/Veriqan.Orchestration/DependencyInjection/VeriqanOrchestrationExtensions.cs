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
    /// Reference-data's CSV root directory is bound from the <c>"Veriqan:CsvReferenceData"</c>
    /// configuration section (e.g. the <c>Veriqan__CsvReferenceData__RootDirectory</c> env var
    /// or appsettings), so a host that supplies that config gets a configured root automatically.
    /// To override the root in code, supply it through that same configuration section rather than
    /// calling <c>AddVeriqanReferenceData</c> a second time (it registers the provider with
    /// <c>AddTransient</c>, so a duplicate call would double-register it).
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
        services.AddVeriqanDisposition();

        // Infrastructure adapters
        // Bind the CSV reference-data root from configuration so a host that supplies
        // Veriqan:CsvReferenceData:RootDirectory (env var or appsettings) gets a configured
        // root automatically — otherwise the worker readiness probe fails csvReferenceDataRoot.
        services.AddVeriqanReferenceData(opts =>
            config.GetSection("Veriqan:CsvReferenceData").Bind(opts));
        services.AddVeriqanExtraction(config);
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

            // Startup hook: always registered when SQL persistence is active.
            // Unconditional because SqlLegalToleranceProvider.For() throws
            // InvalidOperationException on cold-cache access — warming the cache is
            // mandatory for the host to serve any verification request.
            // The service reads Veriqan:RunMigrationsAtStartup internally to decide
            // whether to also run EF Core migrate+seed (default true = backward compat);
            // CI sets it false and runs `--migrate` as a separate pre-step.
            // Fail-loud: DB unreachable or empty cache after seed → host startup aborted.
            services.AddHostedService<VeriqanLegalBaselineStartupService>();
        }
        else
        {
            services.AddVeriqanInMemoryPersistence();
        }

        // Metrics — singleton so the same Meter lives for the process lifetime.
        services.AddSingleton<VeriqanMetrics>();

        // BatchProcessor configuration — read from Veriqan:BatchProcessor section;
        // defaults to Environment.ProcessorCount workers when the section is absent.
        services.Configure<BatchProcessorOptions>(
            config.GetSection(BatchProcessorOptions.Section));

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
        services.TryAddScoped<IVerdictPersistenceService, InMemoryVerdictPersistenceService>();
        services.TryAddSingleton<IVerificationResultStore, InMemoryVerificationResultStore>();
        services.TryAddSingleton<IReprocessAuditRepository, InMemoryReprocessAuditRepository>();

        return services;
    }
}
