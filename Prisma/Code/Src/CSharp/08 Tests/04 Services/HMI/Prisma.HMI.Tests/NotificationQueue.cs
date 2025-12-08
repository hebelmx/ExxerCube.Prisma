namespace ExxerCube.Prisma.HMI.Tests;

/// <summary>Simple FIFO notification queue implementation.</summary>
public sealed class NotificationQueue : INotificationQueue
{
    private readonly Queue<Notification> _queue = new();

    public int Count => _queue.Count;

    public void Enqueue(Notification notification)
    {
        _queue.Enqueue(notification);
    }

    public Notification Dequeue()
    {
        return _queue.Dequeue();
    }
}