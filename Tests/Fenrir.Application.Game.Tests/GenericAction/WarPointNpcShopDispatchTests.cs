using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Abstractions.WarPoint;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.GenericAction;
using Fenrir.Application.Game.Services.WarPoint;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.GenericAction;

public class WarPointNpcShopDispatchTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const short ZoneNumber = 1;

    private const int ShopNpcId = WarPointShopCatalog.NobleDragonNpcId;

    private const int OtherWpNpcId = WarPointShopCatalog.RoyalSerpentNpcId;

    private const int WpItemId = 90200;
    private const int WpItemWarPointPrice = 500;
    private const int OrdinaryItemId = 800;
    private const int OrdinaryItemBuyCost = 1000;
    private const byte NonStackableSort = 9;

    private static async Task<T> RunToCompletionAsync<T>(ValueTask<T> pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("BuyFromNpcShopAsync task never completed.");
        }

        return await task;
    }

    private static WorldDataCache BuildWorldData()
    {
        var wpItem = WorldDataTestRows.Item(WpItemId) with { Sort = NonStackableSort };
        var ordinaryItem = WorldDataTestRows.Item(OrdinaryItemId) with
        {
            Sort = NonStackableSort, BuyCost = OrdinaryItemBuyCost, CheckNpcShop = 2
        };

        var shopNpc = new NpcDefinition(
            WorldDataTestRows.Npc(ShopNpcId) with { Type = 1 },
            [new NpcMenuOptionRowDto(ShopNpcId, NpcFunctionGate.NpcShop, 2)],
            [new NpcShopItemRowDto(ShopNpcId, 0, 0, OrdinaryItemId)],
            [], [], []);

        var otherWpNpc = new NpcDefinition(
            WorldDataTestRows.Npc(OtherWpNpcId) with { Type = 1 },
            [], [], [], [], []);

        var zoneDefinition = new ZoneDefinition(
            WorldDataTestRows.Zone(ZoneNumber),
            [], [],
            [new ZoneNpcSpawnRowDto(ZoneNumber, 0, ShopNpcId, 100f, 0f, 100f, 0f)],
            []);

        return new WorldDataCache
        {
            ItemsById = new[] { wpItem, ordinaryItem }
                .Select(row => new ItemDefinition(row, []))
                .ToFrozenDictionary(d => d.Item.ItemId),
            SkillsById = FrozenDictionary<int, SkillDefinition>.Empty,
            MonstersById = FrozenDictionary<int, MonsterDefinition>.Empty,
            NpcsById = new Dictionary<int, NpcDefinition> { [ShopNpcId] = shopNpc, [OtherWpNpcId] = otherWpNpc }
                .ToFrozenDictionary(),
            QuestsById = FrozenDictionary<int, QuestDefinition>.Empty,
            LevelsByLevel = FrozenDictionary<short, LevelRowDto>.Empty,
            ZonesByNumber =
                new Dictionary<short, ZoneDefinition> { [ZoneNumber] = zoneDefinition }.ToFrozenDictionary(),
            GemSocketsById = FrozenDictionary<int, GemSocketRowDto>.Empty,
            GemSocketsByTypeAndValue = FrozenDictionary<int, GemSocketRowDto>.Empty,
            BloodExchangeCatalog = [],
            EventDefinitions = [],
            ItemMallProductsById = FrozenDictionary<int, ItemMallProductRowDto>.Empty,
            RewardBundleItemsByBundleId = FrozenDictionary<int, ImmutableArray<RewardBundleItemRowDto>>.Empty,
            CashCatalog = CashCatalogBuilder.Build([]),
            CashCatalogVersion = 0
        };
    }

    private static (Zone Zone, PlayerRuntimeState State, FakeCharacterRepository Characters,
        FakeEventLogRepository EventLog) SetUp(WorldDataCache worldData)
    {
        var zone = ZoneTestKit.CreateZone(ZoneNumber, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(CharacterId);
        session.MarkTicketConsumed(AccountId, CharacterId);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, ZoneNumber)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        return (zone, state!, new FakeCharacterRepository(), new FakeEventLogRepository());
    }

    private static GenericActionService CreateService(WorldDataCache worldData, FakeCharacterRepository characters,
        FakeEventLogRepository eventLog, IWarPointShopService? warPointShop)
    {
        return new GenericActionService(characters, worldData, new QuestCatalog(worldData), new PartyRegistry(),
            eventLog, new FakeAccountVaultRepository(), new TradeRegistry(), ZoneTestKit.CreateRegistry(),
            NullLogger<GenericActionService>.Instance, warPointShop);
    }

    private static WarPointShopService CreateWarPointShop(WorldDataCache worldData, FakeWarPointRepository warPoints,
        FakeEventLogRepository eventLog)
    {
        var catalog = new WarPointShopCatalog([new WarPointPriceEntry(WpItemId, WpItemWarPointPrice, 0, [ShopNpcId])]);
        return new WarPointShopService(warPoints, eventLog, catalog, worldData,
            NullLogger<WarPointShopService>.Instance);
    }

    private static DefaultPData BuyMove(int npcId, int itemId, int quantity, int destinationIndex = 0)
    {
        return new DefaultPData
        {
            Page1 = npcId, Index1 = itemId, Quantity1 = quantity, Page2 = ContainerMatrix.InventoryPage0,
            Index2 = destinationIndex, XPost2 = 0, YPost2 = 0
        };
    }

    [Fact]
    public async Task Buy_WarPointPricedItem_RoutesThroughWarPointShopService_NeverTouchesOrdinaryPath()
    {
        var worldData = BuildWorldData();
        var (zone, state, characters, eventLog) = SetUp(worldData);
        var warPoints = new FakeWarPointRepository { NextResult = new WarPointPurchaseResult(true, 1500) };
        var warPointShop = CreateWarPointShop(worldData, warPoints, eventLog);
        var service = CreateService(worldData, characters, eventLog, warPointShop);

        var result = await RunToCompletionAsync(
            service.BuyFromNpcShopAsync(zone, state, AccountId, CharacterId, BuyMove(ShopNpcId, WpItemId, 1),
                CancellationToken.None), zone);

        Assert.Equal(GenericActionStatus.Succeeded, result.Status);
        Assert.Equal(1, warPoints.CallCount);
        Assert.Equal(WpItemWarPointPrice, warPoints.LastCall!.WarPointCost);

        Assert.Null(characters.LastAdjustMoneyAndReplaceContainer);

        var mirrored = state.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0);
        Assert.NotNull(mirrored);
        Assert.Equal(WpItemId, mirrored!.Value.ItemId);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(3, logged.EventCode);
    }

    [Fact]
    public async Task Buy_OrdinaryItem_AtWarPointNpc_FallsThroughToNpcShopPolicy_WhenNotHandled()
    {
        var worldData = BuildWorldData();
        var (zone, state, characters, eventLog) = SetUp(worldData);
        var warPoints = new FakeWarPointRepository();
        var warPointShop = CreateWarPointShop(worldData, warPoints, eventLog);
        var service = CreateService(worldData, characters, eventLog, warPointShop);

        var result = await RunToCompletionAsync(
            service.BuyFromNpcShopAsync(zone, state, AccountId, CharacterId, BuyMove(ShopNpcId, OrdinaryItemId, 0),
                CancellationToken.None), zone);

        Assert.Equal(GenericActionStatus.Succeeded, result.Status);
        Assert.Equal(0, warPoints.CallCount);
        Assert.NotNull(characters.LastAdjustMoneyAndReplaceContainer);
        Assert.Equal(-OrdinaryItemBuyCost, characters.LastAdjustMoneyAndReplaceContainer!.Value.DeltaMoney);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(2, logged.EventCode);
    }

    [Fact]
    public async Task Buy_WarPointItem_ServerRejectsInsufficientWarPoints_ReturnsFailed_DoesNotFallThrough()
    {
        var worldData = BuildWorldData();
        var (zone, state, characters, eventLog) = SetUp(worldData);
        var warPoints = new FakeWarPointRepository { NextResult = new WarPointPurchaseResult(false, 0) };
        var warPointShop = CreateWarPointShop(worldData, warPoints, eventLog);
        var service = CreateService(worldData, characters, eventLog, warPointShop);

        var result = await RunToCompletionAsync(
            service.BuyFromNpcShopAsync(zone, state, AccountId, CharacterId, BuyMove(ShopNpcId, WpItemId, 1),
                CancellationToken.None), zone);

        Assert.Equal(GenericActionStatus.Failed, result.Status);
        Assert.Null(characters.LastAdjustMoneyAndReplaceContainer);
        Assert.Empty(eventLog.LoggedEvents);
        Assert.Null(state.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0));
    }

    [Fact]
    public async Task Buy_WarPointItem_WrongNpc_ReturnsAborted_DoesNotFallThrough()
    {
        var worldData = BuildWorldData();
        var (zone, state, characters, eventLog) = SetUp(worldData);
        var warPoints = new FakeWarPointRepository();
        var warPointShop = CreateWarPointShop(worldData, warPoints, eventLog);
        var service = CreateService(worldData, characters, eventLog, warPointShop);

        var result = await service.BuyFromNpcShopAsync(zone, state, AccountId, CharacterId,
            BuyMove(OtherWpNpcId, WpItemId, 1), CancellationToken.None);

        Assert.Equal(GenericActionStatus.Aborted, result.Status);
        Assert.Equal(0, warPoints.CallCount);
        Assert.Null(characters.LastAdjustMoneyAndReplaceContainer);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task Buy_WithWarPointShopServiceNotInjected_SkipsWarPointRoutingEntirely()
    {
        var worldData = BuildWorldData();
        var (zone, state, characters, eventLog) = SetUp(worldData);
        var service = CreateService(worldData, characters, eventLog, null);

        var result = await service.BuyFromNpcShopAsync(zone, state, AccountId, CharacterId,
            BuyMove(ShopNpcId, WpItemId, 1), CancellationToken.None);

        Assert.Equal(GenericActionStatus.Aborted, result.Status);
        Assert.Null(characters.LastAdjustMoneyAndReplaceContainer);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task Buy_OrdinaryItem_WithWarPointShopServiceNotInjected_StillSucceeds()
    {
        var worldData = BuildWorldData();
        var (zone, state, characters, eventLog) = SetUp(worldData);
        var service = CreateService(worldData, characters, eventLog, null);

        var result = await RunToCompletionAsync(
            service.BuyFromNpcShopAsync(zone, state, AccountId, CharacterId, BuyMove(ShopNpcId, OrdinaryItemId, 0),
                CancellationToken.None), zone);

        Assert.Equal(GenericActionStatus.Succeeded, result.Status);
        Assert.NotNull(characters.LastAdjustMoneyAndReplaceContainer);
        Assert.Equal(-OrdinaryItemBuyCost, characters.LastAdjustMoneyAndReplaceContainer!.Value.DeltaMoney);
    }
}
