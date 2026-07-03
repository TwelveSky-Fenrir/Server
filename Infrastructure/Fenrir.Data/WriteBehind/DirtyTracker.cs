using System.Collections.Concurrent;

namespace Fenrir.Data.WriteBehind;

/// <summary>
///     Thread-safe "what changed since the last flush" accumulator, keyed by entity id (architecture
///     reference §10.5). Any thread may call <see cref="MarkDirty" /> at any time, including concurrently
///     with a <see cref="DrainAll" /> in progress on another thread.
/// </summary>
public sealed class DirtyTracker<TKey> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, DirtyFlags> _entries = new();

    // Maintained by hand via Interlocked instead of forwarding to ConcurrentDictionary<,>.Count, whose real
    // implementation takes every internal partition lock to produce an exact count -- not O(1). The flusher
    // polls Count every 250 ms (§10.5) from a thread that never touches _entries otherwise, so a lock-free
    // read here avoids a recurring, avoidable contention point against concurrent MarkDirty calls from
    // zone-tick threads.
    private int _count;

    /// <summary>
    ///     Number of entities currently marked dirty. Cheap to read without draining -- the flusher polls
    ///     this to detect the entity-count threshold (§10.5) without waiting out the full flush interval.
    /// </summary>
    public int Count => Volatile.Read(ref _count);

    /// <summary>Ors <paramref name="flags" /> into the entry for <paramref name="key" />, creating it if absent.</summary>
    public void MarkDirty(TKey key, DirtyFlags flags)
    {
        // Hand-rolled instead of a single AddOrUpdate call: AddOrUpdate's add/update factory delegates are
        // documented to potentially run more than once per logical call under contention, which would make an
        // Interlocked.Increment placed inside one of them double-count. TryAdd/TryUpdate are each a single
        // atomic attempt with an unambiguous true/false result, so _count only moves on a call that actually
        // won the race to add a brand-new key.
        while (true)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                var merged = existing | flags;
                if (merged == existing || _entries.TryUpdate(key, merged, existing))
                    return; // merged in place (or already a no-op) -- no new key, no count change

                continue; // lost a race with a concurrent MarkDirty/DrainAll on this key -- retry
            }

            if (_entries.TryAdd(key, flags))
            {
                Interlocked.Increment(ref _count);
                return;
            }

            // Another thread added this key between our TryGetValue and TryAdd -- loop back and merge into it.
        }
    }

    /// <summary>Atomically empties the tracker and returns everything it held at the moment of removal.</summary>
    /// <remarks>
    ///     Removal is per-key via <see cref="ConcurrentDictionary{TKey,TValue}.TryRemove(TKey, out TValue)" />
    ///     rather than a wholesale swap of the backing dictionary instance, precisely so a concurrent
    ///     <see cref="MarkDirty" /> can never be lost: if it lands on a key before this method removes it, its
    ///     flags are merged in and drained here; if it lands after (or targets a brand-new key), the entry
    ///     simply survives to be picked up by the next drain. Either way nothing is lost, and nothing is
    ///     drained twice, since every key is removed exactly once.
    /// </remarks>
    public IReadOnlyDictionary<TKey, DirtyFlags> DrainAll()
    {
        var drained = new Dictionary<TKey, DirtyFlags>(Count);

        foreach (var key in _entries.Keys)
            if (_entries.TryRemove(key, out var flags))
            {
                drained[key] = flags;
                Interlocked.Decrement(ref _count);
            }

        return drained;
    }
}
