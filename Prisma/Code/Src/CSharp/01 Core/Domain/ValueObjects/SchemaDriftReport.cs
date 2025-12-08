using System;
using System.Collections.Generic;
using System.Linq;

namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents a report of schema drift between source data and template definition.
/// Schema drift occurs when:
/// - New fields appear in source data that aren't in the template
/// - Required fields are missing from source data
/// - Fields are renamed (detected via fuzzy matching)
/// </summary>
public sealed class SchemaDriftReport
{
    /// <summary>
    /// Gets the template ID that was analyzed.
    /// </summary>
    public string TemplateId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the template type (Excel, XML, DOCX).
    /// </summary>
    public string TemplateType { get; init; } = string.Empty;

    /// <summary>
    /// Gets the template version that was analyzed.
    /// </summary>
    public string TemplateVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when drift was detected.
    /// </summary>
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets whether schema drift was detected.
    /// </summary>
    public bool HasDrift => NewFields.Count > 0 || MissingFields.Count > 0 || RenamedFields.Count > 0;

    /// <summary>
    /// Gets the severity level of the drift.
    /// - None: No drift detected
    /// - Low: Only new optional fields detected
    /// - Medium: Required fields missing or renamed fields detected
    /// - High: Critical required fields missing
    /// </summary>
    public DriftSeverity Severity { get; init; } = DriftSeverity.None;

    /// <summary>
    /// Gets new fields found in source data that aren't mapped in the template.
    /// These are candidates for adding to the template.
    /// </summary>
    public List<NewFieldInfo> NewFields { get; init; } = new();

    /// <summary>
    /// Gets fields that are required in the template but missing from source data.
    /// This may indicate the bank changed their schema.
    /// </summary>
    public List<MissingFieldInfo> MissingFields { get; init; } = new();

    /// <summary>
    /// Gets fields that may have been renamed (detected via fuzzy matching).
    /// </summary>
    public List<RenamedFieldInfo> RenamedFields { get; init; } = new();

    /// <summary>
    /// Gets a human-readable summary of the drift.
    /// </summary>
    public string Summary
    {
        get
        {
            if (!HasDrift)
                return "No schema drift detected.";

            var parts = new List<string>();

            if (NewFields.Count > 0)
                parts.Add($"{NewFields.Count} new field(s)");

            if (MissingFields.Count > 0)
                parts.Add($"{MissingFields.Count} missing field(s)");

            if (RenamedFields.Count > 0)
                parts.Add($"{RenamedFields.Count} renamed field(s)");

            return $"Schema drift detected: {string.Join(", ", parts)}";
        }
    }

    /// <summary>
    /// Gets the total number of drift issues.
    /// </summary>
    public int TotalDriftCount => NewFields.Count + MissingFields.Count + RenamedFields.Count;
}