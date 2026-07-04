using System.Collections.Concurrent;

namespace Fenrir.Data.WriteBehind;

/// <summary>
///     Thread-safe "what changed since the last flush" accumulator. MarkDirty may run concurrently with a DrainAll in
///     progress.
/// </summary>
public sealed class DirtyTracker<TKey> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, DirtyFlags> _entries = new();

    // Hand-maintained via Interlocked: ConcurrentDictionary<,>.Count takes every partition lock (not O(1)); the flusher polls this every 250ms from another thread, so a lock-free read avoids contention with concurrent MarkDirty.
    private int _count;

    /// <summary>Number of entities currently marked dirty; cheap to read without draining.</summary>
    public int Count => Volatile.Read(ref _count);

    /// <summary>Ors <paramref name="flags" /> into the entry for <paramref name="key" />, creating it if absent.</summary>
    public void MarkDirty(TKey key, DirtyFlags flags)
    {
        // Not AddOrUpdate: its factory delegates can run more than once under contention, which would double-count an Interlocked.Increment inside them. TryAdd/TryUpdate give an unambiguous single atomic attempt.
        while (true)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                var merged = existing | flags;
                if (merged == existing || _entries.TryUpdate(key, merged, existing))
                    return; // merged in place (or already a no-op) -- no new key, no count change

                continue; // lost a race with a concurrent MarkDirty/DrainAll on this key -- retry
            }

            if (!_entries.TryAdd(key, flags)) continue;
            Interlocked.Increment(ref _count);
            return;

            // Another thread added this key between our TryGetValue and TryAdd -- loop back and merge into it.
        }
    }

    /// <summary>Atomically empties the tracker and returns everything it held at the moment of removal.</summary>
    /// <remarks>
    ///     Per-key TryRemove, not a dictionary swap: a concurrent MarkDirty either merges in before removal (drained
    ///     here) or survives to the next drain -- never lost, never double-drained.
    /// </remarks>
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
