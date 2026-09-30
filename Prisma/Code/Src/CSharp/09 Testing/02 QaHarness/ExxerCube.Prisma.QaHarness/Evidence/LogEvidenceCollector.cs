// <copyright file="LogEvidenceCollector.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.QaHarness.Evidence;

/// <summary>
/// Captures a snapshot of log messages accumulated in an in-memory buffer
/// (provided at construction time) and writes them to a file.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why ILogger/buffer, not Serilog:</b> Adding an uncentralized Serilog package reference
/// would violate the "check Directory.Packages.props first" rule (Serilog is not currently
/// referenced from the QaHarness library project). The <see cref="InMemoryLogBuffer"/> helper
/// pairs with <c>Microsoft.Extensions.Logging</c> (already available) and captures the same
/// information a Serilog file sink would capture, with zero new package dependencies.
/// The architecture doc allows this approach: §3.5 says "capturing from a provided in-memory
/// buffer or Microsoft.Extensions.Logging capture is fine".
/// </para>
/// <para>
/// Callers supply the <see cref="InMemoryLogBuffer"/> into which log messages flow (e.g. via
/// a custom <see cref="ILoggerProvider"/>). This class then flushes those messages to a file
/// and registers the file as an <see cref="EvidenceKind.LogSnapshot"/> item.
/// </para>
/// </remarks>
public sealed class LogEvidenceCollector : IEvidenceCollector
{
    private readonly InMemoryLogBuffer _buffer;
    private readonly string _runId;
    private readonly string _outputDirectory;
    private readonly ConcurrentBag<EvidenceItem> _items = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="LogEvidenceCollector"/> class.
    /// </summary>
    /// <param name="buffer">The in-memory log buffer from which messages are flushed.</param>
    /// <param name="runId">The identifier for the current harness run.</param>
    /// <param name="outputDirectory">Directory where log snapshot files are written.</param>
    public LogEvidenceCollector(
        InMemoryLogBuffer buffer,
        string runId,
        string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(runId);
        ArgumentNullException.ThrowIfNull(outputDirectory);
        _buffer = buffer;
        _runId = runId;
        _outputDirectory = outputDirectory;
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
            "LogEvidenceCollector does not support screenshot capture.");
        return Task.FromResult(failure);
    }

    /// <inheritdoc/>
    public async Task<Result<EvidenceItem>> CaptureLogSnapshotAsync(
        string label,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Result<EvidenceItem>.WithFailure("CaptureLogSnapshotAsync was cancelled.");

        try
        {
            var lines = _buffer.Flush();
            var safe = string.Concat(label.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
            if (string.IsNullOrEmpty(safe))
                safe = "log";

            var fileName = $"{safe}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.log";
            var filePath = Path.Combine(_outputDirectory, fileName);

            Directory.CreateDirectory(_outputDirectory);
            await File.WriteAllLinesAsync(filePath, lines, Encoding.UTF8, cancellationToken)
                .ConfigureAwait(false);

            var sizeBytes = new FileInfo(filePath).Length;
            var item = new EvidenceItem(
                Kind: EvidenceKind.LogSnapshot,
                Label: label,
                AbsolutePath: filePath,
                CapturedAt: DateTimeOffset.UtcNow,
                SizeBytes: sizeBytes);

            _items.Add(item);
            return Result<EvidenceItem>.WithSuccess(item);
        }
        catch (OperationCanceledException)
        {
            return Result<EvidenceItem>.WithFailure("Log snapshot capture was cancelled.");
        }
        catch (Exception ex)
        {
            return Result<EvidenceItem>.WithFailure($"Log snapshot capture failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<EvidenceItem>>> HarvestFilesAsync(
        string sourcePath,
        string pattern,
        string label,
        CancellationToken cancellationToken = default)
    {
        var failure = Result<IReadOnlyList<EvidenceItem>>.WithFailure(
            "LogEvidenceCollector does not support file harvesting. Use FileEvidenceCollector.");
        return Task.FromResult(failure);
    }

    /// <inheritdoc/>
    public Task<Result<EvidenceItem>> CaptureNetworkTraceAsync(
        string label,
        CancellationToken cancellationToken = default)
    {
        var failure = Result<EvidenceItem>.WithFailure(
            "LogEvidenceCollector does not support network trace capture.");
        return Task.FromResult(failure);
    }
}
