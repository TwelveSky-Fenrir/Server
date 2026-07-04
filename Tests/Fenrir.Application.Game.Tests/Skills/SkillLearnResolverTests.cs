using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Skills;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Tests.Skills;

public class SkillLearnResolverTests
{
    private static SkillDefinition Skill(int skillId, byte type, byte learnCost = 1, byte maxUpgrade = 10)
    {
        var row = WorldDataTestRows.Skill(skillId) with
        {
            Type = type, LearnSkillPoint = learnCost, MaxUpgradePoint = maxUpgrade
        };
        return new SkillDefinition(row, [], []);
    }

    private static ImmutableArray<NpcSkillOfferRowDto> Offers(byte arrayKind, params int[] skillIds)
    {
        return skillIds
            .Select((id, i) => new NpcSkillOfferRowDto(i, 1, arrayKind, 0, null, null, (byte)i, id))
            .ToImmutableArray();
    }

    // ---- Learn ----

    [Fact]
    public void Learn_NotOfferedByThisNpc_Fails()
    {
        var offers = Offers(SkillLearnResolver.SkillTree1, 100);
        var skill = Skill(200, 1);

        var result = SkillLearnResolver.ResolveLearn(offers, SkillLearnResolver.SkillTree1, 200, skill,
            ImmutableDictionary<byte, LearnedSkill>.Empty, 10);

        Assert.False(result.Success);
        Assert.Equal(SkillLearnResolver.LearnFailure.NotOfferedByThisNpc, result.Failure);
    }

    [Fact]
    public void Learn_UnknownSkill_Fails()
    {
        var offers = Offers(SkillLearnResolver.SkillTree1, 100);

        var result = SkillLearnResolver.ResolveLearn(offers, SkillLearnResolver.SkillTree1, 100, null,
            ImmutableDictionary<byte, LearnedSkill>.Empty, 10);

        Assert.Equal(SkillLearnResolver.LearnFailure.UnknownSkill, result.Failure);
    }

    [Fact]
    public void Learn_InsufficientSkillPoints_Fails()
    {
        var offers = Offers(SkillLearnResolver.SkillTree1, 100);
        var skill = Skill(100, 1, 5);

        var result = SkillLearnResolver.ResolveLearn(offers, SkillLearnResolver.SkillTree1, 100, skill,
            ImmutableDictionary<byte, LearnedSkill>.Empty, 4);

        Assert.Equal(SkillLearnResolver.LearnFailure.InsufficientSkillPoints, result.Failure);
    }

    [Fact]
    public void Learn_AlreadyLearnedInAnySlot_Fails()
    {
        var offers = Offers(SkillLearnResolver.SkillTree1, 100);
        var skill = Skill(100, 1);
        var learned = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(25, new LearnedSkill(100, 1));

        var result = SkillLearnResolver.ResolveLearn(offers, SkillLearnResolver.SkillTree1, 100, skill, learned, 10);

        Assert.Equal(SkillLearnResolver.LearnFailure.AlreadyLearned, result.Failure);
    }

    [Theory]
    [InlineData((byte)1, 0, 9)]
    [InlineData((byte)2, 20, 29)]
    public void Learn_SimpleTypes_UseTheirOwnRange(byte skillType, int rangeStart, int rangeEnd)
    {
        var offers = Offers(SkillLearnResolver.SkillTree1, 100);
        var skill = Skill(100, skillType);

        var result = SkillLearnResolver.ResolveLearn(offers, SkillLearnResolver.SkillTree1, 100, skill,
            ImmutableDictionary<byte, LearnedSkill>.Empty, 10);

        Assert.True(result.Success);
        Assert.InRange(result.Slot, rangeStart, rangeEnd);
    }

    [Fact]
    public void Learn_SimpleType_RangeFull_NoFreeSlot_Fails()
    {
        var offers = Offers(SkillLearnResolver.SkillTree1, 100);
        var skill = Skill(100, 1);
        var builder = ImmutableDictionary.CreateBuilder<byte, LearnedSkill>();
        for (byte i = 0; i < 10; i++)
            builder[i] = new LearnedSkill(900 + i, 1);

        var result = SkillLearnResolver.ResolveLearn(offers, SkillLearnResolver.SkillTree1, 100, skill,
            builder.ToImmutable(), 10);

        Assert.Equal(SkillLearnResolver.LearnFailure.NoFreeSlot, result.Failure);
    }

    [Fact]
    public void Learn_Type3_FallsBackToSecondRange_WhenFirstIsFull()
    {
        var offers = Offers(SkillLearnResolver.SkillTree1, 100);
        var skill = Skill(100, 3);
        var builder = ImmutableDictionary.CreateBuilder<byte, LearnedSkill>();
        for (byte i = 10; i < 20; i++)
            builder[i] = new LearnedSkill(900 + i, 1);

        var result = SkillLearnResolver.ResolveLearn(offers, SkillLearnResolver.SkillTree1, 100, skill,
            builder.ToImmutable(), 10);

        Assert.True(result.Success);
        Assert.InRange(result.Slot, 30, 39);
    }

    [Fact]
    public void Learn_ArrayKindMismatch_TreatedAsNotOffered()
    {
        var offers = Offers(SkillLearnResolver.SkillTree1, 100);
        var skill = Skill(100, 1);

        var result = SkillLearnResolver.ResolveLearn(offers, SkillLearnResolver.SkillTree2, 100, skill,
            ImmutableDictionary<byte, LearnedSkill>.Empty, 10);

        Assert.Equal(SkillLearnResolver.LearnFailure.NotOfferedByThisNpc, result.Failure);
    }

    // ---- Upgrade ----

    [Fact]
    public void Upgrade_SlotEmpty_Fails()
    {
        var result = SkillLearnResolver.ResolveUpgrade(5, ImmutableDictionary<byte, LearnedSkill>.Empty, null, 10);

        Assert.Equal(SkillLearnResolver.UpgradeFailure.SlotEmpty, result.Failure);
    }

    [Fact]
    public void Upgrade_InvalidSlotIndex_Fails()
    {
        var result = SkillLearnResolver.ResolveUpgrade(40, ImmutableDictionary<byte, LearnedSkill>.Empty, null, 10);

        Assert.Equal(SkillLearnResolver.UpgradeFailure.InvalidSlot, result.Failure);
    }

    [Fact]
    public void Upgrade_InsufficientSkillPoints_Fails()
    {
        var learned = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(5, new LearnedSkill(100, 3));
        var skill = Skill(100, 1, maxUpgrade: 10);

        var result = SkillLearnResolver.ResolveUpgrade(5, learned, skill, 0);

        Assert.Equal(SkillLearnResolver.UpgradeFailure.InsufficientSkillPoints, result.Failure);
    }

    [Fact]
    public void Upgrade_AlreadyAtMax_Fails()
    {
        var learned = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(5, new LearnedSkill(100, 10));
        var skill = Skill(100, 1, maxUpgrade: 10);

        var result = SkillLearnResolver.ResolveUpgrade(5, learned, skill, 5);

        Assert.Equal(SkillLearnResolver.UpgradeFailure.AlreadyMaxed, result.Failure);
    }

    [Fact]
    public void Upgrade_Valid_IncrementsGradeByOne_CostsOnePoint()
    {
        var learned = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(5, new LearnedSkill(100, 3));
        var skill = Skill(100, 1, maxUpgrade: 10);

        var result = SkillLearnResolver.ResolveUpgrade(5, learned, skill, 5);

        Assert.True(result.Success);
        Assert.Equal(4, result.NewGrade);
    }
}
