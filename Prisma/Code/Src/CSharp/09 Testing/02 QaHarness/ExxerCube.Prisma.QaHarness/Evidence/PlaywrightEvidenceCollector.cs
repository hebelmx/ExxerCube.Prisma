// <copyright file="PlaywrightEvidenceCollector.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace ExxerCube.Prisma.QaHarness.Evidence;

/// <summary>
/// Captures Playwright-based evidence: screenshots from a browser page and
/// (opt-in stub) network HAR traces.
/// </summary>
/// <remarks>
/// Video recording and full network tracing are opt-in; when not enabled they
/// return capability-unavailable failure results rather than throwing.
/// The <see cref="IPage"/> instance is provided at construction time and may be
/// <see langword="null"/> when Playwright is not available — all methods handle
/// the null page gracefully.
/// </remarks>
public sealed class PlaywrightEvidenceCollector : IEvidenceCollector
{
    private readonly IPage? _page;
    private readonly string _runId;
    private readonly string _outputDirectory;
    private readonly bool _networkTracingEnabled;
    private readonly ILogger<PlaywrightEvidenceCollector> _logger;
    private readonly ConcurrentBag<EvidenceItem> _items = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaywrightEvidenceCollector"/> class.
    /// </summary>
    /// <param name="page">The Playwright page to capture from; may be <see langword="null"/> when Playwright is unavailable.</param>
    /// <param name="runId">The identifier for the current harness run.</param>
    /// <param name="outputDirectory">Directory where screenshot files are written.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="networkTracingEnabled">
    /// When <see langword="true"/>, <see cref="CaptureNetworkTraceAsync"/> attempts to stop
    /// an active Playwright trace and save the HAR. Defaults to <see langword="false"/>.
    /// </param>
    public PlaywrightEvidenceCollector(
        IPage? page,
        string runId,
        string outputDirectory,
        ILogger<PlaywrightEvidenceCollector> logger,
        bool networkTracingEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(runId);
        ArgumentNullException.ThrowIfNull(outputDirectory);
        ArgumentNullException.ThrowIfNull(logger);
        _page = page;
        _runId = runId;
        _outputDirectory = outputDirectory;
        _logger = logger;
        _networkTracingEnabled = networkTracingEnabled;
    }

    /// <inheritdoc/>
    public EvidencePackage CurrentPackage =>
        new(_runId, _items.ToList().AsReadOnly());

    /// <inheritdoc/>
    public void Reset() => _items.Clear();

    /// <inheritdoc/>
    public async Task<Result<EvidenceItem>> CaptureScreenshotAsync(
        string label,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Result<EvidenceItem>.WithFailure("CaptureScreenshotAsync was cancelled.");

        if (_page is null)
        {
            return Result<EvidenceItem>.WithFailure(
                "Playwright page is not available; screenshot cannot be captured. Capability: Playwright unavailable.");
        }

        try
        {
            Directory.CreateDirectory(_outputDirectory);
            var safe = string.Concat(label.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
            if (string.IsNullOrEmpty(safe))
                safe = "screenshot";

            var fileName = $"{safe}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.png";
            var filePath = Path.Combine(_outputDirectory, fileName);

            await _page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = filePath,
                FullPage = true,
            }).ConfigureAwait(false);

            var sizeBytes = File.Exists(filePath) ? new FileInfo(filePath).Length : 0L;
            var item = new EvidenceItem(
                Kind: EvidenceKind.Screenshot,
                Label: label,
                AbsolutePath: filePath,
                CapturedAt: DateTimeOffset.UtcNow,
                SizeBytes: sizeBytes);

            _items.Add(item);
            _logger.LogInformation("Screenshot captured: {Label} → {FilePath}", label, filePath);
            return Result<EvidenceItem>.WithSuccess(item);
        }
        catch (OperationCanceledException)
        {
            return Result<EvidenceItem>.WithFailure("Screenshot capture was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Screenshot capture failed for label '{Label}'.", label);
            return Result<EvidenceItem>.WithFailure($"Screenshot capture failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Task<Result<EvidenceItem>> CaptureLogSnapshotAsync(
        string label,
        CancellationToken cancellationToken = default)
    {
        // PlaywrightEvidenceCollector does not own the log buffer — delegate to LogEvidenceCollector.
        return Task.FromResult(Result<EvidenceItem>.WithFailure(
            "PlaywrightEvidenceCollector does not support log snapshot capture. Use LogEvidenceCollector."));
    }

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<EvidenceItem>>> HarvestFilesAsync(
        string sourcePath,
        string pattern,
        string label,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<IReadOnlyList<EvidenceItem>>.WithFailure(
            "PlaywrightEvidenceCollector does not support file harvesting. Use FileEvidenceCollector."));
    }

    /// <inheritdoc/>
    public Task<Result<EvidenceItem>> CaptureNetworkTraceAsync(
        string label,
        CancellationToken cancellationToken = default)
    {
        if (!_networkTracingEnabled)
        {
            return Task.FromResult(Result<EvidenceItem>.WithFailure(
                "Network HAR tracing is not enabled. Pass networkTracingEnabled=true to PlaywrightEvidenceCollector when constructing it."));
        }

        if (_page is null)
        {
            return Task.FromResult(Result<EvidenceItem>.WithFailure(
                "Playwright page is not available; network trace cannot be captured. Capability: Playwright unavailable."));
        }

        // Stub: full Playwright tracing requires tracing to have been started at browser context
        // creation time, which is the caller's responsibility. We record the intent but do not
        // attempt to stop/export here — that is a Chunk-4 concern when the full workflow is wired.
        return Task.FromResult(Result<EvidenceItem>.WithFailure(
            "Network HAR export is opt-in stub (Chunk 4). Enable tracing at browser context creation and call ITracing.StopAsync() directly."));
    }
}
