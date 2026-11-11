// Suppress XML doc warnings for SmartEnum static members.
#pragma warning disable CS1591
namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// SmartEnum for SLA escalation stages.
/// </summary>
public sealed class EscalationLevel : EnumModel
{
    public static readonly EscalationLevel None = new(0, "None", "None");
    public static readonly EscalationLevel Warning = new(1, "Warning", "Warning");
    public static readonly EscalationLevel Critical = new(2, "Critical", "Critical");
    public static readonly EscalationLevel Breached = new(3, "Breached", "Breached");
    public static readonly EscalationLevel Unknown = new(-1, "Unknown", "Unknown");
    public static readonly EscalationLevel Other = new(999, "Other", "Other");

    public EscalationLevel()
    {
    }

    private EscalationLevel(int value, string name, string displayName)
        : base(value, name, displayName)
    {
    }

    public static EscalationLevel FromValue(int value) => FromValue<EscalationLevel>(value);

    public static EscalationLevel FromName(string name) => FromName<EscalationLevel>(name);

    public static implicit operator int(EscalationLevel value) => value.Value;

    public static implicit operator EscalationLevel(int value) => FromValue(value);
}
