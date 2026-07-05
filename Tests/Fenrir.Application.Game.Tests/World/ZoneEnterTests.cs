using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Framing;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers <see cref="Zone" />'s mutual-visibility step of <c>Enter</c>: new arrival and existing neighbors must
///     each learn about the other exactly once.
/// </summary>
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

        // same AOI cell as A (cell size 75, both floor to (0, 0))
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
}
