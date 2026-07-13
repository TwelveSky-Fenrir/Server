using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Fenrir.Cluster.WorldState;

/// <summary>
/// Fixed reference table binding each of the 11 Regular War (Zone049) instance indices to its map
/// (legacy "server number"). Built once, immutable forever after.
/// </summary>
/// <remarks>
/// Values mirror the legacy mapping exactly (index 0→49, 1→146, 2→149, 3→154, 4→157, 5→160, 6→120,
/// 7→121, 8→122, 9→295, 10→164). The legacy array had capacity 13 (<c>MAX_ZONE_49_TYPE_NUM</c>) but only
/// slots 0..10 are ever mapped; slots 11/12 were allocated and never used. Legacy indexed the raw
/// capacity-13 array without any bounds check (a latent out-of-bounds write); the Center authority here
/// only ever drives the 11 mapped instances, so an invalid index can never reach the world state.
/// <para>
/// Deliberately NOT shared with the shard-side <c>RegularWarMapCatalog</c> (which carries additional
/// per-map gameplay config): <c>Fenrir.Cluster</c> must never reference <c>Application.*</c>. The index→map
/// values are duplicated here as the single source of truth on the Center side.
/// </para>
/// </remarks>
public static class RegularWarInstanceMap
{
    private static readonly ImmutableArray<short> ServerByIndex =
        [49, 146, 149, 154, 157, 160, 120, 121, 122, 295, 164];

    private static readonly FrozenDictionary<short, int> IndexByServer =
        BuildReverseLookup();

    /// <summary>The number of mapped RW instances (11).</summary>
    public static int Count => ServerByIndex.Length;

    /// <summary>True when <paramref name="index"/> addresses one of the 11 mapped instances.</summary>
    public static bool IsValidIndex(int index) => index >= 0 && index < ServerByIndex.Length;

    /// <summary>The map id (legacy server number) hosting the RW instance at <paramref name="index"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="index"/> is not in <c>0..10</c>.</exception>
    public static short MapIdOf(int index)
    {
        if (!IsValidIndex(index))
            throw new ArgumentOutOfRangeException(
                nameof(index), index, $"Regular War instance index must be 0..{ServerByIndex.Length - 1}.");

        return ServerByIndex[index];
    }

    /// <summary>Reverse lookup: the instance index owning <paramref name="mapId"/>, if any.</summary>
    public static bool TryGetIndex(short mapId, out int index) => IndexByServer.TryGetValue(mapId, out index);

    private static FrozenDictionary<short, int> BuildReverseLookup()
    {
        var builder = new Dictionary<short, int>(ServerByIndex.Length);
        for (var i = 0; i < ServerByIndex.Length; i++)
            builder[ServerByIndex[i]] = i;

        return builder.ToFrozenDictionary();
    }
}
