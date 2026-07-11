using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Skills;

public class SkillGradeAuthorityTests
{
    [Fact]
    public void GetMaxSkillGradeNum_LearnedSkillPresent_ReturnsStoredGrade()
    {
        var learned = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(3, new LearnedSkill(41, 7));

        Assert.Equal(7, SkillGradeAuthority.GetMaxSkillGradeNum(41, learned));
    }

    [Fact]
    public void GetMaxSkillGradeNum_UnlearnedSkill_ReturnsNegativeOne()
    {
        var learned = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(1, 1));

        Assert.Equal(-1, SkillGradeAuthority.GetMaxSkillGradeNum(999, learned));
    }

    [Fact]
    public void GetMaxSkillGradeNum_EmptyLearnedSkills_ReturnsNegativeOne()
    {
        Assert.Equal(-1,
            SkillGradeAuthority.GetMaxSkillGradeNum(1, ImmutableDictionary<byte, LearnedSkill>.Empty));
    }

    [Fact]
    public void GetMaxSkillGradeNum_MatchesAutoBuffSkillResolversPrivateCopy()
    {
        var learned = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(100, 3));
        var flat = new int[16];
        flat[0] = 100;
        flat[1] = 10;

        var viaResolverClamp = AutoBuffSkillResolver.ResolveRegistration(flat, learned)[0].Grade;
        var viaAuthority = SkillGradeAuthority.GetMaxSkillGradeNum(100, learned);

        Assert.Equal(3, viaAuthority);
        Assert.Equal(viaAuthority, viaResolverClamp);
    }


    [Fact]
    public void SlotConstants_MatchContractIndices()
    {
        Assert.Equal(13, SkillGradeAuthority.EquipSlotCount);
        Assert.Equal(1, SkillGradeAuthority.CapeSlotIndex);
        Assert.Equal(8, SkillGradeAuthority.PetSlotIndex);
        Assert.Equal(PetSlots.EquipmentSlot, SkillGradeAuthority.PetSlotIndex);
    }


    private static ItemDefinition ItemWith(int itemId, byte sort = 0, byte capeInfo3 = 0,
        params ItemBonusSkillRowDto[] bonusSkills)
    {
        var row = WorldDataTestRows.Item(itemId) with { Sort = sort, CapeInfo3 = capeInfo3 };
        return new ItemDefinition(row, bonusSkills.ToImmutableArray());
    }

    [Fact]
    public void GetBonusSkillValue_EmptyEquipment_ReturnsZero()
    {
        var result = SkillGradeAuthority.GetBonusSkillValue(41, [], 0, null, 0, false);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetBonusSkillValue_Term1_SumsEveryMatchingBonusSkillEntry_NotJustFirst()
    {
        var item = ItemWith(500, bonusSkills:
        [
            new ItemBonusSkillRowDto(500, 0, 41, 10),
            new ItemBonusSkillRowDto(500, 1, 41, 5),
            new ItemBonusSkillRowDto(500, 2, 99, 100)
        ]);
        ReadOnlySpan<ItemDefinition?> equip = [item];

        var result = SkillGradeAuthority.GetBonusSkillValue(41, equip, 0, null, 0, false);

        Assert.Equal(15, result);
    }

    [Fact]
    public void GetBonusSkillValue_Term1_NoMatchingSkill_ContributesZero()
    {
        var item = ItemWith(500, bonusSkills: [new ItemBonusSkillRowDto(500, 0, 41, 10)]);
        ReadOnlySpan<ItemDefinition?> equip = [item];

        var result = SkillGradeAuthority.GetBonusSkillValue(999, equip, 0, null, 0, false);

        Assert.Equal(0, result);
    }


    [Fact]
    public void GetBonusSkillValue_Term2_AddsCapeInfo3ForEveryNonEmptySlot_RegardlessOfSlotOrSkill()
    {
        var itemA = ItemWith(501, capeInfo3: 2);
        var itemB = ItemWith(502, capeInfo3: 3);
        ReadOnlySpan<ItemDefinition?> equip = [itemA, itemB, null];

        var result = SkillGradeAuthority.GetBonusSkillValue(41, equip, 0, null, 0, false);

        Assert.Equal(5, result);
    }

    [Fact]
    public void GetBonusSkillValue_Term2_EmptySlotsContributeNothing()
    {
        var equip = new ItemDefinition?[SkillGradeAuthority.EquipSlotCount];

        var result = SkillGradeAuthority.GetBonusSkillValue(41, equip, 0, null, 0, false);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetBonusSkillValue_Terms1And2_CombineAcrossMultipleSlots()
    {
        var itemA = ItemWith(501, capeInfo3: 2, bonusSkills: [new ItemBonusSkillRowDto(501, 0, 41, 7)]);
        var itemB = ItemWith(502, capeInfo3: 1);
        ReadOnlySpan<ItemDefinition?> equip = [itemA, itemB];

        var result = SkillGradeAuthority.GetBonusSkillValue(41, equip, 0, null, 0, false);

        Assert.Equal(10, result);
    }


    private static int Pack(int isByte, int iuByte = 0, int imByte = 0, int izByte = 0)
    {
        return (isByte & 0xFF) | ((iuByte & 0xFF) << 8) | ((imByte & 0xFF) << 16) | ((izByte & 0xFF) << 24);
    }

    private static ItemDefinition?[] EquipWithPet(ItemDefinition petItem)
    {
        var slots = new ItemDefinition?[SkillGradeAuthority.EquipSlotCount];
        slots[SkillGradeAuthority.PetSlotIndex] = petItem;
        return slots;
    }

    [Theory]
    [InlineData(103, 1)]
    [InlineData(82, 2)]
    [InlineData(83, 3)]
    [InlineData(105, 4)]
    [InlineData(104, 5)]
    [InlineData(84, 6)]
    public void GetBonusSkillValue_Term4_SixMappedSkills_MatchStatCalculatorPetGradedIuBonus(int skillId,
        int expectedStatType)
    {
        var packed = Pack(0, expectedStatType * 10 + 4);
        var petItem = ItemWith(8500, 28);

        var result = SkillGradeAuthority.GetBonusSkillValue(skillId, EquipWithPet(petItem), packed, null, 0, false);

        var expected = StatCalculator.PetGradedIuBonus(8500, 28, packed, expectedStatType);
        Assert.Equal(4, expected);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetBonusSkillValue_Term4_SkillNotOneOfTheSixMapped_ContributesZero()
    {
        var packed = Pack(0, 14);
        var petItem = ItemWith(8500, 28);

        var result = SkillGradeAuthority.GetBonusSkillValue(999, EquipWithPet(petItem), packed, null, 0, false);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetBonusSkillValue_Term4_ExcludedAmuletItemId_ContributesZero()
    {
        var packed = Pack(0, 14);
        var petItem = ItemWith(2253, 28);

        var result = SkillGradeAuthority.GetBonusSkillValue(103, EquipWithPet(petItem), packed, null, 0, false);

        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData(11, 1)]
    [InlineData(12, 2)]
    [InlineData(13, 3)]
    public void GetBonusSkillValue_Term5_GrowthPetIuCode_AddsFixedGrade(int iuCode, int expectedGrade)
    {
        var packed = Pack(0, iuCode);
        var petItem = ItemWith(9100, 22);

        var result = SkillGradeAuthority.GetBonusSkillValue(1234, EquipWithPet(petItem), packed, null, 0, false);

        Assert.Equal(expectedGrade, result);
    }

    [Fact]
    public void GetBonusSkillValue_Term5_AmuletSort_NeverContributes_EvenWithMatchingIuCode()
    {
        var packed = Pack(0, 11);
        var petItem = ItemWith(9100, 28);

        var result = SkillGradeAuthority.GetBonusSkillValue(1234, EquipWithPet(petItem), packed, null, 0, false);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetBonusSkillValue_Terms4And5_BothZero_WhenPetPackedValueIsZero()
    {
        var petItem = ItemWith(9100, 22);

        var result = SkillGradeAuthority.GetBonusSkillValue(103, EquipWithPet(petItem), 0, null, 0, false);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetBonusSkillValue_EmptyPetSlot_NoPetContribution()
    {
        var equip = new ItemDefinition?[SkillGradeAuthority.EquipSlotCount];

        var result = SkillGradeAuthority.GetBonusSkillValue(103, equip, Pack(0, 14), null, 0, false);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetBonusSkillValue_ShortSpan_PetSlotOutOfRange_DoesNotThrow()
    {
        ReadOnlySpan<ItemDefinition?> equip = [ItemWith(1)];

        var result = SkillGradeAuthority.GetBonusSkillValue(41, equip, 0, null, 0, false);

        Assert.Equal(0, result);
    }


    [Fact]
    public void GetBonusSkillValue_Term3_GodOfWarriorCapeInCapeSlot_AddsFlatTwo()
    {
        var capeItem = ItemWith(SkillGradeAuthority.GodOfWarriorCapeItemId);
        var slots = new ItemDefinition?[SkillGradeAuthority.EquipSlotCount];
        slots[SkillGradeAuthority.CapeSlotIndex] = capeItem;

        var result = SkillGradeAuthority.GetBonusSkillValue(41, slots, 0, null, 0, false);

        Assert.Equal(2, result);
    }

    [Fact]
    public void GetBonusSkillValue_Term3_AppliesRegardlessOfWhichSkillIsQueried()
    {
        var capeItem = ItemWith(SkillGradeAuthority.GodOfWarriorCapeItemId);
        var slots = new ItemDefinition?[SkillGradeAuthority.EquipSlotCount];
        slots[SkillGradeAuthority.CapeSlotIndex] = capeItem;

        var result = SkillGradeAuthority.GetBonusSkillValue(999, slots, 0, null, 0, false);

        Assert.Equal(2, result);
    }

    [Fact]
    public void GetBonusSkillValue_Term3_OtherCapeItemId_ContributesZero()
    {
        var capeItem = ItemWith(99999);
        var slots = new ItemDefinition?[SkillGradeAuthority.EquipSlotCount];
        slots[SkillGradeAuthority.CapeSlotIndex] = capeItem;

        var result = SkillGradeAuthority.GetBonusSkillValue(41, slots, 0, null, 0, false);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetBonusSkillValue_Term3_MatchingItemIdOutsideCapeSlot_ContributesZero()
    {
        var itemInWrongSlot = ItemWith(SkillGradeAuthority.GodOfWarriorCapeItemId);
        ReadOnlySpan<ItemDefinition?> equip = [itemInWrongSlot];

        var result = SkillGradeAuthority.GetBonusSkillValue(41, equip, 0, null, 0, false);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetBonusSkillValue_Term3_StacksWithTerms1And2()
    {
        var capeItem = ItemWith(SkillGradeAuthority.GodOfWarriorCapeItemId, capeInfo3: 3,
            bonusSkills: [new ItemBonusSkillRowDto(SkillGradeAuthority.GodOfWarriorCapeItemId, 0, 41, 5)]);
        var slots = new ItemDefinition?[SkillGradeAuthority.EquipSlotCount];
        slots[SkillGradeAuthority.CapeSlotIndex] = capeItem;

        var result = SkillGradeAuthority.GetBonusSkillValue(41, slots, 0, null, 0, false);

        Assert.Equal(10, result);
    }


    private static SkillDefinition SkillDefinitionWithType(int skillId, byte type)
    {
        var row = WorldDataTestRows.Skill(skillId) with { Type = type };
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty,
            ImmutableArray<SkillGradeRowDto>.Empty);
    }

    [Fact]
    public void GetBonusSkillValue_Term6_AllFourConditionsHold_AddsFlatOne()
    {
        var buffCategorySkill = SkillDefinitionWithType(82, 2);

        var result = SkillGradeAuthority.GetBonusSkillValue(82, [], 0, buffCategorySkill,
            3, true);

        Assert.Equal(1, result);
    }

    [Fact]
    public void GetBonusSkillValue_Term6_SkillDefinitionNull_ContributesZero()
    {
        var result = SkillGradeAuthority.GetBonusSkillValue(82, [], 0, null, 3, true);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetBonusSkillValue_Term6_SkillTypeNotTwo_ContributesZero()
    {
        var notBuffCategorySkill = SkillDefinitionWithType(82, 1);

        var result = SkillGradeAuthority.GetBonusSkillValue(82, [], 0, notBuffCategorySkill,
            3, true);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetBonusSkillValue_Term6_GuildBuffInactiveOrWrongType_ContributesZero()
    {
        var buffCategorySkill = SkillDefinitionWithType(82, 2);

        Assert.Equal(0, SkillGradeAuthority.GetBonusSkillValue(82, [], 0, buffCategorySkill, 3, false));
        Assert.Equal(0, SkillGradeAuthority.GetBonusSkillValue(82, [], 0, buffCategorySkill, 1, true));
    }

    [Fact]
    public void GetBonusSkillValue_Terms3And6_BothApply_StackAdditively()
    {
        var capeItem = ItemWith(SkillGradeAuthority.GodOfWarriorCapeItemId);
        var slots = new ItemDefinition?[SkillGradeAuthority.EquipSlotCount];
        slots[SkillGradeAuthority.CapeSlotIndex] = capeItem;
        var buffCategorySkill = SkillDefinitionWithType(82, 2);

        var result = SkillGradeAuthority.GetBonusSkillValue(82, slots, 0, buffCategorySkill,
            3, true);

        Assert.Equal(3, result);
    }
}
