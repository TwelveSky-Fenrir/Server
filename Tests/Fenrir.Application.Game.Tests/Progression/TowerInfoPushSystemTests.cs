using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Progression;

/// <summary>
///     A11 -- the periodic per-player tower-war info push (legacy per-player 60-tick "30-second" cadence). Driven
///     through the public <see cref="TowerInfoPushSystem.Simulate" /> so the accrual counter is fed exact legacy-tick
///     counts and nothing else in the zone can write to the pipe -- the push's own frame is then the only possible
///     outbound. Zone 2 hosts tower index 0.
/// </summary>
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
        ZoneTestKit.DrainOutbound(pipe); // discard the enter/replication noise
        return (zone, pipe);
    }

    [Fact]
    public void Simulate_BelowTheCadence_DoesNotPush_ThenPushesExactlyOnCrossing60Ticks()
    {
        var (zone, pipe) = TowerZoneWithWatcher();
        var system = new TowerInfoPushSystem();

        system.Simulate(zone, 59);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe)); // 59 < 60 -- nothing pushed yet

        system.Simulate(zone, 1);
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe)); // the 12-tower snapshot reached the watcher
    }

    [Fact]
    public void Simulate_AMultiTickCatchUpBurst_PushesOnce_ThenResetsTheCounter()
    {
        var (zone, pipe) = TowerZoneWithWatcher();
        var system = new TowerInfoPushSystem();

        system.Simulate(zone, 200); // a stalled host's catch-up burst
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe)); // pushed once, not 200/60 times

        system.Simulate(zone, 1);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe)); // counter reset to 0 (not decremented) -- fresh 60 needed
    }

    [Fact]
    public void Simulate_OnANonTowerZone_NeverPushes()
    {
        var (zone, pipe) = TowerZoneWithWatcher(999); // zone 999 hosts no tower slot
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
