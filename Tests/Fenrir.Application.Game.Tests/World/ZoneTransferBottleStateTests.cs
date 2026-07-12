using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World;

public class ZoneTransferBottleStateTests
{
    [Fact]
    public void SameShardHandoff_CarriesBottleSlotsAndActiveDrunkStateAcrossTheTransfer()
    {
        var source = ZoneTestKit.CreateZone(2);
        var target = ZoneTestKit.CreateZone(3);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 2)));
        source.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(source.TryGetPlayer(10, out var beforeHandoff));
        beforeHandoff!.BottleSlots = beforeHandoff.BottleSlots.SetItem(4, (878, 12));
        beforeHandoff.DrunkBottleIndex = 4;
        beforeHandoff.DrunkBottleTicksRemaining = 60;

        source.Post(ZoneCommand.Leave(10, target));
        source.Tick(TimeSpan.FromMilliseconds(50));
        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(target.TryGetPlayer(10, out var arrived));
        Assert.Equal((878, 12), arrived!.BottleSlots[4]);
        Assert.Equal(4, arrived.DrunkBottleIndex);
        Assert.Equal(60, arrived.DrunkBottleTicksRemaining);
    }
}
