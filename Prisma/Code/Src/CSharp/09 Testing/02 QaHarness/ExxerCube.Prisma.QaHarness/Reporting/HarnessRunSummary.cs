// <copyright file="HarnessRunSummary.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using ExxerCube.Prisma.QaHarness.Evidence;
using ExxerCube.Prisma.QaHarness.Provisioning;
using ExxerCube.Prisma.QaHarness.Traceability;
using ExxerCube.Prisma.QaHarness.Validators;
using ExxerCube.Prisma.QaHarness.Workflows;

namespace ExxerCube.Prisma.QaHarness.Reporting;

/// <summary>
/// Top-level DTO that aggregates every result produced during a complete QA harness run.
/// Passed to <see cref="IReportWriter.WriteAsync"/> to produce Markdown, HTML, or JSON reports.
/// </summary>
/// <param name="RunId">A unique identifier for this harness run (typically a UTC timestamp or GUID).</param>
/// <param name="ProductVersion">
/// The version string of the Prisma assembly under test (read from the running host's assembly info).
/// </param>
/// <param name="StartedAt">UTC timestamp at which the harness run began.</param>
/// <param name="FinishedAt">UTC timestamp at which the harness run completed (all workflows + report write).</param>
/// <param name="ProvisioningResult">The outcome of the environment provisioning phase.</param>
/// <param name="WorkflowResults">Results of each workflow executed during the run, in execution order.</param>
/// <param name="ValidationResults">Results of each domain validator invocation collected across all workflows.</param>
/// <param name="Evidence">The complete evidence package for the entire run, aggregated from all workflows.</param>
/// <param name="TraceabilityMap">
/// The populated traceability map linking workflow and validator results back to
/// PRD requirements, product features, and system invariants.
/// </param>
public sealed record HarnessRunSummary(
    string RunId,
    string ProductVersion,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    EnvironmentProvisioningResult ProvisioningResult,
    IReadOnlyList<WorkflowResult> WorkflowResults,
    IReadOnlyList<ValidationResult> ValidationResults,
    EvidencePackage Evidence,
    ITraceabilityMap TraceabilityMap);
