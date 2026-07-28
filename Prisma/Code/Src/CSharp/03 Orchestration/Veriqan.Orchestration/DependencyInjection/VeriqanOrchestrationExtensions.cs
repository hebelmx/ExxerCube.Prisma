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
using ExxerCube.Prisma.Veriqan.Orchestration.Repositories;
using ExxerCube.Prisma.Veriqan.Orchestration.Reprocess;
using ExxerCube.Prisma.Veriqan.Infrastructure.Reporting;
using ExxerCube.Prisma.Veriqan.Orchestration.Secrets;
using ExxerCube.Prisma.Veriqan.Orchestration.Startup;
using ExxerCube.Prisma.Veriqan.Orchestration.Stores;
using IndQuestResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

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

        // ── Secret provider (Story 6.3) ───────────────────────────────────────────
        // Register the default configuration-backed implementation with TryAddSingleton so
        // a caller can plug in a Key Vault / KMS adapter by registering ISecretProvider BEFORE
        // calling AddVeriqan — the prior registration wins automatically.
        services.TryAddSingleton<ISecretProvider>(sp =>
            new ConfigurationSecretProvider(sp.GetRequiredService<IConfiguration>()));

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

        // SMTP password from secret provider (Story 6.3).
        // Registered here (not inside AddVeriqanReporting) so that ISecretProvider is available
        // via DI at the point SmtpOptions are first resolved. Keeps AddVeriqanReporting free of
        // ISecretProvider dependencies, so tests that call AddVeriqanReporting directly continue
        // to work without registering ISecretProvider.
        services.AddSingleton<IPostConfigureOptions<SmtpOptions>>(sp =>
            new SmtpPasswordSecretPopulator(sp.GetRequiredService<ISecretProvider>()));

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

            // Durable EF-backed stores for the reprocess/resume pipeline (Story 6.1).
            // Registered here (not in AddVeriqanPersistence) because IVerificationResultStore
            // and IReprocessAuditRepository are defined in this Orchestration assembly, and
            // putting the registrations in the Persistence project would create a circular
            // project reference.
            services.AddSingleton<IVerificationResultStore, EfVerificationResultStore>();
            services.AddSingleton<IReprocessAuditRepository, EfReprocessAuditRepository>();

            // Durable EF-backed dead-letter log for BatchProcessor (VERIQAN-E3-S3). Same
            // rationale as above: IBatchExceptionLogRepository is defined in this Orchestration
            // assembly, so the registration lives here rather than in AddVeriqanPersistence.
            services.AddSingleton<IBatchExceptionLogRepository, EfBatchExceptionLogRepository>();
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

        // ── Gate resilience (Story 6.6) ──────────────────────────────────────────
        // Bind options from config (Veriqan:Gate:Resilience); defaults are used when the
        // section is absent so the gate still has a 30 s timeout and an 80 % fail-ratio
        // circuit breaker even without explicit configuration.
        services.Configure<GateResilienceOptions>(
            config.GetSection(GateResilienceOptions.Section));

        // The Polly ResiliencePipeline is a Singleton: the circuit-breaker state must be
        // shared across all scoped pipeline instances so failures from different requests
        // accumulate toward the trip threshold.
        services.AddSingleton(static sp =>
        {
            var opts = sp.GetRequiredService<IOptions<GateResilienceOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<ResilientVerificationPipeline>>();
            return ResilientVerificationPipeline.BuildResiliencePipeline(opts, logger);
        });

        // Inner (concrete) pipeline registered as Scoped under its concrete type so the
        // decorator can resolve it via the service provider without going through the
        // IVerificationPipeline key (which now resolves the decorator).
        services.AddScoped<VerificationPipeline>();

        // Decorator registered as the public interface.  Injects the scoped inner pipeline
        // and the singleton Polly pipeline.
        services.AddScoped<IVerificationPipeline>(static sp => new ResilientVerificationPipeline(
            sp.GetRequiredService<VerificationPipeline>(),
            sp.GetRequiredService<ResiliencePipeline<Result<VerificationOutcome>>>(),
            sp.GetRequiredService<IOptions<GateResilienceOptions>>().Value,
            sp.GetRequiredService<ILogger<ResilientVerificationPipeline>>()));

        services.AddSingleton<IBatchProcessor, BatchProcessor>();

        // IVerificationResultStore and IReprocessAuditRepository are registered by whichever
        // persistence branch ran above:
        //   • SQL path  → EfVerificationResultStore / EfReprocessAuditRepository (AddSingleton,
        //                  registered in the if-block immediately after AddVeriqanPersistence).
        //   • In-memory → InMemoryVerificationResultStore / InMemoryReprocessAuditRepository
        //                  (TryAddSingleton inside AddVeriqanInMemoryPersistence).
        // Both branches register exactly one descriptor each — no duplicate fallback here.
        services.AddScoped<IReprocessService, ReprocessService>();

        return services;
    }

    /// <summary>
    /// Registers lightweight in-memory stubs for <see cref="IVerificationJobRepository"/>,
    /// <see cref="IDispositionRepository"/>, <see cref="IVerificationResultStore"/>,
    /// <see cref="IReprocessAuditRepository"/>, and <see cref="IBatchExceptionLogRepository"/>.
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
        services.TryAddSingleton<IBatchExceptionLogRepository, InMemoryBatchExceptionLogRepository>();

        return services;
    }
}
