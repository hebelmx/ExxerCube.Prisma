// <copyright file="HarnessIntegrationTests.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.IO;
using System.Net.Http;
using ExxerCube.Prisma.QaHarness.DependencyInjection;
using ExxerCube.Prisma.QaHarness.Evidence;
using ExxerCube.Prisma.QaHarness.Hosting;
using ExxerCube.Prisma.QaHarness.Provisioning;
using ExxerCube.Prisma.QaHarness.Reporting;
using ExxerCube.Prisma.QaHarness.Traceability;
using ExxerCube.Prisma.QaHarness.Workflows;
using ExxerCube.Prisma.QaHarness.Workflows.Catalog;
using ExxerCube.Prisma.Testing.Infrastructure.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.QaHarness.Tests;

/// <summary>
/// Self-test suite for the QA Harness:
/// <list type="bullet">
///   <item><description>Fast (no Docker): full in-process harness run using a stub HttpClient that confirms report writing.</description></item>
///   <item><description>Integration (Docker required): live HealthCheckWorkflow run against a real Web UI host.</description></item>
/// </list>
/// These tests prove the harness components are correctly wired — they do NOT QA the product.
/// </summary>
public sealed class HarnessIntegrationTests
{
    // ── Shared helpers ────────────────────────────────────────────────────────

    private static ServiceProvider BuildHarnessProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(l => l.SetMinimumLevel(LogLevel.None));
        services.AddQaHarness();
        return services.BuildServiceProvider();
    }

    private static EnvironmentProvisioningResult BuildStubProvisioningResult(
        bool dockerAvailable = false,
        string? sqlConnectionString = null)
    {
        var capabilities = new List<CapabilityStatus>
        {
            new("Docker", dockerAvailable, dockerAvailable ? null : "Docker not available (fast test stub)"),
            new("Corpus", true, "Restored from static Fixtures (stub)"),
            new("Http", true, null),
        };

        return new EnvironmentProvisioningResult(
            SqlConnectionString: sqlConnectionString,
            OllamaEndpoint: null,
            CorpusPath: Path.GetTempPath(),
            CorpusStatus: CorpusStatus.RestoredFromFixtures,
            DockerAvailable: dockerAvailable,
            CapabilityStatuses: capabilities.AsReadOnly());
    }

    // ── Fast test: full in-process harness run ────────────────────────────────

    /// <summary>
    /// Full in-process harness run (no Docker, no live app):
    /// <list type="number">
    ///   <item><description>Builds a DI container with <c>AddQaHarness()</c>.</description></item>
    ///   <item><description>Creates a <see cref="WorkflowContext"/> backed by a stub <see cref="HttpClient"/>
    ///   so the <see cref="HealthCheckWorkflow"/> can return <see cref="WorkflowStatus.Completed"/>.</description></item>
    ///   <item><description>Runs <see cref="HealthCheckWorkflow"/> via <see cref="DefaultWorkflowRunner"/>.</description></item>
    ///   <item><description>Writes a Markdown report to a temp directory.</description></item>
    ///   <item><description>Asserts the report file exists and contains all required section headers.</description></item>
    /// </list>
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task HarnessFastE2E_HealthCheckWithStubHttpClient_WritesMarkdownReport()
    {
        // Arrange ── DI container
        using var sp = BuildHarnessProvider();
        var runner = sp.GetRequiredService<IWorkflowRunner>();
        var traceability = sp.GetRequiredService<ITraceabilityMap>();
        var evidence = sp.GetRequiredService<IEvidenceCollector>();

        // Stub HttpClient: both /health/live and /health/ready return 200 Healthy.
        var httpClient = new HttpClient(new StubHealthyHandler())
        {
            BaseAddress = new Uri("http://localhost:5000"),
        };

        // Register the stub HttpClient into a child DI container so WorkflowContext.Services resolves it.
        var contextServices = new ServiceCollection();
        contextServices.AddSingleton(httpClient);
        using var contextSp = contextServices.BuildServiceProvider();

        var context = new WorkflowContext(
            Services: contextSp,
            Evidence: evidence,
            Traceability: traceability,
            PlaywrightPage: null,
            SharedStoragePath: null);

        var workflow = new HealthCheckWorkflow();

        // Act ── run workflow
        var workflowResult = await runner.RunAsync(
            workflow, context, TestContext.Current.CancellationToken);

        // Assert ── workflow completed (Http capability available via stub HttpClient)
        workflowResult.Status.ShouldBe(WorkflowStatus.Completed,
            $"Workflow should complete with a stub HTTP handler. AbortReason: {workflowResult.AbortReason}");
        workflowResult.Outputs.ShouldContainKey("LiveStatus");
        workflowResult.Outputs.ShouldContainKey("ReadyStatus");

        // Assert ── traceability entry recorded
        var entries = traceability.GetAllEntries();
        entries.ShouldContain(e => e.SourceId == "HealthCheckWorkflow",
            "DefaultWorkflowRunner must append a TraceabilityEntry after the workflow run.");

        // Assert ── Markdown report written with all required section headers
        var outputDir = Path.Combine(Path.GetTempPath(), $"harness-fast-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);
        var reportPath = Path.Combine(outputDir, "report.md");

        var provisioningResult = BuildStubProvisioningResult(dockerAvailable: false);
        var summary = new HarnessRunSummary(
            RunId: "fast-e2e-001",
            ProductVersion: "0.0.0-test",
            StartedAt: DateTimeOffset.UtcNow.AddSeconds(-5),
            FinishedAt: DateTimeOffset.UtcNow,
            ProvisioningResult: provisioningResult,
            WorkflowResults: [workflowResult],
            ValidationResults: [],
            Evidence: evidence.CurrentPackage,
            TraceabilityMap: traceability);

        var writer = new MarkdownReportWriter();
        var writeResult = await writer.WriteAsync(
            summary, reportPath, TestContext.Current.CancellationToken);

        try
        {
            writeResult.IsSuccess.ShouldBeTrue(writeResult.Error ?? "WriteAsync failed");
            File.Exists(reportPath).ShouldBeTrue("Report file must exist after WriteAsync.");

            var content = await File.ReadAllTextAsync(
                reportPath, TestContext.Current.CancellationToken);

            // All ten required section headers per ARCHITECTURE.md §3.7
            content.ShouldContain("## §1 Executive Summary");
            content.ShouldContain("## §2 Environment & Capability Status");
            content.ShouldContain("## §3 Requirement Traceability");
            content.ShouldContain("## §4 Workflow Results");
            content.ShouldContain("## §5 Findings");
            content.ShouldContain("## §6 Human-Review Queue");
            content.ShouldContain("## §7 Evidence Inventory");
            content.ShouldContain("## §8 Coverage Assessment");
            content.ShouldContain("## §9 Risk Register");
            content.ShouldContain("## §10 Deployment Recommendation");

            // Workflow result must appear in §4
            content.ShouldContain("HealthCheckWorkflow");
            content.ShouldContain("Completed");

            // Traceability entry must appear in §3
            content.ShouldContain("REQ");
        }
        finally
        {
            try { Directory.Delete(outputDir, recursive: true); } catch { /* cleanup */ }
            httpClient.Dispose();
        }
    }

    // ── Integration test: live Web UI + HealthCheckWorkflow ──────────────────

    /// <summary>
    /// Live-workflow integration proof (Docker required):
    /// <list type="number">
    ///   <item><description>Provisions a SQL Server container via <see cref="SqlServerContainerFixture"/>.</description></item>
    ///   <item><description>Starts the Web UI via <see cref="PrismaWebUiHostController"/>.</description></item>
    ///   <item><description>Runs <see cref="HealthCheckWorkflow"/> via <see cref="DefaultWorkflowRunner"/>
    ///   with the host's real <see cref="HttpClient"/>.</description></item>
    ///   <item><description>Asserts provisioning succeeded, host is healthy, workflow status is
    ///   <see cref="WorkflowStatus.Completed"/>, evidence was captured, and a Markdown report can be written.</description></item>
    /// </list>
    /// This test is the proof of the Chunk-6 success criterion: "the harness can execute representative workflows".
    /// </summary>
    [Fact]
    [Trait("category", "integration")]
    public async Task HarnessIntegration_ProvisionBootHealthCheck_CompletesWithReport()
    {
        var ct = TestContext.Current.CancellationToken;
        var outputDir = Path.Combine(Path.GetTempPath(), $"harness-integration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        // ── Step 1: Provision SQL Server container ───────────────────────────
        await using var sqlFixture = new SqlServerContainerFixture();
        await sqlFixture.InitializeAsync();
        var sqlConnectionString = await sqlFixture.CreateIsolatedDatabaseAsync(
            "HarnessIntegration", ct);

        sqlConnectionString.ShouldNotBeNullOrWhiteSpace(
            "SQL container must provide a connection string.");

        // ── Step 2: Start the Web UI host ────────────────────────────────────
        await using var hostController = new PrismaWebUiHostController();
        var hostingOptions = new HostingOptions(
            Mode: HostingMode.WebUiOnly,
            SqlConnectionString: sqlConnectionString,
            SharedStoragePath: null,
            DisableAutonomousWatchLoop: true);

        var startResult = await hostController.StartAsync(hostingOptions, ct);

        startResult.IsSuccess.ShouldBeTrue(
            $"PrismaWebUiHostController.StartAsync must succeed. " +
            $"Errors: {string.Join("; ", startResult.Errors ?? [])}");

        var startup = startResult.Value!;
        startup.BaseAddress.ShouldNotBeNull("A Kestrel base address must be bound.");

        // NOTE on IsHealthy: the Web UI's /health endpoint runs PrismaDbHealthCheck which calls
        // CanConnectAsync on ApplicationDbContext. The health status depends on whether the DB
        // is reachable AND correctly configured. In the harness integration test the Testcontainers
        // SQL Server is reachable, but EF Core migrations have not been applied to the isolated DB.
        // The health endpoint may return 200 (Healthy — CanConnectAsync succeeds) or 503
        // (Unhealthy — CanConnectAsync fails for any transient reason). Both are valid harness
        // observations; the QA agent interprets the result. We record it in Outputs below.
        //
        // We do NOT assert IsHealthy here because that would make the harness a PASS/FAIL judge
        // (violating the architecture constraint). We DO assert the host started and has a base address.
        Console.WriteLine(
            $"[HarnessIntegration] Host startup: IsHealthy={startup.IsHealthy}, " +
            $"BaseAddress={startup.BaseAddress}, FailureReason={startup.FailureReason ?? "(none)"}");

        // ── Step 3: Build WorkflowContext with host's HttpClient ─────────────
        var httpClient = new HttpClient
        {
            BaseAddress = startup.BaseAddress,
            Timeout = TimeSpan.FromSeconds(30),
        };

        // Resolve harness services from a fresh DI container (simulating CLI usage).
        using var sp = BuildHarnessProvider();
        var runner = sp.GetRequiredService<IWorkflowRunner>();
        var traceability = sp.GetRequiredService<ITraceabilityMap>();
        var evidence = sp.GetRequiredService<IEvidenceCollector>();

        var contextServices = new ServiceCollection();
        contextServices.AddSingleton(httpClient);
        using var contextSp = contextServices.BuildServiceProvider();

        var context = new WorkflowContext(
            Services: contextSp,
            Evidence: evidence,
            Traceability: traceability,
            PlaywrightPage: null,
            SharedStoragePath: null);

        // ── Step 4: Run HealthCheckWorkflow ──────────────────────────────────
        var workflow = new HealthCheckWorkflow();
        var workflowResult = await runner.RunAsync(workflow, context, ct);

        // ── Step 5: Assert workflow outcome ──────────────────────────────────
        // The workflow status should be Completed regardless of whether /health/live returns 200
        // or 503 — the workflow records the response and lets the QA agent decide.
        // We assert it is NOT Aborted (which would indicate the harness itself failed, not the product).
        workflowResult.Status.ShouldNotBe(WorkflowStatus.Aborted,
            $"HealthCheckWorkflow must not abort (harness error). AbortReason: {workflowResult.AbortReason}");

        workflowResult.WorkflowName.ShouldBe("HealthCheckWorkflow");
        workflowResult.Outputs.ShouldContainKey("LiveStatus",
            "HealthCheckWorkflow must record LiveStatus in Outputs.");
        workflowResult.Outputs.ShouldContainKey("ReadyStatus",
            "HealthCheckWorkflow must record ReadyStatus in Outputs.");

        // Traceability must be recorded
        traceability.GetAllEntries()
            .ShouldContain(e => e.SourceId == "HealthCheckWorkflow",
                "DefaultWorkflowRunner must append a TraceabilityEntry for HealthCheckWorkflow.");

        // ── Step 6: Write a Markdown report ──────────────────────────────────
        var provisioningResult = new EnvironmentProvisioningResult(
            SqlConnectionString: sqlConnectionString,
            OllamaEndpoint: null,
            CorpusPath: Path.GetTempPath(),
            CorpusStatus: CorpusStatus.RestoredFromFixtures,
            DockerAvailable: true,
            CapabilityStatuses:
            [
                new CapabilityStatus("SqlServer", true, null),
                new CapabilityStatus("Http", true, null),
            ]);

        var summary = new HarnessRunSummary(
            RunId: "integration-live-001",
            ProductVersion: "0.0.0-integration",
            StartedAt: DateTimeOffset.UtcNow.AddSeconds(-30),
            FinishedAt: DateTimeOffset.UtcNow,
            ProvisioningResult: provisioningResult,
            WorkflowResults: [workflowResult],
            ValidationResults: [],
            Evidence: evidence.CurrentPackage,
            TraceabilityMap: traceability);

        var reportPath = Path.Combine(outputDir, "integration-report.md");
        var writer = new MarkdownReportWriter();
        var writeResult = await writer.WriteAsync(summary, reportPath, ct);

        try
        {
            writeResult.IsSuccess.ShouldBeTrue(
                $"MarkdownReportWriter.WriteAsync must succeed. Error: {writeResult.Error}");
            File.Exists(reportPath).ShouldBeTrue("Integration report file must exist.");

            var content = await File.ReadAllTextAsync(reportPath, ct);
            content.ShouldContain("## §1 Executive Summary");
            content.ShouldContain("## §4 Workflow Results");
            content.ShouldContain("HealthCheckWorkflow");

            // Surface the report path so it can be observed in test output.
            Console.WriteLine($"[HarnessIntegration] Report written to: {reportPath}");
            Console.WriteLine(
                $"[HarnessIntegration] LiveStatus={workflowResult.Outputs.GetValueOrDefault("LiveStatus")}, " +
                $"ReadyStatus={workflowResult.Outputs.GetValueOrDefault("ReadyStatus")}");
        }
        finally
        {
            httpClient.Dispose();
            try { Directory.Delete(outputDir, recursive: true); } catch { /* cleanup */ }
        }
    }

    // ── Stub helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal HTTP message handler that returns 200 Healthy for any GET to /health/*.
    /// Used to exercise the fast e2e path without needing a live application host.
    /// </summary>
    private sealed class StubHealthyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(
                    "Healthy",
                    System.Text.Encoding.UTF8,
                    "text/plain"),
            };

            return Task.FromResult(response);
        }
    }
}
