using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.ItemModification;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Crafting;

public class RuneStoneCraftServiceTests
{
    private const int AddStatItemId = RuneStoneCraftCatalog.AddStatItemId;
    private const int RuneCoreItemId = 93514;

    private static async Task<T> RunToCompletionAsync<T>(ValueTask<T> pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("RuneStoneCraftService task never completed.");
        }

        return await task;
    }

    private static (ZoneClientSession Session, Zone Zone, PlayerRuntimeState State, FakeCharacterRepository Repo,
        FakeEventLogQueue EventLog) SetUp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, 10);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(10, out var state));
        return (session, zone, state!, new FakeCharacterRepository(), new FakeEventLogQueue());
    }

    private static void SeedInventorySlot(Zone zone, PlayerRuntimeState state, byte container, byte slot,
        ItemStack stack)
    {
        var updated = state.Inventory.GetContainer(container).SetItem(slot, stack);
        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(container, updated));
        zone.PostInventoryCommand(new InventoryZoneCommand(10, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    private static ItemStack Stack(int itemId, int quantity)
    {
        return new ItemStack(itemId, quantity, 0, 0, 0, 0, 0, 0, 0, 0, 777);
    }

    [Fact]
    public async Task Applied_DecrementsSourceQuantity_KeepsSlotWhenQuantityRemains()
    {
        var (session, zone, state, repo, eventLog) = SetUp();
        SeedInventorySlot(zone, state, ContainerMatrix.InventoryPage0, 5, Stack(AddStatItemId, 3));
        SeedInventorySlot(zone, state, ContainerMatrix.InventoryPage0, 6, Stack(RuneCoreItemId, 1));

        var service = new RuneStoneCraftService(repo, eventLog, NullLogger<RuneStoneCraftService>.Instance);
        var result = await RunToCompletionAsync(
            service.CraftAsync(ContainerMatrix.InventoryPage0, 5, ContainerMatrix.InventoryPage0, 6,
                RuneStoneCraftCatalog.StatSlotSelectorStrength, 0, true, zone, state, 10, CancellationToken.None),
            zone);

        Assert.Null(session.DisconnectReason);
        Assert.True(result.Succeeded);

        Assert.NotNull(repo.LastReplacedContainer);
        Assert.Equal(ContainerMatrix.InventoryPage0, repo.LastReplacedContainer!.Value.Container);
        var persistedSource = Assert.Single(repo.LastReplacedContainer.Value.Items, i => i.Slot == 5);
        Assert.Equal(2, persistedSource.Quantity);
    }

    [Fact]
    public async Task Applied_QuantityDropsBelowOne_ClearsSourceSlot()
    {
        var (session, zone, state, repo, eventLog) = SetUp();
        SeedInventorySlot(zone, state, ContainerMatrix.InventoryPage0, 5, Stack(AddStatItemId, 1));
        SeedInventorySlot(zone, state, ContainerMatrix.InventoryPage0, 6, Stack(RuneCoreItemId, 1));

        var service = new RuneStoneCraftService(repo, eventLog, NullLogger<RuneStoneCraftService>.Instance);
        var result = await RunToCompletionAsync(
            service.CraftAsync(ContainerMatrix.InventoryPage0, 5, ContainerMatrix.InventoryPage0, 6,
                RuneStoneCraftCatalog.StatSlotSelectorStrength, 0, true, zone, state, 10, CancellationToken.None),
            zone);

        Assert.Null(session.DisconnectReason);
        Assert.True(result.Succeeded);

        Assert.NotNull(repo.LastReplacedContainer);
        Assert.DoesNotContain(repo.LastReplacedContainer!.Value.Items, i => i.Slot == 5);
    }

    [Fact]
    public async Task Applied_EnqueuesExactlyOneEventLogRow_WithActionLabelInPayload()
    {
        var (_, zone, state, repo, eventLog) = SetUp();
        SeedInventorySlot(zone, state, ContainerMatrix.InventoryPage0, 5, Stack(AddStatItemId, 1));
        SeedInventorySlot(zone, state, ContainerMatrix.InventoryPage0, 6, Stack(RuneCoreItemId, 1));

        var service = new RuneStoneCraftService(repo, eventLog, NullLogger<RuneStoneCraftService>.Instance);
        await RunToCompletionAsync(
            service.CraftAsync(ContainerMatrix.InventoryPage0, 5, ContainerMatrix.InventoryPage0, 6,
                RuneStoneCraftCatalog.StatSlotSelectorStrength, 0, true, zone, state, 10, CancellationToken.None),
            zone);

        var entry = Assert.Single(eventLog.Enqueued);
        Assert.Equal(10, entry.ActorCharacterId);
        Assert.Contains("STATADD", entry.Payload);
    }

    [Fact]
    public async Task Refused_DoesNotConsumeSourceOrLog()
    {
        var (session, zone, state, repo, eventLog) = SetUp();
        var packed = RuneStoneStatCodec.Encode(1, 1, 1, 1);
        SeedInventorySlot(zone, state, ContainerMatrix.InventoryPage0, 5, Stack(AddStatItemId, 1));
        SeedInventorySlot(zone, state, ContainerMatrix.InventoryPage0, 6, Stack(RuneCoreItemId, 1));

        var service = new RuneStoneCraftService(repo, eventLog, NullLogger<RuneStoneCraftService>.Instance);
        var result = await service.CraftAsync(ContainerMatrix.InventoryPage0, 5, ContainerMatrix.InventoryPage0, 6,
            RuneStoneCraftCatalog.StatSlotSelectorStrength, packed, true, zone, state, 10, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.False(result.Succeeded);
        Assert.Equal(RuneStoneCraftCatalog.ResultCodeAllStatsAlreadyFilled, result.ResultCode);
        Assert.Equal(0, result.NewPackedStat);
        Assert.Null(repo.LastReplacedContainer);
        Assert.Empty(eventLog.Enqueued);
    }

    [Fact]
    public async Task Disconnect_EmptySourceSlot_DoesNotConsumeOrLog()
    {
        var (_, zone, state, repo, eventLog) = SetUp();
        SeedInventorySlot(zone, state, ContainerMatrix.InventoryPage0, 6, Stack(RuneCoreItemId, 1));

        var service = new RuneStoneCraftService(repo, eventLog, NullLogger<RuneStoneCraftService>.Instance);
        var result = await service.CraftAsync(ContainerMatrix.InventoryPage0, 5, ContainerMatrix.InventoryPage0, 6,
            RuneStoneCraftCatalog.StatSlotSelectorStrength, 0, true, zone, state, 10, CancellationToken.None);

        Assert.Equal(RuneStoneCraftOutcome.Disconnect, result.Outcome);
        Assert.Null(repo.LastReplacedContainer);
        Assert.Empty(eventLog.Enqueued);
    }
}
