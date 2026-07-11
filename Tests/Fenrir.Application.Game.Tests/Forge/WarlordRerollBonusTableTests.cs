using Fenrir.Application.Game.Domain.Forge;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Forge;

public class WarlordRerollBonusTableTests
{
    private const byte CapeSort = 8;
    private const byte IAmulet = 7;
    private const byte IArmor = 9;
    private const byte IBoots = 12;
    private const byte ISword = 13;
    private const byte IBlade = 14;
    private const byte IMarble = 15;

    [Fact]
    public void CapeSort_IsAlwaysNoCandidate()
    {
        var result = WarlordRerollBonusTable.TryDrawReplacement(CapeSort, RankChangeResolver.RareItemType, 0,
            new WarlordPityLockState(), new ScriptedRandomSource(0));

        Assert.Equal(WarlordRerollBonusTable.WarlordRerollOutcome.NoCandidate, result.Outcome);
        Assert.Equal(0, result.ReplacementItemId);
    }

    [Fact]
    public void UnmappedSort_IsNoCandidate()
    {
        var result = WarlordRerollBonusTable.TryDrawReplacement(0, RankChangeResolver.RareItemType, 0,
            new WarlordPityLockState(), new ScriptedRandomSource(0));

        Assert.Equal(WarlordRerollBonusTable.WarlordRerollOutcome.NoCandidate, result.Outcome);
    }

    [Fact]
    public void InvalidItemType_IsNoCandidate()
    {
        var result = WarlordRerollBonusTable.TryDrawReplacement(IAmulet, 0, 0,
            new WarlordPityLockState(), new ScriptedRandomSource(0));

        Assert.Equal(WarlordRerollBonusTable.WarlordRerollOutcome.NoCandidate, result.Outcome);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(255)]
    public void PreviousTribeOutOfRange_IsNoCandidate(byte previousTribe)
    {
        var result = WarlordRerollBonusTable.TryDrawReplacement(IAmulet, RankChangeResolver.RareItemType,
            previousTribe, new WarlordPityLockState(), new ScriptedRandomSource(0));

        Assert.Equal(WarlordRerollBonusTable.WarlordRerollOutcome.NoCandidate, result.Outcome);
    }

    [Theory]
    [InlineData(0, 87020)]
    [InlineData(1, 87041)]
    [InlineData(2, 87062)]
    public void RareAmulet_TopDraw_UsesTribeOffset(byte previousTribe, int expectedItemId)
    {
        var result = WarlordRerollBonusTable.TryDrawReplacement(IAmulet, RankChangeResolver.RareItemType,
            previousTribe, new WarlordPityLockState(), new ScriptedRandomSource(0));

        Assert.Equal(WarlordRerollBonusTable.WarlordRerollOutcome.Top, result.Outcome);
        Assert.Equal(expectedItemId, result.ReplacementItemId);
        Assert.True(result.IsTopTierOutcome);
        Assert.False(result.SetsPityLock, "Rare-tier top outcome must never engage the elite-only pity lock.");
    }

    [Fact]
    public void RareAmulet_HasNoMidTier_FallsStraightToBase()
    {
        var result = WarlordRerollBonusTable.TryDrawReplacement(IAmulet, RankChangeResolver.RareItemType, 0,
            new WarlordPityLockState(), new ScriptedRandomSource(5));

        Assert.Equal(WarlordRerollBonusTable.WarlordRerollOutcome.Base, result.Outcome);
        Assert.Equal(87001, result.ReplacementItemId);
    }

    [Fact]
    public void RareArmor_MidBand_Covers5Through45()
    {
        var result = WarlordRerollBonusTable.TryDrawReplacement(IArmor, RankChangeResolver.RareItemType, 0,
            new WarlordPityLockState(), new ScriptedRandomSource(45));

        Assert.Equal(WarlordRerollBonusTable.WarlordRerollOutcome.Mid, result.Outcome);
        Assert.Equal(87006, result.ReplacementItemId);
    }

    [Fact]
    public void RareArmor_ForcedPityDrawOfFifty_FallsToBase_NotMid()
    {
        var pityLock = new WarlordPityLockState();
        pityLock.Engage();

        var result = WarlordRerollBonusTable.TryDrawReplacement(IArmor, RankChangeResolver.RareItemType, 0,
            pityLock, new ScriptedRandomSource(0));

        Assert.Equal(WarlordRerollBonusTable.WarlordRerollOutcome.Base, result.Outcome);
        Assert.Equal(87005, result.ReplacementItemId);
    }

    [Theory]
    [InlineData(ISword, 87013)]
    [InlineData(IBlade, 87014)]
    [InlineData(IMarble, 87015)]
    public void RareSwordFamily_TopDraw_UsesFamilyOffset_NotTribeOffset(byte sort, int expectedItemId)
    {
        var result = WarlordRerollBonusTable.TryDrawReplacement(sort, RankChangeResolver.RareItemType, 2,
            new WarlordPityLockState(), new ScriptedRandomSource(0));

        Assert.Equal(WarlordRerollBonusTable.WarlordRerollOutcome.Top, result.Outcome);
        Assert.Equal(expectedItemId, result.ReplacementItemId);
    }

    [Theory]
    [InlineData(0, 87084)]
    [InlineData(1, 87106)]
    [InlineData(2, 87128)]
    public void EliteAmulet_TopDraw_EngagesPityLock(byte previousTribe, int expectedItemId)
    {
        var pityLock = new WarlordPityLockState();

        var result = WarlordRerollBonusTable.TryDrawReplacement(IAmulet, RankChangeResolver.EliteItemType,
            previousTribe, pityLock, new ScriptedRandomSource(0));

        Assert.Equal(WarlordRerollBonusTable.WarlordRerollOutcome.Top, result.Outcome);
        Assert.Equal(expectedItemId, result.ReplacementItemId);
        Assert.True(result.SetsPityLock);
        Assert.True(pityLock.Engaged);
    }

    [Fact]
    public void EliteBoots_MidBand_Covers1Through15()
    {
        var result = WarlordRerollBonusTable.TryDrawReplacement(IBoots, RankChangeResolver.EliteItemType, 0,
            new WarlordPityLockState(), new ScriptedRandomSource(15));

        Assert.Equal(WarlordRerollBonusTable.WarlordRerollOutcome.Mid, result.Outcome);
        Assert.Equal(87076, result.ReplacementItemId);
    }

    [Fact]
    public void EngagedPityLock_ForcesDrawFiftyRegardlessOfSuppliedRandom()
    {
        var pityLock = new WarlordPityLockState();
        pityLock.Engage();

        var result = WarlordRerollBonusTable.TryDrawReplacement(IBoots, RankChangeResolver.EliteItemType, 0,
            pityLock, new ScriptedRandomSource(0));

        Assert.Equal(WarlordRerollBonusTable.WarlordRerollOutcome.Base, result.Outcome);
        Assert.Equal(87070, result.ReplacementItemId);
    }

    [Fact]
    public void EngagedPityLock_MakesEliteTopTierUnreachable()
    {
        var pityLock = new WarlordPityLockState();
        pityLock.Engage();

        var result = WarlordRerollBonusTable.TryDrawReplacement(IAmulet, RankChangeResolver.EliteItemType, 0,
            pityLock, new ScriptedRandomSource(0));

        Assert.NotEqual(WarlordRerollBonusTable.WarlordRerollOutcome.Top, result.Outcome);
    }

    [Theory]
    [InlineData(RankChangeResolver.RareItemType, false)]
    [InlineData(RankChangeResolver.EliteItemType, true)]
    public void NoticeReachesRecipients_OnlyForEliteTier(byte itemType, bool expected)
    {
        Assert.Equal(expected, WarlordRerollBonusTable.NoticeReachesRecipients(itemType));
    }
}
