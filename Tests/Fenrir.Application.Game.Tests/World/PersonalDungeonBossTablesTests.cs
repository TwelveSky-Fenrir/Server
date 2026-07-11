using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Zone-241 "LOD" personal-dungeon boss tables (Catalog A/D/E) -- see <see cref="PersonalDungeonBossTables" />'s
///     own remarks for the confirmed three-way summon race these tables feed.
/// </summary>
public class PersonalDungeonBossTablesTests
{
    [Theory]
    [InlineData(0, 725)]
    [InlineData(1, 725)]
    [InlineData(2, 728)]
    [InlineData(3, 729)]
    [InlineData(4, 730)]
    [InlineData(5, 731)]
    [InlineData(6, 730)]
    [InlineData(7, 736)]
    [InlineData(8, 737)]
    [InlineData(9, 738)]
    [InlineData(10, 748)]
    [InlineData(11, 749)]
    [InlineData(12, 750)]
    [InlineData(13, 750)] // out-of-range falls through to the tier-12 fallback
    [InlineData(-1, 750)]
    public void ResolveCatalogA_MatchesZoneEnterHandlerTable(int rebirthTier, int expectedBossId)
    {
        Assert.Equal(expectedBossId, PersonalDungeonBossTables.ResolveCatalogA(rebirthTier));
    }

    [Theory]
    [InlineData(0, 725)]
    [InlineData(1, 725)]
    [InlineData(2, 726)]
    [InlineData(3, 727)]
    [InlineData(4, 728)]
    [InlineData(5, 729)]
    [InlineData(6, 730)]
    [InlineData(7, 736)]
    [InlineData(8, 737)]
    [InlineData(9, 738)]
    [InlineData(10, 748)]
    [InlineData(11, 749)]
    [InlineData(12, 750)]
    [InlineData(13, 750)]
    [InlineData(-1, 750)]
    public void ResolveCatalogE_MatchesTickDrivenSummonTable(int rebirthTier, int expectedBossId)
    {
        Assert.Equal(expectedBossId, PersonalDungeonBossTables.ResolveCatalogE(rebirthTier));
    }

    [Theory]
    [InlineData(325, 725)]
    [InlineData(326, 726)]
    [InlineData(327, 727)]
    [InlineData(328, 728)]
    [InlineData(329, 729)]
    [InlineData(330, 730)]
    [InlineData(0, 725)] // unreachable-in-practice fallback
    public void ResolveCatalogD_MatchesServerNumberTable(int serverNumber, int expectedBossId)
    {
        Assert.Equal(expectedBossId, PersonalDungeonBossTables.ResolveCatalogD(serverNumber));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void CatalogAAndCatalogE_AgreeOutsideTiersTwoThroughFive(int rebirthTier)
    {
        Assert.Equal(PersonalDungeonBossTables.ResolveCatalogA(rebirthTier), PersonalDungeonBossTables.ResolveCatalogE(rebirthTier));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void CatalogAAndCatalogE_DivergeAtTiersTwoThroughFive_ConfirmingTheRaceHasVisibleConsequence(int rebirthTier)
    {
        Assert.NotEqual(PersonalDungeonBossTables.ResolveCatalogA(rebirthTier), PersonalDungeonBossTables.ResolveCatalogE(rebirthTier));
    }

    [Fact]
    public void CatalogAAndESummonPosition_MatchesFixedCoordinates()
    {
        Assert.Equal((1f, 21f, 0f), PersonalDungeonBossTables.CatalogAAndESummonPosition);
    }

    [Fact]
    public void CatalogDSummonPosition_DiffersOnlyOnFirstAxis()
    {
        Assert.Equal((0f, 21f, 0f), PersonalDungeonBossTables.CatalogDSummonPosition);
    }
}
