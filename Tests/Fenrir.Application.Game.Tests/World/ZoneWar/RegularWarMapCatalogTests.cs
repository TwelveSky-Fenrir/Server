using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class RegularWarMapCatalogTests
{
    [Theory]
    [InlineData((short)49)]
    [InlineData((short)146)]
    [InlineData((short)149)]
    [InlineData((short)154)]
    [InlineData((short)157)]
    [InlineData((short)160)]
    [InlineData((short)120)]
    [InlineData((short)121)]
    [InlineData((short)122)]
    [InlineData((short)295)]
    [InlineData((short)164)]
    public void TryGet_EveryConfiguredServerNumber_Resolves(short mapId)
    {
        Assert.True(RegularWarMapCatalog.TryGet(mapId, out var config));
        Assert.Equal(mapId, config.MapId);
    }

    [Fact]
    public void ExactlyElevenMapsAreConfigured()
    {
        Assert.Equal(11, RegularWarMapCatalog.ConfiguredMaps.Length);
    }

    [Theory]
    [InlineData((short)1)]
    [InlineData((short)38)]
    [InlineData((short)51)]
    [InlineData((short)0)]
    public void TryGet_AnyOtherServerNumber_Fails(short mapId)
    {
        Assert.False(RegularWarMapCatalog.TryGet(mapId, out _));
    }

    [Fact]
    public void OnlyServer295_IsFlaggedAsBossWar()
    {
        foreach (var config in RegularWarMapCatalog.ConfiguredMaps)
            Assert.Equal(config.MapId == 295, config.IsBossWar);
    }

    [Fact]
    public void OnlyServer160_AnnouncesSmallestPresentTribe()
    {
        foreach (var config in RegularWarMapCatalog.ConfiguredMaps)
            Assert.Equal(config.MapId == 160, config.AnnouncesSmallestPresentTribe);
    }

    [Fact]
    public void Server120_GrantsFiftyTwentyFiveCp_RestrictedToRebirthTierEleven()
    {
        Assert.True(RegularWarMapCatalog.TryGet(120, out var config));
        var rule = Assert.NotNull(config.CpBonusRule);
        Assert.Equal(50, rule.WinningSideAmount);
        Assert.Equal(25, rule.LosingSideAmount);
        Assert.Equal(RegularWarCpBonusCriterion.RebirthTierExactly11, rule.Criterion);

        Assert.True(rule.IsSatisfiedBy(rebirthTier: 11, rebirthCount: 0));
        Assert.False(rule.IsSatisfiedBy(rebirthTier: 10, rebirthCount: 6));
        Assert.False(rule.IsSatisfiedBy(rebirthTier: 12, rebirthCount: 6));
    }

    [Fact]
    public void Server164_GrantsOneHundredFiftyCp_RestrictedToRebirthCountZeroToSix()
    {
        Assert.True(RegularWarMapCatalog.TryGet(164, out var config));
        var rule = Assert.NotNull(config.CpBonusRule);
        Assert.Equal(100, rule.WinningSideAmount);
        Assert.Equal(50, rule.LosingSideAmount);
        Assert.Equal(RegularWarCpBonusCriterion.RebirthCount0To6, rule.Criterion);

        Assert.True(rule.IsSatisfiedBy(rebirthTier: 3, rebirthCount: 0));
        Assert.True(rule.IsSatisfiedBy(rebirthTier: 3, rebirthCount: 6));
        Assert.False(rule.IsSatisfiedBy(rebirthTier: 3, rebirthCount: 7));
    }

    [Theory]
    [InlineData((short)49)]
    [InlineData((short)146)]
    [InlineData((short)149)]
    [InlineData((short)154)]
    [InlineData((short)157)]
    [InlineData((short)160)]
    [InlineData((short)121)]
    [InlineData((short)122)]
    [InlineData((short)295)]
    public void EveryOtherConfiguredMap_GrantsNoCpBonus(short mapId)
    {
        Assert.True(RegularWarMapCatalog.TryGet(mapId, out var config));
        Assert.Null(config.CpBonusRule);
    }
}
