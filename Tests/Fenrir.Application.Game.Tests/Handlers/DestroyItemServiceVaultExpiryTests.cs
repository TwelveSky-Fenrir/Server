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

/// <summary>
///     C1-vault-expiry-enforcement: op89 (CZ_DESTROY_ITEM_SEND) is this protocol's closest "discard" analog
///     to a literal drop/move request, and the originating contract's own Edge cases confirm it is gated
///     identically to the primary CZ_USE_INVENTORY_ITEM_SEND trigger. Companion to
///     <see cref="DestroyItemServiceTests" />, which does not cover this gate at all.
/// </summary>
public class DestroyItemServiceVaultExpiryTests
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

    private static (ZoneClientSession Session, Zone Zone, PlayerRuntimeState State,
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
        return (session, zone, state!, new FakeCharacterRepository(), new FakeEventLogRepository());
    }

    private static void SeedInventory(Zone zone, byte page, byte index, ItemStack item)
    {
        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(page, ImmutableDictionary<byte, ItemStack>.Empty.SetItem(index, item)));
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
    public async Task LastPageExpired_RejectsAndWritesNoEventLogRow()
    {
        var (session, zone, state, characters, eventLog) = SetUp();
        state.InventoryDate = 20200101; // long past
        SeedInventory(zone, ContainerMatrix.InventoryPage1, 0,
            new ItemStack(RareItemId, 1, 10, 0, 0, 0, 0, 0, 0, 0, 777));
        var service = CreateService(characters, eventLog);

        var result = await RunToCompletionAsync(
            service.DestroyAsync(new DestroyItemRequest { Page1 = ContainerMatrix.InventoryPage1, Index1 = 0 },
                zone, state, 10, AccountId, CancellationToken.None), zone);

        Assert.Equal(DestroyItemOutcome.Rejected, result.Outcome);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task LastPageStillValid_DoesNotBlockAnOtherwiseSuccessfulDestroy()
    {
        var (session, zone, state, characters, eventLog) = SetUp();
        state.InventoryDate = 99991231; // far future -- still valid
        SeedInventory(zone, ContainerMatrix.InventoryPage1, 0,
            new ItemStack(RareItemId, 1, 10, 0, 0, 0, 0, 0, 0, 0, 777));
        var service = CreateService(characters, eventLog);

        var result = await RunToCompletionAsync(
            service.DestroyAsync(new DestroyItemRequest { Page1 = ContainerMatrix.InventoryPage1, Index1 = 0 },
                zone, state, 10, AccountId, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(DestroyItemOutcome.Applied, result.Outcome);
        Assert.Single(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task FirstPageAlwaysAccessible_EvenWhenInventoryDateIsExpired()
    {
        var (session, zone, state, characters, eventLog) = SetUp();
        state.InventoryDate = 20200101; // expired -- irrelevant, this destroy targets page0
        SeedInventory(zone, ContainerMatrix.InventoryPage0, 0,
            new ItemStack(RareItemId, 1, 10, 0, 0, 0, 0, 0, 0, 0, 777));
        var service = CreateService(characters, eventLog);

        var result = await RunToCompletionAsync(
            service.DestroyAsync(new DestroyItemRequest { Page1 = ContainerMatrix.InventoryPage0, Index1 = 0 },
                zone, state, 10, AccountId, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(DestroyItemOutcome.Applied, result.Outcome);
    }
}
