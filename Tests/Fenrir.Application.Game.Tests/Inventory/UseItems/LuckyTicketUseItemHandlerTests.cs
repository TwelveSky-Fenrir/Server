using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems;

/// <summary>
///     <see cref="LuckyTicketUseItemHandler" /> post-recovery (workstream lucky-ticket-handler-thresholds): the
///     "no eligible reward" clean-failure path (ticket kept) and the end-to-end success path (reward granted,
///     ticket consumed, fixed per-ticket-family serial stamped). Exact reward-tier/roll magnitudes are covered
///     purely at <see cref="LuckyTicketRewardResolverTests" />; this file exercises the handler's own
///     placement/consumption/mirror wiring around that pure resolver.
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

    private static void SeedInventory(Zone zone, byte page, byte slot, ItemStack item)
    {
        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(page, ImmutableDictionary<byte, ItemStack>.Empty.SetItem(slot, item)));
        zone.PostInventoryCommand(new InventoryZoneCommand(CharacterId, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

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
                throw new TimeoutException("LuckyTicketUseItemHandler task never completed.");
        }

        return await task;
    }

    [Fact]
    public void HandledItemIds_IsExactlyTheThreeLiveIds_ExcludingTheDead17124SubCase()
    {
        var ids = LuckyTicketUseItemHandler.HandledItemIds.ToHashSet();

        Assert.Equal(3, ids.Count);
        Assert.Contains(1035, ids);
        Assert.Contains(1036, ids);
        Assert.Contains(1037, ids);
        Assert.DoesNotContain(17124, ids);
    }

    [Theory]
    [InlineData(1035)]
    [InlineData(1036)]
    [InlineData(1037)]
    public async Task Use_NoEligibleRewardInCatalog_FailsCleanly_NeverConsumesTheTicket(int ticketItemId)
    {
        var (zone, state) = SetUp();
        var ticket = new ItemStack(ticketItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1);
        SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, ticket);

        var worldData = ZoneTestKit.EmptyWorldData(); // no items anywhere -> no draw can ever find an eligible reward
        var writer = new UseItemInventoryWriter(new FakeCharacterRepository(),
            NullLogger<UseItemInventoryWriter>.Instance);
        var handler = new LuckyTicketUseItemHandler(worldData, writer, NullLogger<LuckyTicketUseItemHandler>.Instance);
        var context = new UseItemContext(zone, state, CharacterId, AccountId, ContainerMatrix.InventoryPage0, 0,
            ticket, new ItemDefinition(WorldDataTestRows.Item(ticketItemId), []), 0);

        var response = await handler.HandleAsync(context, CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        var stillThere = after!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0);
        Assert.NotNull(stillThere);
        Assert.Equal(ticketItemId, stillThere!.Value.ItemId);
        Assert.Equal(1, stillThere.Value.Quantity);
    }

    [Theory]
    [InlineData(1035, 100000001)]
    [InlineData(1036, 100000002)]
    [InlineData(1037, 100000003)]
    public async Task Use_EligibleReward_GrantsIt_ConsumesTheTicket_AndStampsTheFamilySerial(int ticketItemId,
        int expectedSerial)
    {
        var (zone, state) = SetUp();
        // level1 < 5 forces the Common tier regardless of the roll (see LuckyTicketRewardResolverTests for the
        // exhaustive roll/level cascade coverage); level2 >= 1 collapses the item-level window to the single
        // fixed value level1+level2 = 6, removing the level pick's own randomness. Every one of the 8 sorts
        // tribe 0's own pool can pick at the Common tier has a matching item at that one fixed level, so the
        // draw always succeeds on its first attempt regardless of which sort the handler's live Random picks.
        state.Level = 1;
        state.Level2 = 5;
        state.PreviousTribe = 0;

        var ticket = new ItemStack(ticketItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1);
        SeedInventory(zone, ContainerMatrix.InventoryPage0, 0, ticket);

        byte[] commonSorts = [7, 9, 10, 11, 12, 13, 14, 15]; // Amulet/Armor/Glove/Ring/Boots/Sword/Blade/Marble
        var itemsById = new Dictionary<int, ItemDefinition>();
        foreach (var sort in commonSorts)
        {
            var itemId = 60000 + sort;
            itemsById[itemId] = new ItemDefinition(
                WorldDataTestRows.Item(itemId) with
                {
                    Level = 6,
                    Type = 1, // ICOMMON
                    Sort = sort,
                    CheckMonsterDrop = 2,
                    CheckAvatarTrade = 2,
                    CheckSetItem = 1,
                    EquipInfo1 = 1
                }, []);
        }

        var worldData = ZoneTestKit.EmptyWorldData(itemsById.ToFrozenDictionary());
        var characters = new FakeCharacterRepository();
        var writer = new UseItemInventoryWriter(characters, NullLogger<UseItemInventoryWriter>.Instance);
        var handler = new LuckyTicketUseItemHandler(worldData, writer, NullLogger<LuckyTicketUseItemHandler>.Instance);
        var context = new UseItemContext(zone, state, CharacterId, AccountId, ContainerMatrix.InventoryPage0, 0,
            ticket, new ItemDefinition(WorldDataTestRows.Item(ticketItemId), []), 0);

        var response = await RunToCompletionAsync(handler.HandleAsync(context, CancellationToken.None), zone);

        Assert.Equal(0, response.Result);

        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Null(after!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0)); // ticket consumed (slot removed)

        var page0 = after.Inventory.GetContainer(ContainerMatrix.InventoryPage0);
        var granted = page0.Values.Where(stack => stack.Serial == expectedSerial).ToList();
        Assert.Single(granted);
        Assert.Contains(granted[0].ItemId, itemsById.Keys);
    }
}
