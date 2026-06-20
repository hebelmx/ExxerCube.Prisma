// <copyright file="ManualReviewWorkflow.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using ExxerCube.Prisma.QaHarness.Evidence;
using ExxerCube.Prisma.QaHarness.Workflows.Attributes;
using Microsoft.Playwright;

namespace ExxerCube.Prisma.QaHarness.Workflows.Catalog;

/// <summary>
/// Navigates the Prisma Web UI to the manual review queue, captures its state as
/// evidence, and records the number of items awaiting review.
/// </summary>
/// <remarks>
/// <para><b>RequiredCapabilities:</b> <c>Playwright</c>.</para>
/// <para>
/// When <see cref="WorkflowContext.PlaywrightPage"/> is <see langword="null"/> the
/// workflow returns <see cref="WorkflowStatus.Aborted"/> immediately.
/// </para>
/// <para>
/// <b>Honest-stub note:</b> this workflow does real Playwright navigation when
/// a page is supplied.  It navigates to the review queue route (<c>/review</c>),
/// captures a screenshot, and counts queue rows via a CSS selector.  It does NOT
/// simulate a reviewer disposition — recording actual review actions requires a
/// human decision and is outside the harness's scope.
/// Full credential/storage-state wiring lands in Chunk 5.
/// </para>
/// </remarks>
[TracesRequirement("FR14")]
[TracesFeature("F-MANUAL-REVIEW")]
public sealed class ManualReviewWorkflow : IWorkflow
{
    private const string ReviewQueuePath = "/review";
    private const string QueueRowSelector = "table tbody tr, .mud-table-row";

    /// <inheritdoc/>
    public string Name => "ManualReviewWorkflow";

    /// <inheritdoc/>
    public string Description => "Navigates to the manual review queue, captures its current state as a screenshot, and records the pending item count.";

    /// <inheritdoc/>
    public IReadOnlyList<string> Tags => ["browser", "review", "ui"];

    /// <inheritdoc/>
    public IReadOnlyList<string> RequiredCapabilities => ["Playwright"];

    /// <inheritdoc/>
    public async Task<WorkflowResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var startedAt = DateTimeOffset.UtcNow;

        if (cancellationToken.IsCancellationRequested)
            return Aborted(startedAt, "ManualReviewWorkflow cancelled before execution.");

        if (context.PlaywrightPage is null)
            return Aborted(startedAt, "PlaywrightPage not available in WorkflowContext.");

        var page = context.PlaywrightPage;
        var outputs = new Dictionary<string, object?>();

        try
        {
            // ── Navigate to review queue ─────────────────────────────────────
            var currentUrl = page.Url;
            var reviewUrl = currentUrl.Contains("://", StringComparison.Ordinal)
                ? new Uri(new Uri(currentUrl), ReviewQueuePath).ToString()
                : ReviewQueuePath;

            await page.GotoAsync(reviewUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 15_000,
            }).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            // ── Capture screenshot ───────────────────────────────────────────
            _ = await context.Evidence.CaptureScreenshotAsync(
                "review-queue-loaded", cancellationToken).ConfigureAwait(false);

            // ── Count items ──────────────────────────────────────────────────
            var rows = await page.QuerySelectorAllAsync(QueueRowSelector).ConfigureAwait(false);
            var itemCount = rows.Count;

            outputs["ReviewQueueUrl"] = reviewUrl;
            outputs["PendingItemCount"] = itemCount;

            cancellationToken.ThrowIfCancellationRequested();

            // ── Log snapshot ─────────────────────────────────────────────────
            _ = await context.Evidence.CaptureLogSnapshotAsync(
                "review-queue-log", cancellationToken).ConfigureAwait(false);

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
            return Aborted(startedAt, "ManualReviewWorkflow was cancelled during execution.");
        }
        catch (Exception ex)
        {
            _ = await context.Evidence.CaptureScreenshotAsync(
                "review-queue-error", CancellationToken.None).ConfigureAwait(false);

            return Aborted(startedAt,
                $"ManualReviewWorkflow failed: {ex.GetType().Name}: {ex.Message}");
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
