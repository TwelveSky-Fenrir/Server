using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Domain.Combat;

public static class KillFeedZoneCatalog
{
    public const short FfaMapNumber = PvpKillRewardZoneCatalog.FfaMapNumber;

    public const short Zone267MapNumber = 267;

    private static readonly FrozenSet<short> ExplicitLeaderboardBackedZoneIds =
        new[] { Zone267MapNumber, FfaMapNumber }.ToFrozenSet();

    public static bool HasLeaderboardStore(short mapId)
    {
        return RegularWarMapCatalog.TryGet(mapId, out _) || ExplicitLeaderboardBackedZoneIds.Contains(mapId);
    }

    public static bool IsFeedEnabled(short mapId)
    {
        return HasLeaderboardStore(mapId);
    }
}
