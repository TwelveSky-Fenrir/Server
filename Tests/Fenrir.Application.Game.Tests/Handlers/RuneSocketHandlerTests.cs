using System.Collections.Immutable;
using Fenrir.Application.Game.Handlers;
using Fenrir.Application.Game.Handlers.ItemModification.Services;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

/// <summary>
///     Drives the real <see cref="RuneSocketHandler" /> (opcode 157) over a real <see cref="Zone" />; ticks the
///     zone while the handler's own <c>PostRuneSocketCommandAndWaitAsync</c>/<c>PostInventoryCommandAndWaitAsync</c>
///     awaits are pending, same pattern as <c>FishingCatchHandlerTests</c>.
/// </summary>
public class RuneSocketHandlerTests
{
    private static async Task RunToCompletionAsync(ValueTask pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("RuneSocketHandler task never completed.");
        }

        await task;
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

        var handler = new RuneSocketHandler(new RuneSocketService(repo, NullLogger<RuneSocketService>.Instance));
        await RunToCompletionAsync(
            handler.HandleAsync(
                new RuneSocketRequest { Sort = 0, RuneIndex = 0, ItemIndex = 93514, Page = 0, Index = 5 }, session,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(93514, state.RuneSystem[0]);
        Assert.Equal(ItemValueCodec.Encode(12, 3, 0, 0), state.RuneSystemStat[0]);

        Assert.NotNull(repo.LastReplacedContainer);
        Assert.Equal(ContainerMatrix.InventoryPage0, repo.LastReplacedContainer!.Value.Container);
        Assert.Empty(repo.LastReplacedContainer.Value.Items);
    }

    [Fact]
    public async Task Insert_ItemIdOutsideRuneFamily_Aborts()
    {
        var (session, _, zone, _, repo) = SetUp();
        SeedInventorySlot(zone, ContainerMatrix.InventoryPage0, 5, new ItemStack(1234, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        var handler = new RuneSocketHandler(new RuneSocketService(repo, NullLogger<RuneSocketService>.Instance));
        await handler.HandleAsync(
            new RuneSocketRequest { Sort = 0, RuneIndex = 0, ItemIndex = 1234, Page = 0, Index = 5 }, session,
            CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task Insert_EmptySourceSlot_Aborts()
    {
        var (session, _, _, _, repo) = SetUp();

        var handler = new RuneSocketHandler(new RuneSocketService(repo, NullLogger<RuneSocketService>.Instance));
        await handler.HandleAsync(
            new RuneSocketRequest { Sort = 0, RuneIndex = 0, ItemIndex = 93514, Page = 0, Index = 5 }, session,
            CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task Remove_MatchingOccupant_GrantsItemBackWithPreservedStat()
    {
        var (session, _, zone, state, repo) = SetUp();
        state.RuneSystem = state.RuneSystem.SetItem(1, 93515);
        state.RuneSystemStat = state.RuneSystemStat.SetItem(1, ItemValueCodec.Encode(20, 1, 0, 0));

        var handler = new RuneSocketHandler(new RuneSocketService(repo, NullLogger<RuneSocketService>.Instance));
        await RunToCompletionAsync(
            handler.HandleAsync(new RuneSocketRequest { Sort = 1, RuneIndex = 1, ItemIndex = 0, Page = 0, Index = 0 },
                session, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, state.RuneSystem[1]);
        Assert.Equal(0, state.RuneSystemStat[1]);

        Assert.NotNull(repo.LastReplacedContainer);
        var granted = Assert.Single(repo.LastReplacedContainer!.Value.Items);
        Assert.Equal(93515, granted.ItemId);
        Assert.Equal(20, granted.Enchant);
        Assert.Equal(1, granted.Combine);
    }

    [Fact]
    public async Task Remove_EmptySlot_Aborts()
    {
        var (session, _, _, _, repo) = SetUp();

        var handler = new RuneSocketHandler(new RuneSocketService(repo, NullLogger<RuneSocketService>.Instance));
        await handler.HandleAsync(
            new RuneSocketRequest { Sort = 1, RuneIndex = 1, ItemIndex = 0, Page = 0, Index = 0 }, session,
            CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task UnknownSort_SilentlyIgnored_NoReplyNoDisconnect()
    {
        var (session, pipe, _, _, repo) = SetUp();

        var handler = new RuneSocketHandler(new RuneSocketService(repo, NullLogger<RuneSocketService>.Instance));
        await handler.HandleAsync(
            new RuneSocketRequest { Sort = 2, RuneIndex = 0, ItemIndex = 0, Page = 0, Index = 0 }, session,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }
}
