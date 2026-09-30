// <copyright file="RequirementTypeDictionary.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Entities;

using ExxerCube.Prisma.Domain.Enum.LookUpTable;
using ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Database entity for dynamically discovered CNBV requirement types.
/// Allows system to evolve without code changes when new legal requirements emerge.
/// Seeded with known types (100-104) from RequirementType enum.
/// </summary>
public class RequirementTypeDictionary : ILookupEntity
{
    /// <summary>
    /// Gets or sets the CNBV official type code (100-999).
    /// Primary key. Matches RequirementType.Value.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the internal name (English).
    /// Examples: "Judicial", "Aseguramiento", "Desbloqueo".
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the official Spanish name per CNBV regulations.
    /// Examples: "Solicitud de Información", "Aseguramiento/Bloqueo".
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the timestamp when this type was first discovered.
    /// For seeded types, this is the system deployment date.
    /// For dynamic types, this is when classification engine first encountered it.
    /// </summary>
    public DateTime DiscoveredAt { get; set; }

    /// <summary>
    /// Gets or sets the document that triggered discovery of this type (if dynamic).
    /// Null for seeded types (100-104, 999).
    /// File path or document identifier for dynamically discovered types.
    /// </summary>
    public string? DiscoveredFromDocument { get; set; }

    /// <summary>
    /// Gets or sets the regex pattern for keyword-based classification.
    /// Examples: "solicita información|estados de cuenta", "asegurar|bloquear|embargar".
    /// Used by classification engine to identify requirement type from document text.
    /// </summary>
    public string? KeywordPattern { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this type is currently active.
    /// False for deprecated or obsolete requirement types.
    /// Classification engine ignores inactive types.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the user who created this dictionary entry (for dynamic types).
    /// Null for seeded types.
    /// AspNetUsers.UserName for manually added types.
    /// "System" for auto-discovered types pending review.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets additional notes about this requirement type.
    /// Legal references, processing notes, special handling instructions.
    /// </summary>
    public string? Notes { get; set; }
}
