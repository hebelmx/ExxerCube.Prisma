// <copyright file="WorkflowContext.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using ExxerCube.Prisma.QaHarness.Evidence;
using ExxerCube.Prisma.QaHarness.Traceability;
using Microsoft.Playwright;

namespace ExxerCube.Prisma.QaHarness.Workflows;

/// <summary>
/// Carries all ambient context a workflow needs to execute: the DI container, the
/// evidence collector, the traceability map, an optional Playwright browser page,
/// and the shared storage path.
/// </summary>
/// <remarks>
/// A new <see cref="WorkflowContext"/> is created for each workflow run by the
/// <see cref="IWorkflowRunner"/> and passed into <see cref="IWorkflow.ExecuteAsync"/>.
/// Workflows must not share a context across runs.
/// </remarks>
/// <param name="Services">
/// The DI service provider scoped to this workflow run.
/// Workflows resolve domain validators, repositories, and other dependencies from it.
/// </param>
/// <param name="Evidence">
/// The evidence collector that accumulates artefacts (screenshots, logs, files) during this run.
/// </param>
/// <param name="Traceability">
/// The traceability map into which the workflow appends entries after execution.
/// </param>
/// <param name="PlaywrightPage">
/// An active Playwright <see cref="IPage"/> when the workflow requires browser automation,
/// or <see langword="null"/> for headless / API-only workflows.
/// </param>
/// <param name="SharedStoragePath">
/// Absolute path to the shared document storage directory, or <see langword="null"/>
/// when the workflow does not require file-system access.
/// </param>
public sealed record WorkflowContext(
    IServiceProvider Services,
    IEvidenceCollector Evidence,
    ITraceabilityMap Traceability,
    IPage? PlaywrightPage = null,
    string? SharedStoragePath = null);
