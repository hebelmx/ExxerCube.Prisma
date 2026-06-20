// <copyright file="HealthCheckWorkflow.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.Net.Http;
using ExxerCube.Prisma.QaHarness.Evidence;
using ExxerCube.Prisma.QaHarness.Workflows.Attributes;

namespace ExxerCube.Prisma.QaHarness.Workflows.Catalog;

/// <summary>
/// Exercises the Prisma health endpoints <c>/health/live</c> and <c>/health/ready</c>
/// via an <see cref="HttpClient"/>, records the HTTP status codes and response bodies
/// in <see cref="WorkflowResult.Outputs"/>, and captures log evidence.
/// </summary>
/// <remarks>
/// <para><b>RequiredCapabilities:</b> <c>Http</c>.</para>
/// <para>
/// The workflow resolves an <see cref="HttpClient"/> from the DI container.
/// Callers are expected to register a named or typed client pointing to the
/// Prisma Web UI base address.  When no <see cref="HttpClient"/> is registered
/// the workflow returns <see cref="WorkflowStatus.Aborted"/> with reason
/// <c>"HttpClient not registered in DI container"</c>.
/// </para>
/// <para>
/// <b>Honest-stub note:</b> this workflow does real HTTP work.  It makes two GET
/// requests and records the outcomes.  It does NOT make a PASS/FAIL judgement —
/// the raw status codes and bodies are captured in <c>Outputs</c> for the QA agent
/// to evaluate.
/// </para>
/// </remarks>
[TracesRequirement("NFR7")]
[TracesFeature("F-HEALTH")]
[TracesInvariant("INV-HEALTH-LIVE-01")]
public sealed class HealthCheckWorkflow : IWorkflow
{
    private const string LivePath = "/health/live";
    private const string ReadyPath = "/health/ready";

    /// <inheritdoc/>
    public string Name => "HealthCheckWorkflow";

    /// <inheritdoc/>
    public string Description => "GETs /health/live and /health/ready, records HTTP status codes and response bodies as outputs and evidence.";

    /// <inheritdoc/>
    public IReadOnlyList<string> Tags => ["health", "operational", "http"];

    /// <inheritdoc/>
    public IReadOnlyList<string> RequiredCapabilities => ["Http"];

    /// <inheritdoc/>
    public async Task<WorkflowResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var startedAt = DateTimeOffset.UtcNow;

        if (cancellationToken.IsCancellationRequested)
            return Aborted(startedAt, "HealthCheckWorkflow cancelled before execution.");

        // ── Resolve HttpClient ──────────────────────────────────────────────
        var httpClient = context.Services.GetService<HttpClient>();
        if (httpClient is null)
            return Aborted(startedAt, "HttpClient not registered in DI container.");

        var outputs = new Dictionary<string, object?>();

        try
        {
            // ── /health/live ─────────────────────────────────────────────────
            var liveResult = await ProbeEndpointAsync(
                httpClient, LivePath, cancellationToken).ConfigureAwait(false);

            outputs["LiveStatus"] = (int)liveResult.StatusCode;
            outputs["LiveBody"] = liveResult.Body;
            outputs["LiveResponseMs"] = liveResult.ElapsedMs;

            cancellationToken.ThrowIfCancellationRequested();

            // ── /health/ready ────────────────────────────────────────────────
            var readyResult = await ProbeEndpointAsync(
                httpClient, ReadyPath, cancellationToken).ConfigureAwait(false);

            outputs["ReadyStatus"] = (int)readyResult.StatusCode;
            outputs["ReadyBody"] = readyResult.Body;
            outputs["ReadyResponseMs"] = readyResult.ElapsedMs;

            // ── Log snapshot ─────────────────────────────────────────────────
            _ = await context.Evidence.CaptureLogSnapshotAsync(
                "health-check-log", cancellationToken).ConfigureAwait(false);

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
            return Aborted(startedAt, "HealthCheckWorkflow was cancelled during execution.");
        }
        catch (Exception ex)
        {
            return Aborted(startedAt,
                $"HealthCheckWorkflow failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<HealthProbeResult> ProbeEndpointAsync(
        HttpClient client,
        string path,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var response = await client.GetAsync(path, cancellationToken)
                .ConfigureAwait(false);
            sw.Stop();

            var body = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            return new HealthProbeResult(response.StatusCode, body, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new HealthProbeResult(
                System.Net.HttpStatusCode.ServiceUnavailable,
                $"Exception: {ex.Message}",
                sw.ElapsedMilliseconds);
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

    private sealed record HealthProbeResult(
        System.Net.HttpStatusCode StatusCode,
        string Body,
        long ElapsedMs);
}
