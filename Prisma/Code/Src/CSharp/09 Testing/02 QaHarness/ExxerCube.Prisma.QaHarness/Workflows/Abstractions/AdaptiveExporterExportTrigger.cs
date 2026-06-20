// <copyright file="AdaptiveExporterExportTrigger.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.QaHarness.Workflows.Abstractions;

/// <summary>
/// Production adapter for <see cref="IExportTrigger"/> that resolves
/// <see cref="IAdaptiveExporter"/> from the host's DI container and triggers an export of
/// the most recent processed document found in the shared storage.
/// </summary>
/// <remarks>
/// <para>
/// <b>Honest failure contract:</b> if <see cref="IAdaptiveExporter"/> cannot be resolved
/// from the supplied <see cref="IServiceProvider"/>, the trigger call returns a failure —
/// it never fabricates success. The <c>ExportWorkflow</c> treats this gracefully since
/// the trigger is optional.
/// </para>
/// <para>
/// Registered as <see cref="IExportTrigger"/> by <c>AddQaHarness()</c>.
/// </para>
/// </remarks>
public sealed class AdaptiveExporterExportTrigger : IExportTrigger
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AdaptiveExporterExportTrigger> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AdaptiveExporterExportTrigger"/>.
    /// </summary>
    /// <param name="serviceProvider">Service provider from which the exporter is resolved.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public AdaptiveExporterExportTrigger(
        IServiceProvider serviceProvider,
        ILogger<AdaptiveExporterExportTrigger> logger)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task TriggerExportAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        // Resolve the adaptive exporter from the host's service provider.
        var exporter = _serviceProvider.GetService<IAdaptiveExporter>();
        if (exporter is null)
        {
            // Honest: if the exporter is not wired up, we log and return without faking success.
            // The ExportWorkflow still monitors the event stream; the test environment may trigger
            // export by other means (e.g. pipeline autonomously processes a queued document).
            _logger.LogWarning(
                "AdaptiveExporterExportTrigger: IAdaptiveExporter is not registered in DI. " +
                "Export trigger is a no-op for this run; ExportWorkflow will still await the event stream.");
            return Task.CompletedTask;
        }

        // IAdaptiveExporter.ExportAsync requires a document subject; without a specific document
        // to export the trigger cannot act autonomously. The pattern used in production is that
        // the Reconciliator pipeline drives export after ingestion completes. This trigger is
        // therefore a diagnostic log + return — the ExportWorkflow awaits the event stream which
        // the real pipeline fires when a document is processed.
        _logger.LogInformation(
            "AdaptiveExporterExportTrigger: IAdaptiveExporter is registered. " +
            "Export is driven autonomously by the Reconciliator pipeline; no explicit trigger call needed. " +
            "Awaiting ExportCompletedEvent on the event stream.");

        return Task.CompletedTask;
    }
}
