using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.Progression;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Handlers;

public class UseInventoryItemConsumablesServiceTests
{
    private const byte SpecialUseSort = 3;
    private const int AccountId = 1;
    private const int LodTicketItemId = 1434;
    private const int StatsClearUpTo99ItemId = 1134;
    private const int StatCleanseUpTo99ItemId = 1137;
    private const int PreserveCharmItemId = 593;
    private const int LuckyEnchantScrollItemId = 1126;
    private const int FactionNoticeItemId = 566;
    private const int TaiyanKeyItemId = 1049;

    private static async Task<UseInventoryItemResponse> RunToCompletionAsync(
        ValueTask<UseInventoryItemResponse?> pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("UseInventoryItemService task never completed.");
        }

        var result = await task;
        Assert.NotNull(result);
        return result!.Value;
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, Zone Zone, PlayerRuntimeState State,
        FakeCharacterRepository Characters) SetUp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, 10);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(10, out var state));
        return (session, pipe, zone, state!, new FakeCharacterRepository());
    }

    private static void SeedInventory(Zone zone, ItemStack item)
    {
        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0,
                ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, item)));
        zone.PostInventoryCommand(new InventoryZoneCommand(10, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    private static UseInventoryItemService CreateService(FakeCharacterRepository characters)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [LodTicketItemId] = new(WorldDataTestRows.Item(LodTicketItemId) with { Sort = SpecialUseSort }, []),
            [StatsClearUpTo99ItemId] =
                new(WorldDataTestRows.Item(StatsClearUpTo99ItemId) with { Sort = SpecialUseSort }, []),
            [StatCleanseUpTo99ItemId] =
                new(WorldDataTestRows.Item(StatCleanseUpTo99ItemId) with { Sort = SpecialUseSort }, []),
            [PreserveCharmItemId] =
                new(WorldDataTestRows.Item(PreserveCharmItemId) with { Sort = SpecialUseSort }, []),
            [LuckyEnchantScrollItemId] =
                new(WorldDataTestRows.Item(LuckyEnchantScrollItemId) with { Sort = SpecialUseSort }, []),
            [FactionNoticeItemId] =
                new(WorldDataTestRows.Item(FactionNoticeItemId) with { Sort = SpecialUseSort }, []),
            [TaiyanKeyItemId] = new(WorldDataTestRows.Item(TaiyanKeyItemId) with { Sort = SpecialUseSort }, [])
        }.ToFrozenDictionary();

        var towerUpgrade = new TowerUpgradeService(new TowerWarState(), characters,
            NullLogger<TowerUpgradeService>.Instance);

        return new UseInventoryItemService(characters, new FakeGuildRepository(), new FakeCashRepository(),
            new FakeOfflineShopRepository(), new FakeEventLogRepository(), new FakeProxyShopExpirationRelayQueue(),
            Options.Create(new GameServerOptions()), ZoneTestKit.EmptyWorldData(itemsById),
            NullLogger<UseInventoryItemService>.Instance, towerUpgrade);
    }

    [Fact]
    public async Task LodTicket_LevelCapAndRebirth_IncrementsBankedRounds_EchoesTheNewTotal_AndConsumesOneUnit()
    {
        var (session, _, zone, state, characters) = SetUp();
        state.Level = LevelProgressionCalculator.MaxLevel;
        state.RebirthCount = 1;
        SeedInventory(zone, new ItemStack(LodTicketItemId, 3, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result);
        Assert.Equal(1, response.Value);

        Assert.True(zone.TryGetPlayer(10, out var refreshed));
        Assert.Equal(1, refreshed!.LodRounds);
        Assert.Equal(2, refreshed.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0)!.Value.Quantity);
    }

    [Fact]
    public async Task LodTicket_BelowLevelCap_FailsCleanly_AndLeavesTheCounterAndItemUntouched()
    {
        var (session, _, zone, state, characters) = SetUp();
        state.Level = LevelProgressionCalculator.MaxLevel - 1;
        state.RebirthCount = 1;
        SeedInventory(zone, new ItemStack(LodTicketItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters);

        var response = await service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.NotNull(response);
        Assert.Equal(1, response!.Value.Result);
        Assert.Null(characters.LastReplacedContainer);
        Assert.Equal(0, state.LodRounds);
    }

    [Fact]
    public async Task StatsClear_MatchingLevelBand_RefundsAllFourStats_AndResetsThemToFloor()
    {
        var (session, _, zone, state, characters) = SetUp();
        state.Level = 50;
        state.StatVit = 10;
        state.StatStr = 5;
        state.StatInt = 1;
        state.StatDex = 8;
        state.StatPoints = 100;
        SeedInventory(zone, new ItemStack(StatsClearUpTo99ItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result);

        Assert.True(zone.TryGetPlayer(10, out var refreshed));
        Assert.Equal(1, refreshed!.StatVit);
        Assert.Equal(1, refreshed.StatStr);
        Assert.Equal(1, refreshed.StatInt);
        Assert.Equal(1, refreshed.StatDex);
        Assert.Equal(120, refreshed.StatPoints);
        Assert.Equal(1, refreshed.Life);
        Assert.Equal(0, refreshed.Mana);
    }

    [Fact]
    public async Task StatsClear_WrongLevelBandForTheUsedId_FailsCleanly_AndLeavesStatsUntouched()
    {
        var (session, _, zone, state, characters) = SetUp();
        state.Level = 145;
        state.RebirthCount = 1;
        state.StatVit = 10;
        SeedInventory(zone, new ItemStack(StatsClearUpTo99ItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters);

        var response = await service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.NotNull(response);
        Assert.Equal(1, response!.Value.Result);
        Assert.Equal(10, state.StatVit);
        Assert.Null(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task StatCleanse_ValidSelector_RefundsOnlyTheTargetedStat()
    {
        var (session, _, zone, state, characters) = SetUp();
        state.Level = 50;
        state.StatStr = 20;
        state.StatVit = 10;
        state.StatPoints = 0;
        SeedInventory(zone, new ItemStack(StatCleanseUpTo99ItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 1,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result);

        Assert.True(zone.TryGetPlayer(10, out var refreshed));
        Assert.Equal(1, refreshed!.StatStr);
        Assert.Equal(10, refreshed.StatVit);
        Assert.Equal(19, refreshed.StatPoints);
    }

    [Fact]
    public async Task StatCleanse_SelectorOutOfRange_FailsCleanly_AndLeavesStatsUntouched()
    {
        var (session, _, zone, state, characters) = SetUp();
        state.Level = 50;
        state.StatStr = 20;
        SeedInventory(zone, new ItemStack(StatCleanseUpTo99ItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters);

        var response = await service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 5,
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(1, response!.Value.Result);
        Assert.Equal(20, state.StatStr);
        Assert.Null(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task StatCleanse_TargetedStatAlreadyAtFloor_FailsCleanly()
    {
        var (_, _, zone, state, characters) = SetUp();
        state.Level = 50;
        state.StatStr = 1;
        SeedInventory(zone, new ItemStack(StatCleanseUpTo99ItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters);

        var response = await service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 1,
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(1, response!.Value.Result);
        Assert.Null(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task PreserveCharm_PlainClick_AddsOneCharge_AndDecrementsOneUnit()
    {
        var (session, _, zone, state, characters) = SetUp();
        SeedInventory(zone, new ItemStack(PreserveCharmItemId, 5, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result);
        Assert.Equal(1, response.Value);
        Assert.Equal(1, response.Value2);

        Assert.True(zone.TryGetPlayer(10, out var refreshed));
        Assert.Equal(1, refreshed!.ProtectForRefine);
        Assert.Equal(4, refreshed.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0)!.Value.Quantity);
    }

    [Fact]
    public async Task PreserveCharm_BulkClick_MultipliesTheChargeByTheCoercedCount()
    {
        var (_, _, zone, state, characters) = SetUp();
        SeedInventory(zone, new ItemStack(PreserveCharmItemId, 5, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 3,
                CancellationToken.None), zone);

        Assert.Equal(3, response.Value);
        Assert.Equal(3, response.Value2);
        Assert.True(zone.TryGetPlayer(10, out var refreshed));
        Assert.Equal(3, refreshed!.ProtectForRefine);
        Assert.Equal(2, refreshed.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0)!.Value.Quantity);
    }

    [Fact]
    public async Task LuckyEnchantScroll_SingleUnitOnly_AddsOneCharge_NoBulkEcho()
    {
        var (session, _, zone, state, characters) = SetUp();
        SeedInventory(zone, new ItemStack(LuckyEnchantScrollItemId, 5, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 3,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result);
        Assert.Equal(0, response.Value);
        Assert.Equal(0, response.Value2);

        Assert.True(zone.TryGetPlayer(10, out var refreshed));
        Assert.Equal(1, refreshed!.ImproveItemValue);
        Assert.Equal(4, refreshed.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0)!.Value.Quantity);
    }

    [Fact]
    public async Task FactionNotice_RechargesTheScrollCountByFive_AndConsumesOneUnit()
    {
        var (session, _, zone, state, characters) = SetUp();
        SeedInventory(zone, new ItemStack(FactionNoticeItemId, 2, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result);

        Assert.True(zone.TryGetPlayer(10, out var refreshed));
        Assert.Equal(5, refreshed!.TribeNotifyScrollCount);
        Assert.Equal(1, refreshed.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0)!.Value.Quantity);
    }

    [Fact]
    public async Task TaiyanKey_AtLevelCap_AddsOneHundredEighty()
    {
        var (session, _, zone, state, characters) = SetUp();
        state.Level = LevelProgressionCalculator.MaxLevel;
        SeedInventory(zone, new ItemStack(TaiyanKeyItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result);

        Assert.True(zone.TryGetPlayer(10, out var refreshed));
        Assert.Equal(180, refreshed!.TaiyanKeyTimer);
    }

    [Fact]
    public async Task TaiyanKey_BelowLevelCap_FailsCleanly_AndLeavesTheTimerUntouched()
    {
        var (session, _, zone, state, characters) = SetUp();
        state.Level = LevelProgressionCalculator.MaxLevel - 1;
        SeedInventory(zone, new ItemStack(TaiyanKeyItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters);

        var response = await service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.NotNull(response);
        Assert.Equal(1, response!.Value.Result);
        Assert.Equal(0, state.TaiyanKeyTimer);
        Assert.Null(characters.LastReplacedContainer);
    }
}
