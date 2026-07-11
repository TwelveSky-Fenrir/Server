using Fenrir.Application.Game.Domain.World.Configuration;

namespace Fenrir.Application.Game.Tests.World.Configuration;

public class ZoneConfigCatalogTests
{
    private static ZoneConfigCatalog Build(params (int MapId, ZoneConfig Config)[] entries)
    {
        return new ZoneConfigCatalog(entries.Select(e => new KeyValuePair<int, ZoneConfig>(e.MapId, e.Config)));
    }

    [Fact]
    public void Empty_HasNoEntries_AndEveryAccessorReturnsNeutralDefault()
    {
        var catalog = ZoneConfigCatalog.Empty;

        Assert.Equal(0, catalog.Count);
        Assert.False(catalog.TryGet(1, out var config));
        Assert.Equal(ZoneConfig.Unconfigured, config);

        Assert.Equal(0, catalog.GetMinLevel(1));
        Assert.Equal(0, catalog.GetMaxLevel(1));
        Assert.Equal(-1, catalog.GetOwnerTribe(1));
        Assert.Equal(0, catalog.GetSecondaryClassification(1));
        Assert.Equal(0, catalog.GetMaxUser(1));
        Assert.Equal(ZoneConfig.Unconfigured, catalog.GetOrDefault(1));
    }

    [Fact]
    public void TryGet_ConfiguredMap_ReturnsFrozenConfig()
    {
        var expected = new ZoneConfig { MinLevel = 100, MaxLevel = 145, OwnerTribe = 3, MaxUser = 600 };
        var catalog = Build((42, expected));

        Assert.True(catalog.TryGet(42, out var config));
        Assert.Equal(expected, config);
        Assert.Equal(1, catalog.Count);
    }

    [Fact]
    public void Accessors_ReturnConfiguredValues()
    {
        var catalog = Build((7, new ZoneConfig
        {
            MinLevel = 30,
            MaxLevel = 60,
            OwnerTribe = 10,
            SecondaryClassification = 2,
            MaxUser = 900
        }));

        Assert.Equal(30, catalog.GetMinLevel(7));
        Assert.Equal(60, catalog.GetMaxLevel(7));
        Assert.Equal(10, catalog.GetOwnerTribe(7));
        Assert.Equal(2, catalog.GetSecondaryClassification(7));
        Assert.Equal(900, catalog.GetMaxUser(7));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(351)]
    [InlineData(999)]
    public void Accessors_ForUnconfiguredOrOutOfRangeMap_YieldLegacyDefaults(int mapId)
    {
        var catalog = Build((5, new ZoneConfig { MinLevel = 10, MaxLevel = 20, OwnerTribe = 1 }));

        Assert.Equal(0, catalog.GetMinLevel(mapId));
        Assert.Equal(0, catalog.GetMaxLevel(mapId));
        Assert.Equal(-1, catalog.GetOwnerTribe(mapId));
        Assert.Equal(0, catalog.GetSecondaryClassification(mapId));
        Assert.Equal(0, catalog.GetMaxUser(mapId));
    }

    [Fact]
    public void GetMaxUser_Unconfigured_ReturnsZeroMeaningNoCap()
    {
        var catalog = Build((3, new ZoneConfig { MaxUser = 250 }));

        Assert.Equal(250, catalog.GetMaxUser(3));
        Assert.Equal(0, catalog.GetMaxUser(4));
    }

    [Fact]
    public void IsWithinLevelBand_UngatedWhenUnconfigured()
    {
        var catalog = ZoneConfigCatalog.Empty;

        Assert.True(catalog.IsWithinLevelBand(1, 1));
        Assert.True(catalog.IsWithinLevelBand(1, 145));
    }

    [Fact]
    public void IsWithinLevelBand_UngatedWhenMaxLevelIsZero()
    {
        var catalog = Build((9, new ZoneConfig { MinLevel = 0, MaxLevel = 0 }));

        Assert.True(catalog.IsWithinLevelBand(9, 0));
        Assert.True(catalog.IsWithinLevelBand(9, 200));
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(120, true)]
    [InlineData(145, true)]
    [InlineData(99, false)]
    [InlineData(146, false)]
    public void IsWithinLevelBand_InclusiveBand(int level, bool expected)
    {
        var catalog = Build((11, new ZoneConfig { MinLevel = 100, MaxLevel = 145 }));

        Assert.Equal(expected, catalog.IsWithinLevelBand(11, level));
    }

    [Fact]
    public void DuplicateMapIds_ResolveLastWriteWins()
    {
        var catalog = new ZoneConfigCatalog(new[]
        {
            new KeyValuePair<int, ZoneConfig>(1, new ZoneConfig { MaxUser = 100 }),
            new KeyValuePair<int, ZoneConfig>(1, new ZoneConfig { MaxUser = 200 })
        });

        Assert.Equal(1, catalog.Count);
        Assert.Equal(200, catalog.GetMaxUser(1));
    }

    [Fact]
    public void Constructor_NullEntries_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ZoneConfigCatalog(null!));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(350, true)]
    [InlineData(175, true)]
    [InlineData(0, false)]
    [InlineData(351, false)]
    [InlineData(-1, false)]
    public void IsValidZoneNumber_EnforcesOneToThreeFifty(int zoneNumber, bool expected)
    {
        Assert.Equal(expected, ZoneConfigCatalog.IsValidZoneNumber(zoneNumber));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(1000, true)]
    [InlineData(500, true)]
    [InlineData(0, false)]
    [InlineData(1001, false)]
    [InlineData(-5, false)]
    [InlineData(101, true)]
    public void IsValidMaxUser_EnforcesOneToOneThousand_NotTheMiswordedOneHundred(int maxUser, bool expected)
    {
        Assert.Equal(expected, ZoneConfigCatalog.IsValidMaxUser(maxUser));
    }
}
