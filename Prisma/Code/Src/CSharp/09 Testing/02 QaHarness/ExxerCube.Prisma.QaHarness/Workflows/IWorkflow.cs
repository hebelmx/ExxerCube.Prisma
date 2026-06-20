// <copyright file="IWorkflow.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Workflows;

/// <summary>
/// Defines a named, self-contained QA scenario that the harness can execute.
/// Implement this interface to add a new workflow to the harness catalog.
/// </summary>
/// <remarks>
/// Workflows are discovered from the DI container; register with
/// <c>AddQaHarness().AddWorkflow&lt;TWorkflow&gt;()</c>.
/// Annotate implementations with
/// <see cref="Attributes.TracesRequirementAttribute"/>, <see cref="Attributes.TracesFeatureAttribute"/>,
/// and <see cref="Attributes.TracesInvariantAttribute"/> so the runner can auto-populate
/// the traceability map after each run.
/// </remarks>
public interface IWorkflow
{
    /// <summary>
    /// Gets the unique human-readable name of this workflow (e.g. <c>"LoginWorkflow"</c>).
    /// Used as <c>WorkflowResult.WorkflowName</c> and as the CLI command name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a one-sentence description of what this workflow exercises.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the set of categorisation tags for this workflow (e.g. <c>["browser", "auth"]</c>).
    /// Used by the CLI <c>--workflow-tag</c> filter.
    /// </summary>
    IReadOnlyList<string> Tags { get; }

    /// <summary>
    /// Gets the capability identifiers that must be available for this workflow to run
    /// (e.g. <c>["Docker", "Playwright", "SiaraSimulator"]</c>).
    /// The runner skips the workflow with <see cref="WorkflowStatus.Skipped"/> when
    /// any required capability is absent.
    /// </summary>
    IReadOnlyList<string> RequiredCapabilities { get; }

    /// <summary>
    /// Executes the workflow scenario using the supplied <paramref name="context"/>.
    /// </summary>
    /// <param name="context">
    /// Ambient run context providing the DI container, evidence collector,
    /// traceability map, and an optional Playwright page.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the workflow mid-run.</param>
    /// <returns>
    /// A <see cref="WorkflowResult"/> describing the terminal state of the execution.
    /// Implementations must not throw — catch exceptions and return
    /// <see cref="WorkflowStatus.Aborted"/> with an appropriate <c>AbortReason</c>.
    /// </returns>
    Task<WorkflowResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default);
}
