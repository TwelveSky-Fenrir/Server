using Fenrir.Application.Game.Domain.Progression;
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
        var configured = new AntiCampingMapGuardPoints([new AntiCampingGuardPoint(0, 0, 0)], null);
        var catalog = new AntiCampingGuardPointCatalog(
            new Dictionary<short, AntiCampingMapGuardPoints> { [999] = configured });

        Assert.False(catalog.IsGuardedMap(999));
    }
}

public class AntiCampingGuardPointCatalogDefaultTests
{
    private static AntiCampingMapGuardPoints Points(short mapId)
    {
        return AntiCampingGuardPointCatalog.Default.GetPoints(mapId);
    }

    [Theory]
    [InlineData((short)2, new[] { -1810f, -1f, 3155f })]
    [InlineData((short)4, new[] { 7839f, 461f, 6520f })]
    [InlineData((short)7, new[] { -831f, 10f, -3392f })]
    [InlineData((short)9, new[] { -2438f, -590f, 6697f })]
    [InlineData((short)12, new[] { -4045f, 0f, 1648f })]
    [InlineData((short)14, new[] { 7174f, 336f, 6191f })]
    [InlineData((short)141, new[] { -1132f, 0f, 3486f })]
    [InlineData((short)143, new[] { -38f, 0f, 4432f })]
    public void SingleSymbolPointMaps_MatchContractTable(short mapId, float[] xyz)
    {
        var points = Points(mapId);

        Assert.Equal(
            new[] { new AntiCampingGuardPoint(xyz[0], xyz[1], xyz[2]) },
            points.HolyStoneSymbolPoints);
    }

    [Fact]
    public void Map3_ThreeSymbolPoints_MatchContractTable_InSwitchCaseOrder()
    {
        Assert.Equal(
            new[]
            {
                new AntiCampingGuardPoint(-6760f, 0f, 1187f),
                new AntiCampingGuardPoint(-7780f, 0f, 400f),
                new AntiCampingGuardPoint(-6864f, 0f, 2761f)
            },
            Points(3).HolyStoneSymbolPoints);
    }

    [Fact]
    public void Map8_ThreeSymbolPoints_MatchContractTable_InSwitchCaseOrder()
    {
        Assert.Equal(
            new[]
            {
                new AntiCampingGuardPoint(4410f, 28f, 4666f),
                new AntiCampingGuardPoint(5493f, 38f, 4174f),
                new AntiCampingGuardPoint(5545f, 41f, 6452f)
            },
            Points(8).HolyStoneSymbolPoints);
    }

    [Fact]
    public void Map13_ThreeSymbolPoints_MatchContractTable_InSwitchCaseOrder()
    {
        Assert.Equal(
            new[]
            {
                new AntiCampingGuardPoint(-7610f, 0f, 5763f),
                new AntiCampingGuardPoint(-6684f, 0f, 5319f),
                new AntiCampingGuardPoint(-5397f, 0f, 5819f)
            },
            Points(13).HolyStoneSymbolPoints);
    }

    [Fact]
    public void Map142_ThreeSymbolPoints_MatchContractTable_InSwitchCaseOrder()
    {
        Assert.Equal(
            new[]
            {
                new AntiCampingGuardPoint(-2505f, 0f, 7201f),
                new AntiCampingGuardPoint(-2063f, 1f, 6846f),
                new AntiCampingGuardPoint(-2948f, 8f, 6105f)
            },
            Points(142).HolyStoneSymbolPoints);
    }

    [Theory]
    [InlineData((short)2, -1276f, -5f, 1826f)]
    [InlineData((short)3, -8086f, 0f, 6225f)]
    [InlineData((short)4, 3770f, 95f, 3173f)]
    [InlineData((short)7, -1879f, 2f, -1105f)]
    [InlineData((short)8, 7326f, 40f, 4224f)]
    [InlineData((short)9, -3703f, -593f, 6223f)]
    [InlineData((short)12, -1306f, -2f, -380f)]
    [InlineData((short)13, -7897f, 9f, 1899f)]
    [InlineData((short)14, 6290f, 340f, 4775f)]
    [InlineData((short)141, 4289f, 0f, 3645f)]
    [InlineData((short)142, 32f, 0f, 2663f)]
    [InlineData((short)143, -67f, -12f, 3046f)]
    public void EveryGuardedMap_TowerPoint_MatchesContractTable(short mapId, float x, float y, float z)
    {
        Assert.Equal(new AntiCampingGuardPoint(x, y, z), Points(mapId).TowerPoint);
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
    public void EveryGuardedMap_TowerPoint_MatchesTowerGuardianCatalog_NoDrift(short mapId)
    {
        var found = TowerGuardianCatalog.TryGetGuardianLocation(mapId, out var x, out var y, out var z);

        Assert.True(found);
        Assert.Equal(new AntiCampingGuardPoint(x, y, z), Points(mapId).TowerPoint);
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
    public void EveryGuardedMap_HasAtLeastOneSymbolPointAndExactlyOneTowerPoint(short mapId)
    {
        var points = Points(mapId);

        Assert.NotEmpty(points.HolyStoneSymbolPoints);
        Assert.NotNull(points.TowerPoint);
    }

    [Fact]
    public void Map3_8_13_142_HaveExactlyThreeSymbolPoints()
    {
        foreach (var mapId in new short[] { 3, 8, 13, 142 })
            Assert.Equal(3, Points(mapId).HolyStoneSymbolPoints.Length);
    }

    [Fact]
    public void EveryOtherGuardedMap_HasExactlyOneSymbolPoint()
    {
        foreach (var mapId in new short[] { 2, 4, 7, 9, 12, 14, 141, 143 })
            Assert.Single(Points(mapId).HolyStoneSymbolPoints);
    }
}
