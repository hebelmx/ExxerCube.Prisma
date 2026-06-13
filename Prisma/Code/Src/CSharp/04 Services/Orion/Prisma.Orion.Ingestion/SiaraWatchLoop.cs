using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Interfaces;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Prisma.Orion.Ingestion;

/// <summary>
/// The SIARA watch loop (MVP-PATH 1.2): the headless poll/watcher that drives ingestion. Each cycle it
/// discovers the documents SIARA presents and, <strong>per discovered document, creates a fresh DI scope</strong>
/// and ingests it through the <see cref="IngestionOrchestrator"/> (download → hash → idempotent journal →
/// event). It then waits a relaxed interval and repeats.
/// </summary>
/// <remarks>
/// <para>
/// This is a <strong>singleton</strong> that owns the scopes (it is driven by the singleton worker), which
/// is why it takes <see cref="IServiceScopeFactory"/> rather than the scoped orchestrator/downloader
/// directly — that would be a captive dependency. It keeps <em>one</em> long-lived discovery scope so the
/// SIARA session stays warm across cycles (the <see cref="ISiaraDocumentSource"/> re-validates it via
/// <see cref="ISiaraSessionProvider.EnsureValidAsync"/>), and creates a <em>fresh</em> scope per document
/// so each pull rides its own browser/session with no state bleed.
/// </para>
/// <para>
/// The loop is resilient: a failed discovery pass or a single failed document is logged and the loop
/// continues on the next cycle — it never throws out of <see cref="RunAsync"/>, so a transient SIARA outage
/// never takes the worker host down. Idempotency comes from the SHA-256 journal, so re-discovering the same
/// documents is a cheap no-op.
/// </para>
/// </remarks>
public sealed class SiaraWatchLoop : IReadinessProbe
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WatchLoopOptions _options;
    private readonly ILogger<SiaraWatchLoop> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Gets a value indicating whether the watch loop is actively polling SIARA. Becomes <see langword="true"/>
    /// once the discovery source is acquired and the poll loop is entered, and returns to <see langword="false"/>
    /// when the loop stops (cancellation) or fails to start. Drives the Orion worker's readiness probe
    /// (MVP-PATH 4.2 / E1) — a truthful signal that ingestion is actually running, not just constructed.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <inheritdoc />
    bool IReadinessProbe.IsReady => IsRunning;

    /// <summary>Initializes a new instance of the <see cref="SiaraWatchLoop"/> class.</summary>
    /// <param name="scopeFactory">The DI scope factory used to create the discovery scope and a scope per document.</param>
    /// <param name="options">The watch-loop options (poll cadence).</param>
    /// <param name="logger">The logger.</param>
    /// <param name="timeProvider">The time provider used for the (testable) inter-cycle delay.</param>
    public SiaraWatchLoop(
        IServiceScopeFactory scopeFactory,
        IOptions<WatchLoopOptions> options,
        ILogger<SiaraWatchLoop> logger,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Runs the watch loop until the token is cancelled. Never throws for business outcomes — discovery and
    /// per-document failures are logged and retried on the next cycle.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>A task that completes when the loop stops (on cancellation).</returns>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "SIARA watch loop starting; polling every {PollInterval}",
            _options.PollInterval);

        // One long-lived discovery scope keeps the SIARA session warm across cycles (the document source
        // re-validates it each pass). Created once for the loop's lifetime.
        using var discoveryScope = _scopeFactory.CreateScope();

        ISiaraDocumentSource source;
        try
        {
            source = discoveryScope.ServiceProvider.GetRequiredService<ISiaraDocumentSource>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SIARA watch loop cannot start: the document discovery source is not available");
            return;
        }

        // The loop is now genuinely running: discovery source acquired, about to poll. Readiness reflects this.
        IsRunning = true;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await PollOnceAsync(source, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SIARA watch-loop cycle failed; retrying after the poll interval");
                }

                try
                {
                    await Task.Delay(_options.PollInterval, _timeProvider, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            IsRunning = false;
        }

        _logger.LogInformation("SIARA watch loop stopped");
    }

    private async Task PollOnceAsync(ISiaraDocumentSource source, CancellationToken cancellationToken)
    {
        var discovered = await source.DiscoverDocumentIdsAsync(cancellationToken).ConfigureAwait(false);
        if (discovered.IsCancelled())
        {
            return;
        }

        if (discovered.IsFailure || discovered.Value is null)
        {
            _logger.LogWarning("SIARA discovery failed: {Errors}", string.Join(", ", discovered.Errors));
            return;
        }

        var documentIds = discovered.Value;
        _logger.LogInformation("SIARA discovery found {Count} document(s) this cycle", documentIds.Count);

        var ingested = 0;
        var duplicates = 0;
        var failures = 0;

        foreach (var documentId in documentIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var result = await IngestOneAsync(documentId, cancellationToken).ConfigureAwait(false);
            if (result.IsCancelled())
            {
                break;
            }

            if (result.IsSuccess && result.Value is not null)
            {
                if (result.Value.WasDuplicate)
                {
                    duplicates++;
                }
                else
                {
                    ingested++;
                }
            }
            else
            {
                failures++;
                _logger.LogWarning(
                    "Failed to ingest SIARA document {DocumentId}: {Errors}",
                    documentId,
                    string.Join(", ", result.Errors));
            }
        }

        _logger.LogInformation(
            "SIARA poll complete: {Ingested} ingested, {Duplicates} duplicate(s) skipped, {Failures} failed",
            ingested,
            duplicates,
            failures);
    }

    /// <summary>
    /// Ingests a single discovered document in its own DI scope, so each pull rides a fresh SIARA
    /// downloader/browser/session with no state bleed between documents.
    /// </summary>
    private async Task<Result<IngestionResult>> IngestOneAsync(string documentId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IngestionOrchestrator>();
        return await orchestrator
            .IngestDocumentAsync(documentId, Guid.NewGuid(), cancellationToken)
            .ConfigureAwait(false);
    }
}
