using Fenrir.Application.Game.Domain.Forge;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Forge;

public class RankChangeResolverTests
{
    private static ItemRowDto Row(int itemId, byte type, byte sort, short level, byte martialLevel = 0,
        byte equipInfo1 = 1, byte checkHighItem = 2, byte checkLowItem = 2, byte checkAvatarTrade = 0,
        byte checkMonsterDrop = 0)
    {
        return WorldDataTestRows.Item(itemId) with
        {
            Type = type, Sort = sort, Level = level, MartialLevel = martialLevel, EquipInfo1 = equipInfo1,
            CheckHighItem = checkHighItem, CheckLowItem = checkLowItem, CheckAvatarTrade = checkAvatarTrade,
            CheckMonsterDrop = checkMonsterDrop
        };
    }

    private static ItemDefinition Def(ItemRowDto row, int? bonusSkillSlot0 = null, int? bonusSkillSlot1 = null)
    {
        List<ItemBonusSkillRowDto> bonuses = [];
        if (bonusSkillSlot0 is { } s0)
            bonuses.Add(new ItemBonusSkillRowDto(row.ItemId, 0, s0, 0));
        if (bonusSkillSlot1 is { } s1)
            bonuses.Add(new ItemBonusSkillRowDto(row.ItemId, 1, s1, 0));

        return new ItemDefinition(row, [..bonuses]);
    }

    private static ItemStack Stack(byte enchant, byte combine)
    {
        return new ItemStack(1, 1, enchant, combine, 0, 0, 0, 0, 0, 0, 999);
    }

    private static ItemRowDto Material1024()
    {
        return WorldDataTestRows.Item(1024);
    }

    private static ItemRowDto Material1025()
    {
        return WorldDataTestRows.Item(1025);
    }

    [Fact]
    public void Upgrade_TargetTypeNormal_IsRejected()
    {
        var target = Def(Row(1, 0, 9, 45));
        var result = RankChangeResolver.ResolveUpgrade(target, Stack(4, 1), Material1024(), 0, 0, [],
            new ScriptedRandomSource(0));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Upgrade_SortOutOfRange_IsRejected()
    {
        var target = Def(Row(1, RankChangeResolver.RareItemType, 6, 45));
        var result = RankChangeResolver.ResolveUpgrade(target, Stack(4, 1), Material1024(), 0, 0, [],
            new ScriptedRandomSource(0));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Upgrade_CheckHighItemNotTwo_IsRejected()
    {
        var target = Def(Row(1, RankChangeResolver.RareItemType, 9, 45, checkHighItem: 0));
        var result = RankChangeResolver.ResolveUpgrade(target, Stack(4, 1), Material1024(), 0, 0, [],
            new ScriptedRandomSource(0));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.Rejected, result.Outcome);
    }

    [Theory]
    [InlineData(3, 1)]
    [InlineData(4, 0)]
    public void Upgrade_EnchantOrCombineBelowThreshold_IsRejected(byte enchant, byte combine)
    {
        var target = Def(Row(1, RankChangeResolver.RareItemType, 9, 45));
        var result = RankChangeResolver.ResolveUpgrade(target, Stack(enchant, combine), Material1024(), 0, 0, [],
            new ScriptedRandomSource(0));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Upgrade_MartialLevelAtCap_IsRejected()
    {
        var target = Def(Row(1, RankChangeResolver.RareItemType, 9, 145, 12));
        var result = RankChangeResolver.ResolveUpgrade(target, Stack(4, 1), Material1024(), 0, 0, [],
            new ScriptedRandomSource(0));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Upgrade_MaterialNotAllowed_IsRejected()
    {
        var target = Def(Row(1, RankChangeResolver.RareItemType, 9, 45));
        var result = RankChangeResolver.ResolveUpgrade(target, Stack(4, 1), WorldDataTestRows.Item(999), 0, 0, [],
            new ScriptedRandomSource(0));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Upgrade_NoCandidate_StillReportsCost()
    {
        var target = Def(Row(1, RankChangeResolver.RareItemType, 9, 45));
        var result = RankChangeResolver.ResolveUpgrade(target, Stack(4, 1), Material1024(), 0, 0, [],
            new ScriptedRandomSource(0));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.NoCandidate, result.Outcome);
        Assert.Equal(100_000, result.Cost);
    }

    [Fact]
    public void Upgrade_Material1024_LowerTierProbability_FailsWhereMaterial1025Succeeds()
    {
        var target = Def(Row(1, RankChangeResolver.RareItemType, 9, 45));
        var candidate = Def(Row(2, RankChangeResolver.RareItemType, 9, 55));

        var with1024 = RankChangeResolver.ResolveUpgrade(target, Stack(4, 1), Material1024(), 0, 0, [target, candidate],
            new ScriptedRandomSource(0, 550));
        var with1025 = RankChangeResolver.ResolveUpgrade(target, Stack(4, 1), Material1025(), 0, 0, [target, candidate],
            new ScriptedRandomSource(0, 550));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.Failed, with1024.Outcome);
        Assert.Equal(RankChangeResolver.RankChangeOutcome.Success, with1025.Outcome);
    }

    [Fact]
    public void Upgrade_Success_IncrementsResultAndAdjustsEnchantCombine()
    {
        var target = Def(Row(1, RankChangeResolver.RareItemType, 9, 45));
        var candidate = Def(Row(2, RankChangeResolver.RareItemType, 9, 55));

        var result = RankChangeResolver.ResolveUpgrade(target, Stack(6, 2), Material1025(), 0, 0, [target, candidate],
            new ScriptedRandomSource(0, 10));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.Success, result.Outcome);
        Assert.Equal(2, result.ResultItemId);
        Assert.Equal(2, result.NewEnchant);
        Assert.Equal(1, result.NewCombine);
    }

    [Fact]
    public void Upgrade_Failure_KeepsEnchantCombineUnchanged()
    {
        var target = Def(Row(1, RankChangeResolver.RareItemType, 9, 45));
        var candidate = Def(Row(2, RankChangeResolver.RareItemType, 9, 55));

        var result = RankChangeResolver.ResolveUpgrade(target, Stack(6, 2), Material1024(), 0, 0, [target, candidate],
            new ScriptedRandomSource(0, 999));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.Failed, result.Outcome);
        Assert.Equal(6, result.NewEnchant);
        Assert.Equal(2, result.NewCombine);
    }

    [Fact]
    public void Upgrade_LuckyCharge_DoesNotAlterProbabilityButIsReportedConsumed()
    {
        var target = Def(Row(1, RankChangeResolver.RareItemType, 9, 45));
        var candidate = Def(Row(2, RankChangeResolver.RareItemType, 9, 55));

        var withoutCharge = RankChangeResolver.ResolveUpgrade(target, Stack(4, 1), Material1024(), 0, 0,
            [target, candidate], new ScriptedRandomSource(0, 550));
        var withCharge = RankChangeResolver.ResolveUpgrade(target, Stack(4, 1), Material1024(), 0, 1,
            [target, candidate], new ScriptedRandomSource(0, 550));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.Failed, withoutCharge.Outcome);
        Assert.Equal(RankChangeResolver.RankChangeOutcome.Failed, withCharge.Outcome);
        Assert.False(withoutCharge.ConsumesLuckyCharge);
        Assert.True(withCharge.ConsumesLuckyCharge);
    }

    [Fact]
    public void Upgrade_Level145Martial_EncodesNextMartialTier()
    {
        var target = Def(Row(1, RankChangeResolver.RareItemType, 9, 145, 2));
        var candidate = Def(Row(2, RankChangeResolver.RareItemType, 9, 145, 3));

        var result = RankChangeResolver.ResolveUpgrade(target, Stack(4, 1), Material1024(), 0, 0, [target, candidate],
            new ScriptedRandomSource(0, 5));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.Success, result.Outcome);
        Assert.Equal(2, result.ResultItemId);
    }

    [Fact]
    public void Upgrade_BonusSkillMismatch_ExcludesCandidate()
    {
        var target = Def(Row(1, RankChangeResolver.RareItemType, 9, 45), 100);
        var candidate = Def(Row(2, RankChangeResolver.RareItemType, 9, 55), 200);

        var result = RankChangeResolver.ResolveUpgrade(target, Stack(4, 1), Material1024(), 0, 0, [target, candidate],
            new ScriptedRandomSource(0));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.NoCandidate, result.Outcome);
    }

    [Fact]
    public void Upgrade_BonusSkillMatchesCandidateSlotOne_IsAccepted()
    {
        var target = Def(Row(1, RankChangeResolver.RareItemType, 9, 45), 100);
        var candidate = Def(Row(2, RankChangeResolver.RareItemType, 9, 55), bonusSkillSlot1: 100);

        var result = RankChangeResolver.ResolveUpgrade(target, Stack(4, 1), Material1024(), 0, 0, [target, candidate],
            new ScriptedRandomSource(0, 5));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.Success, result.Outcome);
    }

    [Theory]
    [InlineData(45, 100_000)]
    [InlineData(144, 1_800_000)]
    public void Upgrade_RareCost_MatchesLegacyTable(short level, int expectedCost)
    {
        var target = Def(Row(1, RankChangeResolver.RareItemType, 9, level));
        var result = RankChangeResolver.ResolveUpgrade(target, Stack(4, 1), Material1024(), 0, 0, [],
            new ScriptedRandomSource(0));

        Assert.Equal(expectedCost, result.Cost);
    }

    [Fact]
    public void Downgrade_TargetAtMinimumRareLevel_IsRejected()
    {
        var target = Def(Row(1, RankChangeResolver.RareItemType, 9, 45));
        var result = RankChangeResolver.ResolveDowngrade(target, Stack(0, 0), Material1024(), 0, 0, [],
            new ScriptedRandomSource(0));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Downgrade_TargetAtMinimumEliteLevel_IsRejected()
    {
        var target = Def(Row(1, RankChangeResolver.EliteItemType, 9, 100));
        var result = RankChangeResolver.ResolveDowngrade(target, Stack(0, 0), Material1024(), 0, 0, [],
            new ScriptedRandomSource(0));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Downgrade_Success_KeepsEnchantAndCombineUnchanged()
    {
        var target = Def(Row(1, RankChangeResolver.RareItemType, 9, 55));
        var candidate = Def(Row(2, RankChangeResolver.RareItemType, 9, 45));

        var result = RankChangeResolver.ResolveDowngrade(target, Stack(7, 3), Material1024(), 0, 0,
            [target, candidate], new ScriptedRandomSource(0, 10));

        Assert.Equal(RankChangeResolver.RankChangeOutcome.Success, result.Outcome);
        Assert.Equal(2, result.ResultItemId);
        Assert.Equal(7, result.NewEnchant);
        Assert.Equal(3, result.NewCombine);
    }

    [Fact]
    public void Downgrade_Level145MartialTwelve_IsAllowed_UnlikeUpgrade()
    {
        var target = Def(Row(1, RankChangeResolver.RareItemType, 9, 145, 12));
        var candidate = Def(Row(2, RankChangeResolver.RareItemType, 9, 145, 11));

        var result = RankChangeResolver.ResolveDowngrade(target, Stack(0, 0), Material1024(), 0, 0,
            [target, candidate], new ScriptedRandomSource(0, 0));

        Assert.NotEqual(RankChangeResolver.RankChangeOutcome.Rejected, result.Outcome);
        Assert.Equal(3_000_000, result.Cost);
    }

    [Theory]
    [InlineData(55, 100_000)]
    [InlineData(144, 1_700_000)]
    public void Downgrade_RareCost_MatchesLegacyTable(short level, int expectedCost)
    {
        var target = Def(Row(1, RankChangeResolver.RareItemType, 9, level));
        var result = RankChangeResolver.ResolveDowngrade(target, Stack(0, 0), Material1024(), 0, 0, [],
            new ScriptedRandomSource(0));

        Assert.Equal(expectedCost, result.Cost);
    }
}
