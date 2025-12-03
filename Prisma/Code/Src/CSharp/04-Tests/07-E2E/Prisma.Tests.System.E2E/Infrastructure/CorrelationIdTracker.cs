using System.Collections.Concurrent;
using System.Text;

namespace Prisma.Tests.System.E2E.Infrastructure;

/// <summary>
/// Tracks correlation IDs across the E2E pipeline to validate they're preserved.
/// </summary>
public sealed class CorrelationIdTracker
{
    private readonly ConcurrentDictionary<string, Guid> _stageCorrelationIds = new();
    private readonly Guid _expectedCorrelationId;

    /// <summary>
    /// Creates a tracker for a specific correlation ID.
    /// </summary>
    public CorrelationIdTracker(Guid expectedCorrelationId)
    {
        _expectedCorrelationId = expectedCorrelationId;
    }

    /// <summary>
    /// Records the correlation ID seen at a specific pipeline stage.
    /// </summary>
    public void RecordStage(string stageName, Guid correlationId)
    {
        _stageCorrelationIds[stageName] = correlationId;
    }

    /// <summary>
    /// Gets all stages where the correlation ID was recorded.
    /// </summary>
    public IReadOnlyDictionary<string, Guid> StageCorrelationIds =>
        _stageCorrelationIds.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    /// <summary>
    /// Validates that all recorded stages have the expected correlation ID.
    /// </summary>
    public (bool IsValid, string[] InvalidStages) Validate()
    {
        var invalidStages = _stageCorrelationIds
            .Where(kvp => kvp.Value != _expectedCorrelationId)
            .Select(kvp => kvp.Key)
            .ToArray();

        return (invalidStages.Length == 0, invalidStages);
    }

    /// <summary>
    /// Gets a summary of correlation ID tracking.
    /// </summary>
    public string GetSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Expected Correlation ID: {_expectedCorrelationId}");
        sb.AppendLine($"Stages Tracked: {_stageCorrelationIds.Count}");
        sb.AppendLine();

        foreach (var (stage, correlationId) in _stageCorrelationIds.OrderBy(kvp => kvp.Key))
        {
            var match = correlationId == _expectedCorrelationId ? "✓" : "✗";
            sb.AppendLine($"  {match} {stage}: {correlationId}");
        }

        return sb.ToString();
    }
}

/// <summary>
/// Stage names for E2E correlation ID tracking.
/// </summary>
public static class PipelineStages
{
    public const string Ingestion = "Ingestion";
    public const string DocumentDownloaded = "DocumentDownloaded";
    public const string QualityAnalysis = "QualityAnalysis";
    public const string OcrProcessing = "OcrProcessing";
    public const string Classification = "Classification";
    public const string ProcessingCompleted = "ProcessingCompleted";
    public const string DatabaseManifest = "DatabaseManifest";
    public const string DatabaseAudit = "DatabaseAudit";
    public const string ExportMetadata = "ExportMetadata";
    public const string HmiNotification = "HmiNotification";
}
