namespace Fenrir.Application.Game.Domain.Combat;

public readonly record struct PvpKillZoneRewardProfile(
    bool GrantContributionPoints,
    bool GrantExperience,
    bool GrantDrop,
    bool GrantDailyMissionProgress,
    int HeroPointAmount)
{
    public static readonly PvpKillZoneRewardProfile None = new(false, false, false, false, 0);
}

public static class PvpKillRewardZoneCatalog
{

        public const short FfaMapNumber = 335;

        public const int FfaHeroPointAmount = 2;

        public const int HeroPointMinimumCombinedLevel = 0;

        private static readonly short[] UnconditionalFullRewardZoneIds = [194, 267, 268, 269];

        private static readonly short[] CityZoneIds = [1, 6, 11, 140];

        public static PvpKillZoneRewardProfile Resolve(short zoneId, bool isStunTrigger)
    {
        if (zoneId == FfaMapNumber)
            return new PvpKillZoneRewardProfile(
                false,
                false,
                true,
                false,
                FfaHeroPointAmount);

        if (Array.IndexOf(UnconditionalFullRewardZoneIds, zoneId) >= 0)
            return new PvpKillZoneRewardProfile(true, true, true, true, 0);

        if (Array.IndexOf(CityZoneIds, zoneId) >= 0)
            return new PvpKillZoneRewardProfile(!isStunTrigger, true, true, true, 0);

        var grantsAnything = !isStunTrigger;
        return new PvpKillZoneRewardProfile(grantsAnything, grantsAnything, grantsAnything, grantsAnything, 0);
    }
}
