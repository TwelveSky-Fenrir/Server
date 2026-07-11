using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers the two config-driven shard-type flags in <c>Zone.ZoneTypeClassification.cs</c>
///     (<c>Zone.IsZone126TypeZone</c>/<c>Zone.IsZone039TypeZone</c>) -- the Fenrir translation of legacy's
///     boot-time <c>mCheckZone126TypeServer</c>/<c>mCheckZone039TypeServer</c> arms
///     (Server/ts25zone/S07_MyGame01.cpp:895-913,821-834). Same shape as the already-covered
///     <c>IsZone241TypeZone</c> classification, so these mirror that test's structure.
/// </summary>
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
        // Contract Side effect 1: "These two classifications are independent of one another." A map listed in
        // one set must not implicitly satisfy the other.
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
        // Contract error semantics: an unrecognized server number leaves both flags in their initialized-false
        // state (Server/ts25zone/S07_MyGame01.cpp:822,896) -- here, empty map-id sets, the Fenrir default.
        var zone = ZoneTestKit.CreateZone(OrdinaryMapId, OptionsWith(new HashSet<short>(), new HashSet<short>()));

        Assert.False(zone.IsZone126TypeZone);
        Assert.False(zone.IsZone039TypeZone);
    }
}
