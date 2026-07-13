using System.Buffers.Binary;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class Zone051Zone053BroadcastResolver
{
    public const int Zone051RangeStart = 10;

    public const int Zone051RangeEnd = 18;

    public const int Zone053RangeStart = 19;

    public const int Zone053RangeEnd = 30;

    public static void ApplyZone051(Zone051Zone053SiegeState state, int selector, ReadOnlySpan<byte> data,
        ILogger logger)
    {
        var slot = ReadInt32(data, 0);
        if (!Zone051Zone053SiegeState.IsValidZone051Slot(slot))
        {
            logger.LogWarning("Zone051 selector {Selector} referenced out-of-range slot {Slot} -- ignored",
                selector, slot);
            return;
        }

        if (Zone051Zone053StateMap.TryMapZone051(selector, out var mapped))
            state.SetZone051(slot, mapped);
    }

    public static void ApplyZone053(Zone051Zone053SiegeState state, int selector, ReadOnlySpan<byte> data,
        ILogger logger)
    {
        var slot = ReadInt32(data, 0);
        if (!Zone051Zone053SiegeState.IsValidZone053Slot(slot))
        {
            logger.LogWarning("Zone053 selector {Selector} referenced out-of-range slot {Slot} -- ignored",
                selector, slot);
            return;
        }

        if (Zone051Zone053StateMap.TryMapZone053(selector, out var mapped))
            state.SetZone053(slot, mapped);
    }

    private static int ReadInt32(ReadOnlySpan<byte> data, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
    }
}
