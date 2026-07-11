using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Combat;

public readonly record struct PvpKillRewardZoneRuntimeState(
    bool SymbolBattleActive = false,
    bool Map195TimeEventActive = false,
    bool Map38DropEnabled = false,
    bool Map38DailyMissionEnabled = false)
{
    public static readonly PvpKillRewardZoneRuntimeState Inactive = default;
}

public static class PvpKillExtendedRewardZones
{
    public const short DtmZoneId = 38;

    private static readonly FrozenSet<short> SymbolBattleZoneIds = new short[]
    {
        2, 3, 4, 7, 8, 9, 12, 13, 14, 141, 142, 143
    }.ToFrozenSet();

    private static readonly FrozenSet<short> RegularWarDropZoneIds = new short[]
    {
        49, 146, 149, 154, 157, 160, 120, 121, 122,
        51, 147, 150, 155, 158, 161,
        53, 148, 151, 152, 153, 156, 159, 162, 163, 164,
        295, 296
    }.ToFrozenSet();

    private static readonly FrozenSet<short> Map195EventZoneIds = new short[] { 195, 196 }.ToFrozenSet();

    private static readonly PvpKillZoneRewardProfile FullReward = new(true, true, true, true, 0);

    public static PvpKillZoneRewardProfile? TryResolve(short zoneId, bool isStunTrigger,
        PvpKillRewardZoneRuntimeState runtime)
    {
        if (SymbolBattleZoneIds.Contains(zoneId))
            return runtime.SymbolBattleActive ? FullReward : PvpKillZoneRewardProfile.None;

        if (zoneId == DtmZoneId)
            return new PvpKillZoneRewardProfile(true, true, runtime.Map38DropEnabled,
                runtime.Map38DailyMissionEnabled, 0);

        if (RegularWarDropZoneIds.Contains(zoneId))
            return FullReward;

        if (Map195EventZoneIds.Contains(zoneId))
            return runtime.Map195TimeEventActive ? FullReward : PvpKillZoneRewardProfile.None;

        return null;
    }
}
