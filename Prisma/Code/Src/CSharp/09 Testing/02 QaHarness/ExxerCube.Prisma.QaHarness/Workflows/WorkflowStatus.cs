// <copyright file="WorkflowStatus.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Workflows;

/// <summary>
/// Describes the terminal state of a workflow execution.
/// </summary>
public enum WorkflowStatus
{
    /// <summary>The workflow ran to completion and all steps executed without unhandled errors.</summary>
    Completed,

    /// <summary>
    /// The workflow was stopped before completion due to an unrecoverable error or a
    /// missing required capability (e.g. Docker unavailable).
    /// See <see cref="WorkflowResult.AbortReason"/> for details.
    /// </summary>
    Aborted,

    /// <summary>
    /// The workflow was not executed because a required capability was absent.
    /// Skipped workflows are reported in the summary but do not count as failures.
    /// </summary>
    Skipped,
}
