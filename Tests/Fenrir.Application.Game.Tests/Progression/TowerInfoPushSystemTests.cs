using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Progression;

public class TowerInfoPushSystemTests
{
    private const short TowerZoneNumber = 2;

    private static (Zone Zone, FakeDuplexPipe Pipe) TowerZoneWithWatcher(short mapId = TowerZoneNumber,
        bool wireTowerWar = true)
    {
        var towerWar = wireTowerWar ? new TowerWarState() : null;
        var zone = ZoneTestKit.CreateZone(mapId, towerWar: towerWar);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, mapId, "Watcher", tribe: 0)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        return (zone, pipe);
    }

    [Fact]
    public void Simulate_BelowTheCadence_DoesNotPush_ThenPushesExactlyOnCrossing60Ticks()
    {
        var (zone, pipe) = TowerZoneWithWatcher();
        var system = new TowerInfoPushSystem();

        system.Simulate(zone, 59);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));

        system.Simulate(zone, 1);
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Simulate_AMultiTickCatchUpBurst_PushesOnce_ThenResetsTheCounter()
    {
        var (zone, pipe) = TowerZoneWithWatcher();
        var system = new TowerInfoPushSystem();

        system.Simulate(zone, 200);
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe));

        system.Simulate(zone, 1);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Simulate_OnANonTowerZone_NeverPushes()
    {
        var (zone, pipe) = TowerZoneWithWatcher(999);
        var system = new TowerInfoPushSystem();

        system.Simulate(zone, 120);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Simulate_OnATowerZoneWithNoTowerStateWired_NeverPushes()
    {
        var (zone, pipe) = TowerZoneWithWatcher(wireTowerWar: false);
        var system = new TowerInfoPushSystem();

        system.Simulate(zone, 120);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }
}
