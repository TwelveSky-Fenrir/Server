using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Forge;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.ItemModification;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class DestroyItemServiceTests
{
    private const int RareItemId = 87000;
    private const int StoneItemId = 1021;
    private const int AccountId = 42;

    private static async Task<DestroyItemResult> RunToCompletionAsync(ValueTask<DestroyItemResult> pending,
        Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("DestroyItemService task never completed.");
        }

        return await task;
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, Zone Zone, PlayerRuntimeState State,
        FakeCharacterRepository Characters, FakeEventLogRepository EventLog) SetUp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, 10);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(10, out var state));
        return (session, pipe, zone, state!, new FakeCharacterRepository(), new FakeEventLogRepository());
    }

    private static void SeedInventory(Zone zone, ItemStack item)
    {
        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0,
                ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, item)));
        zone.PostInventoryCommand(new InventoryZoneCommand(10, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    private static DestroyItemService CreateService(FakeCharacterRepository characters,
        FakeEventLogRepository eventLog)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [RareItemId] = new(WorldDataTestRows.Item(RareItemId) with
            {
                Type = DestroyResolver.RareItemType, Sort = 7, CheckImprove = 2
            }, []),
            [StoneItemId] = new(WorldDataTestRows.Item(StoneItemId), [])
        }.ToFrozenDictionary();

        return new DestroyItemService(characters, ZoneTestKit.EmptyWorldData(itemsById), eventLog,
            NullLogger<DestroyItemService>.Instance);
    }

    [Fact]
    public async Task SuccessfulDestroy_WritesAnItemDestroyEventLogRow_WithTheDestroyedItemAndMoneyGranted()
    {
        var (session, _, zone, state, characters, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(RareItemId, 1, 10, 0, 0, 0, 0, 0, 0, 0, 777));
        var service = CreateService(characters, eventLog);

        var result = await RunToCompletionAsync(
            service.DestroyAsync(new DestroyItemRequest { Page1 = ContainerMatrix.InventoryPage0, Index1 = 0 },
                zone, state, 10, AccountId, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(DestroyItemOutcome.Applied, result.Outcome);
        Assert.Equal(1_000_000, result.Money);
        Assert.Equal(StoneItemId, result.StoneItemId);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(EventLogCategory.ItemDestroy, logged.Category);
        Assert.Equal(AccountId, logged.ActorAccountId);
        Assert.Equal(10, logged.ActorCharacterId);
        Assert.Null(logged.TargetAccountId);
        Assert.Null(logged.TargetCharacterId);
        Assert.Equal(1_000_000L, logged.DeltaMoney);
        Assert.Equal(RareItemId, logged.ItemId);
        Assert.Equal(1, logged.Quantity);
        Assert.Equal((byte?)1, logged.Outcome);
        Assert.NotNull(logged.Payload);
        Assert.Contains($"StoneItemId={StoneItemId}", logged.Payload);
    }

    [Fact]
    public async Task RejectedDestroy_EnchantBelowThreshold_WritesNoEventLogRow()
    {
        var (session, _, zone, state, characters, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(RareItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 777));
        var service = CreateService(characters, eventLog);

        var result = await service.DestroyAsync(
            new DestroyItemRequest { Page1 = ContainerMatrix.InventoryPage0, Index1 = 0 }, zone, state, 10,
            AccountId, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(DestroyItemOutcome.Rejected, result.Outcome);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task MoneyCapOverflow_TreatedAsRejected_WritesNoEventLogRow()
    {
        var (session, _, zone, state, characters, eventLog) = SetUp();
        characters.ThrowOnAdjustMoney = true;
        SeedInventory(zone, new ItemStack(RareItemId, 1, 10, 0, 0, 0, 0, 0, 0, 0, 777));
        var service = CreateService(characters, eventLog);

        var result = await service.DestroyAsync(
            new DestroyItemRequest { Page1 = ContainerMatrix.InventoryPage0, Index1 = 0 }, zone, state, 10,
            AccountId, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(DestroyItemOutcome.Rejected, result.Outcome);
        Assert.Empty(eventLog.LoggedEvents);
    }
}
