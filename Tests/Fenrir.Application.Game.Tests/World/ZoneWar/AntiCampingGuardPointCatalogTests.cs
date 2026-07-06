using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class AntiCampingGuardPointCatalogTests
{
    [Fact]
    public void GuardedMapIds_IsTheFixedTwelveMapSet()
    {
        Assert.Equal(
            new short[] { 2, 3, 4, 7, 8, 9, 12, 13, 14, 141, 142, 143 },
            AntiCampingGuardPointCatalog.GuardedMapIds);
    }

    [Theory]
    [InlineData((short)2)]
    [InlineData((short)3)]
    [InlineData((short)4)]
    [InlineData((short)7)]
    [InlineData((short)8)]
    [InlineData((short)9)]
    [InlineData((short)12)]
    [InlineData((short)13)]
    [InlineData((short)14)]
    [InlineData((short)141)]
    [InlineData((short)142)]
    [InlineData((short)143)]
    public void IsGuardedMap_TrueForEveryEnumeratedServerNumber(short mapId)
    {
        Assert.True(AntiCampingGuardPointCatalog.Empty.IsGuardedMap(mapId));
    }

    [Theory]
    [InlineData((short)1)]
    [InlineData((short)39)]
    [InlineData((short)999)]
    public void IsGuardedMap_FalseForEveryOtherServerNumber(short mapId)
    {
        Assert.False(AntiCampingGuardPointCatalog.Empty.IsGuardedMap(mapId));
    }

    [Fact]
    public void Empty_HasNoConfiguredPointsForAnyGuardedMap()
    {
        foreach (var mapId in AntiCampingGuardPointCatalog.GuardedMapIds)
        {
            var points = AntiCampingGuardPointCatalog.Empty.GetPoints(mapId);
            Assert.Empty(points.HolyStoneSymbolPoints);
            Assert.Null(points.TowerPoint);
        }
    }

    [Fact]
    public void GetPoints_UnconfiguredMap_ReturnsEmptyPoints_EvenWhenGuarded()
    {
        var catalog = new AntiCampingGuardPointCatalog(new Dictionary<short, AntiCampingMapGuardPoints>());

        var points = catalog.GetPoints(2);

        Assert.Same(AntiCampingMapGuardPoints.Empty, points);
    }

    [Fact]
    public void GetPoints_ConfiguredMap_ReturnsConfiguredPoints()
    {
        var configured = new AntiCampingMapGuardPoints(
            [new AntiCampingGuardPoint(1, 2, 3)],
            new AntiCampingGuardPoint(4, 5, 6));
        var catalog = new AntiCampingGuardPointCatalog(
            new Dictionary<short, AntiCampingMapGuardPoints> { [2] = configured });

        var points = catalog.GetPoints(2);

        Assert.Same(configured, points);
    }

    [Fact]
    public void IsGuardedMap_IgnoresWhateverIsConfigured_OnlyTheFixedListMatters()
    {
        // A hypothetical caller mistakenly configures points for a map outside the fixed guarded set --
        // IsGuardedMap must still say no, since the guarded-map set itself is not caller-configurable data.
        var configured = new AntiCampingMapGuardPoints([new AntiCampingGuardPoint(0, 0, 0)], null);
        var catalog = new AntiCampingGuardPointCatalog(
            new Dictionary<short, AntiCampingMapGuardPoints> { [999] = configured });

        Assert.False(catalog.IsGuardedMap(999));
    }
}
