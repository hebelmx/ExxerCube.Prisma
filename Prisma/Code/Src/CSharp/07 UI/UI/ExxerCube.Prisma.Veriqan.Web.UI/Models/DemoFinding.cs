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
}
