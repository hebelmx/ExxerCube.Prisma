// Suppress XML doc warnings for SmartEnum static members.
#pragma warning disable CS1591
namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// SmartEnum for image enhancement filters.
/// </summary>
public sealed class ImageFilterType : EnumModel
{
    public static readonly ImageFilterType None = new(0, "None", "None");
    public static readonly ImageFilterType PilSimple = new(1, "PilSimple", "PIL Simple");
    public static readonly ImageFilterType OpenCvAdvanced = new(2, "OpenCvAdvanced", "OpenCV Advanced");
    public static readonly ImageFilterType Adaptive = new(3, "Adaptive", "Adaptive");
    public static readonly ImageFilterType Polynomial = new(4, "Polynomial", "Polynomial Enhancement");
    public static readonly ImageFilterType Unknown = new(-1, "Unknown", "Unknown");
    public static readonly ImageFilterType Other = new(999, "Other", "Other");

    public ImageFilterType() { }
    private ImageFilterType(int value, string name, string displayName) : base(value, name, displayName) { }

    public static ImageFilterType FromValue(int value) => FromValue<ImageFilterType>(value);
    public static ImageFilterType FromName(string name) => FromName<ImageFilterType>(name);
    public static implicit operator int(ImageFilterType value) => value.Value;
    public static implicit operator ImageFilterType(int value) => FromValue(value);
}
