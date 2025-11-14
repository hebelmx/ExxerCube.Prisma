using System.Collections.Generic;

namespace ExxerCube.Prisma.Domain.Entities;

/// <summary>
/// Consolidated metadata record combining data from XML, DOCX, and PDF sources.
/// </summary>
public class UnifiedMetadataRecord
{
    /// <summary>
    /// Gets or sets the regulatory case information (expediente).
    /// </summary>
    public Expediente? Expediente { get; set; }

    /// <summary>
    /// Gets or sets the list of persons involved (nullable - may be created in later stories).
    /// </summary>
    public List<object>? Personas { get; set; }

    /// <summary>
    /// Gets or sets the regulatory directive information (oficio) (nullable - may be created in later stories).
    /// </summary>
    public object? Oficio { get; set; }

    /// <summary>
    /// Gets or sets the field extraction results (extends existing entity).
    /// </summary>
    public ExtractedFields? ExtractedFields { get; set; }

    /// <summary>
    /// Gets or sets the document classification result.
    /// </summary>
    public ClassificationResult? Classification { get; set; }

    /// <summary>
    /// Gets or sets the field matching results across sources.
    /// </summary>
    public MatchedFields? MatchedFields { get; set; }

    /// <summary>
    /// Gets or sets the PDF requirement summary (nullable - may be created in later stories).
    /// </summary>
    public object? RequirementSummary { get; set; }

    /// <summary>
    /// Gets or sets the SLA tracking information (nullable - may be created in later stories).
    /// </summary>
    public object? SlaStatus { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnifiedMetadataRecord"/> class.
    /// </summary>
    public UnifiedMetadataRecord()
    {
    }
}

