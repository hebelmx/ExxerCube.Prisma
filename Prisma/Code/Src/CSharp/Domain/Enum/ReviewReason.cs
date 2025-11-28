// Suppress XML doc warnings for SmartEnum static members.
#pragma warning disable CS1591
namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// SmartEnum for manual review reasons.
/// </summary>
public sealed class ReviewReason : EnumModel
{
    public static readonly ReviewReason Unknown = new(-1, "Unknown", "Unknown");
    public static readonly ReviewReason LowConfidence = new(0, "LowConfidence", "Low confidence");
    public static readonly ReviewReason AmbiguousClassification = new(1, "AmbiguousClassification", "Ambiguous classification");
    public static readonly ReviewReason ExtractionError = new(2, "ExtractionError", "Extraction error");
    public static readonly ReviewReason Other = new(999, "Other", "Other");

    public ReviewReason() { }
    private ReviewReason(int value, string name, string displayName) : base(value, name, displayName) { }

    public static ReviewReason FromValue(int value) => FromValue<ReviewReason>(value);
    public static ReviewReason FromName(string name) => FromName<ReviewReason>(name);
    public static implicit operator int(ReviewReason value) => value.Value;
    public static implicit operator ReviewReason(int value) => FromValue(value);
}
