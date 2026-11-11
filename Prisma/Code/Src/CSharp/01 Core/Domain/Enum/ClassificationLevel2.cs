// Suppress XML doc warnings for SmartEnum static members.
#pragma warning disable CS1591
namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Secondary classification flavor for regulatory documents.
/// </summary>
public sealed class ClassificationLevel2 : EnumModel
{
    public static readonly ClassificationLevel2 Unknown = new(-1, "Unknown", "Desconocido");
    public static readonly ClassificationLevel2 Especial = new(0, "Especial", "Especial");
    public static readonly ClassificationLevel2 Judicial = new(1, "Judicial", "Judicial");
    public static readonly ClassificationLevel2 Hacendario = new(2, "Hacendario", "Hacendario");
    public static readonly ClassificationLevel2 Other = new(999, "Other", "Otro");

    public ClassificationLevel2()
    {
    }

    private ClassificationLevel2(int value, string name, string displayName)
        : base(value, name, displayName)
    {
    }

    public static ClassificationLevel2 FromValue(int value) => FromValue<ClassificationLevel2>(value);

    public static ClassificationLevel2 FromName(string name) => FromName<ClassificationLevel2>(name);

    public static implicit operator int(ClassificationLevel2 value) => value.Value;

    public static implicit operator ClassificationLevel2(int value) => FromValue(value);
}
