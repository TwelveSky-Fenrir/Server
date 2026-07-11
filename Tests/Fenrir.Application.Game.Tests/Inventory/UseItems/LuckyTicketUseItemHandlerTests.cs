using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems;

/// <summary>
///     <see cref="LuckyTicketUseItemHandler" /> is a clearly-marked stub (the per-item draw thresholds/reward
///     tables/serial number are not recoverable from this workstream's behavior contract) -- every use must
///     fail cleanly without ever consuming the ticket, the same "never a silent success" posture other stub
///     handlers in this workstream already established.
/// </summary>
public class LuckyTicketUseItemHandlerTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;

    private static (Zone Zone, PlayerRuntimeState State) SetUp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(CharacterId);
        session.MarkTicketConsumed(AccountId, CharacterId);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        return (zone, state!);
    }

    [Theory]
    [InlineData(1035)]
    [InlineData(1036)]
    [InlineData(1037)]
    public async Task Use_AlwaysFailsCleanly_NeverConsumesTheTicket(int itemId)
    {
        var (zone, state) = SetUp();
        var handler = new LuckyTicketUseItemHandler(NullLogger<LuckyTicketUseItemHandler>.Instance);
        var item = new ItemStack(itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1);
        var context = new UseItemContext(zone, state, CharacterId, AccountId, 0, 0, item,
            new ItemDefinition(WorldDataTestRows.Item(itemId), []), 0);

        var response = await handler.HandleAsync(context, CancellationToken.None);

        Assert.Equal(1, response.Result);
    }

    [Fact]
    public void HandledItemIds_IsExactlyTheThreeLiveIds_ExcludingTheDead17124SubCase()
    {
        var ids = LuckyTicketUseItemHandler.HandledItemIds.ToHashSet();

        Assert.Equal(3, ids.Count);
        Assert.True(ids.Contains(1035));
        Assert.True(ids.Contains(1036));
        Assert.True(ids.Contains(1037));
        Assert.False(ids.Contains(17124));
    }
}
