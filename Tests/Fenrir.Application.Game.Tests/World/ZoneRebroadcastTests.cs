using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers the periodic keep-alive avatar rebroadcast (<see cref="SimulationClock.AvatarRebroadcastInterval" /> = 3.5
///     s):
///     an idle avatar is re-announced to its AOI neighbors on this cadence, measured against <see cref="Zone.Tick" />
///     rather than wall time.
/// </summary>
public class ZoneRebroadcastTests
{
    private static readonly int OneFrame = FrameWriter.FrameSizeOf<AvatarActionResponse>();

    [Fact]
    public void StationaryPlayer_IsReAnnouncedToNeighbors_AfterTheLegacyCadenceElapses()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (sessionA, pipeA) = ZoneTestKit.CreateSession(1);
        var (sessionB, pipeB) = ZoneTestKit.CreateSession(2);

        // same AOI cell (cell size 75, both floor to (0, 0)) so they are mutual neighbors
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(sessionA, 1, posX: 10f, posZ: 10f)));
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(sessionB, 1, posX: 20f, posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        // drain the entry handshake's own mutual-visibility notices -- only the later keep-alive matters here
        ZoneTestKit.DrainOutbound(pipeA);
        ZoneTestKit.DrainOutbound(pipeB);

        zone.Tick(SimulationClock.AvatarRebroadcastInterval - TimeSpan.FromMilliseconds(100));
        Assert.Empty(ZoneTestKit.DrainOutbound(pipeA));
        Assert.Empty(ZoneTestKit.DrainOutbound(pipeB));

        zone.Tick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(OneFrame, ZoneTestKit.DrainOutbound(pipeA).Length);
        Assert.Equal(OneFrame, ZoneTestKit.DrainOutbound(pipeB).Length);
    }

    [Fact]
    public void StationaryPlayer_IsNotReAnnounced_BeforeTheCadenceElapses_EvenAcrossManySmallTicks()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (sessionA, pipeA) = ZoneTestKit.CreateSession(1);
        var (sessionB, pipeB) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(sessionA, 1, posX: 10f, posZ: 10f)));
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(sessionB, 1, posX: 20f, posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipeA);
        ZoneTestKit.DrainOutbound(pipeB);

        // 60 frames of 50 ms = 3.0 s, still under the 3.5 s cadence
        for (var i = 0; i < 60; i++)
            zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Empty(ZoneTestKit.DrainOutbound(pipeA));
        Assert.Empty(ZoneTestKit.DrainOutbound(pipeB));
    }

    [Fact]
    public void RebroadcastAfterCadence_ResetsTheTimer_ForTheNextCadence()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (sessionA, pipeA) = ZoneTestKit.CreateSession(1);
        var (sessionB, pipeB) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(sessionA, 1, posX: 10f, posZ: 10f)));
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(sessionB, 1, posX: 20f, posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipeA);
        ZoneTestKit.DrainOutbound(pipeB);

        zone.Tick(SimulationClock.AvatarRebroadcastInterval);
        Assert.Equal(OneFrame, ZoneTestKit.DrainOutbound(pipeA).Length);

        // fixed period, not re-armed to "now + interval" only after being read externally
        zone.Tick(TimeSpan.FromSeconds(1));
        Assert.Empty(ZoneTestKit.DrainOutbound(pipeA));

        zone.Tick(SimulationClock.AvatarRebroadcastInterval - TimeSpan.FromSeconds(1));
        Assert.Equal(OneFrame, ZoneTestKit.DrainOutbound(pipeA).Length);
    }

    [Fact]
    public void PlayerWithNoNeighbors_IsNeverBroadcastTo_EvenPastTheCadence()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        zone.Tick(SimulationClock.AvatarRebroadcastInterval + TimeSpan.FromSeconds(1));

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }
}
