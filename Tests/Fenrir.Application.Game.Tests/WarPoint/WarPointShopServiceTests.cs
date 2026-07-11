using System.Collections.Frozen;
using Fenrir.Application.Game.Abstractions.WarPoint;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.WarPoint;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Game;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.WarPoint;

public class WarPointShopServiceTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const short ZoneNumber = 1;
    private const int WpNpc = WarPointShopCatalog.NobleDragonNpcId;
    private const int OtherWpNpc = WarPointShopCatalog.RoyalSerpentNpcId;
    private const int OrdinaryNpc = 1;
    private const int WpItemId = 90200;
    private const byte NonStackableSort = 9;
    private const byte DestinationPage = ContainerMatrix.InventoryPage0;
    private const byte DestinationSlot = 0;

    private static async Task<T> RunToCompletionAsync<T>(ValueTask<T> pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("WarPointShopService task never completed.");
        }

        return await task;
    }

    private static WorldDataCache WorldData()
    {
        var items = new Dictionary<int, ItemDefinition>
        {
            [WpItemId] = new(WorldDataTestRows.Item(WpItemId) with { Sort = NonStackableSort }, [])
        }.ToFrozenDictionary();
        return ZoneTestKit.EmptyWorldData(items);
    }

    private static WarPointShopCatalog Catalog(int contributionPointPrice = 0)
    {
        return new WarPointShopCatalog([new WarPointPriceEntry(WpItemId, 500, contributionPointPrice, [WpNpc])]);
    }

    private static (Zone Zone, PlayerRuntimeState State) SetUp(WorldDataCache worldData)
    {
        var zone = ZoneTestKit.CreateZone(ZoneNumber, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(CharacterId);
        session.MarkTicketConsumed(AccountId, CharacterId);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, ZoneNumber)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        return (zone, state!);
    }

    private static WarPointShopService CreateService(WorldDataCache worldData, WarPointShopCatalog catalog,
        FakeWarPointRepository warPoints, FakeEventLogRepository eventLog)
    {
        return new WarPointShopService(warPoints, eventLog, catalog, worldData,
            NullLogger<WarPointShopService>.Instance);
    }

    [Fact]
    public async Task Buy_AtCorrectNpc_DebitsWarPoints_GrantsItem_LogsAudit()
    {
        var worldData = WorldData();
        var (zone, state) = SetUp(worldData);
        var warPoints = new FakeWarPointRepository { NextResult = new WarPointPurchaseResult(true, 1500) };
        var eventLog = new FakeEventLogRepository();
        var service = CreateService(worldData, Catalog(), warPoints, eventLog);

        var result = await RunToCompletionAsync(
            service.TryBuyAsync(zone, state, AccountId, CharacterId, WpNpc, WpItemId, 1, DestinationPage,
                DestinationSlot, CancellationToken.None), zone);

        Assert.Equal(WarPointBuyStatus.Succeeded, result.Status);

        Assert.NotNull(warPoints.LastCall);
        Assert.Equal(CharacterId, warPoints.LastCall!.CharacterId);
        Assert.Equal(500, warPoints.LastCall!.WarPointCost);
        Assert.Equal(DestinationPage, warPoints.LastCall!.Container);
        Assert.Single(warPoints.LastCall!.Items);
        Assert.Equal(WpItemId, warPoints.LastCall!.Items[0].ItemId);

        var mirrored = state.Inventory.GetSlot(DestinationPage, DestinationSlot);
        Assert.NotNull(mirrored);
        Assert.Equal(WpItemId, mirrored!.Value.ItemId);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(EventLogCategory.NpcShopTrade, logged.Category);
        Assert.Equal(3, logged.EventCode);
        Assert.Equal(AccountId, logged.ActorAccountId);
        Assert.Equal(CharacterId, logged.ActorCharacterId);
        Assert.Equal(WpItemId, logged.ItemId);
        Assert.Equal(1, logged.Quantity);
        Assert.Contains("WarPointBefore=2000", logged.Payload!);
        Assert.Contains("WarPointAfter=1500", logged.Payload!);
    }

    [Fact]
    public async Task Buy_AtOrdinaryNpc_IsNotHandled_RepositoryNotCalled()
    {
        var worldData = WorldData();
        var (zone, state) = SetUp(worldData);
        var warPoints = new FakeWarPointRepository();
        var eventLog = new FakeEventLogRepository();
        var service = CreateService(worldData, Catalog(), warPoints, eventLog);

        var result = await RunToCompletionAsync(
            service.TryBuyAsync(zone, state, AccountId, CharacterId, OrdinaryNpc, WpItemId, 1, DestinationPage,
                DestinationSlot, CancellationToken.None), zone);

        Assert.Equal(WarPointBuyStatus.NotHandled, result.Status);
        Assert.Equal(0, warPoints.CallCount);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task Buy_ItemNotInPriceTable_IsNotHandled()
    {
        var worldData = WorldData();
        var (zone, state) = SetUp(worldData);
        var warPoints = new FakeWarPointRepository();
        var eventLog = new FakeEventLogRepository();
        var service = CreateService(worldData, new WarPointShopCatalog([]), warPoints, eventLog);

        var result = await RunToCompletionAsync(
            service.TryBuyAsync(zone, state, AccountId, CharacterId, WpNpc, WpItemId, 1, DestinationPage,
                DestinationSlot, CancellationToken.None), zone);

        Assert.Equal(WarPointBuyStatus.NotHandled, result.Status);
        Assert.Equal(0, warPoints.CallCount);
    }

    [Fact]
    public async Task Buy_WrongNpcForTribeItem_IsAborted_RepositoryNotCalled()
    {
        var worldData = WorldData();
        var (zone, state) = SetUp(worldData);
        var warPoints = new FakeWarPointRepository();
        var eventLog = new FakeEventLogRepository();
        var service = CreateService(worldData, Catalog(), warPoints, eventLog);

        var result = await RunToCompletionAsync(
            service.TryBuyAsync(zone, state, AccountId, CharacterId, OtherWpNpc, WpItemId, 1, DestinationPage,
                DestinationSlot, CancellationToken.None), zone);

        Assert.Equal(WarPointBuyStatus.Aborted, result.Status);
        Assert.Equal(0, warPoints.CallCount);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task Buy_InsufficientWarPoints_IsSoftRejected_NoAudit_NoMirror()
    {
        var worldData = WorldData();
        var (zone, state) = SetUp(worldData);
        var warPoints = new FakeWarPointRepository { NextResult = new WarPointPurchaseResult(false, 0) };
        var eventLog = new FakeEventLogRepository();
        var service = CreateService(worldData, Catalog(), warPoints, eventLog);

        var result = await RunToCompletionAsync(
            service.TryBuyAsync(zone, state, AccountId, CharacterId, WpNpc, WpItemId, 1, DestinationPage,
                DestinationSlot, CancellationToken.None), zone);

        Assert.Equal(WarPointBuyStatus.SoftRejected, result.Status);
        Assert.Equal(1, warPoints.CallCount);
        Assert.Empty(eventLog.LoggedEvents);
        Assert.Null(state.Inventory.GetSlot(DestinationPage, DestinationSlot));
    }

    [Fact]
    public async Task Buy_InsufficientContributionPoints_IsSoftRejected_RepositoryNotCalled()
    {
        var worldData = WorldData();
        var (zone, state) = SetUp(worldData);
        var warPoints = new FakeWarPointRepository();
        var eventLog = new FakeEventLogRepository();
        var service = CreateService(worldData, Catalog(contributionPointPrice: 100), warPoints, eventLog);

        var result = await RunToCompletionAsync(
            service.TryBuyAsync(zone, state, AccountId, CharacterId, WpNpc, WpItemId, 1, DestinationPage,
                DestinationSlot, CancellationToken.None), zone);

        Assert.Equal(WarPointBuyStatus.SoftRejected, result.Status);
        Assert.Equal(0, warPoints.CallCount);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task Buy_RepositoryThrows_IsAborted()
    {
        var worldData = WorldData();
        var (zone, state) = SetUp(worldData);
        var warPoints = new FakeWarPointRepository { ThrowOnNextCall = true };
        var eventLog = new FakeEventLogRepository();
        var service = CreateService(worldData, Catalog(), warPoints, eventLog);

        var result = await RunToCompletionAsync(
            service.TryBuyAsync(zone, state, AccountId, CharacterId, WpNpc, WpItemId, 1, DestinationPage,
                DestinationSlot, CancellationToken.None), zone);

        Assert.Equal(WarPointBuyStatus.Aborted, result.Status);
        Assert.Empty(eventLog.LoggedEvents);
    }
}
