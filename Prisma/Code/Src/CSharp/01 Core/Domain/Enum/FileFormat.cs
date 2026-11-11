// Suppress XML doc warnings for SmartEnum static members.
#pragma warning disable CS1591
namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// SmartEnum for file formats.
/// </summary>
public sealed class FileFormat : EnumModel
{
    public static readonly FileFormat Pdf = new(0, "Pdf", "PDF");
    public static readonly FileFormat Xml = new(1, "Xml", "XML");
    public static readonly FileFormat Docx = new(2, "Docx", "DOCX");
    public static readonly FileFormat Zip = new(3, "Zip", "ZIP");
    public static readonly FileFormat Unknown = new(-1, "Unknown", "Unknown");
    public static readonly FileFormat Other = new(999, "Other", "Other");

    public FileFormat() { }
    private FileFormat(int value, string name, string displayName) : base(value, name, displayName) { }

    public static FileFormat FromValue(int value) => FromValue<FileFormat>(value);
    public static FileFormat FromName(string name) => FromName<FileFormat>(name);
    public static implicit operator int(FileFormat value) => value.Value;
    public static implicit operator FileFormat(int value) => FromValue(value);
}
