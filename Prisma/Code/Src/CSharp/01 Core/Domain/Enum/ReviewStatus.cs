// Suppress XML doc warnings for SmartEnum static members.
#pragma warning disable CS1591
namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// SmartEnum for review case status.
/// </summary>
public sealed class ReviewStatus : EnumModel
{
    public static readonly ReviewStatus Pending = new(0, "Pending", "Pending");
    public static readonly ReviewStatus InProgress = new(1, "InProgress", "In Progress");
    public static readonly ReviewStatus Completed = new(2, "Completed", "Completed");
    public static readonly ReviewStatus Rejected = new(3, "Rejected", "Rejected");
    public static readonly ReviewStatus Unknown = new(-1, "Unknown", "Unknown");
    public static readonly ReviewStatus Other = new(999, "Other", "Other");

    public ReviewStatus() { }
    private ReviewStatus(int value, string name, string displayName) : base(value, name, displayName) { }

    public static ReviewStatus FromValue(int value) => FromValue<ReviewStatus>(value);
    public static ReviewStatus FromName(string name) => FromName<ReviewStatus>(name);
    public static implicit operator int(ReviewStatus value) => value.Value;
    public static implicit operator ReviewStatus(int value) => FromValue(value);
}
