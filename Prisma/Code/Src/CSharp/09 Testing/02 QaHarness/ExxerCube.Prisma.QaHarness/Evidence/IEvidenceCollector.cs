// <copyright file="IEvidenceCollector.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Evidence;

/// <summary>
/// Collects artefacts (screenshots, logs, files, network traces) during a workflow run
/// and accumulates them into an <see cref="EvidencePackage"/>.
/// </summary>
/// <remarks>
/// Implementations must be stateful per run: each capture call appends to
/// <see cref="CurrentPackage"/>.  Call <see cref="Reset"/> between runs to start a fresh package.
/// </remarks>
public interface IEvidenceCollector
{
    /// <summary>
    /// Captures a screenshot of the current browser page and stores it as a PNG file.
    /// </summary>
    /// <param name="label">A short human-readable label used in the report (e.g. <c>"login-page"</c>).</param>
    /// <param name="cancellationToken">Token used to cancel the capture operation.</param>
    /// <returns>A successful <see cref="Result{T}"/> containing the captured <see cref="EvidenceItem"/>; a failure result when no browser page is available or the screenshot fails.</returns>
    Task<Result<EvidenceItem>> CaptureScreenshotAsync(
        string label,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes the current Serilog log buffer to a file and stores it as a log-snapshot artefact.
    /// </summary>
    /// <param name="label">A short human-readable label used in the report.</param>
    /// <param name="cancellationToken">Token used to cancel the flush operation.</param>
    /// <returns>A successful <see cref="Result{T}"/> containing the captured <see cref="EvidenceItem"/>; a failure result when log capture is unavailable.</returns>
    Task<Result<EvidenceItem>> CaptureLogSnapshotAsync(
        string label,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates files matching <paramref name="pattern"/> under <paramref name="sourcePath"/>
    /// and copies them into the evidence package as <see cref="EvidenceKind.GeneratedFile"/> items.
    /// </summary>
    /// <param name="sourcePath">Root directory to search for files.</param>
    /// <param name="pattern">Glob pattern relative to <paramref name="sourcePath"/> (e.g. <c>"*.siro.xml"</c>).</param>
    /// <param name="label">Common label prefix applied to each harvested file item.</param>
    /// <param name="cancellationToken">Token used to cancel the harvest operation.</param>
    /// <returns>A successful <see cref="Result{T}"/> containing the list of harvested <see cref="EvidenceItem"/> records; a failure result when the source path does not exist.</returns>
    Task<Result<IReadOnlyList<EvidenceItem>>> HarvestFilesAsync(
        string sourcePath,
        string pattern,
        string label,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the active Playwright network trace and saves it as an HTTP Archive (HAR) file.
    /// </summary>
    /// <param name="label">A short human-readable label used in the report.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A successful <see cref="Result{T}"/> containing the captured <see cref="EvidenceItem"/>; a failure result when network tracing is not active.</returns>
    Task<Result<EvidenceItem>> CaptureNetworkTraceAsync(
        string label,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the in-progress evidence package for the current run.
    /// </summary>
    EvidencePackage CurrentPackage { get; }

    /// <summary>
    /// Clears all accumulated items and starts a new empty package.
    /// Call between workflow runs when reusing the same collector instance.
    /// </summary>
    void Reset();
}
