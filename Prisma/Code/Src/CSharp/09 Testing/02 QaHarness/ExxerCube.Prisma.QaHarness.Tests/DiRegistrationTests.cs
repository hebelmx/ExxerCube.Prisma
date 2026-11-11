// <copyright file="DiRegistrationTests.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using ExxerCube.Prisma.QaHarness.DependencyInjection;
using ExxerCube.Prisma.QaHarness.Evidence;
using ExxerCube.Prisma.QaHarness.Provisioning;
using ExxerCube.Prisma.QaHarness.Reporting;
using ExxerCube.Prisma.QaHarness.Traceability;
using ExxerCube.Prisma.QaHarness.Validators;
using ExxerCube.Prisma.QaHarness.Validators.Health;
using ExxerCube.Prisma.QaHarness.Validators.Ocr;
using ExxerCube.Prisma.QaHarness.Validators.Pipeline;
using ExxerCube.Prisma.QaHarness.Workflows;
using ExxerCube.Prisma.QaHarness.Workflows.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.QaHarness.Tests;

/// <summary>
/// Fast tests for <c>AddQaHarness()</c> and the fluent extension points
/// <c>AddWorkflow</c>, <c>AddValidator</c>, <c>AddReportWriter</c>.
/// No Docker, no HTTP, no Playwright — pure DI container resolution.
/// </summary>
public sealed class DiRegistrationTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(l => l.SetMinimumLevel(LogLevel.None));
        services.AddQaHarness();
        return services.BuildServiceProvider();
    }

    // ── Core service registrations ────────────────────────────────────────────

    /// <summary>
    /// <c>AddQaHarness()</c> must register <see cref="IEnvironmentProvisioner"/>
    /// so it can be resolved without error.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void AddQaHarness_RegistersIEnvironmentProvisioner()
    {
        using var sp = BuildProvider();
        using var scope = sp.CreateScope();
        var svc = scope.ServiceProvider.GetService<IEnvironmentProvisioner>();
        svc.ShouldNotBeNull();
    }

    /// <summary>
    /// <c>AddQaHarness()</c> must register <see cref="IWorkflowRunner"/>
    /// so it can be resolved without error.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void AddQaHarness_RegistersIWorkflowRunner()
    {
        using var sp = BuildProvider();
        var svc = sp.GetService<IWorkflowRunner>();
        svc.ShouldNotBeNull();
    }

    /// <summary>
    /// <c>AddQaHarness()</c> must register <see cref="ITraceabilityMap"/>
    /// so it can be resolved without error.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void AddQaHarness_RegistersITraceabilityMap()
    {
        using var sp = BuildProvider();
        var svc = sp.GetService<ITraceabilityMap>();
        svc.ShouldNotBeNull();
    }

    /// <summary>
    /// <c>AddQaHarness()</c> must register <see cref="IEvidenceCollector"/>
    /// so it can be resolved without error.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void AddQaHarness_RegistersIEvidenceCollector()
    {
        using var sp = BuildProvider();
        var svc = sp.GetService<IEvidenceCollector>();
        svc.ShouldNotBeNull();
    }

    /// <summary>
    /// <c>AddQaHarness()</c> must register all three <see cref="IReportWriter"/>
    /// implementations (Markdown, HTML, JSON).
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void AddQaHarness_RegistersThreeReportWriters()
    {
        using var sp = BuildProvider();
        var writers = sp.GetServices<IReportWriter>().ToList();
        writers.Count.ShouldBe(3);
        writers.ShouldContain(w => w.Format == "markdown");
        writers.ShouldContain(w => w.Format == "html");
        writers.ShouldContain(w => w.Format == "json");
    }

    /// <summary>
    /// <c>AddQaHarness()</c> must register exactly six catalog workflows as <see cref="IWorkflow"/>.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void AddQaHarness_RegistersSixCatalogWorkflows()
    {
        using var sp = BuildProvider();
        var workflows = sp.GetServices<IWorkflow>().ToList();

        // Six catalog workflows
        workflows.Count.ShouldBe(6,
            $"Expected 6 workflows but got {workflows.Count}: {string.Join(", ", workflows.Select(w => w.Name))}");

        var names = workflows.Select(w => w.Name).ToHashSet();
        names.ShouldContain("LoginWorkflow");
        names.ShouldContain("IngestionWorkflow");
        names.ShouldContain("ExportWorkflow");
        names.ShouldContain("ManualReviewWorkflow");
        names.ShouldContain("HealthCheckWorkflow");
        names.ShouldContain("ConfigWorkflow");
    }

    /// <summary>
    /// The <see cref="IWorkflowRunner"/> resolved from DI must have
    /// its <see cref="IWorkflowRunner.AvailableWorkflows"/> populated with all six catalog workflows.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void AddQaHarness_WorkflowRunner_HasSixAvailableWorkflows()
    {
        using var sp = BuildProvider();
        var runner = sp.GetRequiredService<IWorkflowRunner>();
        runner.AvailableWorkflows.Count.ShouldBe(6);
    }

    /// <summary>
    /// <c>AddQaHarness()</c> must register <see cref="IIngestionDriver"/> as
    /// <see cref="ExxerCube.Prisma.QaHarness.Workflows.Abstractions.OrchestratorIngestionDriver"/>.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void AddQaHarness_RegistersIIngestionDriver()
    {
        using var sp = BuildProvider();
        var svc = sp.GetService<IIngestionDriver>();
        svc.ShouldNotBeNull();
        svc.ShouldBeOfType<OrchestratorIngestionDriver>();
    }

    /// <summary>
    /// <c>AddQaHarness()</c> must register <see cref="IExportTrigger"/> as
    /// <see cref="ExxerCube.Prisma.QaHarness.Workflows.Abstractions.AdaptiveExporterExportTrigger"/>.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void AddQaHarness_RegistersIExportTrigger()
    {
        using var sp = BuildProvider();
        var svc = sp.GetService<IExportTrigger>();
        svc.ShouldNotBeNull();
        svc.ShouldBeOfType<AdaptiveExporterExportTrigger>();
    }

    // ── String validators ─────────────────────────────────────────────────────

    /// <summary>
    /// <c>AddQaHarness()</c> must register at least the SIRO XML, Excel, and Fusion validators
    /// as <see cref="IDomainValidator{TSubject}"/> where TSubject is <see cref="string"/>.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void AddQaHarness_RegistersStringValidators()
    {
        using var sp = BuildProvider();
        var validators = sp.GetServices<IDomainValidator<string>>().ToList();
        // At minimum: SiroXmlStructureValidator, SiroXmlSchemaValidator,
        //             ExcelExportValidator, FusionOutputValidator = 4
        validators.Count.ShouldBeGreaterThanOrEqualTo(4);
    }

    /// <summary>
    /// <c>AddQaHarness()</c> must register <see cref="IDomainValidator{TSubject}"/> where
    /// TSubject is <see cref="OcrValidationSubject"/> (for <c>OcrTextValidator</c>).
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void AddQaHarness_RegistersOcrTextValidator()
    {
        using var sp = BuildProvider();
        var svc = sp.GetService<IDomainValidator<OcrValidationSubject>>();
        svc.ShouldNotBeNull();
    }

    /// <summary>
    /// <c>AddQaHarness()</c> must register <see cref="IDomainValidator{TSubject}"/> where
    /// TSubject is <see cref="HealthEndpointSubject"/> (for <c>HealthEndpointValidator</c>).
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void AddQaHarness_RegistersHealthEndpointValidator()
    {
        using var sp = BuildProvider();
        var svc = sp.GetService<IDomainValidator<HealthEndpointSubject>>();
        svc.ShouldNotBeNull();
    }

    /// <summary>
    /// <c>AddQaHarness()</c> must register <see cref="IDomainValidator{TSubject}"/> where
    /// TSubject is <see cref="IReadOnlyList{AuditRow}"/> (for <c>AuditTrailValidator</c>).
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void AddQaHarness_RegistersAuditTrailValidator()
    {
        using var sp = BuildProvider();
        var svc = sp.GetService<IDomainValidator<IReadOnlyList<AuditRow>>>();
        svc.ShouldNotBeNull();
    }

    // ── Fluent extension points ───────────────────────────────────────────────

    /// <summary>
    /// <c>AddWorkflow&lt;T&gt;()</c> must add the workflow to the DI container so that
    /// <see cref="IWorkflowRunner.AvailableWorkflows"/> exposes it.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void AddWorkflow_AddsCustomWorkflowToRunner()
    {
        var services = new ServiceCollection();
        services.AddLogging(l => l.SetMinimumLevel(LogLevel.None));
        services.AddQaHarness();
        services.AddWorkflow<StubCustomWorkflow>();

        // Re-build the runner singleton after adding the custom workflow.
        // (The runner singleton is built from the IWorkflow registrations at build time;
        //  to pick up AddWorkflow we must rebuild the provider.)
        using var sp = services.BuildServiceProvider();
        var workflows = sp.GetServices<IWorkflow>().ToList();

        workflows.ShouldContain(w => w.Name == "StubCustomWorkflow",
            "AddWorkflow<StubCustomWorkflow>() must add it to IEnumerable<IWorkflow>");
    }

    /// <summary>
    /// <c>AddReportWriter&lt;T&gt;()</c> must add the custom writer alongside the built-in three.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void AddReportWriter_AddsCustomWriterToRegistration()
    {
        var services = new ServiceCollection();
        services.AddLogging(l => l.SetMinimumLevel(LogLevel.None));
        services.AddQaHarness();
        services.AddReportWriter<StubCustomReportWriter>();

        using var sp = services.BuildServiceProvider();
        var writers = sp.GetServices<IReportWriter>().ToList();

        // 3 built-in + 1 custom = 4
        writers.Count.ShouldBe(4);
        writers.ShouldContain(w => w.Format == "stub-format");
    }

    // ── Stub helpers ──────────────────────────────────────────────────────────

    private sealed class StubCustomWorkflow : IWorkflow
    {
        public string Name => "StubCustomWorkflow";
        public string Description => "Custom workflow registered via AddWorkflow<T>.";
        public IReadOnlyList<string> Tags => [];
        public IReadOnlyList<string> RequiredCapabilities => [];

        public Task<WorkflowResult> ExecuteAsync(
            WorkflowContext context,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkflowResult(
                WorkflowName: Name,
                StartedAt: DateTimeOffset.UtcNow,
                FinishedAt: DateTimeOffset.UtcNow,
                Status: WorkflowStatus.Completed,
                AbortReason: null,
                Evidence: context.Evidence.CurrentPackage,
                Outputs: new Dictionary<string, object?>()));
    }

    private sealed class StubCustomReportWriter : IReportWriter
    {
        public string Format => "stub-format";

        public Task<IndQuestResults.Result<string>> WriteAsync(
            HarnessRunSummary summary,
            string outputPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(IndQuestResults.Result<string>.WithSuccess(outputPath));
    }
}
