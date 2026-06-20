// <copyright file="ExportWorkflow.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.IO;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.QaHarness.Evidence;
using ExxerCube.Prisma.QaHarness.Workflows.Attributes;

namespace ExxerCube.Prisma.QaHarness.Workflows.Catalog;

/// <summary>
/// Triggers an export through the three-process pipeline and awaits an
/// <see cref="ExportCompletedEvent"/> from the <see cref="IEventPublisher"/> stream.
/// Harvests the generated <c>.siro.xml</c> and <c>.datos-carga-oficio.xlsx</c> files
/// from shared storage as evidence.
/// </summary>
/// <remarks>
/// <para><b>RequiredCapabilities:</b> <c>ThreeProcessPipeline</c>.</para>
/// <para>
/// <b>Honest-stub note:</b> when <see cref="IEventPublisher"/> is registered in DI,
/// the workflow subscribes to <c>GetEventStream&lt;ExportCompletedEvent&gt;()</c>
/// with a configurable timeout (default: 60 s) to observe the event fired by the
/// real Athena processing pipeline.  When <c>IEventPublisher</c> is absent the workflow
/// returns <see cref="WorkflowStatus.Aborted"/> with reason
/// <c>"IEventPublisher not registered"</c>.
/// </para>
/// <para>
/// The export trigger itself (file drop / API call) is abstracted behind
/// <see cref="IExportTrigger"/> — when not registered, the workflow still subscribes
/// to the event stream (the caller may trigger export by other means) but records that
/// no trigger adapter was available.
/// Full DI wiring for the trigger is deferred to Chunk 5.
/// </para>
/// </remarks>
[TracesRequirement("FR15")]
[TracesRequirement("FR18")]
[TracesRequirement("FR17")]
[TracesFeature("F-EXPORT")]
[TracesInvariant("INV-AUDIT-01")]
public sealed class ExportWorkflow : IWorkflow
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    /// <inheritdoc/>
    public string Name => "ExportWorkflow";

    /// <inheritdoc/>
    public string Description => "Triggers an export through the three-process pipeline, awaits ExportCompletedEvent, and harvests SIRO XML and Excel evidence from shared storage.";

    /// <inheritdoc/>
    public IReadOnlyList<string> Tags => ["export", "pipeline", "siro"];

    /// <inheritdoc/>
    public IReadOnlyList<string> RequiredCapabilities => ["ThreeProcessPipeline"];

    /// <inheritdoc/>
    public async Task<WorkflowResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var startedAt = DateTimeOffset.UtcNow;

        if (cancellationToken.IsCancellationRequested)
            return Aborted(startedAt, "ExportWorkflow cancelled before execution.");

        // ── Resolve IEventPublisher ──────────────────────────────────────────
        var eventPublisher = context.Services.GetService<IEventPublisher>();
        if (eventPublisher is null)
            return Aborted(startedAt, "IEventPublisher not registered in DI container.");

        var outputs = new Dictionary<string, object?>();
        var tcs = new TaskCompletionSource<ExportCompletedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

        // ── Subscribe to ExportCompletedEvent ───────────────────────────────
        using var subscription = eventPublisher
            .GetEventStream<ExportCompletedEvent>()
            .Subscribe(evt => tcs.TrySetResult(evt));

        try
        {
            // ── Optionally trigger export ────────────────────────────────────
            var trigger = context.Services.GetService<IExportTrigger>();
            if (trigger is not null)
            {
                await trigger.TriggerExportAsync(cancellationToken).ConfigureAwait(false);
                outputs["TriggerUsed"] = trigger.GetType().Name;
            }
            else
            {
                outputs["TriggerUsed"] = "none — caller must trigger export externally";
            }

            // ── Await event with timeout ─────────────────────────────────────
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DefaultTimeout);

            try
            {
                var exportEvent = await tcs.Task
                    .WaitAsync(timeoutCts.Token)
                    .ConfigureAwait(false);

                outputs["ExportFileId"] = exportEvent.FileId.ToString();
                outputs["ExportDestination"] = exportEvent.Destination;
                outputs["ExportFormat"] = exportEvent.Format;
                outputs["ExportSizeBytes"] = exportEvent.ExportedSizeBytes;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout — not a cancellation from the caller
                return Aborted(startedAt,
                    $"ExportWorkflow timed out after {DefaultTimeout.TotalSeconds}s waiting for ExportCompletedEvent.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            // ── Harvest export artefacts from shared storage ─────────────────
            if (!string.IsNullOrWhiteSpace(context.SharedStoragePath) &&
                Directory.Exists(context.SharedStoragePath))
            {
                _ = await context.Evidence.HarvestFilesAsync(
                    context.SharedStoragePath, "*.siro.xml",
                    "export-siro-xml", cancellationToken).ConfigureAwait(false);

                _ = await context.Evidence.HarvestFilesAsync(
                    context.SharedStoragePath, "*.xlsx",
                    "export-excel", cancellationToken).ConfigureAwait(false);
            }

            // ── Log snapshot ─────────────────────────────────────────────────
            _ = await context.Evidence.CaptureLogSnapshotAsync(
                "export-log", cancellationToken).ConfigureAwait(false);

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
            return Aborted(startedAt, "ExportWorkflow was cancelled during execution.");
        }
        catch (Exception ex)
        {
            return Aborted(startedAt,
                $"ExportWorkflow failed: {ex.GetType().Name}: {ex.Message}");
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
/// Abstraction over the mechanism that starts an export run.
/// Implemented and registered in Chunk 5; optional — when absent the
/// <see cref="ExportWorkflow"/> still awaits the event stream.
/// </summary>
public interface IExportTrigger
{
    /// <summary>
    /// Initiates an export operation in the pipeline.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the trigger call.</param>
    Task TriggerExportAsync(CancellationToken cancellationToken = default);
}
