using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Abstractions.WarPoint;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.GenericAction;
using Fenrir.Application.Game.Services.WarPoint;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.GenericAction;

/// <summary>
///     Covers the C13 dual-path dispatch <see cref="GenericActionService.BuyFromNpcShopAsync" /> now performs:
///     a War-Point-priced item is offered to <see cref="IWarPointShopService.TryBuyAsync" /> BEFORE
///     <see cref="NpcShopPolicy.ResolveBuy" />, and only a <see cref="WarPointBuyStatus.NotHandled" /> result falls
///     through to the ordinary money-price path. Neither <see cref="WarPointShopPolicy" />'s own outcome mapping
///     (see <c>WarPointShopServiceTests</c>) nor <see cref="NpcShopPolicy" />'s own outcome mapping (see
///     <c>NpcShopTradeServiceTests</c>) is re-covered here -- this file asserts only the routing decision itself:
///     which path actually runs, and that a short-circuited War-Point outcome never also touches the ordinary
///     money/container path.
/// </summary>
public class WarPointNpcShopDispatchTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const short ZoneNumber = 1; // NpcShopPolicy.TownZoneNumbers

    /// <summary>The War-Point NPC that both satisfies the NpcShop proximity gate and owns the WP catalogue row.</summary>
    private const int ShopNpcId = WarPointShopCatalog.NobleDragonNpcId; // 102

    /// <summary>A second War-Point NPC, present in the world but NOT the owner of <see cref="WpItemId" />.</summary>
    private const int OtherWpNpcId = WarPointShopCatalog.RoyalSerpentNpcId; // 202

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

    /// <summary>
    ///     One town zone, one NPC (<see cref="ShopNpcId" />) offering <see cref="NpcFunctionGate.NpcShop" /> and
    ///     stocking <see cref="OrdinaryItemId" /> in its ordinary shop catalogue, plus a second NPC
    ///     (<see cref="OtherWpNpcId" />) present in the world (but not spawned/offering anything) purely so a
    ///     "wrong War-Point NPC" request still resolves to a real NPC row. <see cref="WpItemId" /> is
    ///     deliberately NOT in <see cref="ShopNpcId" />'s ordinary <c>ShopItems</c> -- a War-Point item bypasses
    ///     shop-membership entirely, so it must never be reachable through <see cref="NpcShopPolicy" /> at all.
    /// </summary>
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
            eventLog, new FakeAccountVaultRepository(), NullLogger<GenericActionService>.Instance, warPointShop);
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

        // The ordinary money/container path must never have run for a War-Point purchase.
        Assert.Null(characters.LastAdjustMoneyAndReplaceContainer);

        var mirrored = state.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0);
        Assert.NotNull(mirrored);
        Assert.Equal(WpItemId, mirrored!.Value.ItemId);

        // EventCode=3 is WarPointShopService's own WarPointShopBuyEventCode, distinct from the ordinary path's
        // NpcShopBuyEventCode=2 -- proves the audit row came from the War-Point branch, not a fallthrough.
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

        // OrdinaryItemId is absent from the War-Point price table, even though ShopNpcId IS a War-Point NPC --
        // TryBuyAsync must answer NotHandled, and BuyFromNpcShopAsync must still complete the ordinary purchase.
        var result = await RunToCompletionAsync(
            service.BuyFromNpcShopAsync(zone, state, AccountId, CharacterId, BuyMove(ShopNpcId, OrdinaryItemId, 0),
                CancellationToken.None), zone);

        Assert.Equal(GenericActionStatus.Succeeded, result.Status);
        Assert.Equal(0, warPoints.CallCount);
        Assert.NotNull(characters.LastAdjustMoneyAndReplaceContainer);
        Assert.Equal(-OrdinaryItemBuyCost, characters.LastAdjustMoneyAndReplaceContainer!.Value.DeltaMoney);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(2, logged.EventCode); // ordinary NpcShopBuyEventCode
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

        // OtherWpNpcId is itself a War-Point NPC, but WpItemId's catalogue row only displays at ShopNpcId.
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
        var service = CreateService(worldData, characters, eventLog, warPointShop: null);

        // WpItemId is not in ShopNpcId's ordinary ShopItems catalogue, so with War-Point routing disabled
        // (the optional constructor parameter's default-null test seam) the request must fall straight into
        // NpcShopPolicy.ResolveBuy and be rejected there (NotInCatalog) -- never reaching any War-Point logic.
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
        var service = CreateService(worldData, characters, eventLog, warPointShop: null);

        var result = await RunToCompletionAsync(
            service.BuyFromNpcShopAsync(zone, state, AccountId, CharacterId, BuyMove(ShopNpcId, OrdinaryItemId, 0),
                CancellationToken.None), zone);

        Assert.Equal(GenericActionStatus.Succeeded, result.Status);
        Assert.NotNull(characters.LastAdjustMoneyAndReplaceContainer);
        Assert.Equal(-OrdinaryItemBuyCost, characters.LastAdjustMoneyAndReplaceContainer!.Value.DeltaMoney);
    }
}
