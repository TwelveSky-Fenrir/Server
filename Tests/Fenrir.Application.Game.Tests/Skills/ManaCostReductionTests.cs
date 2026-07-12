using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Skills;

public class ManaCostReductionTests
{
    private static ItemDefinition ItemWith(int itemId, byte capeInfo1 = 0)
    {
        var row = WorldDataTestRows.Item(itemId) with { CapeInfo1 = capeInfo1 };
        return new ItemDefinition(row, ImmutableArray<ItemBonusSkillRowDto>.Empty);
    }

    [Fact]
    public void GetRatioPercent_EmptyEquipment_ReturnsZero()
    {
        var result = ManaCostReduction.GetRatioPercent([]);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetRatioPercent_EmptySlots_ContributeNothing()
    {
        var equip = new ItemDefinition?[SkillGradeAuthority.EquipSlotCount];

        var result = ManaCostReduction.GetRatioPercent(equip);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetRatioPercent_SumsCapeInfo1AcrossEverySlot_RegardlessOfSlot()
    {
        var itemA = ItemWith(501, capeInfo1: 4);
        var itemB = ItemWith(502, capeInfo1: 6);
        ReadOnlySpan<ItemDefinition?> equip = [itemA, itemB, null];

        var result = ManaCostReduction.GetRatioPercent(equip);

        Assert.Equal(10, result);
    }

    [Fact]
    public void GetRatioPercent_GodOfWarriorCapeInCapeSlot_AddsFlatFifty()
    {
        var capeItem = ItemWith(SkillGradeAuthority.GodOfWarriorCapeItemId);
        var slots = new ItemDefinition?[SkillGradeAuthority.EquipSlotCount];
        slots[SkillGradeAuthority.CapeSlotIndex] = capeItem;

        var result = ManaCostReduction.GetRatioPercent(slots);

        Assert.Equal(50, result);
    }

    [Fact]
    public void GetRatioPercent_GodOfWarriorCapeOutsideCapeSlot_ContributesNoBonus()
    {
        var capeItem = ItemWith(SkillGradeAuthority.GodOfWarriorCapeItemId);
        ReadOnlySpan<ItemDefinition?> equip = [capeItem];

        var result = ManaCostReduction.GetRatioPercent(equip);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetRatioPercent_OtherItemInCapeSlot_ContributesNoBonus()
    {
        var otherCape = ItemWith(99999);
        var slots = new ItemDefinition?[SkillGradeAuthority.EquipSlotCount];
        slots[SkillGradeAuthority.CapeSlotIndex] = otherCape;

        var result = ManaCostReduction.GetRatioPercent(slots);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetRatioPercent_GodOfWarriorCapeBonus_StacksAdditivelyWithPerItemSum()
    {
        var capeItem = ItemWith(SkillGradeAuthority.GodOfWarriorCapeItemId, capeInfo1: 5);
        var otherItem = ItemWith(600, capeInfo1: 3);
        var slots = new ItemDefinition?[SkillGradeAuthority.EquipSlotCount];
        slots[SkillGradeAuthority.CapeSlotIndex] = capeItem;
        slots[2] = otherItem;

        var result = ManaCostReduction.GetRatioPercent(slots);

        Assert.Equal(58, result);
    }
}
