using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems;

public class ScrollOfSeekersUseItemHandlerTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;

    private static async Task<UseInventoryItemResponse> RunToCompletionAsync(
        ValueTask<UseInventoryItemResponse> pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("ScrollOfSeekersUseItemHandler task never completed.");
        }

        return await task;
    }

    private static (Zone Zone, PlayerRuntimeState State, FakeCharacterRepository Characters,
        ScrollOfSeekersUseItemHandler Handler) SetUp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(CharacterId);
        session.MarkTicketConsumed(AccountId, CharacterId);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(CharacterId, out var state));

        var characters = new FakeCharacterRepository();
        var eventLog = new FakeEventLogRepository();
        var writer = new UseItemInventoryWriter(characters, NullLogger<UseItemInventoryWriter>.Instance);
        var handler = new ScrollOfSeekersUseItemHandler(writer, eventLog,
            NullLogger<ScrollOfSeekersUseItemHandler>.Instance);

        return (zone, state!, characters, handler);
    }

    private static ItemStack Scroll(int itemId, int quantity = 1)
    {
        return new ItemStack(itemId, quantity, 0, 0, 0, 0, 0, 0, 0, 0, 1);
    }

    private static UseItemContext Context(Zone zone, PlayerRuntimeState state, int itemId, ItemStack item,
        int value = 0)
    {
        return new UseItemContext(zone, state, CharacterId, AccountId, 0, 0, item,
            new ItemDefinition(WorldDataTestRows.Item(itemId), []), value);
    }

    [Theory]
    [InlineData(1124, 180)]
    [InlineData(8409, 180)]
    [InlineData(1187, 900)]
    [InlineData(7016, 900)]
    [InlineData(8410, 900)]
    public async Task Use_AddsTheDocumentedAmount_ConsumesOneUnit_NeverEchoesTheNewTotal(int itemId,
        int expectedAmount)
    {
        var (zone, state, characters, handler) = SetUp();
        state.ScrollOfSeekersTime = 10;
        var item = Scroll(itemId);

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, itemId, item), CancellationToken.None), zone);

        Assert.Equal(0, response.Result);
        Assert.Equal(0, response.Value);
        Assert.Equal(0, response.Value2);

        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(10 + expectedAmount, after!.ScrollOfSeekersTime);
        Assert.NotNull(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task WouldExceedCeiling_FailsCleanly_NoConsumption()
    {
        var (zone, state, characters, handler) = SetUp();
        state.ScrollOfSeekersTime = BankedCounterMath.GlobalCeiling;
        var item = Scroll(1124);

        var response = await handler.HandleAsync(Context(zone, state, 1124, item), CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Equal(BankedCounterMath.GlobalCeiling, state.ScrollOfSeekersTime);
        Assert.Null(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task BulkStackAndClientSuppliedValue_AreIgnored_OnlyOneUnitConsumed_FlatAmountCredited()
    {
        var (zone, state, characters, handler) = SetUp();
        state.ScrollOfSeekersTime = 0;
        var item = Scroll(1124, 5);

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, 1124, item, 3), CancellationToken.None), zone);

        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(180, after!.ScrollOfSeekersTime);

        var replaced = characters.LastReplacedContainer;
        Assert.NotNull(replaced);
        var remainingSlot = replaced!.Value.Items.SingleOrDefault(i => i.ItemId == 1124);
        Assert.NotNull(remainingSlot);
        Assert.Equal(4, remainingSlot!.Quantity);
    }

    [Fact]
    public void HandledItemIds_IsExactlyTheFiveDocumentedIds()
    {
        var ids = ScrollOfSeekersUseItemHandler.HandledItemIds.ToHashSet();

        Assert.Equal(5, ids.Count);
        foreach (var expected in new[] { 1124, 1187, 7016, 8409, 8410 })
            Assert.Contains(expected, ids);
    }
}
