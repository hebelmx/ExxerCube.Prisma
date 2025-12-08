namespace Prisma.HMI.Tests;

/// <summary>Notification queue interface.</summary>
public interface INotificationQueue
{
    int Count { get; }
    void Enqueue(Notification notification);
    Notification Dequeue();
}