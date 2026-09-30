// <copyright file="WorkflowResult.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using ExxerCube.Prisma.QaHarness.Evidence;

namespace ExxerCube.Prisma.QaHarness.Workflows;

/// <summary>
/// The immutable outcome of a single workflow execution, returned by
/// <see cref="IWorkflowRunner.RunAsync"/>.
/// </summary>
/// <param name="WorkflowName">The name of the workflow that produced this result (matches <see cref="IWorkflow.Name"/>).</param>
/// <param name="StartedAt">UTC timestamp at which workflow execution began.</param>
/// <param name="FinishedAt">UTC timestamp at which workflow execution ended (completed, aborted, or skipped).</param>
/// <param name="Status">The terminal state of the workflow execution.</param>
/// <param name="AbortReason">
/// A human-readable reason why the workflow was aborted or skipped, or
/// <see langword="null"/> when <paramref name="Status"/> is <see cref="WorkflowStatus.Completed"/>.
/// </param>
/// <param name="Evidence">All evidence items captured during this workflow run.</param>
/// <param name="Outputs">
/// Named output values produced by the workflow for downstream consumption
/// (e.g. <c>"SiroXmlPath"</c> → <c>"/tmp/run/output.siro.xml"</c>).
/// </param>
public sealed record WorkflowResult(
    string WorkflowName,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    WorkflowStatus Status,
    string? AbortReason,
    EvidencePackage Evidence,
    IReadOnlyDictionary<string, object?> Outputs);
