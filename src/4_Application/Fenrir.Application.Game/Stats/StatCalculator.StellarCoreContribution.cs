using System.Collections.Frozen;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    private static readonly FrozenDictionary<int, int> StellarDamageDefenseBonusById = new Dictionary<int, int>
    {
        [76527] = 50, [76528] = 150, [76529] = 200, [76530] = 300, [76531] = 350, [76532] = 400, [76533] = 450,
        [76534] = 500, [76535] = 550, [76536] = 600, [76537] = 650, [76538] = 700, [76539] = 750, [76540] = 900,
        [93500] = 125, [93501] = 375, [93502] = 500, [93503] = 750, [93504] = 875, [93505] = 1000, [93506] = 1125,
        [93507] = 1250, [93508] = 1375, [93509] = 1500, [93510] = 1625, [93511] = 1750, [93512] = 1875, [93513] = 2250
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<int, int> StellarElementBonusById = new Dictionary<int, int>
    {
        [76527] = 5, [76528] = 10, [76529] = 15, [76530] = 20, [76531] = 25, [76532] = 30, [76533] = 35,
        [76534] = 40, [76535] = 45, [76536] = 50, [76537] = 55, [76538] = 60, [76539] = 65, [76540] = 70,
        [93500] = 125, [93501] = 250, [93502] = 375, [93503] = 500, [93504] = 625, [93505] = 750, [93506] = 875,
        [93507] = 1000, [93508] = 1125, [93509] = 1250, [93510] = 1375, [93511] = 1500, [93512] = 1625, [93513] = 1750
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<int, int> StellarCriticalDefenceBonusById = new Dictionary<int, int>
    {
        [76530] = 1, [76531] = 1, [76532] = 2, [76533] = 2, [76534] = 3, [76535] = 3,
        [76536] = 4, [76537] = 4, [76538] = 5, [76539] = 5, [76540] = 6,
        [93503] = 1, [93504] = 1, [93505] = 2, [93506] = 2, [93507] = 3, [93508] = 3,
        [93509] = 4, [93510] = 4, [93511] = 6, [93512] = 8, [93513] = 10
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<int, int> StellarMaxLifeBonusById = new Dictionary<int, int>
    {
        [93500] = 250, [93501] = 500, [93502] = 750, [93503] = 1000, [93504] = 1250, [93505] = 1500, [93506] = 1750,
        [93507] = 2000, [93508] = 2250, [93509] = 2500, [93510] = 2750, [93511] = 3000, [93512] = 3250, [93513] = 3500
    }.ToFrozenDictionary();

    private static int LookupStellarBonus(FrozenDictionary<int, int> table, int coreId)
    {
        return table.TryGetValue(coreId, out var value) ? value : 0;
    }

    public static int StellarCoreAttackPowerContribution(CosmeticContext cosmetic)
    {
        return LookupStellarBonus(StellarDamageDefenseBonusById, cosmetic.StellarCoreNumber);
    }

    public static int StellarCoreDefensePowerContribution(CosmeticContext cosmetic)
    {
        return LookupStellarBonus(StellarDamageDefenseBonusById, cosmetic.StellarCoreNumber);
    }

    public static int StellarCoreCriticalDefenceContribution(CosmeticContext cosmetic)
    {
        return LookupStellarBonus(StellarCriticalDefenceBonusById, cosmetic.StellarCoreNumber);
    }

    public static int StellarCoreElementAttackContribution(CosmeticContext cosmetic)
    {
        return LookupStellarBonus(StellarElementBonusById, cosmetic.StellarCoreNumber);
    }

    public static int StellarCoreElementDefenseContribution(CosmeticContext cosmetic)
    {
        return LookupStellarBonus(StellarElementBonusById, cosmetic.StellarCoreNumber);
    }

    public static int StellarCoreMaxLifeContribution(CosmeticContext cosmetic)
    {
        return LookupStellarBonus(StellarMaxLifeBonusById, cosmetic.StellarCoreNumber);
    }
}
