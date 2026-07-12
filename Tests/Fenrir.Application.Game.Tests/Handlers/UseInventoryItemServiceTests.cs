using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.Progression;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Commerce;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Handlers;

public class UseInventoryItemServiceTests
{
    private const byte BottleSort = 26;
    private const byte SpecialUseSort = 3;
    private const int GuildScroll30MinItemId = 558;
    private const int GuildScroll60MinItemId = 1211;
    private const int BottleItemId = 501;
    private const int UnhandledItemId = 999001;
    private const int Ticket500ItemId = 723;
    private const int Ticket100ItemId = 725;
    private const int ProxyShopOneDayItemId = 567;
    private const int ProxyShopOneDayReskinItemId = 8422;
    private const int ProxyShopSevenDayItemId = 592;
    private const int ProxyShopSevenDayReskinItemId = 8423;
    private const int AccountId = 1;
    private const byte ShardId = 1;

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
        FakeCharacterRepository Characters, FakeGuildRepository Guilds, FakeCashRepository Cash,
        FakeEventLogRepository EventLog) SetUp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, 10);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(10, out var state));
        return (session, pipe, zone, state!, new FakeCharacterRepository(), new FakeGuildRepository(),
            new FakeCashRepository(), new FakeEventLogRepository());
    }

    private static void SeedInventory(Zone zone, ItemStack item)
    {
        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0,
                ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, item)));
        zone.PostInventoryCommand(new InventoryZoneCommand(10, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    private static void SeedGuildMembership(Zone zone, int guildId)
    {
        zone.PostGuildCommand(new GuildMembershipZoneCommand(10, guildId, "TestGuild", 2, ""));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    private static UseInventoryItemService CreateService(FakeCharacterRepository characters,
        FakeGuildRepository guilds, FakeCashRepository cash, FakeEventLogRepository eventLog,
        FakeOfflineShopRepository? offlineShops = null, FakeProxyShopExpirationRelayQueue? relay = null)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [GuildScroll30MinItemId] =
                new(WorldDataTestRows.Item(GuildScroll30MinItemId) with { Sort = SpecialUseSort }, []),
            [GuildScroll60MinItemId] =
                new(WorldDataTestRows.Item(GuildScroll60MinItemId) with { Sort = SpecialUseSort }, []),
            [BottleItemId] = new(WorldDataTestRows.Item(BottleItemId) with { Sort = BottleSort }, []),
            [Ticket500ItemId] =
                new(WorldDataTestRows.Item(Ticket500ItemId) with { Sort = SpecialUseSort }, []),
            [Ticket100ItemId] =
                new(WorldDataTestRows.Item(Ticket100ItemId) with { Sort = SpecialUseSort }, []),
            [ProxyShopOneDayItemId] =
                new(WorldDataTestRows.Item(ProxyShopOneDayItemId) with { Sort = SpecialUseSort }, []),
            [ProxyShopOneDayReskinItemId] =
                new(WorldDataTestRows.Item(ProxyShopOneDayReskinItemId) with { Sort = SpecialUseSort }, []),
            [ProxyShopSevenDayItemId] =
                new(WorldDataTestRows.Item(ProxyShopSevenDayItemId) with { Sort = SpecialUseSort }, []),
            [ProxyShopSevenDayReskinItemId] =
                new(WorldDataTestRows.Item(ProxyShopSevenDayReskinItemId) with { Sort = SpecialUseSort }, []),
            [UnhandledItemId] = new(WorldDataTestRows.Item(UnhandledItemId) with { Sort = SpecialUseSort }, [])
        }.ToFrozenDictionary();

        var towerUpgrade = new TowerUpgradeService(new TowerWarState(), characters,
            NullLogger<TowerUpgradeService>.Instance);

        return new UseInventoryItemService(characters, guilds, cash, offlineShops ?? new FakeOfflineShopRepository(),
            eventLog, relay ?? new FakeProxyShopExpirationRelayQueue(),
            Options.Create(new GameServerOptions { ShardId = ShardId }), ZoneTestKit.EmptyWorldData(itemsById),
            NullLogger<UseInventoryItemService>.Instance, towerUpgrade);
    }

    [Fact]
    public async Task GuildScroll_WhileInGuild_RechargesBuffTimeByTheItemsFixedAmount_AndConsumesTheScroll()
    {
        var (session, _, zone, state, characters, guilds, cash, eventLog) = SetUp();
        SeedGuildMembership(zone, 77);
        guilds.Seed(new GuildSummaryDto(77, "TestGuild", 1, 10, 0, 2, 1, 5, 1000L, 0, DateTime.UtcNow, 1));
        SeedInventory(zone, new ItemStack(GuildScroll60MinItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters, guilds, cash, eventLog);
        var before = DateTime.UtcNow;

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result);

        var setBuff = guilds.LastSetBuff!.Value;
        Assert.Equal(77, setBuff.GuildId);
        Assert.Equal(2, setBuff.BuffType);
        Assert.Equal(1, setBuff.BuffState);
        Assert.Equal(60, setBuff.BuffTime);
        Assert.InRange(setBuff.BuffTimeForDiff, before.AddMinutes(60).Ticks, DateTime.UtcNow.AddMinutes(60).Ticks);

        Assert.NotNull(characters.LastReplacedContainer);
        Assert.DoesNotContain(characters.LastReplacedContainer!.Value.Items, i => i.Slot == 0);
    }

    [Fact]
    public async Task GuildScroll_StackedQuantity_DecrementsByOne_AndKeepsTheSlotOccupied()
    {
        var (_, _, zone, state, characters, guilds, cash, eventLog) = SetUp();
        SeedGuildMembership(zone, 77);
        guilds.Seed(new GuildSummaryDto(77, "TestGuild", 1, 10, 0, 0, 0, 0, 0L, 0, DateTime.UtcNow, 1));
        SeedInventory(zone, new ItemStack(GuildScroll30MinItemId, 3, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters, guilds, cash, eventLog);

        await RunToCompletionAsync(
            service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Equal(30, guilds.LastSetBuff!.Value.BuffTime);

        var persisted = Assert.Single(characters.LastReplacedContainer!.Value.Items, i => i.Slot == 0);
        Assert.Equal(2, persisted.Quantity);

        Assert.True(zone.TryGetPlayer(10, out var refreshed));
        Assert.Equal(2, refreshed!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0)!.Value.Quantity);
    }

    [Fact]
    public async Task GuildScroll_WhileNotInAGuild_RepliesResultOne_AndLeavesTheItemUntouched()
    {
        var (session, _, zone, state, characters, guilds, cash, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(GuildScroll30MinItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters, guilds, cash, eventLog);

        var response = await service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.NotNull(response);
        Assert.Equal(1, response!.Value.Result);
        Assert.Null(characters.LastReplacedContainer);
        Assert.Null(guilds.LastSetBuff);
    }

    [Fact]
    public async Task GuildScroll_GuildBuffWriteFails_RepliesResultOne_AndLeavesTheItemUntouched()
    {
        var (session, _, zone, state, characters, guilds, cash, eventLog) = SetUp();
        SeedGuildMembership(zone, 77);
        guilds.Seed(new GuildSummaryDto(77, "TestGuild", 1, 10, 0, 0, 0, 0, 0L, 0, DateTime.UtcNow, 1));
        guilds.ThrowOnSetBuff = true;
        SeedInventory(zone, new ItemStack(GuildScroll30MinItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters, guilds, cash, eventLog);

        var response = await service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.NotNull(response);
        Assert.Equal(1, response!.Value.Result);
        Assert.Null(characters.LastReplacedContainer);
    }


    [Fact]
    public async Task UnhandledItemFamily_MatchesNoDispatchBranch_ReturnsNull_ForHandlerToDisconnect()
    {
        var (_, _, zone, state, characters, guilds, cash, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(UnhandledItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters, guilds, cash, eventLog);

        var response = await service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
            CancellationToken.None);

        Assert.Null(response);
        Assert.Null(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task GpTicket500_Redeems_CreditsTheFixedAmount_ConsumesTheStack_AndLogsAnAuditEntry()
    {
        var (session, _, zone, state, characters, guilds, cash, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(Ticket500ItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters, guilds, cash, eventLog);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result);
        Assert.Equal(0, response.Value);
        Assert.Equal(0, response.Value2);

        Assert.NotNull(cash.LastCredit);
        Assert.Equal(AccountId, cash.LastCredit!.Value.AccountId);
        Assert.Equal(500, cash.LastCredit.Value.Amount);
        Assert.Equal(Ticket500ItemId, cash.LastCredit.Value.ProductId);
        Assert.Equal(500, await cash.GetBalanceAsync(AccountId, CancellationToken.None));

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(AccountId, logged.ActorAccountId);
        Assert.Equal(10, logged.ActorCharacterId);
        Assert.Equal(500L, logged.DeltaMoney);
        Assert.Equal(Ticket500ItemId, logged.ItemId);

        Assert.NotNull(characters.LastReplacedContainer);
        Assert.DoesNotContain(characters.LastReplacedContainer!.Value.Items, i => i.Slot == 0);
    }

    [Fact]
    public async Task GpTicket100_Redeems_CreditsTheFixedAmount()
    {
        var (session, _, zone, state, characters, guilds, cash, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(Ticket100ItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters, guilds, cash, eventLog);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result);
        Assert.Equal(100, cash.LastCredit!.Value.Amount);
        Assert.Equal(100, await cash.GetBalanceAsync(AccountId, CancellationToken.None));
    }

    [Fact]
    public async Task GpTicket_StackedQuantity_DestroysTheEntireStack_AndCreditsOnlyOnce()
    {
        var (_, _, zone, state, characters, guilds, cash, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(Ticket500ItemId, 5, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters, guilds, cash, eventLog);

        await RunToCompletionAsync(
            service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Equal(500, await cash.GetBalanceAsync(AccountId, CancellationToken.None));
        Assert.NotNull(characters.LastReplacedContainer);
        Assert.DoesNotContain(characters.LastReplacedContainer!.Value.Items, i => i.Slot == 0);

        Assert.True(zone.TryGetPlayer(10, out var refreshed));
        Assert.Null(refreshed!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0));
    }

    [Fact]
    public async Task GpTicket_CreditCallFails_RepliesResultOne_AndLeavesTheItemAndBalanceUntouched()
    {
        var (session, _, zone, state, characters, guilds, cash, eventLog) = SetUp();
        cash.ThrowOnCredit = true;
        SeedInventory(zone, new ItemStack(Ticket500ItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters, guilds, cash, eventLog);

        var response = await service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.NotNull(response);
        Assert.Equal(1, response!.Value.Result);
        Assert.Null(characters.LastReplacedContainer);
        Assert.Empty(eventLog.LoggedEvents);
        Assert.Equal(0, await cash.GetBalanceAsync(AccountId, CancellationToken.None));
    }

    [Fact]
    public async Task Bottle_StillAcquiresIntoTheFirstEmptySlot_AfterTheDispatchRefactor()
    {
        var (session, _, zone, state, characters, guilds, cash, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(BottleItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters, guilds, cash, eventLog);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result);
        Assert.NotNull(characters.LastReplacedContainer);
        Assert.DoesNotContain(characters.LastReplacedContainer!.Value.Items, i => i.Slot == 0);

        Assert.True(zone.TryGetPlayer(10, out var refreshed));
        Assert.Equal((BottleItemId, 30), refreshed!.BottleSlots[0]);
    }

    [Fact]
    public async Task ProxyShopRentalExtension_NoExistingShop_ExtendsFromToday_PersistsLogsAndConsumesTheItem()
    {
        var (session, _, zone, state, characters, guilds, cash, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(ProxyShopOneDayItemId, 1, 0, 0, 0, 0, 0, 0, 0, 42, 7));
        var offlineShops = new FakeOfflineShopRepository();
        var relay = new FakeProxyShopExpirationRelayQueue();
        var service = CreateService(characters, guilds, cash, eventLog, offlineShops, relay);
        var expected = GameDate.Today();
        Assert.True(GameDate.TryAddDays(expected, 1, out expected));

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result);
        Assert.Equal(expected, response.Value);
        Assert.Equal(0, response.Value2);

        Assert.Equal(expected, offlineShops.LastExtendRentalNewShopDate);

        var relayed = Assert.Single(relay.Enqueued);
        Assert.Equal(ShardId, relayed.SourceShardId);
        Assert.Equal(10, relayed.CharacterId);
        Assert.Equal(expected, relayed.NewExpirationDate);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(10, logged.ActorCharacterId);
        Assert.Equal(ProxyShopOneDayItemId, logged.ItemId);
        Assert.Equal(1, logged.Quantity);
        Assert.Equal("Serial=7;ExpireDate=42", logged.Payload);

        Assert.NotNull(characters.LastReplacedContainer);
        Assert.DoesNotContain(characters.LastReplacedContainer!.Value.Items, i => i.Slot == 0);
        Assert.True(zone.TryGetPlayer(10, out var refreshed));
        Assert.Null(refreshed!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0));
    }

    [Fact]
    public async Task ProxyShopRentalExtension_ExistingFutureExpiration_CompoundsOntoTheRemainingTime()
    {
        var (_, _, zone, state, characters, guilds, cash, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(ProxyShopSevenDayItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var offlineShops = new FakeOfflineShopRepository();
        var today = GameDate.Today();
        Assert.True(GameDate.TryAddDays(today, 10, out var tenDaysOut));
        offlineShops.SeedShop(new OfflineShopRowDto(10, null, 1, tenDaysOut, 0, 0, 0, 0, 0, ""));
        Assert.True(GameDate.TryAddDays(tenDaysOut, 7, out var expected));
        var service = CreateService(characters, guilds, cash, eventLog, offlineShops);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Equal(0, response.Result);
        Assert.Equal(expected, response.Value);
        Assert.Equal(expected, offlineShops.LastExtendRentalNewShopDate);
    }

    [Fact]
    public async Task ProxyShopRentalExtension_ReskinItemIds_GrantTheSameDayCountsAsTheirOriginals()
    {
        var (_, _, zone, state, characters, guilds, cash, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(ProxyShopSevenDayReskinItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var offlineShops = new FakeOfflineShopRepository();
        var service = CreateService(characters, guilds, cash, eventLog, offlineShops);
        Assert.True(GameDate.TryAddDays(GameDate.Today(), 7, out var expected));

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Equal(0, response.Result);
        Assert.Equal(expected, response.Value);
    }

    [Fact]
    public async Task
        ProxyShopRentalExtension_ExtendRentalPersistenceFails_RepliesResultOne_WithComputedExpiration_AndLeavesTheItemUntouched()
    {
        var (session, _, zone, state, characters, guilds, cash, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(ProxyShopOneDayItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var offlineShops = new FakeOfflineShopRepository { ThrowOnExtendRental = true };
        var relay = new FakeProxyShopExpirationRelayQueue();
        var service = CreateService(characters, guilds, cash, eventLog, offlineShops, relay);
        Assert.True(GameDate.TryAddDays(GameDate.Today(), 1, out var expected));

        var response = await service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.NotNull(response);
        Assert.Equal(1, response!.Value.Result);
        Assert.Equal(expected, response.Value.Value);
        Assert.Equal(0, response.Value.Value2);
        Assert.Null(characters.LastReplacedContainer);
        Assert.Empty(eventLog.LoggedEvents);
        Assert.Empty(relay.Enqueued);
    }

    [Fact]
    public async Task
        ProxyShopRentalExtension_ExtremeExistingExpiration_RepliesResultOne_WithInvalidDateSentinel_AndNoSideEffects()
    {
        var (session, _, zone, state, characters, guilds, cash, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(ProxyShopOneDayItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var offlineShops = new FakeOfflineShopRepository();
        offlineShops.SeedShop(new OfflineShopRowDto(10, null, 1, 99991231, 0, 0, 0, 0, 0, ""));
        var relay = new FakeProxyShopExpirationRelayQueue();
        var service = CreateService(characters, guilds, cash, eventLog, offlineShops, relay);

        var response = await service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.NotNull(response);
        Assert.Equal(1, response!.Value.Result);
        Assert.Equal(GameDate.Invalid, response.Value.Value);
        Assert.Equal(0, response.Value.Value2);
        Assert.Null(offlineShops.LastExtendRentalNewShopDate);
        Assert.Null(characters.LastReplacedContainer);
        Assert.Empty(eventLog.LoggedEvents);
        Assert.Empty(relay.Enqueued);
    }

    [Fact]
    public async Task ProxyShopRentalExtension_StackedQuantity_ConsumesTheWholeStackInOneUse()
    {
        var (_, _, zone, state, characters, guilds, cash, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(ProxyShopOneDayItemId, 5, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters, guilds, cash, eventLog);

        await RunToCompletionAsync(
            service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.NotNull(characters.LastReplacedContainer);
        Assert.DoesNotContain(characters.LastReplacedContainer!.Value.Items, i => i.Slot == 0);
        Assert.True(zone.TryGetPlayer(10, out var refreshed));
        Assert.Null(refreshed!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0));
    }
}
