// <copyright file="ILookupEntity.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Marker interface for lookup table entities used in ModelEnum/SmartEnum patterns.
/// Classes implementing this interface are considered lookup/reference data
/// and are eligible for DbSet&lt;T&gt; registration in the EF Core DbContext.
/// </summary>
/// <remarks>
/// <para>
/// This interface serves as a marker to identify entities that represent
/// lookup tables, reference data, or smart-enum-style entities. These are
/// typically small, relatively static tables that provide enumerated values
/// with additional metadata.
/// </para>
/// <para>
/// Examples of entities that should implement ILookupEntity:
/// - RequirementType, SLAStatus, ReviewStatus, etc.
/// </para>
/// <para>
/// This interface is used by EF Core model validation to ensure only
/// valid lookup entities are registered as DbSet&lt;T&gt; in the DbContext,
/// preventing accidental registration of value objects or DTOs.
/// </para>
/// </remarks>
public interface ILookupEntity
{
    // Marker interface - no members required
}
