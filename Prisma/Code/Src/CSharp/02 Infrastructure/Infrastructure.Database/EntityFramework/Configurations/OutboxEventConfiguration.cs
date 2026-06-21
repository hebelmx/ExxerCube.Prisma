// <copyright file="OutboxEventConfiguration.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Infrastructure.Database.EntityFramework.Configurations;

/// <summary>
/// Entity Framework Core configuration for <see cref="OutboxEvent"/>.
/// Configures the OutboxEvents table used by the reliability-outbox pattern (NFR14).
/// Schema applied via EnsureCreated / migrations — never via live DDL.
/// </summary>
public class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutboxEvent> builder)
    {
        builder.ToTable("OutboxEvents");

        builder.HasKey(o => o.OutboxEventId);

        builder.Property(o => o.OutboxEventId)
            .IsRequired();

        builder.Property(o => o.EventType)
            .HasMaxLength(200)
            .IsRequired();

        // Payload may carry a reasonably large JSON blob; 8000 chars covers all current event types.
        builder.Property(o => o.Payload)
            .HasMaxLength(8000)
            .IsRequired();

        builder.Property(o => o.OccurredAt)
            .IsRequired();

        builder.Property(o => o.ProcessedAt);

        builder.Property(o => o.RetryCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(o => o.LastError)
            .HasMaxLength(2000);

        builder.Property(o => o.DeadLetteredAt);

        // Primary scan path: WHERE ProcessedAt IS NULL AND DeadLetteredAt IS NULL
        builder.HasIndex(o => new { o.ProcessedAt, o.DeadLetteredAt })
            .HasDatabaseName("IX_OutboxEvents_ProcessedAt_DeadLetteredAt");

        // Secondary path: ORDER BY OccurredAt for FIFO retry ordering.
        builder.HasIndex(o => o.OccurredAt)
            .HasDatabaseName("IX_OutboxEvents_OccurredAt");
    }
}
