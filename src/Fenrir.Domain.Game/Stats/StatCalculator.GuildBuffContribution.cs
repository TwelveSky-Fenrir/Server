using Fenrir.Domain.Game.Stats.Context;

namespace Fenrir.Domain.Game.Stats;

public static partial class StatCalculator
{
    private const float GuildBuffTwoGradeMultiplier = 1.1f;
    private const int GuildBuffAttackSuccessType = 0;
    private const int GuildBuffAttackBlockType = 1;
    private const int GuildBuffCriticalType = 2;
    private const int GuildBuffDefensePowerType = 4;
    private const int GuildBuffDefensePowerThreshold = 500;
    private const int GuildBuffDefensePowerFlatBonus = 500;

    private static int ApplyGuildBuffAttackSuccess(int value, ZoneContext zone)
    {
        return IsGuildBuffActive(zone, GuildBuffAttackSuccessType)
            ? (int)(value * GuildBuffTwoGradeMultiplier)
            : value;
    }

    private static int ApplyGuildBuffAttackBlock(int value, ZoneContext zone)
    {
        return IsGuildBuffActive(zone, GuildBuffAttackBlockType)
            ? (int)(value * GuildBuffTwoGradeMultiplier)
            : value;
    }

    private static int ApplyGuildBuffCritical(int value, ZoneContext zone)
    {
        return IsGuildBuffActive(zone, GuildBuffCriticalType) ? value + 1 : value;
    }

    private static int ApplyGuildBuffDefensePower(int value, ZoneContext zone)
    {
        if (!IsGuildBuffActive(zone, GuildBuffDefensePowerType))
            return value;

        return value >= GuildBuffDefensePowerThreshold ? value + GuildBuffDefensePowerFlatBonus : value * 2;
    }

    private static bool IsGuildBuffActive(ZoneContext zone, int type)
    {
        return zone.GuildBuffActive && zone.GuildBuffType == type;
    }
}
