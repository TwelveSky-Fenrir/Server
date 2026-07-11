using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems;

/// <summary>
///     Drives <see cref="TitleRemoveScrollUseItemHandler" /> (op23 items 1200/8419/1494) directly over a real
///     <see cref="Zone" /> so its <c>PostTribeProgressCommandAndWaitAsync</c> mirror actually applies.
/// </summary>
public class TitleRemoveScrollUseItemHandlerTests
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
                throw new TimeoutException("TitleRemoveScrollUseItemHandler task never completed.");
        }

        return await task;
    }

    private static (Zone Zone, PlayerRuntimeState State, FakeCharacterRepository Characters,
        FakeEventLogRepository EventLog, TitleRemoveScrollUseItemHandler Handler) SetUp()
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
        var writer = new UseItemInventoryWriter(characters, NullLogger<UseItemInventoryWriter>.Instance);
        var eventLog = new FakeEventLogRepository();
        var worldData = ZoneTestKit.EmptyWorldData();

        var handler = new TitleRemoveScrollUseItemHandler(worldData, writer, eventLog,
            NullLogger<TitleRemoveScrollUseItemHandler>.Instance);

        return (zone, state!, characters, eventLog, handler);
    }

    private static ItemStack Scroll(int itemId, int quantity = 1)
    {
        return new ItemStack(itemId, quantity, 0, 0, 0, 0, 0, 0, 0, 0, 1);
    }

    private static ItemDefinition Definition(int itemId)
    {
        return new ItemDefinition(WorldDataTestRows.Item(itemId), []);
    }

    private static UseItemContext Context(Zone zone, PlayerRuntimeState state, int itemId, int quantity = 1)
    {
        return new UseItemContext(zone, state, CharacterId, AccountId, ContainerMatrix.InventoryPage0, 0,
            Scroll(itemId, quantity), Definition(itemId), 0);
    }

    [Fact]
    public async Task Item1200_FullRefund_ClearsTitle_RefundsCumulativeCost_ConsumesScroll()
    {
        var (zone, state, characters, eventLog, handler) = SetUp();
        state.Title = 203; // family 2, rank 3 -- cumulative cost of ranks 0-2: 800+1700+2500 = 5000
        state.ContributionPoints = 1000;

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, 1200), CancellationToken.None), zone);

        Assert.Equal(0, response.Result);
        Assert.Equal(6000, response.Value);

        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(0, after!.Title);
        Assert.Equal(6000, after.ContributionPoints);

        Assert.NotNull(characters.LastReplacedContainer);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(EventLogCategory.Currency, logged.Category);
        Assert.Equal(5000, logged.DeltaMoney);
        Assert.Equal(1200, logged.ItemId);
    }

    [Fact]
    public async Task Item1494_ReducedRefund_Returns70PercentOfCumulativeCost()
    {
        var (zone, state, characters, eventLog, handler) = SetUp();
        state.Title = 104; // rank 4 -- cumulative cost of ranks 0-3: 800+1700+2500+3400 = 8400, 70% = 5880
        state.ContributionPoints = 0;

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, 1494), CancellationToken.None), zone);

        Assert.Equal(0, response.Result);
        Assert.Equal(5880, response.Value);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(0, after!.Title);
        Assert.Equal(5880, after.ContributionPoints);
    }

    [Fact]
    public async Task Item8419_TreatedIdenticallyToItem1200_FullRefund()
    {
        var (zone, state, characters, eventLog, handler) = SetUp();
        state.Title = 101; // rank 1 -- cumulative cost of rank 0 only: 800
        state.ContributionPoints = 0;

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, 8419), CancellationToken.None), zone);

        Assert.Equal(0, response.Result);
        Assert.Equal(800, response.Value);
    }

    [Fact]
    public async Task NoTitleHeld_FailsCleanly_NoStateChange()
    {
        var (zone, state, characters, eventLog, handler) = SetUp();
        state.Title = 0;
        state.ContributionPoints = 1000;

        var response = await handler.HandleAsync(Context(zone, state, 1200), CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Equal(1000, state.ContributionPoints);
        Assert.Null(characters.LastReplacedContainer);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task RefundWouldOverflowCeiling_FailsCleanly_NoStateChange()
    {
        var (zone, state, characters, eventLog, handler) = SetUp();
        state.Title = 203; // refund 5000
        state.ContributionPoints = 2_000_000_000; // already at the ceiling -- any positive refund overflows

        var response = await handler.HandleAsync(Context(zone, state, 1200), CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Equal(2_000_000_000, state.ContributionPoints);
        Assert.Null(characters.LastReplacedContainer);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task Level13Title_UnreachableInAnyLiveBuild_StillClearsTitleAndConsumesScroll_ForZeroRefund()
    {
        var (zone, state, characters, eventLog, handler) = SetUp();
        state.Title = 213; // portion 13 -- outside 1-12, CumulativeRefund floors this to 0
        state.ContributionPoints = 1000;

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, 1200), CancellationToken.None), zone);

        Assert.Equal(0, response.Result);
        Assert.Equal(1000, response.Value);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(0, after!.Title);
        Assert.Equal(1000, after.ContributionPoints);
        Assert.NotNull(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task StackedQuantity_ConsumptionUsesTheSharedPlaceholderWholeStackRule()
    {
        // CashItemStackConsumption.IsStackSafe is a shared, explicitly-flagged placeholder (returns false for
        // every item id today) -- this asserts THIS handler inherits that same posture rather than inventing
        // its own, not that a specific "correct" consumption behavior has been confirmed for these 3 ids.
        var (zone, state, characters, eventLog, handler) = SetUp();
        state.Title = 101;
        state.ContributionPoints = 0;

        await RunToCompletionAsync(handler.HandleAsync(Context(zone, state, 1200, 5), CancellationToken.None), zone);

        Assert.NotNull(characters.LastReplacedContainer);
        Assert.DoesNotContain(characters.LastReplacedContainer!.Value.Items, i => i.Slot == 0);
    }
}
