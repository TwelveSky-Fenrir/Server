using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World;

public class ZoneEnterTests
{
    private static readonly int OneFrame = FrameWriter.FrameSizeOf<AvatarActionResponse>();

    [Fact]
    public void Enter_NewArrivalLearnsAboutEachPreExistingNeighbor()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (sessionA, _) = ZoneTestKit.CreateSession(1);
        var (sessionB, pipeB) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(sessionA, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(sessionB, 1, posX: 20f, posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var bInbox = ZoneTestKit.DrainOutbound(pipeB);
        Assert.Equal(OneFrame, bInbox.Length);
    }

    [Fact]
    public void Enter_PreExistingNeighborLearnsAboutTheNewArrival_ExactlyOnce()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (sessionA, pipeA) = ZoneTestKit.CreateSession(1);
        var (sessionB, _) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(sessionA, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipeA);

        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(sessionB, 1, posX: 20f, posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var aInbox = ZoneTestKit.DrainOutbound(pipeA);
        Assert.Equal(OneFrame, aInbox.Length);
    }

    [Fact]
    public void Enter_PreExistingNeighborTaggedToADifferentDungeonInstance_IsNotLearnedAboutByTheNewArrival()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (sessionA, _) = ZoneTestKit.CreateSession(1);
        var (sessionB, pipeB) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(sessionA, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var stateA));
        stateA!.DungeonInstanceId = 1;

        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(sessionB, 1, posX: 20f, posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Empty(ZoneTestKit.DrainOutbound(pipeB));
    }

    [Fact]
    public void Enter_StalePriorSessionForSameCharacter_IsEvictedAndReplacedByTheNewerSession()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (staleSession, stalePipe) = ZoneTestKit.CreateSession(1);
        var (newSession, _) = ZoneTestKit.CreateSession(2);

        staleSession.CurrentZone = zone;

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(staleSession, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(stalePipe);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(newSession, 1, posX: 20f, posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(DisconnectReason.Evicted, staleSession.DisconnectReason);
        Assert.Null(staleSession.CurrentZone);

        Assert.True(zone.TryGetPlayer(10, out var tracked));
        Assert.Same(newSession, tracked!.Session);
        Assert.Equal(20f, tracked.PosX);
        Assert.Equal(20f, tracked.PosZ);
    }

    [Fact]
    public void Enter_TrueDuplicateForTheSameSession_IsIgnored_NotEvicted()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.CurrentZone = zone;

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 20f, posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Same(zone, session.CurrentZone);

        Assert.True(zone.TryGetPlayer(10, out var tracked));
        Assert.Equal(10f, tracked!.PosX);
        Assert.Equal(10f, tracked.PosZ);
    }
}
