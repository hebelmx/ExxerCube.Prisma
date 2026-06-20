// <copyright file="CompositeEvidenceCollector.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.Collections.Concurrent;

namespace ExxerCube.Prisma.QaHarness.Evidence;

/// <summary>
/// Fulfills the full <see cref="IEvidenceCollector"/> interface by composing
/// <see cref="PlaywrightEvidenceCollector"/>, <see cref="LogEvidenceCollector"/>,
/// and <see cref="FileEvidenceCollector"/>, each contributing its specialist capability.
/// </summary>
/// <remarks>
/// This is the primary <see cref="IEvidenceCollector"/> implementation that callers should
/// use in production harness runs. When a specific capability is not needed (e.g. no Playwright
/// page is available) pass <see langword="null"/> for the corresponding collector — the composite
/// returns a graceful capability-unavailable failure instead of throwing.
/// </remarks>
public sealed class CompositeEvidenceCollector : IEvidenceCollector
{
    private readonly PlaywrightEvidenceCollector? _playwright;
    private readonly LogEvidenceCollector? _log;
    private readonly FileEvidenceCollector? _file;
    private readonly string _runId;
    private readonly ConcurrentBag<EvidenceItem> _allItems = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeEvidenceCollector"/> class.
    /// </summary>
    /// <param name="runId">The identifier for the current harness run, shared across all child collectors.</param>
    /// <param name="playwright">Collector for Playwright-based evidence (screenshots, traces). May be <see langword="null"/>.</param>
    /// <param name="log">Collector for log snapshots. May be <see langword="null"/>.</param>
    /// <param name="file">Collector for harvested pipeline output files. May be <see langword="null"/>.</param>
    public CompositeEvidenceCollector(
        string runId,
        PlaywrightEvidenceCollector? playwright = null,
        LogEvidenceCollector? log = null,
        FileEvidenceCollector? file = null)
    {
        ArgumentNullException.ThrowIfNull(runId);
        _runId = runId;
        _playwright = playwright;
        _log = log;
        _file = file;
    }

    /// <inheritdoc/>
    public EvidencePackage CurrentPackage =>
        new(_runId, _allItems.ToList().AsReadOnly());

    /// <inheritdoc/>
    public void Reset()
    {
        _allItems.Clear();
        _playwright?.Reset();
        _log?.Reset();
        _file?.Reset();
    }

    /// <inheritdoc/>
    public async Task<Result<EvidenceItem>> CaptureScreenshotAsync(
        string label,
        CancellationToken cancellationToken = default)
    {
        if (_playwright is null)
        {
            return Result<EvidenceItem>.WithFailure(
                "Screenshot capture unavailable: no PlaywrightEvidenceCollector was provided.");
        }

        var result = await _playwright.CaptureScreenshotAsync(label, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess && result.Value is not null)
            _allItems.Add(result.Value);

        return result;
    }

    /// <inheritdoc/>
    public async Task<Result<EvidenceItem>> CaptureLogSnapshotAsync(
        string label,
        CancellationToken cancellationToken = default)
    {
        if (_log is null)
        {
            return Result<EvidenceItem>.WithFailure(
                "Log snapshot capture unavailable: no LogEvidenceCollector was provided.");
        }

        var result = await _log.CaptureLogSnapshotAsync(label, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess && result.Value is not null)
            _allItems.Add(result.Value);

        return result;
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<EvidenceItem>>> HarvestFilesAsync(
        string sourcePath,
        string pattern,
        string label,
        CancellationToken cancellationToken = default)
    {
        if (_file is null)
        {
            return Result<IReadOnlyList<EvidenceItem>>.WithFailure(
                "File harvest unavailable: no FileEvidenceCollector was provided.");
        }

        var result = await _file.HarvestFilesAsync(sourcePath, pattern, label, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess && result.Value is not null)
        {
            foreach (var item in result.Value)
                _allItems.Add(item);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<Result<EvidenceItem>> CaptureNetworkTraceAsync(
        string label,
        CancellationToken cancellationToken = default)
    {
        if (_playwright is null)
        {
            return Result<EvidenceItem>.WithFailure(
                "Network trace capture unavailable: no PlaywrightEvidenceCollector was provided.");
        }

        var result = await _playwright.CaptureNetworkTraceAsync(label, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess && result.Value is not null)
            _allItems.Add(result.Value);

        return result;
    }
}
