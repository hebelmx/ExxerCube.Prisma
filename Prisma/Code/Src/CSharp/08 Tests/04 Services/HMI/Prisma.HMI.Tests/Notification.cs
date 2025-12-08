namespace ExxerCube.Prisma.HMI.Tests;

/// <summary>Notification display model.</summary>
public sealed record Notification(
    string Title,
    string Message,
    NotificationSeverity Severity,
    DateTimeOffset Timestamp);