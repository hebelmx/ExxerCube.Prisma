// Suppress XML doc warnings for SmartEnum static members.
#pragma warning disable CS1591
namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Primary classification bucket for regulatory documents.
/// </summary>
public sealed class ClassificationLevel1 : EnumModel
{
    public static readonly ClassificationLevel1 Unknown = new(-1, "Unknown", "Desconocido");
    public static readonly ClassificationLevel1 Aseguramiento = new(0, "Aseguramiento", "Aseguramiento");
    public static readonly ClassificationLevel1 Desembargo = new(1, "Desembargo", "Desembargo");
    public static readonly ClassificationLevel1 Documentacion = new(2, "Documentacion", "Documentación");
    public static readonly ClassificationLevel1 Informacion = new(3, "Informacion", "Información");
    public static readonly ClassificationLevel1 Transferencia = new(4, "Transferencia", "Transferencia");
    public static readonly ClassificationLevel1 OperacionesIlicitas = new(5, "OperacionesIlicitas", "Operaciones ilícitas");
    public static readonly ClassificationLevel1 Other = new(999, "Other", "Otro");

    public ClassificationLevel1()
    {
    }

    private ClassificationLevel1(int value, string name, string displayName)
        : base(value, name, displayName)
    {
    }

    public static ClassificationLevel1 FromValue(int value) => FromValue<ClassificationLevel1>(value);

    public static ClassificationLevel1 FromName(string name) => FromName<ClassificationLevel1>(name);

    public static implicit operator int(ClassificationLevel1 value) => value.Value;

    public static implicit operator ClassificationLevel1(int value) => FromValue(value);
}
