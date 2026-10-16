using System.Collections.Concurrent;
using Wayni.Deudores.Core.Messaging;

namespace Wayni.Deudores.Admin.Web.Services;

/// <summary>Keeps the last completion webhooks received from the Processor (in memory, newest first).</summary>
public sealed class NotificationStore
{
    private const int Capacity = 50;
    private readonly ConcurrentQueue<ReceivedNotification> _items = new();

    public void Add(ImportCompletedNotification notification)
    {
        _items.Enqueue(new ReceivedNotification(DateTimeOffset.UtcNow, notification));
        while (_items.Count > Capacity && _items.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<ReceivedNotification> Latest() => _items.Reverse().ToList();
}

public sealed record ReceivedNotification(DateTimeOffset ReceivedAt, ImportCompletedNotification Notification);
