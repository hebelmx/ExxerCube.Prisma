using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Services.Manifest;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Prisma.Orion.Ingestion;

/// <summary>
/// The SIARA watch loop (MVP-PATH 1.2 / 2.1): the headless poll/watcher that drives ingestion. Each cycle it
/// discovers the cases SIARA presents and, <strong>per discovered case, creates a fresh DI scope</strong>
/// and ingests it through the <see cref="IngestionOrchestrator"/> (download all companion files → SHA-256
/// idempotency → store → ONE broadcast event per case). It then waits a relaxed interval and repeats.
/// </summary>
/// <remarks>
/// <para>
/// This is a <strong>singleton</strong> that owns the scopes (it is driven by the singleton worker), which
/// is why it takes <see cref="IServiceScopeFactory"/> rather than the scoped orchestrator/downloader
/// directly — that would be a captive dependency. It keeps <em>one</em> long-lived discovery scope so the
/// SIARA session stays warm across cycles (the <see cref="ISiaraDocumentSource"/> re-validates it via
/// <see cref="ISiaraSessionProvider.EnsureValidAsync"/>), and creates a <em>fresh</em> scope per case
/// so each pull rides its own browser/session with no state bleed.
/// </para>
/// <para>
/// The loop is resilient: a failed discovery pass or a single failed case is logged and the loop continues
/// on the next cycle — it never throws out of <see cref="RunAsync"/>, so a transient SIARA outage never
/// takes the worker host down. Idempotency comes from the SHA-256 journal, so re-discovering the same cases
/// is a cheap no-op.
/// </para>
/// <para>
/// When <see cref="ExpectedManifestOptions.Enabled"/> is <see langword="true"/>, each cycle also
/// reconciles the discovered cases against the operator-supplied expected manifest (Item B #8 + F #12):
/// the report is logged at INFO with the structured key <c>PerCycleReconciliationReport</c> and persisted
/// as a JSON artifact under <c>reports/cycle-{utcStamp}.json</c> via <see cref="IStoragePathResolver"/>.
/// When disabled (default), behavior is identical to before.
/// </para>
/// </remarks>
public sealed class SiaraWatchLoop : IReadinessProbe
{
    private static readonly JsonSerializerOptions ReportJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WatchLoopOptions _options;
    private readonly ExpectedManifestOptions _manifestOptions;
    private readonly ILogger<SiaraWatchLoop> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IExpectedManifestProvider? _manifestProvider;
    private readonly IManifestReconciler? _reconciler;
    private readonly IStoragePathResolver? _storagePathResolver;

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
    /// <param name="scopeFactory">The DI scope factory used to create the discovery scope and a scope per case.</param>
    /// <param name="options">The watch-loop options (poll cadence).</param>
    /// <param name="logger">The logger.</param>
    /// <param name="timeProvider">The time provider used for the (testable) inter-cycle delay.</param>
    /// <param name="manifestOptions">
    /// Optional expected-manifest options. When <see langword="null"/> or disabled, reconciliation is skipped.
    /// </param>
    /// <param name="manifestProvider">
    /// Optional provider for loading the expected manifest. Required when reconciliation is enabled.
    /// </param>
    /// <param name="reconciler">
    /// Optional reconciler. Required when reconciliation is enabled.
    /// </param>
    /// <param name="storagePathResolver">
    /// Optional resolver used to persist the cycle report as JSON. When <see langword="null"/> or
    /// resolution fails, the persist step is skipped gracefully (log + continue).
    /// </param>
    public SiaraWatchLoop(
        IServiceScopeFactory scopeFactory,
        IOptions<WatchLoopOptions> options,
        ILogger<SiaraWatchLoop> logger,
        TimeProvider timeProvider,
        IOptions<ExpectedManifestOptions>? manifestOptions = null,
        IExpectedManifestProvider? manifestProvider = null,
        IManifestReconciler? reconciler = null,
        IStoragePathResolver? storagePathResolver = null)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _scopeFactory = scopeFactory;
        _options = options.Value;
        _manifestOptions = manifestOptions?.Value ?? new ExpectedManifestOptions();
        _logger = logger;
        _timeProvider = timeProvider;
        _manifestProvider = manifestProvider;
        _reconciler = reconciler;
        _storagePathResolver = storagePathResolver;
    }

    /// <summary>
    /// Runs the watch loop until the token is cancelled. Never throws for business outcomes — discovery and
    /// per-case failures are logged and retried on the next cycle.
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
        var discovered = await source.DiscoverCasesAsync(cancellationToken).ConfigureAwait(false);
        if (discovered.IsCancelled())
        {
            return;
        }

        if (discovered.IsFailure || discovered.Value is null)
        {
            _logger.LogWarning("SIARA case discovery failed: {Errors}", string.Join(", ", discovered.Errors));
            return;
        }

        var cases = discovered.Value;
        _logger.LogInformation("SIARA discovery found {Count} case(s) this cycle", cases.Count);

        var ingested = 0;
        var duplicates = 0;
        var failures = 0;

        // Accumulate per-case info for reconciliation (only used when reconciliation is enabled;
        // built regardless to avoid branching in the hot case loop — it's just list-appending).
        var discoveredOficios = new List<DiscoveredOficio>(cases.Count);

        foreach (var siaraCase in cases)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var result = await IngestOneAsync(siaraCase, cancellationToken).ConfigureAwait(false);
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

                // Build the DiscoveredOficio from the SiaraCase's declared files + the isComplete
                // flag surfaced on IngestionResult (Item B #8 / F #12).
                var downloadedFiles = siaraCase.Files
                    .Select(f => new DownloadedFileEntry(
                        FileName: f.FileName,
                        Extension: Path.GetExtension(f.FileName).TrimStart('.'),
                        Format: f.Format))
                    .ToList();

                discoveredOficios.Add(new DiscoveredOficio(
                    CaseId: siaraCase.CaseId,
                    DownloadedFiles: downloadedFiles,
                    IsComplete: result.Value.IsComplete));
            }
            else
            {
                failures++;
                _logger.LogWarning(
                    "Failed to ingest SIARA case {CaseId}: {Errors}",
                    siaraCase.CaseId,
                    string.Join(", ", result.Errors));
            }
        }

        _logger.LogInformation(
            "SIARA poll complete: {Ingested} ingested, {Duplicates} duplicate(s) skipped, {Failures} failed",
            ingested,
            duplicates,
            failures);

        // ── Per-cycle reconciliation (Item B #8 + F #12) ─────────────────────────────────────────
        // Only runs when opted-in via ExpectedManifest:Enabled=true. When disabled, all the code
        // above is identical to the pre-feature behavior.
        if (_manifestOptions.Enabled)
        {
            await RunReconciliationAsync(
                discoveredOficios,
                ingested, duplicates, failures,
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Loads the expected manifest, runs the reconciler, logs the report, and persists it.
    /// Fully fault-tolerant: any failure is logged at Warning and swallowed — the loop is never crashed.
    /// </summary>
    private async Task RunReconciliationAsync(
        IReadOnlyList<DiscoveredOficio> discoveredOficios,
        int ingested,
        int duplicates,
        int failures,
        CancellationToken cancellationToken)
    {
        if (_manifestProvider is null || _reconciler is null)
        {
            _logger.LogWarning(
                "Manifest reconciliation is enabled but IExpectedManifestProvider or IManifestReconciler is not registered. " +
                "Register both in the DI container to activate reconciliation.");
            return;
        }

        // Load the expected manifest (fault-tolerant — missing/bad file → skip with warning).
        var manifestResult = await _manifestProvider.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (manifestResult.IsCancelled())
        {
            return;
        }

        if (manifestResult.IsFailure || manifestResult.Value is null)
        {
            _logger.LogWarning(
                "Expected manifest could not be loaded; skipping reconciliation. Errors: {Errors}",
                string.Join(", ", manifestResult.Errors));
            return;
        }

        // Pure reconciliation — no I/O.
        var reconciliation = _reconciler.Reconcile(manifestResult.Value, discoveredOficios);

        var cycleReport = new CycleReconciliationReport(
            CycleUtc: DateTimeOffset.UtcNow,
            Discovered: discoveredOficios.Count,
            Ingested: ingested,
            Duplicates: duplicates,
            Failures: failures,
            Reconciliation: reconciliation);

        // (a) Structured log at INFO (key "PerCycleReconciliationReport").
        _logger.LogInformation(
            "PerCycleReconciliationReport: Complete={Complete}, Partial={Partial}, Missing={Missing}, " +
            "Extra={Extra}, TotalFiles={TotalFiles}, Discovered={Discovered}, Ingested={Ingested}",
            reconciliation.Complete.Count,
            reconciliation.Partial.Count,
            reconciliation.Missing.Count,
            reconciliation.Extra.Count,
            reconciliation.DownloadedFiles.Count,
            discoveredOficios.Count,
            ingested);

        // (b) Persist as JSON artifact (skip gracefully if resolver is null/fails).
        await PersistReportAsync(cycleReport, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Persists the cycle report as <c>reports/cycle-{utcStamp}.json</c> via
    /// <see cref="IStoragePathResolver"/>. Fail-open: any error is logged and swallowed.
    /// </summary>
    private async Task PersistReportAsync(CycleReconciliationReport report, CancellationToken cancellationToken)
    {
        if (_storagePathResolver is null)
        {
            return; // No resolver configured — skip persist silently.
        }

        var stamp = report.CycleUtc.UtcDateTime.ToString("yyyy-MM-ddTHH-mm-ssZ", System.Globalization.CultureInfo.InvariantCulture);
        var relativePath = $"reports/cycle-{stamp}.json";

        var resolveResult = _storagePathResolver.Resolve(relativePath);
        if (resolveResult.IsFailure || resolveResult.Value is null)
        {
            _logger.LogWarning(
                "Could not resolve storage path for cycle report '{RelativePath}': {Errors}. Report persist skipped.",
                relativePath,
                string.Join(", ", resolveResult.Errors));
            return;
        }

        try
        {
            var fullPath = resolveResult.Value;
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(report, ReportJsonOptions);
            await File.WriteAllTextAsync(fullPath, json, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Cycle reconciliation report persisted to '{Path}'", fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Failed to persist cycle reconciliation report to '{RelativePath}' (fail-open).",
                relativePath);
        }
    }

    /// <summary>
    /// Ingests a single discovered case in its own DI scope, so each pull rides a fresh SIARA
    /// downloader/browser/session with no state bleed between cases.
    /// </summary>
    private async Task<IndQuestResults.Result<IngestionResult>> IngestOneAsync(SiaraCase siaraCase, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IngestionOrchestrator>();
        return await orchestrator
            .IngestCaseAsync(siaraCase, Guid.NewGuid(), cancellationToken)
            .ConfigureAwait(false);
    }
}
