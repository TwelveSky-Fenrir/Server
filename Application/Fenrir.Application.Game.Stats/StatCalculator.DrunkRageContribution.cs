using System.Collections.Frozen;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{

        public static readonly FrozenSet<int> DrunkPotionIds = new[] { 878, 879, 880, 881, 882 }.ToFrozenSet();

        public static readonly FrozenDictionary<int, DrunkEffect> DrunkEffectsById = new Dictionary<int, DrunkEffect>
    {
        [878] = new(90, 110),
        [879] = new(AttackPowerPercent: 80, DefensePowerPercent: 200),
        [880] = new(CriticalPercent: 105, CriticalDefencePercent: 90),
        [881] = new(AttackSuccessPercent: 80),
        [882] = new(ElementAttackPercent: 130, ElementDefensePercent: 50)
    }.ToFrozenDictionary();

        public static readonly FrozenDictionary<int, int> RageBuffPercentByGauge = new Dictionary<int, int>
    {
        [1] = 105,
        [2] = 107,
        [3] = 109,
        [4] = 111,
        [5] = 114,
        [6] = 117,
        [7] = 120,
        [8] = 125,
        [9] = 130,
        [10] = 140
    }.ToFrozenDictionary();

    private static bool RageBuffStateActive => false;

        public static int ScaleByPercent(int value, int percent)
    {
        return percent == 100 ? value : (int)((long)value * percent / 100);
    }

        public static DrunkEffect? ResolveDrunkEffect(int drunkStateId)
    {
        return DrunkEffectsById.TryGetValue(drunkStateId, out var effect) ? effect : null;
    }

        public static int ResolveRageBuffPercent(int gauge)
    {
        return RageBuffPercentByGauge.TryGetValue(gauge, out var percent) ? percent : 100;
    }


        public static int ApplyDrunkMaxLife(int maxLife, ZoneContext zone)
    {
        return DrunkEffectsById.TryGetValue(zone.DrunkStateId, out var e)
            ? ScaleByPercent(maxLife, e.MaxLifePercent)
            : maxLife;
    }

        public static int ApplyDrunkCriticalDefence(int criticalDefence, ZoneContext zone)
    {
        return DrunkEffectsById.TryGetValue(zone.DrunkStateId, out var e)
            ? ScaleByPercent(criticalDefence, e.CriticalDefencePercent)
            : criticalDefence;
    }


        public static int ApplyDrunkAttackPower(int attackPower, ZoneContext zone)
    {
        return DrunkEffectsById.TryGetValue(zone.DrunkStateId, out var e)
            ? ScaleByPercent(attackPower, e.AttackPowerPercent)
            : attackPower;
    }

        public static int ApplyDrunkDefensePower(int defensePower, ZoneContext zone)
    {
        return DrunkEffectsById.TryGetValue(zone.DrunkStateId, out var e)
            ? ScaleByPercent(defensePower, e.DefensePowerPercent)
            : defensePower;
    }

        public static int ApplyDrunkCritical(int critical, ZoneContext zone)
    {
        return DrunkEffectsById.TryGetValue(zone.DrunkStateId, out var e)
            ? ScaleByPercent(critical, e.CriticalPercent)
            : critical;
    }

        public static int ApplyDrunkAttackSuccess(int attackSuccess, ZoneContext zone)
    {
        return DrunkEffectsById.TryGetValue(zone.DrunkStateId, out var e)
            ? ScaleByPercent(attackSuccess, e.AttackSuccessPercent)
            : attackSuccess;
    }

        public static int ApplyDrunkElementAttack(int elementAttack, ZoneContext zone)
    {
        return DrunkEffectsById.TryGetValue(zone.DrunkStateId, out var e)
            ? ScaleByPercent(elementAttack, e.ElementAttackPercent)
            : elementAttack;
    }

        public static int ApplyDrunkElementDefense(int elementDefense, ZoneContext zone)
    {
        return DrunkEffectsById.TryGetValue(zone.DrunkStateId, out var e)
            ? ScaleByPercent(elementDefense, e.ElementDefensePercent)
            : elementDefense;
    }


        public static int ApplyRageAttackMultiplier(int baseAttack, ZoneContext zone)
    {
        if (!RageBuffStateActive)
            return baseAttack;

        return ScaleByPercent(baseAttack, ResolveRageBuffPercent(zone.RageGauge));
    }
}

public readonly record struct DrunkEffect(
    int MaxLifePercent = 100,
    int AttackPowerPercent = 100,
    int DefensePowerPercent = 100,
    int CriticalPercent = 100,
    int CriticalDefencePercent = 100,
    int AttackSuccessPercent = 100,
    int ElementAttackPercent = 100,
    int ElementDefensePercent = 100);
