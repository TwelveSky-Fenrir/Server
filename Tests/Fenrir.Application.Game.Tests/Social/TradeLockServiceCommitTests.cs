using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Social;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Social;

public class TradeLockServiceCommitTests
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
                throw new TimeoutException("TradeLockService.CommitAsync never completed.");
        }

        await task;
    }

    private static (ZoneClientSession SessionA, FakeDuplexPipe PipeA, ZoneClientSession SessionB,
        FakeDuplexPipe PipeB, Zone Zone, PlayerRuntimeState StateA, PlayerRuntimeState StateB) SetUp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (sessionA, pipeA) = ZoneTestKit.CreateSession(1);
        var (sessionB, pipeB) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(sessionA, 1, name: "Alice")));
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(sessionB, 1, name: "Bob")));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipeA);
        ZoneTestKit.DrainOutbound(pipeB);

        Assert.True(zone.TryGetPlayer(10, out var stateA));
        Assert.True(zone.TryGetPlayer(20, out var stateB));

        return (sessionA, pipeA, sessionB, pipeB, zone, stateA!, stateB!);
    }

    private static void SeedInventorySlot(Zone zone, int characterId, byte container, byte slot, ItemStack stack)
    {
        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(container,
                ImmutableDictionary<byte, ItemStack>.Empty.SetItem(slot, stack)));
        zone.PostInventoryCommand(new InventoryZoneCommand(characterId, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    private static TradeSession StartSession(TradeRegistry trades, int playerAId, int playerBId)
    {
        trades.TryAsk(playerAId, playerBId);
        trades.TryAnswer(playerBId, true, out _);
        trades.TryStart(playerAId, out var session);
        return session;
    }

    [Fact]
    public async Task CommitAsync_SuccessfulTrade_CommitsThroughIdempotentRepositoryWithNonEmptyToken()
    {
        var (sessionA, pipeA, sessionB, pipeB, zone, stateA, stateB) = SetUp();
        SeedInventorySlot(zone, 10, ContainerMatrix.InventoryPage0, 0,
            new ItemStack(1026, 1, 0, 0, 0, 0, 0, 0, 0, 0, 555));

        var trades = new TradeRegistry();
        var session = StartSession(trades, 10, 20);
        session.SideA.Slots[0] = (ContainerMatrix.InventoryPage0, 0, new ItemStack(1026, 1, 0, 0, 0, 0, 0, 0, 0, 0, 555));
        session.SideA.Money = 500;

        var repo = new FakeTradeCommitRepository();
        var service = new TradeLockService(trades, repo, NullLogger<TradeLockService>.Instance);

        await RunToCompletionAsync(
            service.CommitAsync(session, stateA, zone, stateB, zone, 10, CancellationToken.None), zone);

        var call = Assert.Single(repo.Calls);
        Assert.NotEqual(Guid.Empty, call.TradeToken);
        Assert.Equal(10, call.CharacterA);
        Assert.Equal(20, call.CharacterB);

        Assert.Equal(-500L, call.DeltaMoneyA);
        Assert.Equal(500L, call.DeltaMoneyB);
        Assert.Equal(500L, call.OfferedMoneyA);
        Assert.Equal(0L, call.OfferedMoneyB);

        Assert.NotNull(call.TradedItemsA);
        var tradedItem = Assert.Single(call.TradedItemsA!);
        Assert.Equal(1026, tradedItem.ItemId);
        Assert.NotNull(call.TradedItemsB);
        Assert.Empty(call.TradedItemsB!);

        Assert.False(trades.TryGetSession(10, out _));
        Assert.False(trades.TryGetSession(20, out _));
        Assert.Null(sessionA.DisconnectReason);
        Assert.Null(sessionB.DisconnectReason);

        var toA = ZoneTestKit.DrainOutbound(pipeA);
        var toB = ZoneTestKit.DrainOutbound(pipeB);
        Assert.Equal(FrameWriter.FrameSizeOf<TradeEndResponse>(), toA.Length);
        Assert.Equal(FrameWriter.FrameSizeOf<TradeEndResponse>(), toB.Length);

        Assert.False(stateA.Inventory.GetContainer(ContainerMatrix.InventoryPage0).ContainsKey(0));
        var bContainer = stateB.Inventory.GetContainer(ContainerMatrix.InventoryPage0);
        Assert.True(bContainer.ContainsKey(0));
        Assert.Equal(1026, bContainer[0].ItemId);
    }

    [Fact]
    public async Task CommitAsync_TwoDistinctTrades_EachGetsItsOwnFreshToken()
    {
        var (_, pipeA, _, pipeB, zone, stateA, stateB) = SetUp();
        var repo = new FakeTradeCommitRepository();
        var trades = new TradeRegistry();
        var service = new TradeLockService(trades, repo, NullLogger<TradeLockService>.Instance);

        var firstSession = StartSession(trades, 10, 20);
        await RunToCompletionAsync(
            service.CommitAsync(firstSession, stateA, zone, stateB, zone, 10, CancellationToken.None), zone);
        ZoneTestKit.DrainOutbound(pipeA);
        ZoneTestKit.DrainOutbound(pipeB);

        var secondSession = StartSession(trades, 10, 20);
        await RunToCompletionAsync(
            service.CommitAsync(secondSession, stateA, zone, stateB, zone, 10, CancellationToken.None), zone);

        Assert.Equal(2, repo.Calls.Count);
        Assert.NotEqual(repo.Calls[0].TradeToken, repo.Calls[1].TradeToken);
    }

    [Fact]
    public async Task CommitAsync_RepositoryThrows_PropagatesAndLeavesSessionRegisteredForBothSides()
    {
        var (_, _, _, _, zone, stateA, stateB) = SetUp();
        var trades = new TradeRegistry();
        var session = StartSession(trades, 10, 20);

        var repo = new FakeTradeCommitRepository { ThrowOnNextExecute = new InvalidOperationException("simulated SQL failure") };
        var service = new TradeLockService(trades, repo, NullLogger<TradeLockService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunToCompletionAsync(
                service.CommitAsync(session, stateA, zone, stateB, zone, 10, CancellationToken.None), zone));

        Assert.True(trades.TryGetSession(10, out _));
        Assert.True(trades.TryGetSession(20, out _));
    }
}
