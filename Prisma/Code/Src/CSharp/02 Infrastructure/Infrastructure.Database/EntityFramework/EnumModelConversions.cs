using ExxerCube.Prisma.Domain.Enum;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExxerCube.Prisma.Infrastructure.Database.EntityFramework;

/// <summary>
/// Extension helpers for mapping EnumModel-derived SmartEnums to ints in EF Core.
/// </summary>
public static class EnumModelConversions
{
    /// <summary>
    /// Registers a conversion between an EnumModel SmartEnum and its integer value.
    /// </summary>
    public static PropertyBuilder<TEnum> HasEnumModelConversion<TEnum>(this PropertyBuilder<TEnum> builder)
        where TEnum : EnumModel, new()
    {
        return builder.HasConversion(
            v => v.Value,
            v => EnumModel.FromValue<TEnum>(v));
    }
}
