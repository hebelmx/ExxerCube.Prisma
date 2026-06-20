// <copyright file="QaHarnessServiceCollectionExtensions.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using ExxerCube.Prisma.QaHarness.Evidence;
using ExxerCube.Prisma.QaHarness.Hosting;
using ExxerCube.Prisma.QaHarness.Provisioning;
using ExxerCube.Prisma.QaHarness.Reporting;
using ExxerCube.Prisma.QaHarness.Traceability;
using ExxerCube.Prisma.QaHarness.Validators;
using ExxerCube.Prisma.QaHarness.Validators.Export;
using ExxerCube.Prisma.QaHarness.Validators.Health;
using ExxerCube.Prisma.QaHarness.Validators.Ocr;
using ExxerCube.Prisma.QaHarness.Validators.Pipeline;
using ExxerCube.Prisma.QaHarness.Validators.SiroXml;
using ExxerCube.Prisma.QaHarness.Workflows;
using ExxerCube.Prisma.QaHarness.Workflows.Abstractions;
using ExxerCube.Prisma.QaHarness.Workflows.Catalog;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.QaHarness.DependencyInjection;

/// <summary>
/// Extension methods for registering QA Harness services with an <see cref="IServiceCollection"/>.
/// </summary>
public static class QaHarnessServiceCollectionExtensions
{
    // ── Lifetime rationale ─────────────────────────────────────────────────────
    // Transient:
    //   • Workflow implementations — each QA run gets fresh workflow state; no shared fields.
    //   • Report writers — stateless; writing is driven entirely by the summary passed to WriteAsync.
    //   • Validators — stateless pure functions; safe to share but Transient avoids stale state.
    //   • Host controllers — owning disposable resources (WAF, SQL containers) that must be
    //     scoped to a single harness run; Transient ensures a fresh controller per resolve.
    //   • Evidence collectors — own mutable internal state (item lists) that must be fresh per run.
    // Singleton:
    //   • TraceabilityMap — accumulates entries across the lifetime of a run; one map per DI root.
    //   • DockerHealthGate — checks a global system property; safe to cache the result in a run.
    //   • CorpusSeeder — stateless helper with a logger; singleton is fine.
    // Scoped:
    //   • PrismaEnvironmentProvisioner — owns its SQL container fixture (a non-trivial resource);
    //     registering Scoped ties its lifetime to the QA runner scope so callers can scope-manage
    //     provisioning and guarantee TeardownAsync is called once per run.

    /// <summary>
    /// Registers all core QA Harness services into the dependency injection container.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance so calls can be chained.</returns>
    /// <remarks>
    /// Registers, in order:
    /// <list type="bullet">
    ///   <item><description>Provisioning: <see cref="DockerHealthGate"/>, <see cref="CorpusSeeder"/>, <see cref="IEnvironmentProvisioner"/> → <see cref="PrismaEnvironmentProvisioner"/>.</description></item>
    ///   <item><description>Hosting: <see cref="PrismaWebUiHostController"/>, <see cref="ThreeProcessHostController"/>.</description></item>
    ///   <item><description>Evidence: individual collectors + a composite <see cref="IEvidenceCollector"/>.</description></item>
    ///   <item><description>Traceability: <see cref="ITraceabilityMap"/> → <see cref="TraceabilityMap"/>.</description></item>
    ///   <item><description>Workflows: <see cref="IWorkflowRunner"/> → <see cref="DefaultWorkflowRunner"/>; all six catalog workflows as <see cref="IWorkflow"/>.</description></item>
    ///   <item><description>Validators: all six domain validators.</description></item>
    ///   <item><description>Report writers: all three writers as <see cref="IReportWriter"/>.</description></item>
    ///   <item><description>Workflow seam adapters: <see cref="IIngestionDriver"/> → <see cref="OrchestratorIngestionDriver"/>; <see cref="IExportTrigger"/> → <see cref="AdaptiveExporterExportTrigger"/>.</description></item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddQaHarness(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // ── Area 1: Provisioning ─────────────────────────────────────────────
        // DockerHealthGate and CorpusSeeder are singletons: they are stateless probes that are
        // safe to cache for the entire run. PrismaEnvironmentProvisioner is Scoped so that the
        // SQL container fixture it owns can be properly torn down when the caller disposes the scope.
        services.AddSingleton<DockerHealthGate>();
        services.AddSingleton<CorpusSeeder>();
        services.AddScoped<IEnvironmentProvisioner, PrismaEnvironmentProvisioner>();

        // ── Area 2: Hosting ──────────────────────────────────────────────────
        // Transient: each call to start a host should get a fresh controller so multiple test
        // sessions do not share WAF/Kestrel/container resources.
        services.AddTransient<PrismaWebUiHostController>();
        services.AddTransient<ThreeProcessHostController>();
        services.AddTransient<IApplicationHostController, PrismaWebUiHostController>();

        // ── Area 5: Evidence ─────────────────────────────────────────────────
        // All individual collectors (FileEvidenceCollector, LogEvidenceCollector,
        // PlaywrightEvidenceCollector) require a runId or an IPage that is only known at
        // workflow-execution time — they cannot be resolved from DI without those params.
        // The CompositeEvidenceCollector is registered as IEvidenceCollector; it receives a
        // runId derived from the current timestamp and starts with no sub-collectors (callers
        // can provide sub-collectors via overloads, or use CompositeEvidenceCollector.Add
        // before passing the instance to a workflow context).
        services.AddTransient<IEvidenceCollector>(sp =>
        {
            // Build a run ID from the current timestamp so each evidence package is distinct.
            var runId = $"run-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            return new CompositeEvidenceCollector(
                runId,
                playwright: null,  // Playwright page is provided per-workflow at runtime
                log: null,         // LogEvidenceCollector requires InMemoryLogBuffer from caller
                file: null);       // FileEvidenceCollector requires runId from caller
        });

        // ── Area 6: Traceability ─────────────────────────────────────────────
        // Singleton: the traceability map accumulates entries over the whole run.
        services.AddSingleton<ITraceabilityMap, TraceabilityMap>();

        // ── Area 3: Workflows ────────────────────────────────────────────────
        // Workflows are Transient — each run gets a fresh instance with no state carried over.
        // All six catalog workflows are registered both as their concrete type AND as IWorkflow so
        // that IWorkflowRunner can enumerate them via GetServices<IWorkflow>().
        services.AddTransient<IWorkflow, LoginWorkflow>();
        services.AddTransient<IWorkflow, IngestionWorkflow>();
        services.AddTransient<IWorkflow, ExportWorkflow>();
        services.AddTransient<IWorkflow, ManualReviewWorkflow>();
        services.AddTransient<IWorkflow, HealthCheckWorkflow>();
        services.AddTransient<IWorkflow, ConfigWorkflow>();

        // WorkflowRunner resolves IEnumerable<IWorkflow> to populate AvailableWorkflows.
        // Registered as Singleton so the runner's workflow list is stable for the run.
        services.AddSingleton<IWorkflowRunner>(sp =>
        {
            var workflows = sp.GetServices<IWorkflow>();
            return new DefaultWorkflowRunner(workflows);
        });

        // ── Area 4: Domain Validators ────────────────────────────────────────
        // Validators are stateless pure functions; Transient avoids any stale state risk.
        services.AddTransient<IDomainValidator<string>, SiroXmlStructureValidator>();
        services.AddTransient<IDomainValidator<string>, SiroXmlSchemaValidator>();
        services.AddTransient<IDomainValidator<string>, ExcelExportValidator>();
        services.AddTransient<IDomainValidator<string>, FusionOutputValidator>();
        services.AddTransient<IDomainValidator<OcrValidationSubject>, OcrTextValidator>();
        services.AddTransient<IDomainValidator<HealthEndpointSubject>, HealthEndpointValidator>();
        services.AddTransient<IDomainValidator<IReadOnlyList<AuditRow>>, AuditTrailValidator>();

        // ── Area 7: Reporting ─────────────────────────────────────────────────
        // Report writers are stateless; Transient is the safe default.
        // All three are registered as IReportWriter so callers can request IEnumerable<IReportWriter>.
        services.AddTransient<IReportWriter, MarkdownReportWriter>();
        services.AddTransient<IReportWriter, HtmlReportWriter>();
        services.AddTransient<IReportWriter, JsonReportWriter>();

        // ── Workflow seam adapters ────────────────────────────────────────────
        // Registered as Transient: adapters delegate to the service provider at call time, so they
        // carry no state between runs.
        services.AddTransient<IIngestionDriver, OrchestratorIngestionDriver>();
        services.AddTransient<IExportTrigger, AdaptiveExporterExportTrigger>();

        return services;
    }

    // ── Fluent extension points (§5 of the architecture) ──────────────────────

    /// <summary>
    /// Adds a custom workflow to the harness, making it available to the
    /// <see cref="IWorkflowRunner"/> and the CLI.
    /// </summary>
    /// <typeparam name="TWorkflow">
    /// A concrete type implementing <see cref="IWorkflow"/>. Must have a public constructor
    /// resolvable by the DI container.
    /// </typeparam>
    /// <param name="services">The service collection to add the workflow to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddWorkflow<TWorkflow>(this IServiceCollection services)
        where TWorkflow : class, IWorkflow
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<IWorkflow, TWorkflow>();
        return services;
    }

    /// <summary>
    /// Adds a custom domain validator to the harness.
    /// </summary>
    /// <typeparam name="TSubject">The subject type the validator operates on.</typeparam>
    /// <typeparam name="TValidator">
    /// A concrete type implementing <see cref="IDomainValidator{TSubject}"/>. Must have a public
    /// constructor resolvable by the DI container.
    /// </typeparam>
    /// <param name="services">The service collection to add the validator to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddValidator<TSubject, TValidator>(this IServiceCollection services)
        where TValidator : class, IDomainValidator<TSubject>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<IDomainValidator<TSubject>, TValidator>();
        return services;
    }

    /// <summary>
    /// Adds a custom report writer to the harness, making it selectable by format in the CLI.
    /// </summary>
    /// <typeparam name="TWriter">
    /// A concrete type implementing <see cref="IReportWriter"/>. Must have a public constructor
    /// resolvable by the DI container.
    /// </typeparam>
    /// <param name="services">The service collection to add the writer to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddReportWriter<TWriter>(this IServiceCollection services)
        where TWriter : class, IReportWriter
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<IReportWriter, TWriter>();
        return services;
    }
}
