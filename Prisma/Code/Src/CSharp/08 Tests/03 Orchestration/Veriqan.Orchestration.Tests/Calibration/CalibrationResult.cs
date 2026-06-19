using System.Collections.Generic;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Calibration;

// ---------------------------------------------------------------------------
// §5.2 — Calibration result data structures
// ---------------------------------------------------------------------------

/// <summary>
/// Verdict counts across the corpus for one CheckId.
/// </summary>
/// <param name="CheckId">Rule identifier.</param>
/// <param name="PassCount">Number of specimens where the rule Passed.</param>
/// <param name="FailCount">Number of specimens where the rule Failed.</param>
/// <param name="InsufficientDataCount">Number of specimens where data was insufficient.</param>
public sealed record CheckIdStats(
    string CheckId,
    int PassCount,
    int FailCount,
    int InsufficientDataCount)
{
    /// <summary>
    /// Detection rate: TP / (TP + FN) over specimens that intend this CheckId to Fail.
    /// Null when no KnownBroken specimen declares this CheckId as an intended defect.
    /// </summary>
    public double? DetectionRate { get; init; }

    /// <summary>
    /// False-positive rate: KnownGood specimens where this CheckId newly Failed / KnownGood count.
    /// </summary>
    public double? FalsePositiveRate { get; init; }
}

/// <summary>
/// Result of evaluating one specimen through the pipeline.
/// </summary>
/// <param name="Specimen">The manifest entry.</param>
/// <param name="Skipped">True when the PDF file was absent — no pipeline run.</param>
/// <param name="Signal">Verdict signal (null when Skipped).</param>
/// <param name="FindingCount">Total number of findings (0 when Skipped).</param>
/// <param name="FailCheckIds">CheckIds that returned Fail verdict.</param>
/// <param name="NewFails">
/// Fail CheckIds that are NOT in AllowedFails ∪ IntendedDefects.CheckId.
/// Must be empty for KnownGood specimens (cardinal-rule guard).
/// </param>
/// <param name="DetectedDefects">For KnownBroken: IntendedDefect CheckIds that were actually Fail (TP).</param>
/// <param name="MissedDefects">For KnownBroken: IntendedDefect CheckIds that were NOT Fail (FN).</param>
public sealed record SpecimenResult(
    CorpusSpecimen Specimen,
    bool Skipped,
    string? Signal,
    int FindingCount,
    IReadOnlyList<string> FailCheckIds,
    IReadOnlyList<string> NewFails,
    IReadOnlyList<string> DetectedDefects,
    IReadOnlyList<string> MissedDefects);

/// <summary>
/// One threshold-evidence probe row.
/// </summary>
/// <param name="CheckId">The rule being probed.</param>
/// <param name="Specimen">Specimen file name.</param>
/// <param name="Label">KnownGood or KnownBroken.</param>
/// <param name="MeasuredValue">The measured quantity (e.g. min body point size).</param>
/// <param name="CurrentThreshold">The threshold the rule currently uses.</param>
/// <param name="Margin">MeasuredValue − CurrentThreshold (positive = above floor).</param>
/// <param name="Note">Optional human-readable note about what was measured.</param>
public sealed record ThresholdProbeRow(
    string CheckId,
    string Specimen,
    SpecimenLabel Label,
    string MeasuredValue,
    string CurrentThreshold,
    string Margin,
    string Note);

/// <summary>
/// Aggregated result of running the calibration harness over the full corpus.
/// </summary>
/// <param name="SpecimenResults">Per-specimen outcomes.</param>
/// <param name="CheckIdStats">Per-CheckId aggregated stats.</param>
/// <param name="ThresholdProbes">Threshold-evidence probe rows.</param>
public sealed record CalibrationResult(
    IReadOnlyList<SpecimenResult> SpecimenResults,
    IReadOnlyList<CheckIdStats> CheckIdStats,
    IReadOnlyList<ThresholdProbeRow> ThresholdProbes);
