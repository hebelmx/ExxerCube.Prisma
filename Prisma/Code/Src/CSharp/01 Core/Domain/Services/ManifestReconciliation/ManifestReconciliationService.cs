using System.Collections.Generic;
using System.Linq;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;

namespace ExxerCube.Prisma.Domain.Services.Manifest;

/// <summary>
/// Pure domain implementation of <see cref="IManifestReconciler"/> (Item B #8 + F #12).
/// No I/O, no DI dependencies — a simple value-in/value-out algorithm on the manifest model.
/// </summary>
/// <remarks>
/// Reconciliation algorithm:
/// <list type="number">
///   <item>Index discovered cases by <c>CaseId</c> (case-insensitive).</item>
///   <item>For each oficio in the expected manifest: if discovered → compare formats (Complete or Partial);
///         if not discovered → Missing.</item>
///   <item>For each discovered case not in expected → Extra (sobra).</item>
///   <item>Build flat downloaded-file list across all discovered cases (Item F).</item>
/// </list>
/// </remarks>
public sealed class ManifestReconciliationService : IManifestReconciler
{
    /// <inheritdoc />
    public ManifestReconciliationReport Reconcile(
        ExpectedManifest expected,
        IReadOnlyList<DiscoveredOficio> actual)
    {
        // Index actual by caseId for O(1) lookup.
        var actualIndex = actual.ToDictionary(
            d => d.CaseId,
            d => d,
            StringComparer.OrdinalIgnoreCase);

        var complete = new List<OficioReconciliationEntry>();
        var partial = new List<OficioReconciliationEntry>();
        var missing = new List<OficioReconciliationEntry>();
        var expectedCaseIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var expectedOficio in expected.Oficios)
        {
            expectedCaseIds.Add(expectedOficio.CaseId);

            if (!actualIndex.TryGetValue(expectedOficio.CaseId, out var discovered))
            {
                // Expected but never arrived this cycle.
                missing.Add(new OficioReconciliationEntry(
                    expectedOficio.CaseId,
                    MissingFormats: expectedOficio.ExpectedFormats.ToList(),
                    ExtraFormats: []));
                continue;
            }

            // Compare expected formats vs downloaded formats.
            var downloadedFormats = discovered.DownloadedFiles
                .Select(f => f.Format)
                .ToHashSet();

            var missingFormats = expectedOficio.ExpectedFormats
                .Where(f => !downloadedFormats.Contains(f))
                .ToList();

            var extraFormats = downloadedFormats
                .Where(f => !expectedOficio.ExpectedFormats.Contains(f))
                .ToList();

            var entry = new OficioReconciliationEntry(
                expectedOficio.CaseId,
                MissingFormats: missingFormats,
                ExtraFormats: extraFormats);

            if (missingFormats.Count == 0)
            {
                complete.Add(entry);
            }
            else
            {
                partial.Add(entry);
            }
        }

        // Oficios discovered but not in the expected manifest → Extra (sobra).
        var extra = actual
            .Where(d => !expectedCaseIds.Contains(d.CaseId))
            .Select(d => new OficioReconciliationEntry(
                d.CaseId,
                MissingFormats: [],
                ExtraFormats: d.DownloadedFiles.Select(f => f.Format).ToList()))
            .ToList();

        // Build flat file list (Item F): every downloaded file across all cases.
        var downloadedFiles = actual
            .SelectMany(d => d.DownloadedFiles.Select(f => new FileDownloadSummary(
                FileName: f.FileName,
                Extension: f.Extension,
                Format: f.Format,
                CaseId: d.CaseId,
                IsComplete: d.IsComplete)))
            .ToList();

        return new ManifestReconciliationReport
        {
            Complete = complete,
            Partial = partial,
            Missing = missing,
            Extra = extra,
            DownloadedFiles = downloadedFiles,
        };
    }
}
