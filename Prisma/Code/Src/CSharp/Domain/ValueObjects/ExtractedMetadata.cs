using System;
using ExxerCube.Prisma.Domain.Entities;

namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents extracted metadata from regulatory documents (XML, DOCX, PDF).
/// </summary>
public class ExtractedMetadata
{
    /// <summary>
    /// Gets or sets the expediente information extracted from the document.
    /// </summary>
    public Expediente? Expediente { get; set; }

    /// <summary>
    /// Gets or sets the extracted fields (extends existing ExtractedFields concept).
    /// </summary>
    public ExtractedFields? ExtractedFields { get; set; }

    /// <summary>
    /// Gets or sets the RFC values extracted from the document.
    /// </summary>
    public string[]? RfcValues { get; set; }

    /// <summary>
    /// Gets or sets the names extracted from the document.
    /// </summary>
    public string[]? Names { get; set; }

    /// <summary>
    /// Gets or sets the dates extracted from the document.
    /// </summary>
    public DateTime[]? Dates { get; set; }

    /// <summary>
    /// Gets or sets the legal references extracted from the document.
    /// </summary>
    public string[]? LegalReferences { get; set; }

    /// <summary>
    /// Gets or sets additional metadata as a dictionary.
    /// </summary>
    public System.Collections.Generic.Dictionary<string, object>? AdditionalMetadata { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractedMetadata"/> class.
    /// </summary>
    public ExtractedMetadata()
    {
    }
}

