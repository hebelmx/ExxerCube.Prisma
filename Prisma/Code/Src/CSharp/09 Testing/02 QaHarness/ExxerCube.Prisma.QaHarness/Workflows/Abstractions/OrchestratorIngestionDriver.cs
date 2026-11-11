// <copyright file="OrchestratorIngestionDriver.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using ExxerCube.Prisma.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Prisma.Orion.Ingestion;

namespace ExxerCube.Prisma.QaHarness.Workflows.Abstractions;

/// <summary>
/// Production adapter for <see cref="IIngestionDriver"/> that resolves
/// <see cref="ISiaraDocumentSource"/> and <see cref="IngestionOrchestrator"/> from a scoped
/// DI service provider and performs a single discover-then-ingest cycle.
/// </summary>
/// <remarks>
/// <para>
/// <b>Honest failure contract:</b> if either <see cref="ISiaraDocumentSource"/> or
/// <see cref="IngestionOrchestrator"/> cannot be resolved from the supplied
/// <see cref="IServiceProvider"/>, or if the SIARA simulator returns no cases, the method
/// returns the appropriate failure status — it never fabricates success.
/// </para>
/// <para>
/// Registered as <see cref="IIngestionDriver"/> by <c>AddQaHarness()</c>.
/// </para>
/// </remarks>
public sealed class OrchestratorIngestionDriver : IIngestionDriver
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrchestratorIngestionDriver> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="OrchestratorIngestionDriver"/>.
    /// </summary>
    /// <param name="serviceProvider">The DI service provider from which services are resolved.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public OrchestratorIngestionDriver(
        IServiceProvider serviceProvider,
        ILogger<OrchestratorIngestionDriver> logger)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IngestionAttemptSummary> IngestNextCaseAsync(
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new IngestionAttemptSummary(IngestionAttemptStatus.Failed, null);
        }

        // ── Resolve discovery source ──────────────────────────────────────────
        var documentSource = _serviceProvider.GetService<ISiaraDocumentSource>();
        if (documentSource is null)
        {
            _logger.LogWarning(
                "OrchestratorIngestionDriver: ISiaraDocumentSource is not registered in DI. " +
                "Cannot discover cases. Returning Failed — no fabricated success.");
            return new IngestionAttemptSummary(IngestionAttemptStatus.Failed, null);
        }

        // ── Resolve orchestrator ──────────────────────────────────────────────
        var orchestrator = _serviceProvider.GetService<IngestionOrchestrator>();
        if (orchestrator is null)
        {
            _logger.LogWarning(
                "OrchestratorIngestionDriver: IngestionOrchestrator is not registered in DI. " +
                "Returning Failed — no fabricated success.");
            return new IngestionAttemptSummary(IngestionAttemptStatus.Failed, null);
        }

        try
        {
            // ── Discover available cases ──────────────────────────────────────
            _logger.LogInformation("OrchestratorIngestionDriver: discovering SIARA cases.");
            var discoveryResult = await documentSource
                .DiscoverCasesAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!discoveryResult.IsSuccessNotNull || discoveryResult.Value!.Count == 0)
            {
                _logger.LogInformation("OrchestratorIngestionDriver: no cases available in SIARA simulator.");
                return new IngestionAttemptSummary(IngestionAttemptStatus.NoCaseAvailable, null);
            }

            var siaraCase = discoveryResult.Value[0];
            var correlationId = Guid.NewGuid();

            _logger.LogInformation(
                "OrchestratorIngestionDriver: ingesting case {CaseId} (correlationId={CorrelationId}).",
                siaraCase.CaseId, correlationId);

            // ── Run ingestion ─────────────────────────────────────────────────
            var result = await orchestrator
                .IngestCaseAsync(siaraCase, correlationId, cancellationToken)
                .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                var errors = result.Errors?.ToList() ?? [];
                var reason = errors.Count > 0
                    ? string.Join("; ", errors)
                    : "IngestCaseAsync returned a failed result.";
                _logger.LogWarning("OrchestratorIngestionDriver: IngestCaseAsync failed — {Reason}.", reason);
                return new IngestionAttemptSummary(IngestionAttemptStatus.Failed, siaraCase.CaseId);
            }

            _logger.LogInformation(
                "OrchestratorIngestionDriver: ingestion succeeded for case {CaseId}.", siaraCase.CaseId);

            return new IngestionAttemptSummary(IngestionAttemptStatus.Completed, siaraCase.CaseId);
        }
        catch (OperationCanceledException)
        {
            return new IngestionAttemptSummary(IngestionAttemptStatus.Failed, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OrchestratorIngestionDriver: unhandled exception.");
            return new IngestionAttemptSummary(IngestionAttemptStatus.Failed, null);
        }
    }
}
