using System;
using System.Collections.Generic;
using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Domain.Services.Manifest;

/// <summary>
/// The output of one reconciliation pass produced by <see cref="IManifestReconciler"/>. Carries the
/// per-oficio disposition buckets and the flat per-cycle file list (Item F #12).
/// </summary>
public sealed record ManifestReconciliationReport
{
    /// <summary>
    /// Oficios that are present in both the expected manifest AND the cycle discovery, and for which
    /// every expected companion format was downloaded.
    /// </summary>
    public IReadOnlyList<OficioReconciliationEntry> Complete { get; init; } = [];

    /// <summary>
    /// Oficios present in both expected and discovered, but with at least one expected companion format
    /// missing (best-effort partial; maps to <c>ReviewReason.IncompleteCase</c>).
    /// </summary>
    public IReadOnlyList<OficioReconciliationEntry> Partial { get; init; } = [];

    /// <summary>
    /// Oficios that appear in the expected manifest but were NOT discovered this cycle (never arrived).
    /// </summary>
    public IReadOnlyList<OficioReconciliationEntry> Missing { get; init; } = [];

    /// <summary>
    /// Oficios that were discovered this cycle but are NOT listed in the expected manifest (sobra —
    /// unexpected arrivals).
    /// </summary>
    public IReadOnlyList<OficioReconciliationEntry> Extra { get; init; } = [];

    /// <summary>
    /// Flat list of every file downloaded during the cycle across all cases (Item F #12).
    /// </summary>
    public IReadOnlyList<FileDownloadSummary> DownloadedFiles { get; init; } = [];
}

/// <summary>
/// Per-oficio reconciliation detail: the case id, its bucket assignment, and any per-format gaps.
/// </summary>
/// <param name="CaseId">The SIARA case identifier.</param>
/// <param name="MissingFormats">
/// Expected formats that were NOT downloaded for this case (empty for <em>Complete</em> and
/// <em>Missing</em>-type entries; populated for <em>Partial</em>).
/// </param>
/// <param name="ExtraFormats">
/// Downloaded formats that were NOT in the expected list (empty unless the case has unexpected files).
/// </param>
public sealed record OficioReconciliationEntry(
    string CaseId,
    IReadOnlyList<FileFormat> MissingFormats,
    IReadOnlyList<FileFormat> ExtraFormats);

/// <summary>
/// A single downloaded file reported in the per-cycle file list (Item F #12 — "per-cycle
/// downloaded-file report").
/// </summary>
/// <param name="FileName">The base file name.</param>
/// <param name="Extension">The extension without the leading dot.</param>
/// <param name="Format">The detected file format.</param>
/// <param name="CaseId">The SIARA case this file belongs to.</param>
/// <param name="IsComplete">
/// <see langword="true"/> when the case this file belongs to downloaded all its companion files.
/// </param>
public sealed record FileDownloadSummary(
    string FileName,
    string Extension,
    FileFormat Format,
    string CaseId,
    bool IsComplete);

/// <summary>
/// The full per-cycle reconciliation payload that the watch loop logs (structured Serilog) and persists
/// as a JSON artifact under <c>reports/cycle-{utcStamp}.json</c>.
/// </summary>
/// <param name="CycleUtc">UTC timestamp of the cycle boundary (when reconciliation ran).</param>
/// <param name="Discovered">Total cases discovered by SIARA this cycle.</param>
/// <param name="Ingested">New cases ingested (not duplicates) this cycle.</param>
/// <param name="Duplicates">Cases skipped as duplicates this cycle.</param>
/// <param name="Failures">Cases that failed ingestion this cycle.</param>
/// <param name="Reconciliation">The reconciliation report produced by <see cref="IManifestReconciler"/>.</param>
public sealed record CycleReconciliationReport(
    DateTimeOffset CycleUtc,
    int Discovered,
    int Ingested,
    int Duplicates,
    int Failures,
    ManifestReconciliationReport Reconciliation);
