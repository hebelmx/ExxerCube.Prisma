using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ExxerCube.Prisma.Infrastructure.Database.EntityFramework.Configurations;

/// <summary>
/// Entity Framework Core configuration for ReviewDecision entity.
/// </summary>
public class ReviewDecisionConfiguration : IEntityTypeConfiguration<ReviewDecision>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ReviewDecision> builder)
    {
        var reviewReasonComparer = new ValueComparer<ReviewReason>(
            (l, r) => (l ?? ReviewReason.Unknown).Value == (r ?? ReviewReason.Unknown).Value,
            v => (v ?? ReviewReason.Unknown).Value.GetHashCode(),
            v => ReviewReason.FromValue((v ?? ReviewReason.Unknown).Value));

        var decisionTypeComparer = new ValueComparer<DecisionType>(
            (l, r) => (l ?? DecisionType.Unknown).Value == (r ?? DecisionType.Unknown).Value,
            v => (v ?? DecisionType.Unknown).Value.GetHashCode(),
            v => DecisionType.FromValue((v ?? DecisionType.Unknown).Value));

        builder.ToTable("ReviewDecisions");

        builder.HasKey(d => d.DecisionId);

        builder.Property(d => d.DecisionId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.CaseId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.DecisionType)
            .IsRequired()
            .HasConversion(
                v => v.Value,
                v => DecisionType.FromValue(v))
            .Metadata.SetValueComparer(decisionTypeComparer);

        builder.Property(d => d.ReviewerId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.ReviewedAt)
            .IsRequired();

        builder.Property(d => d.Notes)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(d => d.ReviewReason)
            .IsRequired()
            .HasConversion(
                v => v.Value,
                v => ReviewReason.FromValue(v))
            .Metadata.SetValueComparer(reviewReasonComparer);

        // Store OverriddenFields as JSON
        builder.Property(d => d.OverriddenFields)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>())
            .HasMaxLength(4000);

        // Store OverriddenClassification as JSON
        builder.Property(d => d.OverriddenClassification)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<ClassificationResult>(v, (JsonSerializerOptions?)null))
            .HasMaxLength(2000);

        // Foreign key relationship to ReviewCase
        builder.HasOne<ReviewCase>()
            .WithMany()
            .HasForeignKey(d => d.CaseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        builder.HasIndex(d => d.CaseId)
            .HasDatabaseName("IX_ReviewDecisions_CaseId");

        builder.HasIndex(d => d.ReviewerId)
            .HasDatabaseName("IX_ReviewDecisions_ReviewerId");

        builder.HasIndex(d => d.ReviewedAt)
            .HasDatabaseName("IX_ReviewDecisions_ReviewedAt");
    }
}

