using System.Numerics;

namespace CodexMonitor;

public sealed record NotificationItem(long Id, MonitoredThread Task, float Age, float Duration, long? HistoryId = null);

/// <summary>Bounded queue. Waiting items only start their timer when they become visible.</summary>
public sealed class NotificationQueue
{
    private readonly object gate = new();
    private readonly List<NotificationItem> items = [];
    private long sequence;
    public const int Capacity = 20;

    public void Add(MonitoredThread task, float duration, long? historyId = null, bool first = false)
    {
        lock (gate)
        {
            var replacement = items.FindIndex(item => item.Task.Id == task.Id);
            var item = new NotificationItem(++sequence, task, 0,
                NotificationGeometry.FiniteClamp(duration, 4, 15, 7), historyId);
            if (replacement >= 0) { items[replacement] = item; return; }
            if (items.Count >= Capacity) items.RemoveAt(3);
            if (first) items.Insert(0, item);
            else items.Add(item);
        }
    }

    public NotificationItem[] Visible(int count = 3)
    {
        lock (gate) return items.Take(Math.Clamp(count, 1, 3)).ToArray();
    }

    public void Advance(float seconds, int visibleCount, long? pausedId = null)
    {
        if (!float.IsFinite(seconds) || seconds < 0) return;
        lock (gate)
        {
            for (var index = 0; index < Math.Min(items.Count, Math.Clamp(visibleCount, 1, 3)); index++)
                if (items[index].Id != pausedId) items[index] = items[index] with { Age = items[index].Age + seconds };
            items.RemoveAll(item => item.Age >= item.Duration);
        }
    }

    public void Dismiss(long id) { lock (gate) items.RemoveAll(item => item.Id == id); }
    public void Clear() { lock (gate) items.Clear(); }
    public NotificationItem[] Drain() { lock (gate) { var result = items.ToArray(); items.Clear(); return result; } }
    public NotificationItem[] Snapshot() { lock (gate) return items.ToArray(); }
    public void RestartTimers()
    {
        lock (gate)
            for (var index = 0; index < items.Count; index++) items[index] = items[index] with { Age = 0 };
    }
    public void Reconcile(MonitorSnapshot snapshot)
    {
        lock (gate) items.RemoveAll(item => item.HistoryId != null && item.Task.NeedsAttention
            && !snapshot.Threads.Any(task => task.Id == item.Task.Id && task.IsObserved && (item.Task.State == "question"
                ? item.Task.QuestionIds.Intersect(task.QuestionIds).Any() : task.State == item.Task.State)));
    }
    public int Count { get { lock (gate) return items.Count; } }
}

public enum StackDirection { Auto, Down, Up }

public static class NotificationGeometry
{
    public static float FiniteClamp(float value, float min, float max, float fallback) =>
        float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    // The anchor is the horizontal center of the first notification's top edge.
    // Notifications near the bottom stack upwards, keeping the whole group visible.
    public static Vector2 Place(Vector2 anchor, Vector2 origin, Vector2 viewport,
        Vector2 size, int index, int count, float gap, float margin = 12, StackDirection direction = StackDirection.Auto,
        Vector2 offset = default)
    {
        var x = origin.X + FiniteClamp(anchor.X, 0, 1, 0.5f) * viewport.X - size.X / 2 + offset.X;
        var y = origin.Y + FiniteClamp(anchor.Y, 0, 1, 0.22f) * viewport.Y + offset.Y;
        var upwards = direction == StackDirection.Up || (direction == StackDirection.Auto && anchor.Y > 0.5f);
        var span = (Math.Clamp(count, 1, 3) - 1) * (size.Y + gap);
        var minY = origin.Y + margin + (upwards ? span : 0);
        var maxY = origin.Y + viewport.Y - margin - size.Y - (upwards ? 0 : span);
        x = Math.Clamp(x, origin.X + margin, Math.Max(origin.X + margin, origin.X + viewport.X - margin - size.X));
        y = Math.Clamp(y, minY, Math.Max(minY, maxY));
        return new Vector2(x, y + index * (size.Y + gap) * (upwards ? -1 : 1));
    }

    public static Vector2 AnchorAfterDrag(Vector2 topLeft, Vector2 size, Vector2 delta, Vector2 origin, Vector2 viewport)
    {
        var center = topLeft + new Vector2(size.X / 2, 0) + delta - origin;
        return new Vector2(FiniteClamp(center.X / viewport.X, 0, 1, 0.5f), FiniteClamp(center.Y / viewport.Y, 0, 1, 0.22f));
    }

    // Snap the first toast's center/top edge. Offsets are reset by the caller when dragging.
    public static Vector2 Snap(Vector2 anchor, Vector2 viewport, Vector2 size, float grid, float threshold = 10)
    {
        var pixel = anchor * viewport;
        if (grid > 0) pixel = new Vector2(MathF.Round(pixel.X / grid) * grid, MathF.Round(pixel.Y / grid) * grid);
        float Nearest(float value, params float[] guides)
        {
            var nearest = guides.MinBy(guide => Math.Abs(value - guide));
            return Math.Abs(value - nearest) <= threshold ? nearest : value;
        }
        pixel.X = Nearest(pixel.X, size.X / 2 + 12, viewport.X / 2, viewport.X - size.X / 2 - 12);
        pixel.Y = Nearest(pixel.Y, 12, viewport.Y / 2, viewport.Y - size.Y - 12);
        return new Vector2(FiniteClamp(pixel.X / viewport.X, 0, 1, 0.5f), FiniteClamp(pixel.Y / viewport.Y, 0, 1, 0.22f));
    }
}
