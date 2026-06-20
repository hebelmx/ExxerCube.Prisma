// <copyright file="IIngestionDriver.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Workflows.Abstractions;

/// <summary>
/// Thin abstraction over <c>IngestionOrchestrator.IngestCaseAsync</c> that allows
/// <c>IngestionWorkflow</c> to be tested without referencing the Orion worker assembly directly.
/// </summary>
/// <remarks>
/// The concrete production adapter (<c>OrchestratorIngestionDriver</c>) is registered by
/// <c>AddQaHarness()</c>. When no concrete adapter is registered, the workflow returns
/// <see cref="ExxerCube.Prisma.QaHarness.Workflows.WorkflowStatus.Aborted"/> with reason
/// <c>"IIngestionDriver not registered in DI container"</c>.
/// </remarks>
public interface IIngestionDriver
{
    /// <summary>
    /// Discovers and ingests the next available SIARA case.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A lightweight summary of the ingestion attempt.</returns>
    Task<IngestionAttemptSummary> IngestNextCaseAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Lightweight summary of a single ingestion attempt returned by <see cref="IIngestionDriver"/>.
/// </summary>
/// <param name="Status">Whether the ingestion completed, was skipped (no case found), or failed.</param>
/// <param name="CaseId">The identifier of the ingested case, or <see langword="null"/> when no case was found.</param>
public sealed record IngestionAttemptSummary(IngestionAttemptStatus Status, string? CaseId);

/// <summary>
/// Terminal states for a single ingestion attempt.
/// </summary>
public enum IngestionAttemptStatus
{
    /// <summary>A case was found and successfully ingested.</summary>
    Completed,

    /// <summary>No cases were available in the simulator.</summary>
    NoCaseAvailable,

    /// <summary>The attempt failed due to a downstream error.</summary>
    Failed,
}
