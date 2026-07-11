using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems;

/// <summary>
///     <see cref="ScrollOfSeekersUseItemHandler" /> is a clearly-marked stub (the 180-vs-900 per-id amount
///     split is not recoverable from this workstream's behavior contract) -- every use must fail cleanly
///     without ever consuming the scroll, the same "never a silent success" posture other stub handlers in
///     this workstream already established.
/// </summary>
public class ScrollOfSeekersUseItemHandlerTests
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
    [InlineData(1124)]
    [InlineData(1187)]
    [InlineData(7016)]
    [InlineData(8409)]
    [InlineData(8410)]
    public async Task Use_AlwaysFailsCleanly_NeverConsumesTheScroll(int itemId)
    {
        var (zone, state) = SetUp();
        var handler = new ScrollOfSeekersUseItemHandler(NullLogger<ScrollOfSeekersUseItemHandler>.Instance);
        var item = new ItemStack(itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1);
        var context = new UseItemContext(zone, state, CharacterId, AccountId, 0, 0, item,
            new ItemDefinition(WorldDataTestRows.Item(itemId), []), 0);

        var response = await handler.HandleAsync(context, CancellationToken.None);

        Assert.Equal(1, response.Result);
    }

    [Fact]
    public void HandledItemIds_IsExactlyTheFiveDocumentedIds()
    {
        var ids = ScrollOfSeekersUseItemHandler.HandledItemIds.ToHashSet();

        Assert.Equal(5, ids.Count);
        foreach (var expected in new[] { 1124, 1187, 7016, 8409, 8410 })
            Assert.True(ids.Contains(expected));
    }
}
