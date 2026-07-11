namespace Fenrir.Application.Game.Stats;

public enum DecorationStatKind
{

        MaxLife = 1,

        MaxMana = 2,

        AttackPower = 3,

        DefensePower = 4,

        AttackBlock = 6,

        ElementDefensePower = 8
}

public static partial class StatCalculator
{


        public static int ReturnIUEffectValue(int effectSort, int itemSort, int itemLevel)
    {
        if (ResolveIuEffectCoefficients(effectSort, itemSort) is not { } coefficients)
            return 0;

        if (itemLevel >= 146)
            return 0;

        var level = (float)itemLevel;
        var (rampBase, pivot, step) = level switch
        {
            < 100f => (0f, 45f, 0.10f),
            < 113f => (6f, 100f, 0.20f),
            _ => (8f, 113f, 0.50f)
        };

        var r = rampBase + (level - pivot) * step;
        return (int)(coefficients.Base + r * coefficients.K);
    }

        public static int IUEffectSlotContribution(int effectSort, int itemSort, int itemLevel, int iuPointCount)
    {
        return ReturnIUEffectValue(effectSort, itemSort, itemLevel) * iuPointCount;
    }

        private static (float Base, float K)? ResolveIuEffectCoefficients(int effectSort, int itemSort)
    {
        return (effectSort, itemSort) switch
        {
            (1, 4 or >= 13 and <= 21) => (14.34f, 0.72f),

            (2, 8) => (2.00f, 0.10f),
            (2, 9) => (6.36f, 0.32f),
            (2, 10) => (1.82f, 0.09f),
            (2, 12) => (0.91f, 0.05f),

            (3, 10) => (13.36f, 0.67f),
            (3, >= 13 and <= 21) => (5.73f, 0.29f),

            (4, 9) => (0.95f, 0.05f),
            (4, 12) => (2.23f, 0.11f),

            (5, 11) => (2.00f, 0.26f),

            (6, 7) => (1.00f, 0.13f),

            _ => null
        };
    }


        public static bool IsDecorationItem(int itemType, int equipInfoCategory)
    {
        return itemType == 5 && equipInfoCategory is 11 or 12 or 13 or 14;
    }

        public static int DecorationStatContribution(DecorationStatKind stat, EquippedItemSlot?[] bySlot)
    {
        var total = 0;
        for (var d = 9; d <= 12; d++)
        {
            if (bySlot[d] is not { } deco) continue;
            if (!IsDecorationItem(deco.Item.Type, deco.Item.EquipInfo2)) continue;

            var decoPacked = PackDecorationUpgradeOctets(deco.Enchant, 0, 0, 0);
            total += ReturnNewStat(stat, decoPacked);
        }

        return total;
    }

        public static int ReturnNewStat(DecorationStatKind stat, int packedUpgradeValue)
    {
        if (packedUpgradeValue == 0)
            return 0;

        var octetIs = (sbyte)packedUpgradeValue;
        var octetIu = (sbyte)(packedUpgradeValue >> 8);
        var octetIm = (sbyte)(packedUpgradeValue >> 16);
        var octetIz = (sbyte)(packedUpgradeValue >> 24);

        return ReturnNewValue(1, stat, octetIs)
               + ReturnNewValue(1, stat, octetIu)
               + ReturnNewValue(1, stat, octetIm)
               + ReturnNewValue(2, stat, octetIz);
    }

        public static int ReturnNewValue(int tableSort, DecorationStatKind stat, int octet)
    {
        return tableSort switch
        {
            1 => ReturnNewValueLowOctet(stat, octet),
            2 => ReturnNewValueHighOctet(stat, octet),
            _ => 0
        };
    }

        public static int PackDecorationUpgradeOctets(int octetIs, int octetIu, int octetIm, int octetIz)
    {
        return (byte)(sbyte)octetIs
               | ((byte)(sbyte)octetIu << 8)
               | ((byte)(sbyte)octetIm << 16)
               | ((byte)(sbyte)octetIz << 24);
    }

    private static int ReturnNewValueLowOctet(DecorationStatKind stat, int octet)
    {
        return stat switch
        {
            DecorationStatKind.MaxLife => octet is >= 41 and <= 60 ? 100 * (octet - 40) : 0,
            DecorationStatKind.MaxMana => octet is >= 61 and <= 80 ? 125 * (octet - 60) : 0,
            DecorationStatKind.DefensePower => octet is >= 1 and <= 20 ? 50 * octet : 0,
            DecorationStatKind.AttackBlock => octet is >= 21 and <= 40 ? 20 * (octet - 20) : 0,
            DecorationStatKind.ElementDefensePower => octet is >= 81 and <= 100 ? 50 * (octet - 80) : 0,
            _ => 0
        };
    }

    private static int ReturnNewValueHighOctet(DecorationStatKind stat, int octet)
    {
        return stat switch
        {
            DecorationStatKind.MaxLife => MaxLifeHighOctetTier(octet),
            DecorationStatKind.MaxMana => 0,
            DecorationStatKind.DefensePower => octet is >= 1 and <= 25 ? ModuloFiveMap(octet) : 0,
            DecorationStatKind.AttackBlock => 0,
            DecorationStatKind.ElementDefensePower => octet is >= 26 and <= 50
                ? ModuloFiveMap(octet)
                : 0,
            _ => 0
        };
    }

    private static int MaxLifeHighOctetTier(int octet)
    {
        return octet switch
        {
            >= 1 and <= 5 or >= 26 and <= 30 => 400,
            >= 6 and <= 10 or >= 31 and <= 35 => 800,
            >= 11 and <= 15 or >= 36 and <= 40 => 1200,
            >= 16 and <= 20 or >= 41 and <= 45 => 1600,
            >= 21 and <= 25 or >= 46 and <= 50 => 2000,
            _ => 0
        };
    }

    private static int ModuloFiveMap(int octet)
    {
        return (octet % 5) switch
        {
            1 => 200,
            2 => 400,
            3 => 600,
            4 => 800,
            0 => 1000,
            _ => 0
        };
    }
}
