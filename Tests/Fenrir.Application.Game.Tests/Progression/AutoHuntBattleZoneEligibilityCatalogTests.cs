using Fenrir.Application.Game.Domain.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

public class AutoHuntBattleZoneEligibilityCatalogTests
{
    [Theory]
    [InlineData((short)49, 10, 89)]
    [InlineData((short)51, 20, 29)]
    [InlineData((short)53, 30, 39)]
    [InlineData((short)120, 146, 156)]
    [InlineData((short)121, 150, 153)]
    [InlineData((short)122, 154, 156)]
    [InlineData((short)146, 90, 112)]
    [InlineData((short)147, 50, 59)]
    [InlineData((short)148, 60, 69)]
    [InlineData((short)149, 70, 79)]
    [InlineData((short)150, 80, 89)]
    [InlineData((short)151, 90, 99)]
    [InlineData((short)152, 100, 105)]
    [InlineData((short)153, 106, 112)]
    [InlineData((short)154, 1, 157)]
    [InlineData((short)155, 116, 118)]
    [InlineData((short)156, 119, 121)]
    [InlineData((short)157, 124, 134)]
    [InlineData((short)158, 125, 127)]
    [InlineData((short)159, 128, 130)]
    [InlineData((short)160, 135, 145)]
    [InlineData((short)161, 134, 136)]
    [InlineData((short)162, 137, 139)]
    [InlineData((short)163, 140, 142)]
    [InlineData((short)164, 145, 151)]
    public void RecognizedServerNumbers_BlockExactlyWithinTheirLevelBand(short mapId, int lower, int upper)
    {
        Assert.False(AutoHuntBattleZoneEligibilityCatalog.IsBlocked(mapId, lower - 1, 0));
        Assert.True(AutoHuntBattleZoneEligibilityCatalog.IsBlocked(mapId, lower, 0));
        Assert.True(AutoHuntBattleZoneEligibilityCatalog.IsBlocked(mapId, upper, 0));
        Assert.False(AutoHuntBattleZoneEligibilityCatalog.IsBlocked(mapId, upper + 1, 0));
    }

    [Fact]
    public void Map154_BandCoversEveryAttainableLevel_SoItIsAnUnconditionalBlockInPractice()
    {
        Assert.True(AutoHuntBattleZoneEligibilityCatalog.IsBlocked(154, 1, 0));
        Assert.True(AutoHuntBattleZoneEligibilityCatalog.IsBlocked(154, 157, 0));
        Assert.False(AutoHuntBattleZoneEligibilityCatalog.IsBlocked(154, 0, 0));
        Assert.False(AutoHuntBattleZoneEligibilityCatalog.IsBlocked(154, 158, 0));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(100)]
    public void Map295_BlocksOnlyAtLevel157AndOnlyBelowRebirthTier7(int rebirthTier)
    {
        var shouldBlock = rebirthTier < 7;
        Assert.Equal(shouldBlock, AutoHuntBattleZoneEligibilityCatalog.IsBlocked(295, 157, rebirthTier));

        Assert.False(AutoHuntBattleZoneEligibilityCatalog.IsBlocked(295, 156, rebirthTier));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(100)]
    public void Map296_BlocksOnlyAtLevel157AndOnlyAtOrAboveRebirthTier7(int rebirthTier)
    {
        var shouldBlock = rebirthTier >= 7;
        Assert.Equal(shouldBlock, AutoHuntBattleZoneEligibilityCatalog.IsBlocked(296, 157, rebirthTier));

        Assert.False(AutoHuntBattleZoneEligibilityCatalog.IsBlocked(296, 156, rebirthTier));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    public void Map322_SameRebirthSplitAsMap295_EvenThoughMootUnderTermAB(int rebirthTier)
    {
        var shouldBlock = rebirthTier < 7;
        Assert.Equal(shouldBlock, AutoHuntBattleZoneEligibilityCatalog.IsBlocked(322, 157, rebirthTier));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    public void Map323_SameRebirthSplitAsMap296_EvenThoughMootUnderTermAB(int rebirthTier)
    {
        var shouldBlock = rebirthTier >= 7;
        Assert.Equal(shouldBlock, AutoHuntBattleZoneEligibilityCatalog.IsBlocked(323, 157, rebirthTier));
    }

    [Theory]
    [InlineData((short)319)]
    [InlineData((short)320)]
    [InlineData((short)321)]
    public void Maps319To321_AlwaysBlockRegardlessOfLevelOrRebirth(short mapId)
    {
        Assert.True(AutoHuntBattleZoneEligibilityCatalog.IsBlocked(mapId, 1, 0));
        Assert.True(AutoHuntBattleZoneEligibilityCatalog.IsBlocked(mapId, 200, 20));
    }

    [Theory]
    [InlineData((short)1)]
    [InlineData((short)38)]
    [InlineData((short)240)]
    [InlineData((short)241)]
    [InlineData((short)999)]
    public void UnrecognizedServerNumbers_ContributeNothingToBlocking(short mapId)
    {
        Assert.False(AutoHuntBattleZoneEligibilityCatalog.IsBlocked(mapId, 157, 7));
        Assert.False(AutoHuntBattleZoneEligibilityCatalog.IsBlocked(mapId, 1, 0));
    }
}
