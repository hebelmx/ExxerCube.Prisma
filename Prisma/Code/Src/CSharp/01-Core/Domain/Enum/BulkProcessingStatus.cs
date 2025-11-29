// Suppress XML doc warnings for SmartEnum static members.
#pragma warning disable CS1591
namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// SmartEnum for bulk processing status.
/// </summary>
public sealed class BulkProcessingStatus : EnumModel
{
    public static readonly BulkProcessingStatus Pending = new(0, "Pending", "Pending");
    public static readonly BulkProcessingStatus ProcessingXml = new(1, "ProcessingXml", "Processing XML");
    public static readonly BulkProcessingStatus ProcessingOcr = new(2, "ProcessingOcr", "Processing OCR");
    public static readonly BulkProcessingStatus Comparing = new(3, "Comparing", "Comparing");
    public static readonly BulkProcessingStatus Complete = new(4, "Complete", "Complete");
    public static readonly BulkProcessingStatus Error = new(5, "Error", "Error");
    public static readonly BulkProcessingStatus Unknown = new(-1, "Unknown", "Unknown");
    public static readonly BulkProcessingStatus Other = new(999, "Other", "Other");

    public BulkProcessingStatus() { }
    private BulkProcessingStatus(int value, string name, string displayName) : base(value, name, displayName) { }

    public static BulkProcessingStatus FromValue(int value) => FromValue<BulkProcessingStatus>(value);
    public static BulkProcessingStatus FromName(string name) => FromName<BulkProcessingStatus>(name);
    public static implicit operator int(BulkProcessingStatus value) => value.Value;
    public static implicit operator BulkProcessingStatus(int value) => FromValue(value);
}
