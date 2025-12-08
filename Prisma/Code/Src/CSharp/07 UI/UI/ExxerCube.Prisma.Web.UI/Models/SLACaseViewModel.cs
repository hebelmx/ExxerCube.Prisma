using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Web.UI.Models;

/// <summary>
/// View model combining SLA status with file metadata for display in the SLA Dashboard.
/// </summary>
public class SLACaseViewModel
{
    /// <summary>
    /// Gets or sets the SLA status information.
    /// </summary>
    public SLAStatus SLAStatus { get; set; } = null!;

    /// <summary>
    /// Gets or sets the file metadata (nullable if file metadata is not found).
    /// </summary>
    public FileMetadata? FileMetadata { get; set; }

    /// <summary>
    /// Gets the file name from metadata or falls back to FileId.
    /// </summary>
    public string DisplayFileName => FileMetadata?.FileName ?? SLAStatus.FileId;

    /// <summary>
    /// Gets the formatted time remaining as a string (e.g., "2h 15m" or "Breached").
    /// </summary>
    public string FormattedTimeRemaining
    {
        get
        {
            if (SLAStatus.IsBreached)
                return "Breached";

            var remaining = SLAStatus.RemainingTime;
            if (remaining.TotalHours >= 1)
            {
                var hours = (int)remaining.TotalHours;
                var minutes = remaining.Minutes;
                return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
            }

            if (remaining.TotalMinutes >= 1)
            {
                return $"{(int)remaining.TotalMinutes}m";
            }

            return "< 1m";
        }
    }

    /// <summary>
    /// Gets the color for the time remaining based on escalation level.
    /// </summary>
    public MudBlazor.Color TimeRemainingColor => SLAStatus.EscalationLevel.Value switch
    {
        3 => MudBlazor.Color.Dark,   // Breached
        2 => MudBlazor.Color.Error,  // Critical
        1 => MudBlazor.Color.Warning, // Warning
        _ => MudBlazor.Color.Success
    };

    /// <summary>
    /// Gets the color for the escalation level chip.
    /// </summary>
    public MudBlazor.Color EscalationColor => SLAStatus.EscalationLevel.Value switch
    {
        3 => MudBlazor.Color.Dark,
        2 => MudBlazor.Color.Error,
        1 => MudBlazor.Color.Warning,
        _ => MudBlazor.Color.Success
    };

    /// <summary>
    /// Gets the text for the escalation level chip.
    /// </summary>
    public string EscalationText => SLAStatus.EscalationLevel.Name switch
    {
        nameof(EscalationLevel.Breached) => "Breached",
        nameof(EscalationLevel.Critical) => "Critical",
        nameof(EscalationLevel.Warning) => "Warning",
        _ => "None"
    };

    /// <summary>
    /// Gets the status chip color.
    /// </summary>
    public MudBlazor.Color StatusColor => SLAStatus.IsBreached ? MudBlazor.Color.Error : MudBlazor.Color.Success;

    /// <summary>
    /// Gets the status chip text.
    /// </summary>
    public string StatusText => SLAStatus.IsBreached ? "Breached" : "Active";
}
