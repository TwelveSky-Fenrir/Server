using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Framing;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers <see cref="Zone" />'s "mutual visibility" step of <c>Enter</c> (Zone.cs's own doc comment:
///     "existing neighbors learn about the new arrival, and the new arrival learns about them"). Regression
///     coverage for a mixed-up send direction that would leave a new arrival blind to everyone already in the
///     zone while double-sending the neighbor's own arrival notice to itself.
/// </summary>
public class ZoneEnterTests
{
    private static readonly int OneFrame = FrameWriter.FrameSizeOf<ZcAvatarActionRecv>();

    [Fact]
    public void Enter_NewArrivalLearnsAboutEachPreExistingNeighbor()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (sessionA, _) = ZoneTestKit.CreateSession(1);
        var (sessionB, pipeB) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(sessionA, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        // Same AOI cell as A (cell size 75, both floor to (0, 0)).
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(sessionB, 1, posX: 20f, posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        // The new arrival (B) must receive exactly one avatar-action frame per pre-existing neighbor (A) --
        // this is how it learns A was already there.
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

        // A must be told about B's arrival exactly once -- not zero (invisible new player) and not twice (the
        // direct-send loop and the broadcast both firing for the same recipient).
        var aInbox = ZoneTestKit.DrainOutbound(pipeA);
        Assert.Equal(OneFrame, aInbox.Length);
    }
}
