// <copyright file="ConfigWorkflow.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.Net.Http;
using ExxerCube.Prisma.QaHarness.Evidence;
using ExxerCube.Prisma.QaHarness.Workflows.Attributes;
using Microsoft.Extensions.Configuration;

namespace ExxerCube.Prisma.QaHarness.Workflows.Catalog;

/// <summary>
/// Reads observable configuration values from the running Prisma application and
/// records them in <see cref="WorkflowResult.Outputs"/> and evidence.
/// </summary>
/// <remarks>
/// <para><b>RequiredCapabilities:</b> <c>Http</c>.</para>
/// <para>
/// This workflow probes the <c>/health/ready</c> endpoint and also resolves
/// <see cref="IConfiguration"/> from the DI container if available, extracting
/// key settings (connection string presence, logging level, feature flags).
/// It never writes configuration — read-only observation only.
/// </para>
/// <para>
/// <b>Honest-stub note:</b> when <see cref="HttpClient"/> is absent the workflow
/// returns <see cref="WorkflowStatus.Aborted"/>.  Configuration extraction from
/// <see cref="IConfiguration"/> is opportunistic — when it is absent, only the
/// HTTP observation path runs.  Full in-process DI binding lands in Chunk 5.
/// </para>
/// </remarks>
[TracesRequirement("NFR11")]
[TracesFeature("F-CONFIG")]
public sealed class ConfigWorkflow : IWorkflow
{
    private const string ReadyPath = "/health/ready";

    // Configuration keys to surface as evidence — non-sensitive names only
    private static readonly IReadOnlyList<string> ObservedConfigKeys =
    [
        "Logging:LogLevel:Default",
        "Logging:LogLevel:Microsoft.AspNetCore",
        "AllowedHosts",
        "SharedStoragePath",
        "Kestrel:Endpoints:Http:Url",
    ];

    /// <inheritdoc/>
    public string Name => "ConfigWorkflow";

    /// <inheritdoc/>
    public string Description => "Reads observable configuration values from the running application via HTTP and IConfiguration, recording settings as evidence.";

    /// <inheritdoc/>
    public IReadOnlyList<string> Tags => ["config", "operational", "http"];

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
            return Aborted(startedAt, "ConfigWorkflow cancelled before execution.");

        var httpClient = context.Services.GetService<HttpClient>();
        if (httpClient is null)
            return Aborted(startedAt, "HttpClient not registered in DI container.");

        var outputs = new Dictionary<string, object?>();

        try
        {
            // ── Verify application is alive via /health/ready ────────────────
            using var response = await httpClient.GetAsync(
                ReadyPath, cancellationToken).ConfigureAwait(false);

            outputs["HealthReadyStatus"] = (int)response.StatusCode;
            outputs["ApplicationReachable"] = response.IsSuccessStatusCode;

            cancellationToken.ThrowIfCancellationRequested();

            // ── Extract observable config values from IConfiguration (optional) ──
            var configuration = context.Services.GetService<IConfiguration>();
            if (configuration is not null)
            {
                foreach (var key in ObservedConfigKeys)
                {
                    var value = configuration[key];
                    // Only surface keys that are present and non-sensitive
                    if (value is not null)
                        outputs[$"Config:{key}"] = value;
                }

                outputs["ConfigProviderAvailable"] = true;
            }
            else
            {
                outputs["ConfigProviderAvailable"] = false;
            }

            // ── Log snapshot ─────────────────────────────────────────────────
            _ = await context.Evidence.CaptureLogSnapshotAsync(
                "config-observation-log", cancellationToken).ConfigureAwait(false);

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
            return Aborted(startedAt, "ConfigWorkflow was cancelled during execution.");
        }
        catch (Exception ex)
        {
            return Aborted(startedAt,
                $"ConfigWorkflow failed: {ex.GetType().Name}: {ex.Message}");
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
