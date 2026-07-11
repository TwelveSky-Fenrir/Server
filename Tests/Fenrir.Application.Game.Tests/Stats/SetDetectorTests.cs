using Fenrir.Application.Game.Stats;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Stats;

/// <summary>
///     Reference vectors for <see cref="StatCalculator.DetectLegacySetNumber" /> (sub-mechanic A of B4-set,
///     MyUtil::ReturnSetItemValue). The exact per-set id tables are a deferred open question (see the detector's
///     file header), so the id-gated rare/legendary/god/elite branches are inert here and detection currently
///     resolves to {0, 50} plus, via <see cref="SetBonusTables.ResolveEffectiveSetNumber" />, the NXT tiers.
///     These tests pin the LIVE behaviour (the item-sort-based -> 50 fallback and slot exclusions), the inert
///     state of the deferred branches, and the end-to-end wiring the detector feeds.
/// </summary>
public class SetDetectorTests
{
    // Legacy equip indices: amulet 0, cape 1, armor 2, gloves 3, ring 4, boots 5, null 6, weapon 7, pet 8.
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

    /// <summary>Builds the six gear slots, each with the given item-sort classification value.</summary>
    private static EquippedItemSlot[] SixGear(byte sort)
    {
        var list = new List<EquippedItemSlot>();
        foreach (var slot in SixGearSlots) list.Add(Equip(slot, sort));
        return [.. list];
    }

    // ---- no set ----

    [Fact]
    public void NoEquipment_ReturnsZero()
    {
        Assert.Equal(0, StatCalculator.DetectLegacySetNumber([]));
    }

    [Fact]
    public void SixNonLegendaryGearPieces_ReturnsZero()
    {
        // sort 0 is neither 1 nor 4, so counter2 stays 0 and no other (deferred) branch matches.
        Assert.Equal(0, StatCalculator.DetectLegacySetNumber(SixGear(0)));
    }

    // ---- mixed/count fallback -> 50 (LIVE: item-sort classification in {1,4}) ----

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
        // All six are sort in {1,4} (three of each), so counter2 == 6.
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
        // counter2 == 5, counter1 == 0 (legendary-piece id table deferred/empty): 5 != 6 and 5+0 != 6.
        EquippedItemSlot[] gear =
        [
            Equip(Amulet, 1), Equip(Armor, 1), Equip(Gloves, 1),
            Equip(Ring, 1), Equip(Boots, 1), Equip(Weapon, 0)
        ];
        Assert.Equal(0, StatCalculator.DetectLegacySetNumber(gear));
    }

    // ---- slot exclusions: cape(1), null(6), pet(8) never count ----

    [Fact]
    public void CapeSlotLegendary_DoesNotCountTowardFallback()
    {
        // Five gear pieces sort 1 + a legendary CAPE: cape is excluded, so counter2 == 5 -> 0.
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
        // Five gear pieces sort 1 + legendary null(6) + legendary pet(8): both excluded -> counter2 == 5 -> 0.
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
        // Six legendary gear pieces already satisfy the rule; extra legendary cape/pet neither help nor break it.
        EquippedItemSlot[] gear =
        [
            Equip(Amulet, 1), Equip(Armor, 1), Equip(Gloves, 1),
            Equip(Ring, 1), Equip(Boots, 1), Equip(Weapon, 1),
            Equip(Cape, 1), Equip(Pet, 1)
        ];
        Assert.Equal(50, StatCalculator.DetectLegacySetNumber(gear));
    }

    // ---- deferred (id-gated) branches are currently inert ----

    [Fact]
    public void RareSetShapedEquipment_WithoutConfirmedIds_ReturnsZero()
    {
        // Two ordinary (non-sort-1/4) items in the ring+amulet subgroup would be a rare set once the id tables
        // are populated; with the tables deferred this must resolve to 0, never a mis-detection.
        EquippedItemSlot[] gear = [Equip(Ring), Equip(Amulet)];
        Assert.Equal(0, StatCalculator.DetectLegacySetNumber(gear));
    }

    [Fact]
    public void DetectorNeverReturnsAnNxtTier()
    {
        // NXT (101-103) is out of this detector's scope; it is overlaid by ResolveEffectiveSetNumber instead.
        var gear = SixGear(1);
        var result = StatCalculator.DetectLegacySetNumber(gear);
        Assert.True(result is < 101 or > 103);
    }

    // ---- end-to-end wiring: detector output feeds ResolveEffectiveSetNumber (the existing set-bonus path) ----

    [Fact]
    public void DetectedFifty_FlowsThroughResolveEffectiveSetNumber()
    {
        var gear = SixGear(1);
        var legacySetNumber = StatCalculator.DetectLegacySetNumber(gear);

        // previousTribe 0 with no NXT pieces: NXT detection yields 0, so the detected legacy number survives.
        var effective = SetBonusTables.ResolveEffectiveSetNumber(0, gear, legacySetNumber);

        Assert.Equal(50, effective);
        // Documents the downstream consequence set 50 carries in the existing bonus tables.
        Assert.Equal(7, SetBonusTables.GetWrapperCriticalBonus(effective));
    }

    [Fact]
    public void NxtEquipment_OverlaysDetectorResult()
    {
        // Full NXT tribe-0 kit (weapon 77000 + five pieces 77003-77007): the detector sees no legacy set (0),
        // and ResolveEffectiveSetNumber overlays NXT 103, preserving legacy's NXT-first priority.
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
