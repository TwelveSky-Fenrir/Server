using System.Collections.Frozen;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    private static readonly FrozenDictionary<int, RankBuffEffect> RankBuffEffectsByTier =
        new Dictionary<int, RankBuffEffect>
        {
            [1] = new(RankBuffStat.DefensePower, 1000),
            [2] = new(RankBuffStat.ElementDefensePower, 1000),
            [3] = new(RankBuffStat.ElementAttackPower, 1000),
            [4] = new(RankBuffStat.AttackBlock, 1000),
            [5] = new(RankBuffStat.AttackSuccess, 1000),
            [6] = new(RankBuffStat.MaxLife, 1000),
            [7] = new(RankBuffStat.AttackPower, 500)
        }.ToFrozenDictionary();

    private static readonly FrozenSet<short> RankBuffSuppressedZones = ((short[])[124, 335]).ToFrozenSet();

    private static int RankBuffBonusFor(RankBuffStat stat, ZoneContext zone)
    {
        if (zone.RankBuffType == 0)
            return 0;
        if (RankBuffSuppressedZones.Contains(zone.ZoneNumber))
            return 0;

        return RankBuffEffectsByTier.TryGetValue(zone.RankBuffType, out var effect) && effect.Stat == stat
            ? effect.Magnitude
            : 0;
    }


    public static int RankBuffMaxLifeBonus(ZoneContext zone)
    {
        return RankBuffBonusFor(RankBuffStat.MaxLife, zone);
    }

    public static int RankBuffDefensePowerBonus(ZoneContext zone)
    {
        return RankBuffBonusFor(RankBuffStat.DefensePower, zone);
    }

    public static int RankBuffElementDefensePowerBonus(ZoneContext zone)
    {
        return RankBuffBonusFor(RankBuffStat.ElementDefensePower, zone);
    }

    public static int RankBuffElementAttackPowerBonus(ZoneContext zone)
    {
        return RankBuffBonusFor(RankBuffStat.ElementAttackPower, zone);
    }

    public static int RankBuffAttackBlockBonus(ZoneContext zone)
    {
        return RankBuffBonusFor(RankBuffStat.AttackBlock, zone);
    }

    public static int RankBuffAttackSuccessBonus(ZoneContext zone)
    {
        return RankBuffBonusFor(RankBuffStat.AttackSuccess, zone);
    }

    public static int RankBuffAttackPowerBonus(ZoneContext zone)
    {
        return RankBuffBonusFor(RankBuffStat.AttackPower, zone);
    }

    private enum RankBuffStat
    {
        None = 0,
        DefensePower = 1,
        ElementDefensePower = 2,
        ElementAttackPower = 3,
        AttackBlock = 4,
        AttackSuccess = 5,
        MaxLife = 6,
        AttackPower = 7
    }

    private readonly record struct RankBuffEffect(RankBuffStat Stat, int Magnitude);
}
