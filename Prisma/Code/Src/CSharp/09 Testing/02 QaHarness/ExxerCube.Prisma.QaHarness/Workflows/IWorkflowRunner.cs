// <copyright file="IWorkflowRunner.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Workflows;

/// <summary>
/// Executes <see cref="IWorkflow"/> instances, enforces capability checks, and
/// populates the traceability map from workflow attributes after each run.
/// </summary>
/// <remarks>
/// The default implementation (<c>DefaultWorkflowRunner</c>) runs workflows sequentially
/// to avoid shared-state collisions.  It resolves all registered <see cref="IWorkflow"/>
/// instances from the DI container and exposes them via <see cref="AvailableWorkflows"/>.
/// </remarks>
public interface IWorkflowRunner
{
    /// <summary>
    /// Executes a single <paramref name="workflow"/> within the supplied <paramref name="context"/>.
    /// </summary>
    /// <remarks>
    /// The runner checks <see cref="IWorkflow.RequiredCapabilities"/> against the available
    /// capabilities derived from the <c>EnvironmentProvisioningResult</c>.  When a required
    /// capability is missing the workflow is returned immediately with
    /// <see cref="WorkflowStatus.Skipped"/> without calling
    /// <see cref="IWorkflow.ExecuteAsync"/>.
    /// </remarks>
    /// <param name="workflow">The workflow instance to execute.</param>
    /// <param name="context">Ambient run context for evidence, traceability, and DI access.</param>
    /// <param name="cancellationToken">Token used to cancel the workflow mid-run.</param>
    /// <returns>A <see cref="WorkflowResult"/> describing the terminal state of the execution.</returns>
    Task<WorkflowResult> RunAsync(
        IWorkflow workflow,
        WorkflowContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the complete list of workflows registered with the harness and available for execution.
    /// </summary>
    IReadOnlyList<IWorkflow> AvailableWorkflows { get; }
}
