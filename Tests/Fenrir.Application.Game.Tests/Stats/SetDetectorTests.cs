using Fenrir.Application.Game.Stats;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Stats;

public class SetDetectorTests
{
    private const int Amulet = 0;
    private const int Cape = 1;
    private const int Armor = 2;
    private const int Gloves = 3;
    private const int Ring = 4;
    private const int Boots = 5;
    private const int Null = 6;
    private const int Weapon = 7;
    private const int Pet = 8;

    private static readonly int[] SixGearSlots = [Amulet, Armor, Gloves, Ring, Boots, Weapon];

    private static ItemRowDto Item(int itemId, byte sort = 0, byte martialLevelLimit = 0)
    {
        return new ItemRowDto(
            itemId, $"Item{itemId}", null, null, null,
            0, sort, 0, 0, 0,
            1, 0, 0, 0,
            0, 0, 0, 1, martialLevelLimit,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0,
            0, 0, null,
            0, 0, 0, 0, 0);
    }

    private static EquippedItemSlot Equip(int slotIndex, byte sort = 0, int itemId = 1000)
    {
        return new EquippedItemSlot(slotIndex, Item(itemId + slotIndex, sort), 0, 0, 0, 0);
    }

        private static EquippedItemSlot[] SixGear(byte sort)
    {
        var list = new List<EquippedItemSlot>();
        foreach (var slot in SixGearSlots) list.Add(Equip(slot, sort));
        return [.. list];
    }


    [Fact]
    public void NoEquipment_ReturnsZero()
    {
        Assert.Equal(0, StatCalculator.DetectLegacySetNumber([]));
    }

    [Fact]
    public void SixNonLegendaryGearPieces_ReturnsZero()
    {
        Assert.Equal(0, StatCalculator.DetectLegacySetNumber(SixGear(0)));
    }


    [Fact]
    public void SixSortOneGearPieces_ReturnsFifty()
    {
        Assert.Equal(50, StatCalculator.DetectLegacySetNumber(SixGear(1)));
    }

    [Fact]
    public void SixSortFourGearPieces_ReturnsFifty()
    {
        Assert.Equal(50, StatCalculator.DetectLegacySetNumber(SixGear(4)));
    }

    [Fact]
    public void MixedSortOneAndFourGearPieces_ReturnsFifty()
    {
        EquippedItemSlot[] gear =
        [
            Equip(Amulet, 1), Equip(Armor, 4), Equip(Gloves, 1),
            Equip(Ring, 4), Equip(Boots, 1), Equip(Weapon, 4)
        ];
        Assert.Equal(50, StatCalculator.DetectLegacySetNumber(gear));
    }

    [Fact]
    public void FiveLegendaryOneOrdinaryGearPiece_ReturnsZero()
    {
        EquippedItemSlot[] gear =
        [
            Equip(Amulet, 1), Equip(Armor, 1), Equip(Gloves, 1),
            Equip(Ring, 1), Equip(Boots, 1), Equip(Weapon, 0)
        ];
        Assert.Equal(0, StatCalculator.DetectLegacySetNumber(gear));
    }


    [Fact]
    public void CapeSlotLegendary_DoesNotCountTowardFallback()
    {
        EquippedItemSlot[] gear =
        [
            Equip(Amulet, 1), Equip(Armor, 1), Equip(Gloves, 1),
            Equip(Ring, 1), Equip(Boots, 1), Equip(Cape, 1)
        ];
        Assert.Equal(0, StatCalculator.DetectLegacySetNumber(gear));
    }

    [Fact]
    public void NullAndPetSlotsLegendary_DoNotCountTowardFallback()
    {
        EquippedItemSlot[] gear =
        [
            Equip(Amulet, 1), Equip(Armor, 1), Equip(Gloves, 1),
            Equip(Ring, 1), Equip(Boots, 1), Equip(Null, 1), Equip(Pet, 1)
        ];
        Assert.Equal(0, StatCalculator.DetectLegacySetNumber(gear));
    }

    [Fact]
    public void SixGearPlusLegendaryNonGearSlots_StillReturnsFifty()
    {
        EquippedItemSlot[] gear =
        [
            Equip(Amulet, 1), Equip(Armor, 1), Equip(Gloves, 1),
            Equip(Ring, 1), Equip(Boots, 1), Equip(Weapon, 1),
            Equip(Cape, 1), Equip(Pet, 1)
        ];
        Assert.Equal(50, StatCalculator.DetectLegacySetNumber(gear));
    }


    [Fact]
    public void RareSetShapedEquipment_WithoutConfirmedIds_ReturnsZero()
    {
        EquippedItemSlot[] gear = [Equip(Ring), Equip(Amulet)];
        Assert.Equal(0, StatCalculator.DetectLegacySetNumber(gear));
    }

    [Fact]
    public void DetectorNeverReturnsAnNxtTier()
    {
        var gear = SixGear(1);
        var result = StatCalculator.DetectLegacySetNumber(gear);
        Assert.True(result is < 101 or > 103);
    }


    [Fact]
    public void DetectedFifty_FlowsThroughResolveEffectiveSetNumber()
    {
        var gear = SixGear(1);
        var legacySetNumber = StatCalculator.DetectLegacySetNumber(gear);

        var effective = SetBonusTables.ResolveEffectiveSetNumber(0, gear, legacySetNumber);

        Assert.Equal(50, effective);
        Assert.Equal(7, SetBonusTables.GetWrapperCriticalBonus(effective));
    }

    [Fact]
    public void NxtEquipment_OverlaysDetectorResult()
    {
        EquippedItemSlot[] gear =
        [
            NxtEquip(Weapon, 77000),
            NxtEquip(Amulet, 77003),
            NxtEquip(Armor, 77004),
            NxtEquip(Gloves, 77005),
            NxtEquip(Ring, 77006),
            NxtEquip(Boots, 77007)
        ];

        Assert.Equal(0, StatCalculator.DetectLegacySetNumber(gear));
        Assert.Equal(103, SetBonusTables.ResolveEffectiveSetNumber(0, gear, StatCalculator.DetectLegacySetNumber(gear)));
    }

    private static EquippedItemSlot NxtEquip(int slotIndex, int itemId)
    {
        return new EquippedItemSlot(slotIndex, Item(itemId), 0, 0, 0, 0);
    }
}
