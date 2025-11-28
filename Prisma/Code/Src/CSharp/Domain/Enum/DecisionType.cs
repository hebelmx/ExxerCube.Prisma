// Suppress XML doc warnings for SmartEnum static members.
#pragma warning disable CS1591
namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// SmartEnum for manual review decision outcomes.
/// </summary>
public sealed class DecisionType : EnumModel
{
    public static readonly DecisionType Approve = new(0, "Approve", "Approve");
    public static readonly DecisionType Reject = new(1, "Reject", "Reject");
    public static readonly DecisionType RequestMoreInfo = new(2, "RequestMoreInfo", "Request more info");
    public static readonly DecisionType Unknown = new(-1, "Unknown", "Unknown");
    public static readonly DecisionType Other = new(999, "Other", "Other");

    public DecisionType() { }
    private DecisionType(int value, string name, string displayName) : base(value, name, displayName) { }

    public static DecisionType FromValue(int value) => FromValue<DecisionType>(value);
    public static DecisionType FromName(string name) => FromName<DecisionType>(name);
    public static implicit operator int(DecisionType value) => value.Value;
    public static implicit operator DecisionType(int value) => FromValue(value);
}
