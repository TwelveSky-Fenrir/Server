using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The three <c>MyUtil::WrapCheck</c> destination groups that are NOT one of the sixteen tribe-corridor
///     zones <see cref="TribeGuardCorridorCatalog" />/<see cref="TribeGuardCorridorGate" /> already model: the
///     win-zone-038 group, the rebirth-exact-count group, and the rebirth-at-least-one instanced group. All
///     three are static, boot-time, read-only lookup tables -- never varies at runtime, same posture as
///     <c>ZonePvpZoneCatalog</c>.
/// </summary>
/// <remarks>
///     Réf. C++ (A8-corridor-respawn contract, edge cases "win-zone-038 group", "rebirth-gated group",
///     "instanced group") : Server/ts25zone/S07_MyGame03.cpp:5840-5849 (win-zone-038 destinations 39, 74, 144,
///     145, 313) ; :5850-5923 (rebirth-gated destinations 241-249/292-294/311-312, each mapped to its own exact
///     required rebirth count) ; :5924-5941 (instanced destinations 325-330, live <c>#else</c> branch requiring
///     rebirth at least 1 -- the controlling macro <c>USE_INSTANCE</c> is defined unconditionally,
///     Server/Header/Protocol/DEFINE.h:67, outside the M33 build-variant block, so this is the branch every
///     build compiles ; the alternative "exactly rebirth 12" branch is dead code and is NOT modeled here).
/// </remarks>
public static class WrapCheckSpecialDestinationCatalog
{
    private static readonly FrozenSet<short> WinZone038Destinations =
        new short[] { 39, 74, 144, 145, 313 }.ToFrozenSet();

    private static readonly FrozenDictionary<short, int> RequiredRebirthCountByDestination =
        new Dictionary<short, int>
        {
            [241] = 1,
            [242] = 2,
            [243] = 3,
            [244] = 4,
            [245] = 5,
            [246] = 6,
            [311] = 6,
            [247] = 7,
            [248] = 8,
            [249] = 9,
            [292] = 10,
            [293] = 11,
            [294] = 12,
            [312] = 12
        }.ToFrozenDictionary();

    private static readonly FrozenSet<short> InstancedDestinations =
        new short[] { 325, 326, 327, 328, 329, 330 }.ToFrozenSet();

    /// <summary>True for the five destinations gated on "does the mover hold (or ally with the holder of) zone 38 right now".</summary>
    public static bool IsWinZone038Destination(short zoneId)
    {
        return WinZone038Destinations.Contains(zoneId);
    }

    /// <summary>The exact rebirth count this destination requires, if it belongs to the rebirth-gated group.</summary>
    public static bool TryGetRequiredRebirthCount(short zoneId, out int requiredRebirthCount)
    {
        return RequiredRebirthCountByDestination.TryGetValue(zoneId, out requiredRebirthCount);
    }

    /// <summary>True for the six instance-server destinations, gated on "has the mover rebirthed at least once".</summary>
    public static bool IsInstancedDestination(short zoneId)
    {
        return InstancedDestinations.Contains(zoneId);
    }
}
