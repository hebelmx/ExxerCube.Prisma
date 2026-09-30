// <copyright file="WorkflowRunnerTests.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using ExxerCube.Prisma.QaHarness.Evidence;
using ExxerCube.Prisma.QaHarness.Provisioning;
using ExxerCube.Prisma.QaHarness.Traceability;
using ExxerCube.Prisma.QaHarness.Workflows;
using ExxerCube.Prisma.QaHarness.Workflows.Attributes;
using ExxerCube.Prisma.QaHarness.Workflows.Catalog;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.QaHarness.Tests;

/// <summary>
/// Fast unit tests for <see cref="DefaultWorkflowRunner"/>.
/// No Docker, no Playwright, no HTTP — pure in-process logic.
/// </summary>
public sealed class WorkflowRunnerTests
{
    // ── Shared helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="WorkflowContext"/> backed by a minimal DI container
    /// that optionally contains an <see cref="EnvironmentProvisioningResult"/>.
    /// </summary>
    private static WorkflowContext BuildContext(EnvironmentProvisioningResult? provisioningResult = null)
    {
        var services = new ServiceCollection();
        if (provisioningResult is not null)
            services.AddSingleton(provisioningResult);

        var sp = services.BuildServiceProvider();
        var traceability = new TraceabilityMap();
        var evidence = Substitute.For<IEvidenceCollector>();
        evidence.CurrentPackage.Returns(new EvidencePackage("test-run", []));

        return new WorkflowContext(
            Services: sp,
            Evidence: evidence,
            Traceability: traceability);
    }

    private static EnvironmentProvisioningResult ProvisioningWithCapability(
        string capability, bool available) =>
        new(
            SqlConnectionString: null,
            OllamaEndpoint: null,
            CorpusPath: "/tmp/corpus",
            CorpusStatus: CorpusStatus.RestoredFromFixtures,
            DockerAvailable: true,
            CapabilityStatuses:
            [
                new CapabilityStatus(capability, available, available ? null : $"{capability} not available"),
            ]);

    // ── AvailableWorkflows reflects the registered set ────────────────────────

    /// <summary>
    /// <see cref="DefaultWorkflowRunner.AvailableWorkflows"/> must expose exactly the
    /// workflows passed to the constructor.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void DefaultWorkflowRunner_AvailableWorkflows_ReflectsRegisteredSet()
    {
        // Arrange
        var w1 = new AlwaysCompletedStubWorkflow("W1", []);
        var w2 = new AlwaysCompletedStubWorkflow("W2", []);
        var runner = new DefaultWorkflowRunner([w1, w2]);

        // Act + Assert
        runner.AvailableWorkflows.Count.ShouldBe(2);
        runner.AvailableWorkflows.ShouldContain(w => w.Name == "W1");
        runner.AvailableWorkflows.ShouldContain(w => w.Name == "W2");
    }

    // ── Skipped when RequiredCapability is absent ─────────────────────────────

    /// <summary>
    /// When a workflow declares a <see cref="IWorkflow.RequiredCapabilities"/> entry that is
    /// absent from <see cref="EnvironmentProvisioningResult.CapabilityStatuses"/>,
    /// the runner must return <see cref="WorkflowStatus.Skipped"/> without executing the workflow.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task DefaultWorkflowRunner_RunAsync_SkipsWhenRequiredCapabilityAbsent()
    {
        // Arrange — capability "Http" is explicitly marked unavailable
        var provisioning = ProvisioningWithCapability("Http", available: false);
        var context = BuildContext(provisioning);

        var workflow = new AlwaysCompletedStubWorkflow("TestWorkflow", ["Http"]);
        var runner = new DefaultWorkflowRunner([workflow]);

        // Act
        var result = await runner.RunAsync(workflow, context, TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(WorkflowStatus.Skipped);
        result.AbortReason.ShouldNotBeNullOrWhiteSpace();
        (result.AbortReason ?? string.Empty).ShouldContain("Http");

        // The stub's execute flag must NOT have been set
        workflow.WasExecuted.ShouldBeFalse();
    }

    /// <summary>
    /// Mirrors the architecture §13 path: <see cref="HealthCheckWorkflow"/> declares
    /// capability <c>"Http"</c>; when it is missing the runner returns Skipped.
    /// This is the "capability-unavailable" path for a real catalog workflow.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task HealthCheckWorkflow_RunAsync_SkippedWhenHttpCapabilityAbsent()
    {
        // Arrange
        var provisioning = ProvisioningWithCapability("Http", available: false);
        var context = BuildContext(provisioning);

        var workflow = new HealthCheckWorkflow();
        var runner = new DefaultWorkflowRunner([workflow]);

        // Act
        var result = await runner.RunAsync(workflow, context, TestContext.Current.CancellationToken);

        // Assert — runner-level skip, not an Abort inside the workflow
        result.Status.ShouldBe(WorkflowStatus.Skipped);
        (result.AbortReason ?? string.Empty).ShouldContain("Http");
    }

    // ── Traceability entry appended from attributes ───────────────────────────

    /// <summary>
    /// After running a workflow annotated with <see cref="TracesRequirementAttribute"/>
    /// and <see cref="TracesFeatureAttribute"/>, the runner must append a
    /// <see cref="TraceabilityEntry"/> to <see cref="WorkflowContext.Traceability"/> that
    /// references those IDs.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task DefaultWorkflowRunner_RunAsync_AppendsTraceabilityEntryFromAttributes()
    {
        // Arrange — no provisioning result = all capabilities satisfied (no capability gating)
        var context = BuildContext();

        var workflow = new AnnotatedStubWorkflow();
        var runner = new DefaultWorkflowRunner([workflow]);

        // Act
        var result = await runner.RunAsync(workflow, context, TestContext.Current.CancellationToken);

        // Assert — workflow ran to completion
        result.Status.ShouldBe(WorkflowStatus.Completed);

        // Assert — traceability entry was appended
        var entries = context.Traceability.GetAllEntries();
        entries.ShouldNotBeEmpty();

        var entry = entries.FirstOrDefault(e => e.SourceId == "AnnotatedStubWorkflow");
        entry.ShouldNotBeNull();

        // The [TracesRequirement("REQ-STUB-01")] must be surfaced
        entry!.Requirements.ShouldContain(r => r.Id == "REQ-STUB-01");

        // The [TracesFeature("F-STUB")] must be surfaced
        entry.Features.ShouldContain(f => f.Id == "F-STUB");
    }

    // ── Exception inside workflow → Aborted ──────────────────────────────────

    /// <summary>
    /// When a workflow throws an unhandled exception, the runner must catch it and
    /// return <see cref="WorkflowStatus.Aborted"/> with the exception message in
    /// <see cref="WorkflowResult.AbortReason"/>. The exception must not propagate.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task DefaultWorkflowRunner_RunAsync_WorkflowThrows_ReturnsAborted()
    {
        // Arrange
        var context = BuildContext();
        var workflow = new ThrowingStubWorkflow("Simulated pipeline failure");
        var runner = new DefaultWorkflowRunner([workflow]);

        // Act — must not throw
        var result = await runner.RunAsync(workflow, context, TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(WorkflowStatus.Aborted);
        result.AbortReason.ShouldNotBeNullOrWhiteSpace();
        (result.AbortReason ?? string.Empty).ShouldContain("Simulated pipeline failure");
    }

    // ── Traceability appended even for Skipped workflows ─────────────────────

    /// <summary>
    /// Even when a workflow is Skipped (capability absent), the runner should still
    /// append a traceability entry so the coverage map is complete.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public async Task DefaultWorkflowRunner_RunAsync_SkippedWorkflow_StillAppendsTraceability()
    {
        // Arrange
        var provisioning = ProvisioningWithCapability("Docker", available: false);
        var context = BuildContext(provisioning);

        var workflow = new AnnotatedStubWorkflow(requiredCapabilities: ["Docker"]);
        var runner = new DefaultWorkflowRunner([workflow]);

        // Act
        var result = await runner.RunAsync(workflow, context, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(WorkflowStatus.Skipped);

        // Traceability entry still expected
        var entries = context.Traceability.GetAllEntries();
        entries.ShouldNotBeEmpty();
        entries.ShouldContain(e => e.SourceId == "AnnotatedStubWorkflow");
    }

    // ── Multiple workflows AvailableWorkflows count ───────────────────────────

    /// <summary>
    /// The runner constructed with zero workflows exposes an empty list.
    /// </summary>
    [Fact]
    [Trait("category", "fast")]
    public void DefaultWorkflowRunner_AvailableWorkflows_EmptyWhenNoneRegistered()
    {
        var runner = new DefaultWorkflowRunner([]);
        runner.AvailableWorkflows.ShouldBeEmpty();
    }

    // ── Stub implementations ─────────────────────────────────────────────────

    /// <summary>
    /// A minimal stub workflow that always completes and tracks whether it was executed.
    /// </summary>
    private sealed class AlwaysCompletedStubWorkflow : IWorkflow
    {
        private readonly IReadOnlyList<string> _requiredCapabilities;

        public AlwaysCompletedStubWorkflow(string name, IReadOnlyList<string> requiredCapabilities)
        {
            Name = name;
            _requiredCapabilities = requiredCapabilities;
            WasExecuted = false;
        }

        public string Name { get; }
        public string Description => $"Stub: {Name}";
        public IReadOnlyList<string> Tags => ["stub"];
        public IReadOnlyList<string> RequiredCapabilities => _requiredCapabilities;
        public bool WasExecuted { get; private set; }

        public Task<WorkflowResult> ExecuteAsync(
            WorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            WasExecuted = true;
            return Task.FromResult(new WorkflowResult(
                WorkflowName: Name,
                StartedAt: DateTimeOffset.UtcNow,
                FinishedAt: DateTimeOffset.UtcNow,
                Status: WorkflowStatus.Completed,
                AbortReason: null,
                Evidence: context.Evidence.CurrentPackage,
                Outputs: new Dictionary<string, object?>()));
        }
    }

    /// <summary>
    /// A stub workflow annotated with <see cref="TracesRequirementAttribute"/> and
    /// <see cref="TracesFeatureAttribute"/> so the runner's reflection path can be exercised.
    /// </summary>
    [TracesRequirement("REQ-STUB-01")]
    [TracesFeature("F-STUB")]
    private sealed class AnnotatedStubWorkflow : IWorkflow
    {
        private readonly IReadOnlyList<string> _requiredCapabilities;

        public AnnotatedStubWorkflow(IReadOnlyList<string>? requiredCapabilities = null)
        {
            _requiredCapabilities = requiredCapabilities ?? [];
        }

        public string Name => "AnnotatedStubWorkflow";
        public string Description => "Stub with traceability attributes.";
        public IReadOnlyList<string> Tags => ["stub"];
        public IReadOnlyList<string> RequiredCapabilities => _requiredCapabilities;

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

    /// <summary>
    /// A stub workflow that throws unconditionally from <see cref="ExecuteAsync"/>.
    /// </summary>
    private sealed class ThrowingStubWorkflow : IWorkflow
    {
        private readonly string _message;

        public ThrowingStubWorkflow(string message)
        {
            _message = message;
        }

        public string Name => "ThrowingStubWorkflow";
        public string Description => "Throws to test runner error handling.";
        public IReadOnlyList<string> Tags => ["stub"];
        public IReadOnlyList<string> RequiredCapabilities => [];

        public Task<WorkflowResult> ExecuteAsync(
            WorkflowContext context,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(_message);
    }
}
