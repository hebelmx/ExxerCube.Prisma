using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IndQuestResults;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Calibration;

/// <summary>
/// §5.3 — Renders a Markdown calibration report from a <see cref="CalibrationResult"/>
/// and writes it to <c>docs/qa/calibration/calibration-report.md</c> under the repo root.
/// </summary>
/// <remarks>
/// The renderer locates the repo root by walking up from <c>AppContext.BaseDirectory</c>
/// until it finds the directory containing <c>Prisma/Fixtures</c>.  If not found the write
/// step is silently skipped — never throws.
/// </remarks>
public static class CalibrationReportRenderer
{
    // Cross-platform path segments — a hardcoded backslash string created a literal
    // "docs\qa\calibration\..." file in the repo root on Linux instead of writing to
    // docs/qa/calibration/. Path.Combine yields the correct separator per OS.
    private static readonly string ReportRelPath =
        Path.Combine("docs", "qa", "calibration", "calibration-report.md");

    /// <summary>
    /// Renders the Markdown report and writes it to the repo.
    /// </summary>
    /// <param name="manifest">The deserialized manifest used for the run.</param>
    /// <param name="result">The aggregated calibration result.</param>
    /// <param name="cancellationToken">Propagated to the file-write operation.</param>
    /// <param name="repoRootHint">
    /// Optional explicit repo root path.  When provided this overrides the automatic
    /// walk-up search from <c>AppContext.BaseDirectory</c>, which fails when the build
    /// output is outside the repo (e.g. the BuildArtifacts directory in this project).
    /// </param>
    /// <returns>
    /// The absolute path to the written report, or <see langword="null"/> if the repo root
    /// could not be located (CI environments without the full repo tree).
    /// Never throws for control flow.
    /// </returns>
    public static async Task<string?> RenderAndWriteAsync(
        CorpusManifest manifest,
        CalibrationResult result,
        CancellationToken cancellationToken = default,
        string? repoRootHint = null)
    {
        if (cancellationToken.IsCancellationRequested)
            return null;

        try
        {
            var markdown = Render(manifest, result);
            var repoRoot = repoRootHint ?? LocateRepoRoot();
            if (repoRoot is null)
                return null;

            var reportPath = Path.Combine(repoRoot, ReportRelPath);
            var dir = Path.GetDirectoryName(reportPath);
            if (dir is not null)
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(reportPath, markdown, Encoding.UTF8, cancellationToken)
                .ConfigureAwait(false);

            return reportPath;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception)
        {
            // Never throw — write failure is non-fatal.
            return null;
        }
    }

    // -----------------------------------------------------------------------
    // Markdown rendering
    // -----------------------------------------------------------------------

    internal static string Render(CorpusManifest manifest, CalibrationResult result)
    {
        var sb = new StringBuilder();
        var specimens = manifest.Specimens;
        var knownGoodCount     = specimens.Count(s => s.Label == SpecimenLabel.KnownGood);
        var knownSyntheticCount = specimens.Count(s => s.Label == SpecimenLabel.KnownSynthetic);
        var knownBrokenCount   = specimens.Count(s => s.Label == SpecimenLabel.KnownBroken);
        var totalCount         = specimens.Count;
        var runDate            = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC");

        // -----------------------------------------------------------------------
        // (a) Honesty banner
        // -----------------------------------------------------------------------
        sb.AppendLine("# Veriqan Corpus Calibration Report");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {runDate}  ");
        sb.AppendLine($"**Corpus size:** {totalCount} specimen(s)  ");
        sb.AppendLine(
            $"**KnownGood:** {knownGoodCount} · " +
            $"**KnownSynthetic:** {knownSyntheticCount} (non-compliant placeholders — NOT compliance evidence) · " +
            $"**KnownBroken:** {knownBrokenCount}");
        sb.AppendLine();

        if (knownBrokenCount == 0)
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## ⚠️ UNCALIBRATED — Detection Power Unmeasured");
            sb.AppendLine();
            sb.AppendLine(
                "**This corpus contains ZERO KnownBroken specimens.  " +
                "All thresholds remain UNCALIBRATED — detection power (true-positive rate) " +
                "is UNMEASURED.  The distributions below are one-sided (good-only).  " +
                "Adding deliberately-broken specimens is the single highest-value next step " +
                "before drawing any conclusion about rule sensitivity.**");
            sb.AppendLine();
            sb.AppendLine("---");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("---");
        }

        // -----------------------------------------------------------------------
        // Per-specimen summary
        // -----------------------------------------------------------------------
        sb.AppendLine();
        sb.AppendLine("## Specimen Run Summary");
        sb.AppendLine();
        sb.AppendLine("| Specimen | Label | Skipped | Signal | Findings | NewFails | AllowedFails (ref-data gaps) | KnownFixtureDefects (PDF non-compliance) |");
        sb.AppendLine("|----------|-------|---------|--------|----------|----------|------------------------------|------------------------------------------|");

        foreach (var sr in result.SpecimenResults)
        {
            var skipped = sr.Skipped ? "YES" : "no";
            var signal = sr.Signal ?? "—";
            var newFailsStr = sr.NewFails.Count == 0 ? "none" : string.Join(", ", sr.NewFails);
            var allowedStr = sr.Specimen.AllowedFails.Count == 0
                ? "none"
                : string.Join(", ", sr.Specimen.AllowedFails.Select(af => af.CheckId));
            var fixtureDefectsStr = sr.Specimen.KnownFixtureDefects.Count == 0
                ? "none"
                : string.Join(", ", sr.Specimen.KnownFixtureDefects.Select(fd => fd.CheckId));
            sb.AppendLine(
                $"| {sr.Specimen.FileName} " +
                $"| {sr.Specimen.Label} " +
                $"| {skipped} " +
                $"| {signal} " +
                $"| {sr.FindingCount} " +
                $"| {newFailsStr} " +
                $"| {allowedStr} " +
                $"| {fixtureDefectsStr} |");
        }

        sb.AppendLine();

        // -----------------------------------------------------------------------
        // Fail CheckIds per specimen
        // -----------------------------------------------------------------------
        sb.AppendLine("## Fail CheckIds per Specimen");
        sb.AppendLine();
        foreach (var sr in result.SpecimenResults.Where(r => !r.Skipped && r.FailCheckIds.Count > 0))
        {
            sb.AppendLine($"### {sr.Specimen.FileName} ({sr.Specimen.Label})");
            sb.AppendLine();
            sb.AppendLine("| CheckId | AllowedFails (ref-data gap) | KnownFixtureDefects (PDF non-compliance) | IntendedDefects | NewFail? |");
            sb.AppendLine("|---------|----------------------------|------------------------------------------|-----------------|----------|");

            var allowedSet = new HashSet<string>(
                sr.Specimen.AllowedFails.Select(af => af.CheckId), StringComparer.OrdinalIgnoreCase);
            var fixtureDefectSet = new HashSet<string>(
                sr.Specimen.KnownFixtureDefects.Select(fd => fd.CheckId), StringComparer.OrdinalIgnoreCase);
            var intendedSet = new HashSet<string>(
                sr.Specimen.IntendedDefects.Select(d => d.CheckId), StringComparer.OrdinalIgnoreCase);

            foreach (var checkId in sr.FailCheckIds)
            {
                var inAllowed  = allowedSet.Contains(checkId)       ? "yes" : "no";
                var inFixture  = fixtureDefectSet.Contains(checkId) ? "yes" : "no";
                var inIntended = intendedSet.Contains(checkId)      ? "yes" : "no";
                var isNew = sr.NewFails.Contains(checkId, StringComparer.OrdinalIgnoreCase) ? "**YES**" : "no";
                sb.AppendLine($"| {checkId} | {inAllowed} | {inFixture} | {inIntended} | {isNew} |");
            }
            sb.AppendLine();
        }

        // -----------------------------------------------------------------------
        // (b) Per-CheckId verdict matrix
        // -----------------------------------------------------------------------
        sb.AppendLine("## Per-CheckId Verdict Matrix");
        sb.AppendLine();

        if (result.CheckIdStats.Count == 0)
        {
            sb.AppendLine("_No Fail findings across the corpus._");
        }
        else
        {
            sb.AppendLine("| CheckId | FailCount | PassCount | InsufficientData | DetectionRate | FalsePositiveRate |");
            sb.AppendLine("|---------|-----------|-----------|-----------------|---------------|-------------------|");

            foreach (var stat in result.CheckIdStats.OrderBy(s => s.CheckId))
            {
                var dr = stat.DetectionRate.HasValue ? $"{stat.DetectionRate.Value:P0}" : "n/a";
                var fpr = stat.FalsePositiveRate.HasValue ? $"{stat.FalsePositiveRate.Value:P0}" : "n/a";
                sb.AppendLine(
                    $"| {stat.CheckId} " +
                    $"| {stat.FailCount} " +
                    $"| {stat.PassCount} " +
                    $"| {stat.InsufficientDataCount} " +
                    $"| {dr} " +
                    $"| {fpr} |");
            }
        }
        sb.AppendLine();

        // -----------------------------------------------------------------------
        // (c) Threshold-evidence tables
        // -----------------------------------------------------------------------
        sb.AppendLine("## Threshold-Evidence Tables");
        sb.AppendLine();

        if (result.ThresholdProbes.Count == 0)
        {
            sb.AppendLine("_No threshold-evidence probe rows collected._");
        }
        else
        {
            var groups = result.ThresholdProbes
                .GroupBy(p => p.CheckId, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                sb.AppendLine($"### {group.Key}");
                sb.AppendLine();

                if (knownBrokenCount == 0)
                {
                    sb.AppendLine("_Distribution is **good-only** — no separation measurable until KnownBroken specimens are added._");
                    sb.AppendLine();
                }

                sb.AppendLine("| Specimen | Label | MeasuredValue | CurrentThreshold | Margin | Note |");
                sb.AppendLine("|----------|-------|---------------|-----------------|--------|------|");

                foreach (var row in group)
                {
                    sb.AppendLine(
                        $"| {row.Specimen} " +
                        $"| {row.Label} " +
                        $"| {row.MeasuredValue} " +
                        $"| {row.CurrentThreshold} " +
                        $"| {row.Margin} " +
                        $"| {row.Note} |");
                }
                sb.AppendLine();
            }
        }

        // -----------------------------------------------------------------------
        // (d) Recommendations
        // -----------------------------------------------------------------------
        sb.AppendLine("## Recommendations");
        sb.AppendLine();
        sb.AppendLine("> **All items below are Proposed, NOT applied.**  " +
                      "Threshold changes are legal/compliance decisions and must never be auto-applied.");
        sb.AppendLine();

        if (knownBrokenCount == 0)
        {
            sb.AppendLine(
                "- [ ] **Acquire KnownBroken specimens** — at least one per calibratable rule " +
                "(LAW-TYPO-MINSIZE, LAW-SEC-SIZECAP, LAW-ADS-PLACEMENT, LAW-§19-INTERES, currency residuals). " +
                "Without broken specimens no threshold recommendation can be made.");
            sb.AppendLine(
                "- [ ] **Promote KnownSynthetic to KnownGood** only after regenerating the fixture " +
                "PDFs with correct Aptos fonts, proper section structure, and full verbatim legends. " +
                "Current KnownSynthetic specimens are NOT compliant statements.");
            sb.AppendLine(
                "- [ ] **Confirm AllowedFails are reference-data gaps only** — review the " +
                "AllowedFails table above; every entry must be explained by omitted optional " +
                "reference data.  Genuine PDF non-compliance must live in KnownFixtureDefects, " +
                "not AllowedFails.");
        }
        else
        {
            sb.AppendLine("- [ ] _(Recommendations will be populated once the corpus contains both classes.)_");
        }

        sb.AppendLine();

        // -----------------------------------------------------------------------
        // Suppression buckets legend (two distinct tables)
        // -----------------------------------------------------------------------
        sb.AppendLine("## AllowedFails Legend (reference-data gaps only)");
        sb.AppendLine();
        sb.AppendLine("> These Fails are suppressed ONLY because optional reference data was omitted from");
        sb.AppendLine("> the synthetic bundle (e.g. faked RFC/rate/period).  They say nothing about the");
        sb.AppendLine("> PDF's compliance and are NOT fixture defects.");
        sb.AppendLine();
        sb.AppendLine("| Specimen | CheckId | Reason |");
        sb.AppendLine("|----------|---------|--------|");

        foreach (var s in specimens)
        {
            foreach (var af in s.AllowedFails)
            {
                sb.AppendLine($"| {s.FileName} | {af.CheckId} | {af.Reason} |");
            }
        }

        if (!specimens.Any(s => s.AllowedFails.Count > 0))
            sb.AppendLine("| (none) | | |");

        sb.AppendLine();

        sb.AppendLine("## KnownFixtureDefects Legend (genuine PDF non-compliance)");
        sb.AppendLine();
        sb.AppendLine("> These Fails reflect GENUINE non-compliance properties of the fixture PDF itself:");
        sb.AppendLine("> sub-floor typography, non-Aptos fonts, missing mandatory sections, absent verbatim");
        sb.AppendLine("> legends, pagination issues.  A green driver test with these suppressed MUST NOT be");
        sb.AppendLine("> read as proof of compliance — these fixtures are NOT compliant statements.");
        sb.AppendLine();
        sb.AppendLine("| Specimen | CheckId | Reason |");
        sb.AppendLine("|----------|---------|--------|");

        foreach (var s in specimens)
        {
            foreach (var fd in s.KnownFixtureDefects)
            {
                sb.AppendLine($"| {s.FileName} | {fd.CheckId} | {fd.Reason} |");
            }
        }

        if (!specimens.Any(s => s.KnownFixtureDefects.Count > 0))
            sb.AppendLine("| (none) | | |");

        sb.AppendLine();

        return sb.ToString();
    }

    // -----------------------------------------------------------------------
    // Repo-root locator
    // -----------------------------------------------------------------------

    /// <summary>
    /// Walks up the directory tree from <c>AppContext.BaseDirectory</c> until finding
    /// the directory that contains <c>Prisma/Fixtures</c> (the repo root marker).
    /// Returns <see langword="null"/> when not found.
    /// </summary>
    internal static string? LocateRepoRoot()
    {
        try
        {
            var dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                // Repo root is the directory that contains "Prisma\Fixtures" (or "Prisma/Fixtures").
                var marker = Path.Combine(dir, "Prisma", "Fixtures");
                if (Directory.Exists(marker))
                    return dir;

                var parent = Directory.GetParent(dir)?.FullName;
                if (parent == dir || parent is null)
                    break;
                dir = parent;
            }
        }
        catch (Exception)
        {
            // Never throw.
        }
        return null;
    }
}
