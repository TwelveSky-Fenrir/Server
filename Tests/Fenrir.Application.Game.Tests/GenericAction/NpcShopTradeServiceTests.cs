using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.GenericAction;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.GenericAction;

public class NpcShopTradeServiceTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const int NpcId = 1;
    private const short ZoneNumber = 1;

    private static async Task<T> RunToCompletionAsync<T>(ValueTask<T> pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("GenericActionService NPC-shop task never completed.");
        }

        return await task;
    }

    private static ItemRowDto SellableItem(int itemId, byte sort, int sellCost, byte checkNpcSell = 0)
    {
        return WorldDataTestRows.Item(itemId) with { Sort = sort, SellCost = sellCost, CheckNpcSell = checkNpcSell };
    }

    private static ItemRowDto BuyableItem(int itemId, byte sort, int buyCost)
    {
        return WorldDataTestRows.Item(itemId) with { Sort = sort, BuyCost = buyCost, CheckNpcShop = 2 };
    }

        private static WorldDataCache BuildWorldData(ItemRowDto[] itemsById, int[]? shopCatalogItemIds = null)
    {
        var catalogIds = shopCatalogItemIds ?? itemsById.Select(row => row.ItemId).ToArray();

        var npc = new NpcDefinition(
            WorldDataTestRows.Npc(NpcId) with { Type = 1 },
            [new NpcMenuOptionRowDto(NpcId, NpcFunctionGate.NpcShop, 2)],
            catalogIds.Select((id, i) => new NpcShopItemRowDto(NpcId, 0, (byte)i, id)).ToImmutableArray(),
            [], [], []);

        var zoneDefinition = new ZoneDefinition(
            WorldDataTestRows.Zone(ZoneNumber),
            [], [],
            [new ZoneNpcSpawnRowDto(ZoneNumber, 0, NpcId, 100f, 0f, 100f, 0f)],
            []);

        return new WorldDataCache
        {
            ItemsById = itemsById.Select(row => new ItemDefinition(row, [])).ToFrozenDictionary(d => d.Item.ItemId),
            SkillsById = FrozenDictionary<int, SkillDefinition>.Empty,
            MonstersById = FrozenDictionary<int, MonsterDefinition>.Empty,
            NpcsById = new Dictionary<int, NpcDefinition> { [NpcId] = npc }.ToFrozenDictionary(),
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

    private static void SeedInventory(Zone zone, params (byte Slot, ItemStack Item)[] slots)
    {
        var content = ImmutableDictionary<byte, ItemStack>.Empty;
        foreach (var (slot, item) in slots)
            content = content.SetItem(slot, item);

        var containers =
            ImmutableArray.Create(new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0, content));
        zone.PostInventoryCommand(new InventoryZoneCommand(CharacterId, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    private static GenericActionService CreateService(WorldDataCache worldData, FakeCharacterRepository characters,
        FakeEventLogRepository eventLog)
    {
        return new GenericActionService(characters, worldData, new QuestCatalog(worldData), new PartyRegistry(),
            eventLog, new FakeAccountVaultRepository(), NullLogger<GenericActionService>.Instance);
    }

    private static DefaultPData SellMove(int quantity = 0)
    {
        return new DefaultPData
        {
            Page1 = ContainerMatrix.InventoryPage0, Index1 = 0, Quantity1 = quantity, Page2 = 0, Index2 = 0,
            XPost2 = 0, YPost2 = 0
        };
    }

    private static DefaultPData BuyMove(int itemId, int quantity, int destinationIndex = 0)
    {
        return new DefaultPData
        {
            Page1 = NpcId, Index1 = itemId, Quantity1 = quantity, Page2 = ContainerMatrix.InventoryPage0,
            Index2 = destinationIndex, XPost2 = 0, YPost2 = 0
        };
    }

    [Fact]
    public async Task Sell_NonStackable_Succeeds_LogsNpcShopTradeAuditRow()
    {
        var worldData = BuildWorldData([SellableItem(700, 9, 500)]);
        var (zone, state, characters, eventLog) = SetUp(worldData);
        SeedInventory(zone, (0, new ItemStack(700, 1, 0, 0, 0, 0, 0, 0, 0, 0, 555)));
        var service = CreateService(worldData, characters, eventLog);

        var result = await RunToCompletionAsync(
            service.SellToNpcShopAsync(zone, state, AccountId, CharacterId, SellMove(), CancellationToken.None),
            zone);

        Assert.Equal(GenericActionStatus.Succeeded, result.Status);
        Assert.NotNull(characters.LastAdjustMoneyAndReplaceContainer);
        Assert.Equal(500, characters.LastAdjustMoneyAndReplaceContainer!.Value.DeltaMoney);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(EventLogCategory.NpcShopTrade, logged.Category);
        Assert.Equal(1, logged.EventCode);
        Assert.Equal(AccountId, logged.ActorAccountId);
        Assert.Equal(CharacterId, logged.ActorCharacterId);
        Assert.Null(logged.TargetAccountId);
        Assert.Null(logged.TargetCharacterId);
        Assert.Equal(500, logged.DeltaMoney);
        Assert.Equal(700, logged.ItemId);
        Assert.Equal(1, logged.Quantity);
        Assert.Equal((byte)1, logged.Outcome);
    }

    [Fact]
    public async Task Sell_Stackable_PartialQuantity_LogsSoldQuantity_NotFullStack()
    {
        var worldData = BuildWorldData([SellableItem(50, 2, 10)]);
        var (zone, state, characters, eventLog) = SetUp(worldData);
        SeedInventory(zone, (0, new ItemStack(50, 20, 0, 0, 0, 0, 0, 0, 0, 0, 900)));
        var service = CreateService(worldData, characters, eventLog);

        var result = await RunToCompletionAsync(
            service.SellToNpcShopAsync(zone, state, AccountId, CharacterId, SellMove(5), CancellationToken.None),
            zone);

        Assert.Equal(GenericActionStatus.Succeeded, result.Status);
        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(50, logged.DeltaMoney);
        Assert.Equal(5, logged.Quantity);
        Assert.Equal(50, logged.ItemId);
    }

    [Fact]
    public async Task Sell_Rejected_LogsNothing()
    {
        var worldData = BuildWorldData([SellableItem(700, 9, 500, 1)]);
        var (zone, state, characters, eventLog) = SetUp(worldData);
        SeedInventory(zone, (0, new ItemStack(700, 1, 0, 0, 0, 0, 0, 0, 0, 0, 555)));
        var service = CreateService(worldData, characters, eventLog);

        var result = await service.SellToNpcShopAsync(zone, state, AccountId, CharacterId, SellMove(),
            CancellationToken.None);

        Assert.Equal(GenericActionStatus.Aborted, result.Status);
        Assert.Null(characters.LastAdjustMoneyAndReplaceContainer);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task Buy_NonStackable_Succeeds_LogsNpcShopTradeAuditRow()
    {
        var worldData = BuildWorldData([BuyableItem(800, 9, 1000)]);
        var (zone, state, characters, eventLog) = SetUp(worldData);
        var service = CreateService(worldData, characters, eventLog);

        var result = await RunToCompletionAsync(
            service.BuyFromNpcShopAsync(zone, state, AccountId, CharacterId, BuyMove(800, 0), CancellationToken.None),
            zone);

        Assert.Equal(GenericActionStatus.Succeeded, result.Status);
        Assert.NotNull(characters.LastAdjustMoneyAndReplaceContainer);
        Assert.Equal(-1000, characters.LastAdjustMoneyAndReplaceContainer!.Value.DeltaMoney);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(EventLogCategory.NpcShopTrade, logged.Category);
        Assert.Equal(2, logged.EventCode);
        Assert.Equal(AccountId, logged.ActorAccountId);
        Assert.Equal(CharacterId, logged.ActorCharacterId);
        Assert.Equal(-1000, logged.DeltaMoney);
        Assert.Equal(800, logged.ItemId);
        Assert.Equal(1, logged.Quantity);
        Assert.Equal((byte)1, logged.Outcome);
    }

    [Fact]
    public async Task Buy_Stackable_MergeIntoExistingStack_LogsOnlyThePurchasedQuantity()
    {
        var worldData = BuildWorldData([BuyableItem(50, 2, 100)]);
        var (zone, state, characters, eventLog) = SetUp(worldData);
        SeedInventory(zone, (0, new ItemStack(50, 10, 0, 0, 0, 0, 0, 0, 0, 0, 900)));
        var service = CreateService(worldData, characters, eventLog);

        var result = await RunToCompletionAsync(
            service.BuyFromNpcShopAsync(zone, state, AccountId, CharacterId, BuyMove(50, 5), CancellationToken.None),
            zone);

        Assert.Equal(GenericActionStatus.Succeeded, result.Status);
        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(-500, logged.DeltaMoney);
        Assert.Equal(5, logged.Quantity);
    }

    [Fact]
    public async Task Buy_NotInNpcCatalog_IsRejected_LogsNothing()
    {
        var worldData = BuildWorldData([BuyableItem(800, 9, 1000), BuyableItem(999, 9, 1000)], [800]);
        var (zone, state, characters, eventLog) = SetUp(worldData);
        var service = CreateService(worldData, characters, eventLog);

        var result = await service.BuyFromNpcShopAsync(zone, state, AccountId, CharacterId, BuyMove(999, 0),
            CancellationToken.None);

        Assert.Equal(GenericActionStatus.Aborted, result.Status);
        Assert.Null(characters.LastAdjustMoneyAndReplaceContainer);
        Assert.Empty(eventLog.LoggedEvents);
    }
}
