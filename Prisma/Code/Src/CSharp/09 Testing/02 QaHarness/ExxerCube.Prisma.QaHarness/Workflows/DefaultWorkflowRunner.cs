// <copyright file="DefaultWorkflowRunner.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using ExxerCube.Prisma.QaHarness.Provisioning;
using ExxerCube.Prisma.QaHarness.Traceability;
using ExxerCube.Prisma.QaHarness.Workflows.Attributes;
using ExxerCube.Prisma.QaHarness.Evidence;

namespace ExxerCube.Prisma.QaHarness.Workflows;

/// <summary>
/// Default implementation of <see cref="IWorkflowRunner"/> that runs workflows
/// sequentially, enforces capability checks, and auto-populates the traceability
/// map from <see cref="TracesRequirementAttribute"/>, <see cref="TracesFeatureAttribute"/>,
/// and <see cref="TracesInvariantAttribute"/> after each run.
/// </summary>
/// <remarks>
/// Capability gating: before calling <see cref="IWorkflow.ExecuteAsync"/>, the runner
/// resolves <see cref="EnvironmentProvisioningResult"/> from the context's
/// <see cref="WorkflowContext.Services"/> (if registered) and compares each entry in
/// <see cref="IWorkflow.RequiredCapabilities"/> against the available capability list.
/// Any missing capability causes the workflow to be returned immediately with
/// <see cref="WorkflowStatus.Skipped"/> — <c>ExecuteAsync</c> is never called.
///
/// Exception safety: any exception escaping <c>ExecuteAsync</c> is caught and converted
/// to a <see cref="WorkflowStatus.Aborted"/> result so the runner never propagates
/// workflow exceptions to the caller.
/// </remarks>
public sealed class DefaultWorkflowRunner : IWorkflowRunner
{
    private readonly IReadOnlyList<IWorkflow> _workflows;

    /// <summary>
    /// Initializes a new instance of <see cref="DefaultWorkflowRunner"/> with the
    /// complete set of registered workflows.
    /// </summary>
    /// <param name="workflows">
    /// All <see cref="IWorkflow"/> instances registered in the DI container.
    /// Must not be <see langword="null"/>.
    /// </param>
    public DefaultWorkflowRunner(IEnumerable<IWorkflow> workflows)
    {
        ArgumentNullException.ThrowIfNull(workflows);
        _workflows = workflows.ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public IReadOnlyList<IWorkflow> AvailableWorkflows => _workflows;

    /// <inheritdoc/>
    public async Task<WorkflowResult> RunAsync(
        IWorkflow workflow,
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(context);

        var startedAt = DateTimeOffset.UtcNow;

        if (cancellationToken.IsCancellationRequested)
        {
            return BuildResult(workflow.Name, startedAt, WorkflowStatus.Aborted,
                "RunAsync was cancelled before execution began.",
                context.Evidence.CurrentPackage,
                emptyOutputs: true);
        }

        // ── Capability check ────────────────────────────────────────────────
        if (workflow.RequiredCapabilities.Count > 0)
        {
            var missingCapability = FindMissingCapability(workflow, context);
            if (missingCapability is not null)
            {
                var skippedAt = DateTimeOffset.UtcNow;
                AppendTraceability(workflow, context.Traceability, skippedAt);
                return BuildResult(workflow.Name, startedAt, WorkflowStatus.Skipped,
                    $"Required capability '{missingCapability}' is not available.",
                    context.Evidence.CurrentPackage,
                    emptyOutputs: true,
                    finishedAt: skippedAt);
            }
        }

        // ── Execute ─────────────────────────────────────────────────────────
        WorkflowResult result;
        try
        {
            result = await workflow.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = BuildResult(workflow.Name, startedAt, WorkflowStatus.Aborted,
                "Workflow was cancelled during execution.",
                context.Evidence.CurrentPackage,
                emptyOutputs: true);
        }
        catch (Exception ex)
        {
            result = BuildResult(workflow.Name, startedAt, WorkflowStatus.Aborted,
                $"Unhandled exception in workflow: {ex.GetType().Name}: {ex.Message}",
                context.Evidence.CurrentPackage,
                emptyOutputs: true);
        }

        // ── Traceability ────────────────────────────────────────────────────
        AppendTraceability(workflow, context.Traceability, result.FinishedAt);

        return result;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Checks each required capability against the registered
    /// <see cref="EnvironmentProvisioningResult"/> (resolved from DI, if present).
    /// Returns the name of the first missing capability, or <see langword="null"/>
    /// when all capabilities are satisfied.
    /// </summary>
    private static string? FindMissingCapability(IWorkflow workflow, WorkflowContext context)
    {
        // Try to resolve the provisioning result from the DI container.
        // If it is not registered (e.g. in unit tests) all capabilities are considered available.
        var provisioningResult = context.Services
            .GetService<EnvironmentProvisioningResult>();

        if (provisioningResult is null)
            return null;

        foreach (var capability in workflow.RequiredCapabilities)
        {
            var status = provisioningResult.CapabilityStatuses
                .FirstOrDefault(c => string.Equals(c.Capability, capability, StringComparison.OrdinalIgnoreCase));

            if (status is null || !status.Available)
                return capability;
        }

        return null;
    }

    /// <summary>
    /// Reads <see cref="TracesRequirementAttribute"/>, <see cref="TracesFeatureAttribute"/>,
    /// and <see cref="TracesInvariantAttribute"/> from the workflow type via reflection
    /// and appends a <see cref="TraceabilityEntry"/> to the traceability map.
    /// </summary>
    private static void AppendTraceability(
        IWorkflow workflow,
        ITraceabilityMap traceabilityMap,
        DateTimeOffset recordedAt)
    {
        var type = workflow.GetType();

        var requirements = type
            .GetCustomAttributes(typeof(TracesRequirementAttribute), inherit: true)
            .Cast<TracesRequirementAttribute>()
            .Select(a => new RequirementRef(a.Id, a.Id))
            .ToList()
            .AsReadOnly();

        var features = type
            .GetCustomAttributes(typeof(TracesFeatureAttribute), inherit: true)
            .Cast<TracesFeatureAttribute>()
            .Select(a => new FeatureRef(a.Id, a.Id))
            .ToList()
            .AsReadOnly();

        var invariants = type
            .GetCustomAttributes(typeof(TracesInvariantAttribute), inherit: true)
            .Cast<TracesInvariantAttribute>()
            .Select(a => new InvariantRef(a.Id, a.Id))
            .ToList()
            .AsReadOnly();

        var entry = new TraceabilityEntry(
            SourceId: workflow.Name,
            SourceKind: TraceabilitySourceKind.Workflow,
            Requirements: requirements,
            Features: features,
            Invariants: invariants,
            RecordedAt: recordedAt);

        traceabilityMap.AddEntry(entry);
    }

    private static WorkflowResult BuildResult(
        string workflowName,
        DateTimeOffset startedAt,
        WorkflowStatus status,
        string? abortReason,
        EvidencePackage evidence,
        bool emptyOutputs,
        DateTimeOffset? finishedAt = null)
    {
        return new WorkflowResult(
            WorkflowName: workflowName,
            StartedAt: startedAt,
            FinishedAt: finishedAt ?? DateTimeOffset.UtcNow,
            Status: status,
            AbortReason: abortReason,
            Evidence: evidence,
            Outputs: emptyOutputs
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>());
    }
}
