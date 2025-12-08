// Suppress XML doc warnings for SmartEnum static members.
#pragma warning disable CS1591
namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// SmartEnum for audit action types.
/// </summary>
public sealed class AuditActionType : EnumModel
{
    public static readonly AuditActionType Download = new(0, "Download", "Download");
    public static readonly AuditActionType Classification = new(1, "Classification", "Classification");
    public static readonly AuditActionType Move = new(2, "Move", "Move");
    public static readonly AuditActionType Extraction = new(3, "Extraction", "Extraction");
    public static readonly AuditActionType Review = new(4, "Review", "Review");
    public static readonly AuditActionType Export = new(5, "Export", "Export");
    public static readonly AuditActionType Escalation = new(6, "Escalation", "Escalation");
    public static readonly AuditActionType Unknown = new(-1, "Unknown", "Unknown");
    public static readonly AuditActionType Other = new(999, "Other", "Other");

    public AuditActionType() { }
    private AuditActionType(int value, string name, string displayName) : base(value, name, displayName) { }

    public static AuditActionType FromValue(int value) => FromValue<AuditActionType>(value);
    public static AuditActionType FromName(string name) => FromName<AuditActionType>(name);
    public static implicit operator int(AuditActionType value) => value.Value;
    public static implicit operator AuditActionType(int value) => FromValue(value);
}
