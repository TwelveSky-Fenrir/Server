using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Fenrir.Cluster.Center.WorldState;

public static class RegularWarInstanceMap
{
    private static readonly ImmutableArray<short> ServerByIndex =
        [49, 146, 149, 154, 157, 160, 120, 121, 122, 295, 164];

    private static readonly FrozenDictionary<short, int> IndexByServer =
        BuildReverseLookup();

    public static int Count => ServerByIndex.Length;

    public static bool IsValidIndex(int index)
    {
        return index >= 0 && index < ServerByIndex.Length;
    }

    public static short MapIdOf(int index)
    {
        if (!IsValidIndex(index))
            throw new ArgumentOutOfRangeException(
                nameof(index), index, $"Regular War instance index must be 0..{ServerByIndex.Length - 1}.");

        return ServerByIndex[index];
    }

    public static bool TryGetIndex(short mapId, out int index)
    {
        return IndexByServer.TryGetValue(mapId, out index);
    }

    private static FrozenDictionary<short, int> BuildReverseLookup()
    {
        var builder = new Dictionary<short, int>(ServerByIndex.Length);
        for (var i = 0; i < ServerByIndex.Length; i++)
            builder[ServerByIndex[i]] = i;

        return builder.ToFrozenDictionary();
    }
}
