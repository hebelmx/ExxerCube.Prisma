namespace ExxerCube.Prisma.Infrastructure.Database.EntityFramework.Configurations;

/// <summary>
/// Entity Framework Core configuration for Persona entity.
/// </summary>
public class PersonaConfiguration : IEntityTypeConfiguration<Persona>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Persona> builder)
    {
        builder.ToTable("Persona");

        builder.HasKey(p => p.ParteId);

        builder.Property(p => p.ParteId)
            .IsRequired();

        builder.Property(p => p.Caracter)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.PersonaTipo)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.Paterno)
            .HasMaxLength(200);

        builder.Property(p => p.Materno)
            .HasMaxLength(200);

        builder.Property(p => p.Nombre)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Rfc)
            .HasMaxLength(13);

        builder.Property(p => p.Relacion)
            .HasMaxLength(200);

        builder.Property(p => p.Domicilio)
            .HasMaxLength(500);

        builder.Property(p => p.Complementarios)
            .HasMaxLength(1000);

        // Store RFC variants as JSON in database
        builder.Property(p => p.RfcVariants)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .HasMaxLength(2000);

        // Index for RFC lookup performance
        builder.HasIndex(p => p.Rfc)
            .HasDatabaseName("IX_Persona_Rfc");
    }
}

