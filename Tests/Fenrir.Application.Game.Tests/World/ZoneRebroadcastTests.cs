using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World;

public class ZoneRebroadcastTests
{
    private static readonly int OneFrame = FrameWriter.FrameSizeOf<AvatarActionResponse>();

    [Fact]
    public void StationaryPlayer_IsReAnnouncedToNeighbors_AfterTheLegacyCadenceElapses()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (sessionA, pipeA) = ZoneTestKit.CreateSession(1);
        var (sessionB, pipeB) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(sessionA, 1, posX: 10f, posZ: 10f)));
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(sessionB, 1, posX: 20f, posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

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
