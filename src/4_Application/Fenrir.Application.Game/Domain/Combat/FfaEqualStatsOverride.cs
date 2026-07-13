using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Combat;

public static class FfaEqualStatsOverride
{
    private const int FixedAttackPower = 45000;
    private const int FixedDefensePower = 30000;
    private const int FixedAttackSuccess = 30000;
    private const int FixedAttackBlock = 5000;
    private const int FixedElementAttackPower = 1000;
    private const int FixedElementDefensePower = 500;
    private const int FixedCritical = 100;
    private const int FixedCriticalDefence = 65;
    private const int FixedLuck = 300;

    public static bool IsEqualStatsZone(short zoneId)
    {
        return zoneId == PvpKillRewardZoneCatalog.FfaMapNumber;
    }

    public static EffectiveStats Apply(short zoneId, EffectiveStats realStats)
    {
        if (!IsEqualStatsZone(zoneId))
            return realStats;

        return realStats with
        {
            AttackPower = FixedAttackPower,
            DefensePower = FixedDefensePower,
            AttackSuccess = FixedAttackSuccess,
            AttackBlock = FixedAttackBlock,
            ElementAttackPower = FixedElementAttackPower,
            ElementDefensePower = FixedElementDefensePower,
            Critical = FixedCritical,
            CriticalDefence = FixedCriticalDefence,
            Luck = FixedLuck
        };
    }
}
