using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Skills;

/// <summary>
///     Covers <see cref="SkillGradeAuthority" /> -- the D2-skillcast-helpers contract's
///     <c>GetMaxSkillGradeNum</c>/<c>GetBonusSkillValue</c> server-authoritative helpers. Reference vectors for
///     terms 4/5 are cross-checked directly against <see cref="StatCalculator" />'s own pet-contribution
///     methods (the building blocks <see cref="SkillGradeAuthority.GetBonusSkillValue" /> composes) rather than
///     re-encoding the packed-byte arithmetic a second time.
/// </summary>
public class SkillGradeAuthorityTests
{
    // ================= GetMaxSkillGradeNum =================

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
        // AutoBuffSkillResolver.ResolveRegistration exercises its own private, behaviourally-identical
        // GetMaxSkillGradeNum copy via its clamp step -- cross-check the two agree for the same input, since
        // this shared helper is meant to be a safe drop-in replacement for that private copy (see class
        // remarks / wiringManifest: the private copy is NOT deleted by this slice, only superseded).
        var learned = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(100, 3));
        var flat = new int[16];
        flat[0] = 100;
        flat[1] = 10; // requested grade 10, above the learned grade of 3 -- must clamp to 3

        var viaResolverClamp = AutoBuffSkillResolver.ResolveRegistration(flat, learned)[0].Grade;
        var viaAuthority = SkillGradeAuthority.GetMaxSkillGradeNum(100, learned);

        Assert.Equal(3, viaAuthority);
        Assert.Equal(viaAuthority, viaResolverClamp);
    }

    // ================= GetBonusSkillValue: slot constants =================

    [Fact]
    public void SlotConstants_MatchContractIndices()
    {
        Assert.Equal(13, SkillGradeAuthority.EquipSlotCount);
        Assert.Equal(1, SkillGradeAuthority.CapeSlotIndex);
        Assert.Equal(8, SkillGradeAuthority.PetSlotIndex);
        Assert.Equal(PetSlots.EquipmentSlot, SkillGradeAuthority.PetSlotIndex);
    }

    // ================= GetBonusSkillValue: term 1 (skill-specific per-item bonus table) =================

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
            new ItemBonusSkillRowDto(500, 1, 41, 5), // same skill listed twice -- both must be summed
            new ItemBonusSkillRowDto(500, 2, 99, 100) // different skill -- must not contribute
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

    // ================= GetBonusSkillValue: term 2 (unconditional per-slot flat add, CapeInfo3) =================

    [Fact]
    public void GetBonusSkillValue_Term2_AddsCapeInfo3ForEveryNonEmptySlot_RegardlessOfSlotOrSkill()
    {
        // Neither slot below is the cape slot position, and neither carries a bonus-skill table entry for the
        // queried skill -- CapeInfo3 must still be added for both, per the contract's "not restricted to the
        // cape slot despite the field's name" edge case.
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

        // term 1: 7 (only itemA matches skill 41); term 2: 2 + 1 = 3 (both non-empty slots) -- total 10.
        Assert.Equal(10, result);
    }

    // ================= GetBonusSkillValue: terms 4/5 (pet slot) =================

    /// <summary>Packs four signed bytes into the sub-field-2 int, low byte = IS (matches PetStatContributionTests).</summary>
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
        // IU byte tens digit == statType, units digit == grade 4 -- StatCalculator.PetGradedIuBonus's own
        // gate; exercised here as a reference vector for GetBonusSkillValue's composition of it.
        var packed = Pack(0, expectedStatType * 10 + 4);
        var petItem = ItemWith(8500, sort: 28); // amulet sort

        var result = SkillGradeAuthority.GetBonusSkillValue(skillId, EquipWithPet(petItem), packed, null, 0, false);

        var expected = StatCalculator.PetGradedIuBonus(8500, 28, packed, expectedStatType);
        Assert.Equal(4, expected); // sanity: grade digit echoed as-is
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetBonusSkillValue_Term4_SkillNotOneOfTheSixMapped_ContributesZero()
    {
        var packed = Pack(0, 14); // would match statType 1 grade 4, if the queried skill mapped to it
        var petItem = ItemWith(8500, sort: 28);

        var result = SkillGradeAuthority.GetBonusSkillValue(999, EquipWithPet(petItem), packed, null, 0, false);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetBonusSkillValue_Term4_ExcludedAmuletItemId_ContributesZero()
    {
        // 2253 is one of StatCalculator.PetContribution.cs's own six hardcoded PetGradedAmuletExcludedIds.
        var packed = Pack(0, 14); // statType 1, grade 4 -- would otherwise match skill 103
        var petItem = ItemWith(2253, sort: 28);

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
        var petItem = ItemWith(9100, sort: 22); // NOT the amulet sort (28)

        var result = SkillGradeAuthority.GetBonusSkillValue(1234, EquipWithPet(petItem), packed, null, 0, false);

        Assert.Equal(expectedGrade, result);
    }

    [Fact]
    public void GetBonusSkillValue_Term5_AmuletSort_NeverContributes_EvenWithMatchingIuCode()
    {
        // Sort 28 (amulet) is excluded from term 5's gate even though the IU byte would otherwise match --
        // the contract's "different, non-overlapping pet-slot item category" edge case.
        var packed = Pack(0, 11);
        var petItem = ItemWith(9100, sort: 28);

        var result = SkillGradeAuthority.GetBonusSkillValue(1234, EquipWithPet(petItem), packed, null, 0, false);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetBonusSkillValue_Terms4And5_BothZero_WhenPetPackedValueIsZero()
    {
        // The documented "safe default" posture (class remarks): petPackedValue 0 never produces a false
        // contribution from either pet term, regardless of skill queried or pet sort -- so a caller with no
        // confirmed source for this field yet can pass 0 without risking a false BonusGradeMismatch/SkillHack1.
        var petItem = ItemWith(9100, sort: 22);

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
        ReadOnlySpan<ItemDefinition?> equip = [ItemWith(1)]; // length 1, well short of PetSlotIndex (8)

        var result = SkillGradeAuthority.GetBonusSkillValue(41, equip, 0, null, 0, false);

        Assert.Equal(0, result);
    }

    // ================= GetBonusSkillValue: term 3 (cape-slot hardcoded item id 1404) =================
    // Resolved by the skillgrade-authority-magnitudes supplemental contract (MyFactor.cpp:4543-4546).

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
        // No item id besides 1404 can trigger term 3 -- any other cape-slot item contributes only via
        // terms 1/2 (both zero here), never the extra +2.
        var capeItem = ItemWith(99999);
        var slots = new ItemDefinition?[SkillGradeAuthority.EquipSlotCount];
        slots[SkillGradeAuthority.CapeSlotIndex] = capeItem;

        var result = SkillGradeAuthority.GetBonusSkillValue(41, slots, 0, null, 0, false);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetBonusSkillValue_Term3_MatchingItemIdOutsideCapeSlot_ContributesZero()
    {
        // Item id 1404 equipped in any slot OTHER than the cape slot must not trigger term 3.
        var itemInWrongSlot = ItemWith(SkillGradeAuthority.GodOfWarriorCapeItemId);
        ReadOnlySpan<ItemDefinition?> equip = [itemInWrongSlot]; // slot 0, not CapeSlotIndex (1)

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

        // term 1: 5, term 2: 3, term 3: 2 -- total 10.
        Assert.Equal(10, result);
    }

    // ================= GetBonusSkillValue: term 6 (guild-buff type-3, sType==2 skill, flat +1) =================
    // Resolved by the skillgrade-authority-magnitudes supplemental contract (MyFactor.cpp:4573-4575,
    // STRUCT.h:136).

    private static SkillDefinition SkillDefinitionWithType(int skillId, byte type)
    {
        var row = WorldDataTestRows.Skill(skillId) with { Type = type };
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty,
            ImmutableArray<SkillGradeRowDto>.Empty);
    }

    [Fact]
    public void GetBonusSkillValue_Term6_AllFourConditionsHold_AddsFlatOne()
    {
        var buffCategorySkill = SkillDefinitionWithType(82, type: 2);

        var result = SkillGradeAuthority.GetBonusSkillValue(82, [], 0, buffCategorySkill,
            guildBuffType: 3, guildBuffActive: true);

        Assert.Equal(1, result);
    }

    [Fact]
    public void GetBonusSkillValue_Term6_SkillDefinitionNull_ContributesZero()
    {
        // A null skill definition models SKILLSYSTEM::Search's NULL return for an out-of-range/zeroed skill
        // id -- never satisfies the predicate even with the guild-buff gate fully open.
        var result = SkillGradeAuthority.GetBonusSkillValue(82, [], 0, null, guildBuffType: 3, guildBuffActive: true);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetBonusSkillValue_Term6_SkillTypeNotTwo_ContributesZero()
    {
        var notBuffCategorySkill = SkillDefinitionWithType(82, type: 1);

        var result = SkillGradeAuthority.GetBonusSkillValue(82, [], 0, notBuffCategorySkill,
            guildBuffType: 3, guildBuffActive: true);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetBonusSkillValue_Term6_GuildBuffInactiveOrWrongType_ContributesZero()
    {
        var buffCategorySkill = SkillDefinitionWithType(82, type: 2);

        Assert.Equal(0, SkillGradeAuthority.GetBonusSkillValue(82, [], 0, buffCategorySkill, 3, false));
        Assert.Equal(0, SkillGradeAuthority.GetBonusSkillValue(82, [], 0, buffCategorySkill, 1, true));
    }

    [Fact]
    public void GetBonusSkillValue_Terms3And6_BothApply_StackAdditively()
    {
        var capeItem = ItemWith(SkillGradeAuthority.GodOfWarriorCapeItemId);
        var slots = new ItemDefinition?[SkillGradeAuthority.EquipSlotCount];
        slots[SkillGradeAuthority.CapeSlotIndex] = capeItem;
        var buffCategorySkill = SkillDefinitionWithType(82, type: 2);

        var result = SkillGradeAuthority.GetBonusSkillValue(82, slots, 0, buffCategorySkill,
            guildBuffType: 3, guildBuffActive: true);

        Assert.Equal(3, result); // term 3 (+2) + term 6 (+1)
    }
}
