using System.Collections.Concurrent;

namespace Fenrir.Data.WriteBehind;

public sealed class DirtyTracker<TKey> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, DirtyFlags> _entries = new();

    private int _count;

    public int Count => Volatile.Read(ref _count);

    public void MarkDirty(TKey key, DirtyFlags flags)
    {
        while (true)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                var merged = existing | flags;
                if (merged == existing || _entries.TryUpdate(key, merged, existing))
                    return;

                continue;
            }

            if (!_entries.TryAdd(key, flags)) continue;
            Interlocked.Increment(ref _count);
            return;
        }
    }

    public IReadOnlyDictionary<TKey, DirtyFlags> DrainAll()
    {
        var drained = new Dictionary<TKey, DirtyFlags>(Math.Max(0, Count));

        foreach (var key in _entries.Keys)
            if (_entries.TryRemove(key, out var flags))
            {
                drained[key] = flags;
                Interlocked.Decrement(ref _count);
            }

        return drained;
    }
}
