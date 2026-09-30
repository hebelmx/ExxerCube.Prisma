// <copyright file="FileEvidenceCollector.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.QaHarness.Evidence;

/// <summary>
/// Harvests files from a directory by glob pattern and accumulates them as
/// <see cref="EvidenceKind.GeneratedFile"/> items in the current <see cref="EvidencePackage"/>.
/// </summary>
/// <remarks>
/// <see cref="CaptureScreenshotAsync"/> and <see cref="CaptureNetworkTraceAsync"/> are not
/// applicable to this collector and return failure results explaining why.
/// <see cref="CaptureLogSnapshotAsync"/> is also not applicable.
/// For a full-featured composite that delegates each method to the appropriate collector
/// see <see cref="CompositeEvidenceCollector"/>.
/// </remarks>
public sealed class FileEvidenceCollector : IEvidenceCollector
{
    private readonly ILogger<FileEvidenceCollector> _logger;
    private readonly string _runId;
    private readonly ConcurrentBag<EvidenceItem> _items = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FileEvidenceCollector"/> class.
    /// </summary>
    /// <param name="runId">The identifier for the current harness run.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public FileEvidenceCollector(string runId, ILogger<FileEvidenceCollector> logger)
    {
        ArgumentNullException.ThrowIfNull(runId);
        ArgumentNullException.ThrowIfNull(logger);
        _runId = runId;
        _logger = logger;
    }

    /// <inheritdoc/>
    public EvidencePackage CurrentPackage =>
        new(_runId, _items.ToList().AsReadOnly());

    /// <inheritdoc/>
    public void Reset() => _items.Clear();

    /// <inheritdoc/>
    public Task<Result<EvidenceItem>> CaptureScreenshotAsync(
        string label,
        CancellationToken cancellationToken = default)
    {
        var failure = Result<EvidenceItem>.WithFailure(
            "FileEvidenceCollector does not support screenshot capture. Use CompositeEvidenceCollector with a PlaywrightEvidenceCollector.");
        return Task.FromResult(failure);
    }

    /// <inheritdoc/>
    public Task<Result<EvidenceItem>> CaptureLogSnapshotAsync(
        string label,
        CancellationToken cancellationToken = default)
    {
        var failure = Result<EvidenceItem>.WithFailure(
            "FileEvidenceCollector does not support log snapshot capture. Use CompositeEvidenceCollector with a LogEvidenceCollector.");
        return Task.FromResult(failure);
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<EvidenceItem>>> HarvestFilesAsync(
        string sourcePath,
        string pattern,
        string label,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Result<IReadOnlyList<EvidenceItem>>.WithFailure("HarvestFilesAsync was cancelled.");

        if (!Directory.Exists(sourcePath))
        {
            _logger.LogWarning("Source path does not exist: {SourcePath}", sourcePath);
            return Result<IReadOnlyList<EvidenceItem>>.WithFailure($"Source path does not exist: {sourcePath}");
        }

        try
        {
            var matched = await Task.Run(
                () => Directory.EnumerateFiles(sourcePath, pattern, SearchOption.AllDirectories).ToList(),
                cancellationToken).ConfigureAwait(false);

            var items = new List<EvidenceItem>(matched.Count);
            foreach (var filePath in matched)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var info = new FileInfo(filePath);
                    var item = new EvidenceItem(
                        Kind: EvidenceKind.GeneratedFile,
                        Label: $"{label}/{info.Name}",
                        AbsolutePath: filePath,
                        CapturedAt: DateTimeOffset.UtcNow,
                        SizeBytes: info.Exists ? info.Length : 0L);

                    _items.Add(item);
                    items.Add(item);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to stat file {FilePath}; skipping.", filePath);
                }
            }

            _logger.LogInformation(
                "Harvested {Count} file(s) matching '{Pattern}' from {SourcePath}.",
                items.Count, pattern, sourcePath);

            return Result<IReadOnlyList<EvidenceItem>>.WithSuccess(items.AsReadOnly());
        }
        catch (OperationCanceledException)
        {
            return Result<IReadOnlyList<EvidenceItem>>.WithFailure("File harvest was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error harvesting files from {SourcePath}.", sourcePath);
            return Result<IReadOnlyList<EvidenceItem>>.WithFailure($"File harvest failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Task<Result<EvidenceItem>> CaptureNetworkTraceAsync(
        string label,
        CancellationToken cancellationToken = default)
    {
        var failure = Result<EvidenceItem>.WithFailure(
            "FileEvidenceCollector does not support network trace capture. Use CompositeEvidenceCollector with a PlaywrightEvidenceCollector.");
        return Task.FromResult(failure);
    }
}
