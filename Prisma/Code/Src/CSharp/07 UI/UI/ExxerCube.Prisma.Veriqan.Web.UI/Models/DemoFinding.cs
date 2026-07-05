using ExxerCube.Prisma.Veriqan.Domain.Extraction;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Models;

/// <summary>
/// View-model representation of a single compliance check finding,
/// shaped after <c>Veriqan.Domain.Verification.RuleFinding</c>.
/// Used by the demo data service to avoid dependency on internal
/// <c>VerdictSummary</c> factory methods.
/// </summary>
public sealed class DemoFinding
{
    /// <summary>Gets the check identifier (e.g. "CL-21").</summary>
    public string CheckId { get; init; } = string.Empty;

    /// <summary>Gets the outcome of the check.</summary>
    public FindingVerdict Verdict { get; init; }

    /// <summary>Gets the algorithmic technique used.</summary>
    public TechniqueClass Technique { get; init; }

    /// <summary>Gets the severity of the finding (meaningful only when <see cref="Verdict"/> is Fail).</summary>
    public FindingSeverity Severity { get; init; }

    /// <summary>Gets a human-readable label for the check.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Gets the expected value, or <see langword="null"/> when not applicable.</summary>
    public string? Expected { get; init; }

    /// <summary>Gets the observed value, or <see langword="null"/> when not applicable.</summary>
    public string? Observed { get; init; }

    /// <summary>Gets the DOF numeral reference (Mexican regulation article), or empty string.</summary>
    public string DofNumeral { get; init; } = string.Empty;

    /// <summary>
    /// Gets the regulatory tier of the check.
    /// Sourced from <c>checklist-tiers.csv</c> in the reference bundle via
    /// <see cref="ExxerCube.Prisma.Veriqan.Web.UI.Services.ChecklistIds.Tier"/>.
    /// </summary>
    public ChecklistTier Tier { get; init; }

    /// <summary>
    /// Gets the engine's confidence in this finding (0.0–1.0, inclusive).
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       <c>1.0</c> for deterministic / structural / presence / format rules — these rules
    ///       consume no confidence-guarded extracted field; the verdict is certain.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       Values in the <c>0.90–0.98</c> range for arithmetic and field-value rules that
    ///       consume saldo or amount fields extracted from the document text layer.
    ///       These mirror the real engine's "min consumed field confidence" for a
    ///       digital (non-scanned) statement and are representative, not measured.
    ///     </description>
    ///   </item>
    /// </list>
    /// Wired by Epic 4 Story 4.1.
    /// </summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>
    /// Gets whether this check is a visual/layout rule (vs. a data/arithmetic rule).
    /// Sourced from the real check ledger (VLD-S4a) via <c>RealCheckLedger</c>; defaults to
    /// <see langword="false"/> for canned/demo-data findings that predate the ledger.
    /// </summary>
    public bool IsVisual { get; init; }

    /// <summary>
    /// Gets the PDF location where the relevant field was found (or expected), or
    /// <see langword="null"/> when location information is not available (e.g. canned demo
    /// data, or a rule that does not report a locator).
    /// Wired by VLD-S4a from <c>RuleFinding.Locator</c>.
    /// </summary>
    public FieldLocator? Locator { get; init; }
}
