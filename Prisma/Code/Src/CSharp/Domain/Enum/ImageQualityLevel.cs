// Suppress XML doc warnings for SmartEnum static members.
#pragma warning disable CS1591
namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// SmartEnum for document image quality levels.
/// </summary>
public sealed class ImageQualityLevel : EnumModel
{
    public static readonly ImageQualityLevel Unknown = new(-1, "Unknown", "Unknown");
    public static readonly ImageQualityLevel Q1_Poor = new(1, "Q1_Poor", "Q1 - Poor");
    public static readonly ImageQualityLevel Q2_MediumPoor = new(2, "Q2_MediumPoor", "Q2 - Medium Poor");
    public static readonly ImageQualityLevel Q3_Low = new(3, "Q3_Low", "Q3 - Low");
    public static readonly ImageQualityLevel Q4_VeryLow = new(4, "Q4_VeryLow", "Q4 - Very Low");
    public static readonly ImageQualityLevel Pristine = new(5, "Pristine", "Pristine");
    public static readonly ImageQualityLevel Other = new(999, "Other", "Other");

    public ImageQualityLevel() { }
    private ImageQualityLevel(int value, string name, string displayName) : base(value, name, displayName) { }

    public static ImageQualityLevel FromValue(int value) => FromValue<ImageQualityLevel>(value);
    public static ImageQualityLevel FromName(string name) => FromName<ImageQualityLevel>(name);
    public static implicit operator int(ImageQualityLevel value) => value.Value;
    public static implicit operator ImageQualityLevel(int value) => FromValue(value);
}
