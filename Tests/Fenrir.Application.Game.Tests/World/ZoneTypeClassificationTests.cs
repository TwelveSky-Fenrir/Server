using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World;

public class ZoneTypeClassificationTests
{
    private const short Zone126MapId = 126;
    private const short Zone039MapId = 39;
    private const short OrdinaryMapId = 50;

    private static GameServerOptions OptionsWith(ISet<short> zone126, ISet<short> zone039)
    {
        return new GameServerOptions { Zone126TypeMapIds = zone126, Zone039TypeMapIds = zone039 };
    }

    [Fact]
    public void IsZone126TypeZone_ReflectsConfiguredMapIds()
    {
        var options = OptionsWith(new HashSet<short> { Zone126MapId }, new HashSet<short>());

        var zone126 = ZoneTestKit.CreateZone(Zone126MapId, options);
        var zoneOrdinary = ZoneTestKit.CreateZone(OrdinaryMapId, options);

        Assert.True(zone126.IsZone126TypeZone);
        Assert.False(zoneOrdinary.IsZone126TypeZone);
    }

    [Fact]
    public void IsZone039TypeZone_ReflectsConfiguredMapIds()
    {
        var options = OptionsWith(new HashSet<short>(), new HashSet<short> { Zone039MapId });

        var zone039 = ZoneTestKit.CreateZone(Zone039MapId, options);
        var zoneOrdinary = ZoneTestKit.CreateZone(OrdinaryMapId, options);

        Assert.True(zone039.IsZone039TypeZone);
        Assert.False(zoneOrdinary.IsZone039TypeZone);
    }

    [Fact]
    public void Zone126And039_AreIndependentClassifications()
    {
        var options = OptionsWith(new HashSet<short> { Zone126MapId }, new HashSet<short> { Zone039MapId });

        var zone126 = ZoneTestKit.CreateZone(Zone126MapId, options);
        var zone039 = ZoneTestKit.CreateZone(Zone039MapId, options);

        Assert.True(zone126.IsZone126TypeZone);
        Assert.False(zone126.IsZone039TypeZone);

        Assert.True(zone039.IsZone039TypeZone);
        Assert.False(zone039.IsZone126TypeZone);
    }

    [Fact]
    public void UnconfiguredShard_LeavesBothFlagsFalse_NoError()
    {
        var zone = ZoneTestKit.CreateZone(OrdinaryMapId, OptionsWith(new HashSet<short>(), new HashSet<short>()));

        Assert.False(zone.IsZone126TypeZone);
        Assert.False(zone.IsZone039TypeZone);
    }

    [Fact]
    public void IsDungeonServerZone_KnownDungeonServerNumber_IsTrue_WithNoConfigurationNeeded()
    {
        var zone = ZoneTestKit.CreateZone(46);

        Assert.True(zone.IsDungeonServerZone);
    }

    [Fact]
    public void IsDungeonServerZone_OrdinaryMapId_IsFalse()
    {
        var zone = ZoneTestKit.CreateZone(OrdinaryMapId);

        Assert.False(zone.IsDungeonServerZone);
    }
}
