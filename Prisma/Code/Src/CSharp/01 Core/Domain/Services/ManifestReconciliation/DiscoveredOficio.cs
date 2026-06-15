using System.Collections.Generic;
using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Domain.Services.Manifest;

/// <summary>
/// Runtime representation of one SIARA case actually discovered and (attempted) downloaded during a
/// watch-loop cycle. Passed to <see cref="IManifestReconciler"/> alongside the
/// <see cref="ExpectedManifest"/> so the reconciler can compare what was expected versus what arrived.
/// </summary>
/// <param name="CaseId">The SIARA case identifier (matches <see cref="ExpectedOficio.CaseId"/>).</param>
/// <param name="DownloadedFiles">
/// The files that were successfully downloaded for this case during the cycle.
/// </param>
/// <param name="IsComplete">
/// <see langword="true"/> when every companion file that was discovered for the case was also
/// downloaded successfully (mirrors <c>IngestionOrchestrator</c>'s <c>IsComplete</c> logic).
/// </param>
public sealed record DiscoveredOficio(
    string CaseId,
    IReadOnlyList<DownloadedFileEntry> DownloadedFiles,
    bool IsComplete);

/// <summary>
/// Identifies a single file that was downloaded for a case during a cycle.
/// </summary>
/// <param name="FileName">The base file name (e.g. <c>oficio-222.pdf</c>).</param>
/// <param name="Extension">The extension without the leading dot (e.g. <c>pdf</c>).</param>
/// <param name="Format">The detected <see cref="FileFormat"/>.</param>
public sealed record DownloadedFileEntry(
    string FileName,
    string Extension,
    FileFormat Format);
