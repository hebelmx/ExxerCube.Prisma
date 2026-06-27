using ExxerCube.Prisma.Veriqan.Domain.Enums;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Models;

/// <summary>
/// View-model for a QA analyst disposition record attached to a verification job.
/// Uses the real <see cref="DispositionAction"/> enum from <c>Veriqan.Domain</c>
/// so the values are consistent with what the pipeline persists.
/// </summary>
public sealed class DemoDisposition
{
    /// <summary>Gets or sets the unique identifier of the disposition record.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Gets or sets the verification job this disposition belongs to.</summary>
    public Guid JobId { get; init; }

    /// <summary>Gets or sets the check identifier being dispositioned (e.g. "CL-21"),
    /// or empty string when the disposition applies to the whole verdict.</summary>
    public string CheckId { get; init; } = string.Empty;

    /// <summary>Gets or sets the disposition action chosen by the analyst.</summary>
    public DispositionAction Action { get; init; }

    /// <summary>Gets or sets the free-text justification written by the analyst.</summary>
    public string Comment { get; init; } = string.Empty;

    /// <summary>Gets or sets the analyst's display name.</summary>
    public string AnalystName { get; init; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp when the disposition was recorded.</summary>
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
