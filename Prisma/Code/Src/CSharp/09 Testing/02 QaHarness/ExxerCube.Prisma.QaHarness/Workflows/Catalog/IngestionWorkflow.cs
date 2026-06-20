// <copyright file="IngestionWorkflow.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.IO;
using ExxerCube.Prisma.QaHarness.Evidence;
using ExxerCube.Prisma.QaHarness.Provisioning;
using ExxerCube.Prisma.QaHarness.Workflows.Attributes;

namespace ExxerCube.Prisma.QaHarness.Workflows.Catalog;

/// <summary>
/// Exercises the SIARA ingestion path: discovers a companion case via the SIARA simulator
/// and invokes <c>IngestionOrchestrator.IngestCaseAsync</c> on the real pipeline.
/// </summary>
/// <remarks>
/// <para><b>RequiredCapabilities:</b> <c>Playwright</c>, <c>Docker</c>, <c>SiaraSimulator</c>, <c>Corpus</c>.</para>
/// <para>
/// <b>Corpus guard:</b> when the SIARA simulator corpus directory is absent
/// (<see cref="CorpusStatus.AbsentNoGenerator"/> or <see cref="CorpusStatus.AbsentGeneratorFailed"/>),
/// the workflow returns <see cref="WorkflowStatus.Aborted"/> with
/// <c>AbortReason = "CorpusAbsent"</c> per Architecture §13 R2.
/// </para>
/// <para>
/// <b>Honest-stub note:</b> this workflow is a structured, honest scaffold.
/// It resolves <see cref="EnvironmentProvisioningResult"/> from DI to check corpus status,
/// and harvests any <c>*.siro.xml</c> files from <see cref="WorkflowContext.SharedStoragePath"/>
/// after the ingest call.  The call to <c>IngestionOrchestrator.IngestCaseAsync</c> is
/// delegated to an <see cref="IIngestionDriver"/> resolved from DI — Chunk 5 wires the real
/// implementation; when absent the workflow returns <see cref="WorkflowStatus.Aborted"/> with
/// reason <c>"IIngestionDriver not registered"</c>.
/// </para>
/// </remarks>
[TracesRequirement("FR1")]
[TracesRequirement("FR2")]
[TracesRequirement("FR17")]
[TracesFeature("F-INGESTION")]
[TracesInvariant("INV-AUDIT-01")]
public sealed class IngestionWorkflow : IWorkflow
{
    /// <inheritdoc/>
    public string Name => "IngestionWorkflow";

    /// <inheritdoc/>
    public string Description => "Discovers a SIARA case via the simulator and ingests it through IngestionOrchestrator; harvests generated SIRO XML evidence.";

    /// <inheritdoc/>
    public IReadOnlyList<string> Tags => ["ingestion", "pipeline", "siara"];

    /// <inheritdoc/>
    public IReadOnlyList<string> RequiredCapabilities => ["Playwright", "Docker", "SiaraSimulator", "Corpus"];

    /// <inheritdoc/>
    public async Task<WorkflowResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var startedAt = DateTimeOffset.UtcNow;

        if (cancellationToken.IsCancellationRequested)
            return Aborted(startedAt, "IngestionWorkflow cancelled before execution.");

        // ── Corpus guard (Architecture §13 R2) ──────────────────────────────
        var provisioning = context.Services.GetService<EnvironmentProvisioningResult>();
        if (provisioning is not null)
        {
            var corpusStatus = provisioning.CorpusStatus;
            if (corpusStatus is CorpusStatus.AbsentNoGenerator or CorpusStatus.AbsentGeneratorFailed)
                return Aborted(startedAt, "CorpusAbsent");
        }

        // ── Resolve ingestion driver ─────────────────────────────────────────
        var driver = context.Services.GetService<IIngestionDriver>();
        if (driver is null)
            return Aborted(startedAt, "IIngestionDriver not registered in DI container.");

        var outputs = new Dictionary<string, object?>();

        try
        {
            // ── Run ingestion ────────────────────────────────────────────────
            var ingestResult = await driver.IngestNextCaseAsync(cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
                return Aborted(startedAt, "Cancelled mid-execution.");

            outputs["IngestResult"] = ingestResult.Status;
            outputs["CaseId"] = ingestResult.CaseId;

            // ── Harvest evidence from shared storage ─────────────────────────
            if (!string.IsNullOrWhiteSpace(context.SharedStoragePath) &&
                Directory.Exists(context.SharedStoragePath))
            {
                _ = await context.Evidence.HarvestFilesAsync(
                    context.SharedStoragePath, "*.siro.xml",
                    "ingestion-siro-xml", cancellationToken).ConfigureAwait(false);

                _ = await context.Evidence.HarvestFilesAsync(
                    context.SharedStoragePath, "*.fusion.json",
                    "ingestion-fusion-json", cancellationToken).ConfigureAwait(false);
            }

            // ── Log snapshot ─────────────────────────────────────────────────
            _ = await context.Evidence.CaptureLogSnapshotAsync(
                "ingestion-log", cancellationToken).ConfigureAwait(false);

            return new WorkflowResult(
                WorkflowName: Name,
                StartedAt: startedAt,
                FinishedAt: DateTimeOffset.UtcNow,
                Status: WorkflowStatus.Completed,
                AbortReason: null,
                Evidence: context.Evidence.CurrentPackage,
                Outputs: outputs);
        }
        catch (OperationCanceledException)
        {
            return Aborted(startedAt, "IngestionWorkflow was cancelled during execution.");
        }
        catch (Exception ex)
        {
            return Aborted(startedAt,
                $"IngestionWorkflow failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private WorkflowResult Aborted(DateTimeOffset startedAt, string reason) =>
        new(
            WorkflowName: Name,
            StartedAt: startedAt,
            FinishedAt: DateTimeOffset.UtcNow,
            Status: WorkflowStatus.Aborted,
            AbortReason: reason,
            Evidence: new EvidencePackage(Name, []),
            Outputs: new Dictionary<string, object?>());
}

/// <summary>
/// Thin abstraction over <c>IngestionOrchestrator.IngestCaseAsync</c> so the
/// workflow can be tested without referencing the worker assembly directly.
/// The concrete adapter is registered in Chunk 5.
/// </summary>
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
