// <copyright file="RequirementTypeDictionaryConfiguration.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Infrastructure.Database.EntityFramework.Configurations;

using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Entity Framework Core configuration for RequirementTypeDictionary.
/// Includes seed data for known CNBV requirement types (100-104, 999).
/// </summary>
public class RequirementTypeDictionaryConfiguration : IEntityTypeConfiguration<RequirementTypeDictionary>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RequirementTypeDictionary> builder)
    {
        builder.ToTable("RequirementTypeDictionary");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.DiscoveredAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(e => e.DiscoveredFromDocument)
            .HasMaxLength(500);

        builder.Property(e => e.KeywordPattern)
            .HasMaxLength(1000);

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(256);

        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        // Index for active type lookups (classification engine queries)
        builder.HasIndex(e => e.IsActive)
            .HasDatabaseName("IX_RequirementTypeDictionary_IsActive");

        // Seed data from RequirementType enum (legal research)
        builder.HasData(
            new RequirementTypeDictionary
            {
                Id = RequirementType.InformationRequest.Value,
                Name = RequirementType.InformationRequest.Name,
                DisplayName = RequirementType.InformationRequest.DisplayName,
                DiscoveredAt = new DateTime(2025, 1, 25, 0, 0, 0, DateTimeKind.Utc),
                KeywordPattern = "solicita información|estados de cuenta",
                IsActive = true,
                CreatedBy = "System",
                Notes = "Art. 142 LIC - Judicial/Fiscal/Administrative information requests",
            },
            new RequirementTypeDictionary
            {
                Id = RequirementType.Aseguramiento.Value,
                Name = RequirementType.Aseguramiento.Name,
                DisplayName = RequirementType.Aseguramiento.DisplayName,
                DiscoveredAt = new DateTime(2025, 1, 25, 0, 0, 0, DateTimeKind.Utc),
                KeywordPattern = "asegurar|bloquear|embargar",
                IsActive = true,
                CreatedBy = "System",
                Notes = "Art. 2(V)(b) - SAME DAY execution required",
            },
            new RequirementTypeDictionary
            {
                Id = RequirementType.Desbloqueo.Value,
                Name = RequirementType.Desbloqueo.Name,
                DisplayName = RequirementType.Desbloqueo.DisplayName,
                DiscoveredAt = new DateTime(2025, 1, 25, 0, 0, 0, DateTimeKind.Utc),
                KeywordPattern = "desbloquear|liberar",
                IsActive = true,
                CreatedBy = "System",
                Notes = "R29 Type 102 - Release of frozen funds",
            },
            new RequirementTypeDictionary
            {
                Id = RequirementType.Transferencia.Value,
                Name = RequirementType.Transferencia.Name,
                DisplayName = RequirementType.Transferencia.DisplayName,
                DiscoveredAt = new DateTime(2025, 1, 25, 0, 0, 0, DateTimeKind.Utc),
                KeywordPattern = "transferir.*CLABE|CLABE.*transferir",
                IsActive = true,
                CreatedBy = "System",
                Notes = "R29 Type 103 - Electronic transfer to government account",
            },
            new RequirementTypeDictionary
            {
                Id = RequirementType.SituacionFondos.Value,
                Name = RequirementType.SituacionFondos.Name,
                DisplayName = RequirementType.SituacionFondos.DisplayName,
                DiscoveredAt = new DateTime(2025, 1, 25, 0, 0, 0, DateTimeKind.Utc),
                KeywordPattern = "cheque de caja|poner a disposición",
                IsActive = true,
                CreatedBy = "System",
                Notes = "R29 Type 104 - Cashier's check to judicial authority",
            },
            new RequirementTypeDictionary
            {
                Id = RequirementType.Unknown.Value,
                Name = RequirementType.Unknown.Name,
                DisplayName = RequirementType.Unknown.DisplayName,
                DiscoveredAt = new DateTime(2025, 1, 25, 0, 0, 0, DateTimeKind.Utc),
                KeywordPattern = null,
                IsActive = true,
                CreatedBy = "System",
                Notes = "Fallback for unrecognized requirement types - triggers manual review",
            });
    }
}
