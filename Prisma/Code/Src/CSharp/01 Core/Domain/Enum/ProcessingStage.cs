// Suppress XML doc warnings for SmartEnum static members.
#pragma warning disable CS1591
namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// SmartEnum for pipeline processing stages.
/// </summary>
public sealed class ProcessingStage : EnumModel
{
    public static readonly ProcessingStage Ingestion = new(0, "Ingestion", "Ingestion");
    public static readonly ProcessingStage Extraction = new(1, "Extraction", "Extraction");
    public static readonly ProcessingStage DecisionLogic = new(2, "DecisionLogic", "Decision Logic");
    public static readonly ProcessingStage Export = new(3, "Export", "Export");
    public static readonly ProcessingStage Unknown = new(-1, "Unknown", "Unknown");
    public static readonly ProcessingStage Other = new(999, "Other", "Other");

    public ProcessingStage() { }
    private ProcessingStage(int value, string name, string displayName) : base(value, name, displayName) { }

    public static ProcessingStage FromValue(int value) => FromValue<ProcessingStage>(value);
    public static ProcessingStage FromName(string name) => FromName<ProcessingStage>(name);
    public static implicit operator int(ProcessingStage value) => value.Value;
    public static implicit operator ProcessingStage(int value) => FromValue(value);
}
