namespace ExxerCube.Prisma.Infrastructure.Database.EntityFramework.Configurations;

using ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Entity Framework Core configuration for SLAStatus entity.
/// </summary>
public class SLAStatusConfiguration : IEntityTypeConfiguration<SLAStatus>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SLAStatus> builder)
    {
        builder.ToTable("SLAStatus");

        builder.HasKey(s => s.FileId);

        builder.Property(s => s.FileId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.IntakeDate)
            .IsRequired();

        builder.Property(s => s.Deadline)
            .IsRequired();

        builder.Property(s => s.DaysPlazo)
            .IsRequired();

        builder.Property(s => s.RemainingTime)
            .IsRequired()
            .HasConversion(
                v => v.Ticks,
                v => TimeSpan.FromTicks(v));

        builder.Property(s => s.IsAtRisk)
            .IsRequired();

        builder.Property(s => s.IsBreached)
            .IsRequired();

        builder.Property(s => s.EscalationLevel)
            .IsRequired()
            .HasEnumModelConversion();

        builder.Property(s => s.EscalatedAt);

        // Foreign key relationship to FileMetadata
        builder.HasOne<FileMetadata>()
            .WithOne()
            .HasForeignKey<SLAStatus>(s => s.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        builder.HasIndex(s => s.Deadline)
            .HasDatabaseName("IX_SLAStatus_Deadline");

        builder.HasIndex(s => s.IsAtRisk)
            .HasDatabaseName("IX_SLAStatus_IsAtRisk");

        builder.HasIndex(s => s.IsBreached)
            .HasDatabaseName("IX_SLAStatus_IsBreached");

        builder.HasIndex(s => s.EscalationLevel)
            .HasDatabaseName("IX_SLAStatus_EscalationLevel");
    }
}

