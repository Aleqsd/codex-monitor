namespace CodexMonitor;

// FIFO eviction preserves hot working sets without clearing every entry at the limit.
internal sealed class BoundedCache<TKey, TValue>(int capacity) where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> values = new();
    private readonly Queue<TKey> order = new();
    internal int Count => values.Count;
    internal bool TryGetValue(TKey key, out TValue value) => values.TryGetValue(key, out value!);
    internal TValue Add(TKey key, TValue value)
    {
        if (values.ContainsKey(key)) { values[key] = value; return value; }
        while (values.Count >= capacity) values.Remove(order.Dequeue());
        values.Add(key, value); order.Enqueue(key); return value;
    }
    internal void Clear() { values.Clear(); order.Clear(); }
}
