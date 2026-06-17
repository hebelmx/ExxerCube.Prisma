using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;

namespace ExxerCube.Prisma.Veriqan.Domain.Verification;

/// <summary>
/// The rich, in-memory output produced by a single <c>IVecValidationRule</c> evaluation.
/// This is a transient value object used during a verification run; it is intentionally
/// richer than the <see cref="Entities.Finding"/> persistence entity, which only stores
/// a subset of these fields.
/// </summary>
/// <remarks>
/// <para>
/// <b>Design note (ADR-V2/V3):</b> <see cref="RuleFinding"/> lives in
/// <c>Veriqan.Domain.Verification</c> alongside the engine contracts to avoid a name
/// collision with the existing <see cref="Entities.Finding"/> entity (Story 1.3).
/// Mapping <see cref="RuleFinding"/> → <see cref="Entities.Finding"/> is deferred to
/// the Epic 7 persistence story; do not add that mapping here.
/// </para>
/// <para>
/// <b>Determinism (NFR-5):</b> this record is immutable after construction.
/// The engine sorts findings by <see cref="CheckId"/> before returning, guaranteeing
/// a stable output order for identical inputs.
/// </para>
/// </remarks>
/// <param name="CheckId">
/// Rule identifier, e.g. <c>"CL-21"</c> or <c>"CL-EXAMPLE-PASS"</c>.
/// Must match the corresponding <c>IVecValidationRule.CheckId</c>.
/// </param>
/// <param name="Verdict">
/// Outcome of the check (Pass / Fail / InsufficientData).
/// </param>
/// <param name="Technique">
/// Algorithmic technique used to produce the verdict.
/// </param>
/// <param name="Severity">
/// Business-impact severity for the finding; use <see cref="FindingSeverity.Info"/>
/// for passing checks.
/// </param>
/// <param name="EngineVersion">
/// Semantic version (or model identifier for <see cref="TechniqueClass.Ml"/> rules)
/// of the component that produced this finding, e.g. <c>"1.0.0"</c>.
/// </param>
/// <param name="Expected">
/// The value the rule expected to find, rendered as a human-readable string;
/// <see langword="null"/> when not applicable (e.g. presence checks).
/// </param>
/// <param name="Observed">
/// The value the rule actually observed in the submitted content;
/// <see langword="null"/> when the field was not found or the check is non-comparative.
/// </param>
/// <param name="ToleranceApplied">
/// The tolerance band that was applied when comparing numeric values (ADR-V3);
/// <see langword="null"/> when no tolerance was used.
/// </param>
/// <param name="Locator">
/// PDF coordinates identifying where the relevant field was found (or expected);
/// <see langword="null"/> when location information is not available.
/// </param>
public sealed record RuleFinding(
    string CheckId,
    FindingVerdict Verdict,
    TechniqueClass Technique,
    FindingSeverity Severity,
    string EngineVersion,
    string? Expected = null,
    string? Observed = null,
    decimal? ToleranceApplied = null,
    FieldLocator? Locator = null)
{
    // -----------------------------------------------------------------------
    // Convenience factories — enforce consistent field population per verdict
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a <see cref="FindingVerdict.Pass"/> finding.
    /// </summary>
    /// <param name="checkId">Rule identifier.</param>
    /// <param name="technique">Technique used by the rule.</param>
    /// <param name="engineVersion">Version of the rule implementation.</param>
    /// <param name="observed">Observed value (equals the expected).</param>
    /// <param name="toleranceApplied">Tolerance that was satisfied, if any.</param>
    /// <param name="locator">Location of the field in the document, if available.</param>
    public static RuleFinding Pass(
        string checkId,
        TechniqueClass technique,
        string engineVersion,
        string? observed = null,
        decimal? toleranceApplied = null,
        FieldLocator? locator = null) =>
        new(
            CheckId: checkId,
            Verdict: FindingVerdict.Pass,
            Technique: technique,
            Severity: FindingSeverity.Info,
            EngineVersion: engineVersion,
            Expected: null,
            Observed: observed,
            ToleranceApplied: toleranceApplied,
            Locator: locator);

    /// <summary>
    /// Creates a <see cref="FindingVerdict.Fail"/> finding.
    /// </summary>
    /// <param name="checkId">Rule identifier.</param>
    /// <param name="technique">Technique used by the rule.</param>
    /// <param name="severity">Business severity of the failure.</param>
    /// <param name="engineVersion">Version of the rule implementation.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="observed">The observed value.</param>
    /// <param name="toleranceApplied">Tolerance that was exceeded, if applicable.</param>
    /// <param name="locator">Location of the field in the document, if available.</param>
    public static RuleFinding Fail(
        string checkId,
        TechniqueClass technique,
        FindingSeverity severity,
        string engineVersion,
        string? expected = null,
        string? observed = null,
        decimal? toleranceApplied = null,
        FieldLocator? locator = null) =>
        new(
            CheckId: checkId,
            Verdict: FindingVerdict.Fail,
            Technique: technique,
            Severity: severity,
            EngineVersion: engineVersion,
            Expected: expected,
            Observed: observed,
            ToleranceApplied: toleranceApplied,
            Locator: locator);

    /// <summary>
    /// Creates a <see cref="FindingVerdict.InsufficientData"/> finding.
    /// Used when a required reference-data capability is unavailable (FR-20)
    /// or when the rule cannot determine the verdict from available inputs.
    /// </summary>
    /// <param name="checkId">Rule identifier.</param>
    /// <param name="technique">Technique the rule would have used.</param>
    /// <param name="engineVersion">Version of the rule implementation.</param>
    /// <param name="reason">
    /// Optional human-readable explanation stored in <see cref="RuleFinding.Observed"/>.
    /// </param>
    public static RuleFinding InsufficientData(
        string checkId,
        TechniqueClass technique,
        string engineVersion,
        string? reason = null) =>
        new(
            CheckId: checkId,
            Verdict: FindingVerdict.InsufficientData,
            Technique: technique,
            Severity: FindingSeverity.Warning,
            EngineVersion: engineVersion,
            Expected: null,
            Observed: reason,
            ToleranceApplied: null,
            Locator: null);
}
