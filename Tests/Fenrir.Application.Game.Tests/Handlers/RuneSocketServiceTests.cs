using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.Handlers;
using Fenrir.Application.Game.Handlers.Handlers;
using Fenrir.Application.Game.Services.ItemModification;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

/// <summary>
///     Drives the real <see cref="RuneSocketService" /> (opcode 157) over a real <see cref="Zone" />; ticks the
///     zone while the service's own <c>PostRuneSocketCommandAndWaitAsync</c>/<c>PostInventoryCommandAndWaitAsync</c>
///     awaits are pending, same pattern as <c>FishingCatchHandlerTests</c>.
/// </summary>
public class RuneSocketServiceTests
{
    private static async Task<T> RunToCompletionAsync<T>(ValueTask<T> pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("RuneSocketService task never completed.");
        }

        return await task;
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, Zone Zone, PlayerRuntimeState State,
        FakeCharacterRepository Repository) SetUp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, 10);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(10, out var state));
        return (session, pipe, zone, state!, new FakeCharacterRepository());
    }

    private static void SeedInventorySlot(Zone zone, byte container, byte slot, ItemStack stack)
    {
        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(container, ImmutableDictionary<byte, ItemStack>.Empty.SetItem(slot, stack)));
        zone.PostInventoryCommand(new InventoryZoneCommand(10, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public async Task Insert_ValidRuneItem_SocketsAndClearsInventorySlot()
    {
        var (session, _, zone, state, repo) = SetUp();
        SeedInventorySlot(zone, ContainerMatrix.InventoryPage0, 5,
            new ItemStack(93514, 1, 12, 3, 0, 0, 0, 0, 0, 0, 777));

        var service = new RuneSocketService(repo, NullLogger<RuneSocketService>.Instance);
        var result = await RunToCompletionAsync(
            service.InsertAsync(new RuneSocketRequest { Sort = 0, RuneIndex = 0, ItemIndex = 93514, Page = 0, Index = 5 },
                zone, state, 10, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(RuneSocketOutcome.Applied, result.Outcome);
        Assert.Equal(93514, state.RuneSystem[0]);
        Assert.Equal(ItemValueCodec.Encode(12, 3, 0, 0), state.RuneSystemStat[0]);

        Assert.NotNull(repo.LastReplacedContainer);
        Assert.Equal(ContainerMatrix.InventoryPage0, repo.LastReplacedContainer!.Value.Container);
        Assert.Empty(repo.LastReplacedContainer.Value.Items);
    }

    [Fact]
    public async Task Insert_ItemIdOutsideRuneFamily_Rejected()
    {
        var (_, _, zone, state, repo) = SetUp();
        SeedInventorySlot(zone, ContainerMatrix.InventoryPage0, 5, new ItemStack(1234, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        var service = new RuneSocketService(repo, NullLogger<RuneSocketService>.Instance);
        var result = await service.InsertAsync(
            new RuneSocketRequest { Sort = 0, RuneIndex = 0, ItemIndex = 1234, Page = 0, Index = 5 }, zone, state, 10,
            CancellationToken.None);

        Assert.Equal(RuneSocketOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public async Task Insert_EmptySourceSlot_Rejected()
    {
        var (_, _, zone, state, repo) = SetUp();

        var service = new RuneSocketService(repo, NullLogger<RuneSocketService>.Instance);
        var result = await service.InsertAsync(
            new RuneSocketRequest { Sort = 0, RuneIndex = 0, ItemIndex = 93514, Page = 0, Index = 5 }, zone, state, 10,
            CancellationToken.None);

        Assert.Equal(RuneSocketOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public async Task Remove_MatchingOccupant_GrantsItemBackWithPreservedStat()
    {
        var (session, _, zone, state, repo) = SetUp();
        state.RuneSystem = state.RuneSystem.SetItem(1, 93515);
        state.RuneSystemStat = state.RuneSystemStat.SetItem(1, ItemValueCodec.Encode(20, 1, 0, 0));

        var service = new RuneSocketService(repo, NullLogger<RuneSocketService>.Instance);
        var result = await RunToCompletionAsync(
            service.RemoveAsync(new RuneSocketRequest { Sort = 1, RuneIndex = 1, ItemIndex = 0, Page = 0, Index = 0 },
                zone, state, 10, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(RuneSocketOutcome.Applied, result.Outcome);
        Assert.Equal(0, state.RuneSystem[1]);
        Assert.Equal(0, state.RuneSystemStat[1]);

        Assert.NotNull(repo.LastReplacedContainer);
        var granted = Assert.Single(repo.LastReplacedContainer!.Value.Items);
        Assert.Equal(93515, granted.ItemId);
        Assert.Equal(20, granted.Enchant);
        Assert.Equal(1, granted.Combine);
    }

    [Fact]
    public async Task Remove_EmptySlot_Rejected()
    {
        var (_, _, zone, state, repo) = SetUp();

        var service = new RuneSocketService(repo, NullLogger<RuneSocketService>.Instance);
        var result = await service.RemoveAsync(
            new RuneSocketRequest { Sort = 1, RuneIndex = 1, ItemIndex = 0, Page = 0, Index = 0 }, zone, state, 10,
            CancellationToken.None);

        Assert.Equal(RuneSocketOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public async Task UnknownSort_SilentlyIgnored_NoReplyNoDisconnect()
    {
        // Sort 0/1 dispatch to InsertAsync/RemoveAsync respectively; any other sort falls straight through the
        // handler's own switch (no default case, matching the legacy) without ever touching the service --
        // exercise the real handler here.
        var (session, pipe, zone, _, repo) = SetUp();

        var handler = new RuneSocketHandler(new RuneSocketService(repo, NullLogger<RuneSocketService>.Instance));
        await handler.HandleAsync(
            new RuneSocketRequest { Sort = 2, RuneIndex = 0, ItemIndex = 0, Page = 0, Index = 0 }, session,
            CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }
}
