using System.Collections.Frozen;

namespace Fenrir.Domain.Game.Stats;

public static partial class StatCalculator
{
    public const short FreeForAllZoneNumber = 335;

    public const int FreeForAllMaxLife = 100000;

    public const int FreeForAllMaxMana = 30000;


    private static readonly FrozenSet<short> BalanceStatZoneNumbers =
        new short[] { 19, 20, 21, 34, 49, 120, 154, 175, 176, 177, 190, 191, 192, 193 }.ToFrozenSet();

    public static bool IsFreeForAllZone(short zoneNumber)
    {
        return zoneNumber == FreeForAllZoneNumber;
    }

    public static int ApplyFreeForAllMaxLife(int computedMaxLife, short zoneNumber)
    {
        return IsFreeForAllZone(zoneNumber) ? FreeForAllMaxLife : computedMaxLife;
    }

    public static int ApplyFreeForAllMaxMana(int computedMaxMana, short zoneNumber)
    {
        return IsFreeForAllZone(zoneNumber) ? FreeForAllMaxMana : computedMaxMana;
    }


    public static bool IsBalanceStatZone(short zoneNumber)
    {
        return BalanceStatZoneNumbers.Contains(zoneNumber);
    }

    public static int BalanceLevelTerm(int baseLevel)
    {
        return baseLevel switch
        {
            <= 112 => 112,
            <= 145 => 145,
            > 156 => baseLevel,
            _ => 156
        };
    }

    public static int BalanceVitality(int baseLevel)
    {
        return baseLevel switch { <= 112 => 336, <= 145 => 842, > 156 => 1, _ => 1420 };
    }

    public static int BalanceStrength(int baseLevel)
    {
        return baseLevel switch { <= 112 => 351, <= 145 => 886, > 156 => 1, _ => 1411 };
    }

    public static (int Vitality, int Strength, int Intelligence, int Dexterity) BalanceAttributes(int baseLevel)
    {
        return (BalanceVitality(baseLevel), BalanceStrength(baseLevel), 1, 1);
    }
}
