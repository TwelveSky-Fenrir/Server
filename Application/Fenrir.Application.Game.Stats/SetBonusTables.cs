using System.Collections.Frozen;

namespace Fenrir.Application.Game.Stats;

public readonly record struct SetCoefficients(
    float AttackPower,
    float DefensePower,
    float AttackSuccess,
    float AttackBlock,
    float ElementAttackPower,
    float ElementDefensePower,
    float Critical,
    float CriticalDefence,
    float Luck);

public static class SetBonusTables
{
    private static readonly FrozenDictionary<int, SetCoefficients> CoefficientsBySetNumber =
        new Dictionary<int, SetCoefficients>
        {
            [1] = new(0f, 0f, 0f, 0f, 0.4f, 0.4f, 0f, 0f, 0f),
            [2] = new(0.4f, 0.4f, 0f, 0f, 0f, 0f, 0f, 0f, 0f),
            [3] = new(0f, 0.6f, 0.6f, 0.6f, 0f, 0f, 0f, 0f, 0f),
            [4] = new(0.6f, 0f, 0f, 0.6f, 0f, 0f, 0f, 0f, 0f),
            [5] = new(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.05f, 0.05f, 0f),
            [6] = new(0f, 0f, 0f, 0f, 0.4f, 0.4f, 0f, 0f, 0f),
            [7] = new(0.4f, 0.4f, 0f, 0f, 0f, 0f, 0f, 0f, 0f),
            [8] = new(0f, 0f, 0.4f, 0.4f, 0f, 0f, 0f, 0f, 0f),
            [9] = new(0.6f, 0.6f, 0.6f, 0.6f, 0f, 0f, 0.05f, 0f, 0f),
            [10] = new(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.05f, 0.05f, 0.05f),
            [11] = new(0f, 0f, 0f, 0f, 0.6f, 0.6f, 0f, 0f, 0f),
            [12] = new(0.6f, 0.6f, 0f, 0f, 0f, 0f, 0f, 0f, 0f),
            [13] = new(0f, 0.7f, 0.7f, 0.7f, 0f, 0f, 0f, 0f, 0f),
            [14] = new(0.7f, 0f, 0f, 0.7f, 0f, 0f, 0f, 0f, 0f),
            [15] = new(1.10f, 1.10f, 1.10f, 1.10f, 1.10f, 1.10f, 0f, 0f, 0f),
            [16] = new(0f, 0f, 0f, 0f, 0.6f, 0.6f, 0f, 0f, 0f),
            [17] = new(0.6f, 0.6f, 0f, 0f, 0f, 0f, 0f, 0f, 0f),
            [18] = new(0f, 0f, 0.7f, 0.7f, 0f, 0f, 0f, 0f, 0f),
            [19] = new(0.7f, 0.7f, 0.7f, 0.7f, 0f, 0f, 0f, 0f, 0f),
            [20] = new(1.10f, 1.10f, 1.10f, 1.10f, 1.10f, 1.10f, 0.05f, 0.05f, 0.10f),
            [21] = new(1.2f, 1.2f, 1.2f, 1.2f, 1.2f, 1.2f, 0.05f, 0.05f, 0f),
            [22] = new(0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.04f, 0.04f, 0f),
            [50] = new(0.55f, 0.55f, 0.55f, 0.55f, 0.55f, 0.55f, 0f, 0f, 0.05f),
            [51] = new(0f, 0.05f, 0.05f, 0.05f, 0f, 0f, 0.02f, 0.02f, 0f)
        }.ToFrozenDictionary();

    private static readonly int[] CapeDefenseByCombineTable =
        [0, 40, 80, 120, 160, 200, 240, 280, 320, 360, 400, 440, 520];

    private static readonly int[] WeaponIuSet3Table =
        [0, 3500, 4500, 5500, 6500, 7500, 8000, 8500, 9000, 9500, 10000, 10500, 11000];

    private static readonly int[][] NxtWeaponIdsByTribe =
    [
        [77000, 77001, 77002],
        [77008, 77009, 77010],
        [77016, 77017, 77018]
    ];

    private static readonly int[][] NxtPieceIdsByTribe =
    [
        [77003, 77004, 77005, 77006, 77007],
        [77011, 77012, 77013, 77014, 77015],
        [77019, 77020, 77021, 77022, 77023]
    ];

        public static SetCoefficients GetCoefficients(int setNumber, int slotIndex, bool isLegendaryItem)
    {
        return setNumber == 30
            ? GetSet30Coefficients(slotIndex, isLegendaryItem)
            : CoefficientsBySetNumber.GetValueOrDefault(setNumber);
    }

        private static SetCoefficients GetSet30Coefficients(int slotIndex, bool isLegendaryItem)
    {
        var factor = slotIndex switch
        {
            1 or 10 => 0.55f,
            9 or 11 or 12 => 0f,
            _ => isLegendaryItem ? 0.55f : 1.10f
        };

        return new SetCoefficients(factor, factor, factor, factor, factor, factor, 0f, 0f, 0.05f);
    }

        public static int LinearByCombine(byte combine, int perUnit)
    {
        var clamped = Math.Min((int)combine, 12);
        return clamped * perUnit;
    }

        public static int CapeDefenseByCombine(byte combine)
    {
        var clamped = Math.Min((int)combine, 12);
        return CapeDefenseByCombineTable[clamped];
    }

        public static int WeaponIuSet3AttackPowerBonus(byte enchant)
    {
        var clamped = Math.Min((int)enchant, 12);
        return WeaponIuSet3Table[clamped];
    }

        public static int CapeIuBonus(EquippedItemSlot? capeSlot, int sort, float perUnit)
    {
        if (capeSlot is not { } cape) return 0;
        if (cape.Item.Sort != 29) return 0;
        if (cape.Combine < 10) return 0;

        var tens = cape.Combine / 10;
        var units = cape.Combine % 10;
        return tens != sort ? 0 : (int)((units + 1) * perUnit);
    }

        public static int DetectNxtSetNumber(byte previousTribe, IReadOnlyList<EquippedItemSlot> equipment)
    {
        if (previousTribe > 2) return 0;

        var weaponIds = NxtWeaponIdsByTribe[previousTribe];
        var pieceIds = NxtPieceIdsByTribe[previousTribe];
        var matched = 0;

        foreach (var slot in equipment)
        {
            var itemId = slot.Item.ItemId;
            if (slot.SlotIndex == 7)
            {
                if (Array.IndexOf(weaponIds, itemId) >= 0) matched++;
            }
            else if (slot.SlotIndex is 0 or 2 or 3 or 4 or 5)
            {
                if (Array.IndexOf(pieceIds, itemId) >= 0) matched++;
            }
        }

        return matched switch
        {
            >= 6 => 103,
            >= 4 => 102,
            >= 2 => 101,
            _ => 0
        };
    }

        public static int ResolveEffectiveSetNumber(byte previousTribe, IReadOnlyList<EquippedItemSlot> equipment,
        int legacySetNumber)
    {
        var nxt = DetectNxtSetNumber(previousTribe, equipment);
        return nxt != 0 ? nxt : legacySetNumber;
    }

        public static int GetFlatLifeBonus(int setNumber)
    {
        var nxtOrSet20 = setNumber switch
        {
            101 => 1000,
            102 => 2000,
            103 => 3000,
            20 => 20000,
            _ => 0
        };

        var pieceBonus = (setNumber == 13 ? 1000 : 0) + (setNumber == 18 ? 1100 : 0);

        var anySetBonus = setNumber != 0 ? 15000 : 0;

        return nxtOrSet20 + pieceBonus + anySetBonus;
    }

        public static int GetFlatManaBonus(int setNumber)
    {
        var nxt = setNumber switch { 101 => 1000, 102 => 2000, 103 => 3000, _ => 0 };
        return nxt + (setNumber == 12 ? 1000 : 0) + (setNumber == 17 ? 1100 : 0);
    }

    public static int GetBaseFlatAttackPowerBonus(int setNumber)
    {
        return setNumber switch { 101 => 500, 102 => 1000, 103 => 1500, _ => 0 };
    }

    public static int GetBaseFlatDefensePowerBonus(int setNumber)
    {
        return setNumber switch { 101 => 1000, 102 => 2000, 103 => 3000, _ => 0 };
    }

    public static int GetWrapperAttackSuccessBonus(int setNumber)
    {
        var nxt = setNumber switch { 101 => 250, 102 => 500, 103 => 750, _ => 0 };
        return nxt + (setNumber == 14 ? 100 : 0);
    }

    public static int GetWrapperAttackBlockBonus(int setNumber)
    {
        return setNumber switch { 101 => 250, 102 => 500, 103 => 750, _ => 0 };
    }

    public static int GetWrapperElementAttackPowerBonus(int setNumber)
    {
        return setNumber == 19 ? 500 : 0;
    }

        public static int GetWrapperCriticalBonus(int setNumber)
    {
        return setNumber switch
        {
            11 or 15 or 16 => 2,
            19 => 5,
            20 or 30 or 50 => 7,
            _ => 0
        };
    }

    public static int GetBaseCriticalFlatBonus(int setNumber)
    {
        return setNumber == 103 ? 1 : 0;
    }

    public static int GetBaseCriticalDefenceFlatBonus(int setNumber)
    {
        return setNumber switch
        {
            103 => 1,
            15 => 2,
            20 or 30 or 50 => 7,
            _ => 0
        };
    }
}
