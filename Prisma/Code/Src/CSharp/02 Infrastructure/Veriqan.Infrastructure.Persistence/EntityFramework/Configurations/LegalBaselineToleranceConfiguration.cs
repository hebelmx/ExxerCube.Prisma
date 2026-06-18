using ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Crypto;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.EntityFramework.Configurations;

/// <summary>
/// EF Core fluent configuration for <see cref="LegalBaselineToleranceRecord"/>.
/// All numeric columns store AES-encrypted ciphertext (Base64) via
/// <see cref="AesEncryptedDecimalConverter"/>.
/// </summary>
internal sealed class LegalBaselineToleranceConfiguration : IEntityTypeConfiguration<LegalBaselineToleranceRecord>
{
    private readonly AesEncryptedDecimalConverter _converter;

    /// <summary>
    /// Initializes a new instance of <see cref="LegalBaselineToleranceConfiguration"/>.
    /// </summary>
    /// <param name="converter">The AES value converter shared across all encrypted decimal columns.</param>
    public LegalBaselineToleranceConfiguration(AesEncryptedDecimalConverter converter)
    {
        _converter = converter;
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<LegalBaselineToleranceRecord> builder)
    {
        builder.ToTable("LegalBaselineTolerances");

        builder.HasKey(r => r.CheckId);

        builder.Property(r => r.CheckId)
            .HasMaxLength(50)
            .IsRequired()
            .ValueGeneratedNever();

        // Numeric columns are persisted as encrypted Base64 strings.
        // nvarchar(500) provides headroom for the IV+ciphertext Base64 of a decimal value.
        builder.Property(r => r.LegalDefault)
            .HasColumnType("nvarchar(500)")
            .HasConversion(_converter)
            .IsRequired();

        builder.Property(r => r.Min)
            .HasColumnType("nvarchar(500)")
            .HasConversion(_converter)
            .IsRequired();

        builder.Property(r => r.Max)
            .HasColumnType("nvarchar(500)")
            .HasConversion(_converter)
            .IsRequired();
    }
}
