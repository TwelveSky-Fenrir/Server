using System.Collections.Frozen;
using Fenrir.Domain.Game.Stats.Context;

namespace Fenrir.Domain.Game.Stats;

public static partial class StatCalculator
{
    private static readonly FrozenDictionary<int, int> StellarDamageDefenseBonusById = new Dictionary<int, int>
    {
        [76527] = 50, [76528] = 150, [76529] = 200, [76530] = 300, [76531] = 350, [76532] = 400, [76533] = 450,
        [76534] = 500, [76535] = 550, [76536] = 600, [76537] = 650, [76538] = 700, [76539] = 750, [76540] = 900,
        [93500] = 50, [93501] = 150, [93502] = 200, [93503] = 300, [93504] = 350, [93505] = 400, [93506] = 450,
        [93507] = 500, [93508] = 550, [93509] = 600, [93510] = 650, [93511] = 700, [93512] = 750, [93513] = 900
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<int, int> StellarElementBonusById = new Dictionary<int, int>
    {
        [76527] = 5, [76528] = 10, [76529] = 15, [76530] = 20, [76531] = 25, [76532] = 30, [76533] = 35,
        [76534] = 40, [76535] = 45, [76536] = 50, [76537] = 55, [76538] = 60, [76539] = 65, [76540] = 70,
        [93500] = 5, [93501] = 10, [93502] = 15, [93503] = 20, [93504] = 25, [93505] = 30, [93506] = 35,
        [93507] = 40, [93508] = 45, [93509] = 50, [93510] = 55, [93511] = 60, [93512] = 65, [93513] = 70
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<int, int> StellarCriticalDefenceBonusById = new Dictionary<int, int>
    {
        [76530] = 1, [76531] = 1, [76532] = 2, [76533] = 2, [76534] = 3, [76535] = 3,
        [76536] = 4, [76537] = 4, [76538] = 5, [76539] = 5, [76540] = 6,
        [93503] = 1, [93504] = 1, [93505] = 2, [93506] = 2, [93507] = 3, [93508] = 3,
        [93509] = 4, [93510] = 4, [93511] = 5, [93512] = 5, [93513] = 6
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
        return 0;
    }
}
