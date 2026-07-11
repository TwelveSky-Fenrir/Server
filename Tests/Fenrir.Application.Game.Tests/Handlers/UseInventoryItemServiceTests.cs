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

/// <summary>
///     Drives the real <see cref="UseInventoryItemService" /> (opcode 23) over a real <see cref="Zone" />; ticks
///     the zone while the service's own <c>PostInventoryCommandAndWaitAsync</c> await is pending, same pattern as
///     <c>SkyUpgradeItemServiceTests</c>. Covers the Guild Scroll, GP ticket (itemId 723/725), and proxy-shop
///     rental-extension (itemId 567/592/8422/8423) families added on top of the pre-existing Bottle-only
///     dispatch, plus one Bottle regression case guarding the dispatch refactor itself. The Faction Transfer
///     Scroll family (8153/8154) used to be covered here too, against a since-removed permit-banking stub --
///     see <c>Tests/Fenrir.Application.Game.Tests/Inventory/UseItems/TribeScrollTransferUseItemHandlerTests.cs</c>
///     for its real coverage now.
/// </summary>
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
        ValueTask<UseInventoryItemResponse> pending, Zone zone)
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

        return await task;
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

        // BuffType/BuffState carried through unchanged. The seeded baseline (1000L ticks -- effectively
        // year 1) is already in the past relative to "now", so GuildBuffTopUp restarts counting from now
        // rather than stacking onto that stale timestamp (Server/ts25extra/S08_MyDB.cpp:1151-1186's own
        // "already expired" branch) -- BuffTime becomes exactly the scroll's 60 minutes, not 5+60.
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
        Assert.Equal(1, response.Result);
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
        Assert.Equal(1, response.Result);
        Assert.Null(characters.LastReplacedContainer);
    }

    // The old "TribeTransferScroll_Use_GrantsOnePermit_AndConsumesTheScroll" test was removed here: it covered
    // a superseded permit-banking stub (ResolveTribeTransferScrollAsync). The real op23 items 8153/8154
    // mechanism (13-gate precondition chain + atomic best-effort equip/skill remap) is now
    // TribeScrollTransferUseItemHandler (workstream C11), covered by its own dedicated test file
    // (Tests/Fenrir.Application.Game.Tests/Inventory/UseItems/TribeScrollTransferUseItemHandlerTests.cs).

    [Fact]
    public async Task UnhandledItemFamily_RepliesResultOne_AndLeavesTheItemUntouched()
    {
        var (session, _, zone, state, characters, guilds, cash, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(UnhandledItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters, guilds, cash, eventLog);

        var response = await service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(1, response.Result);
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
        // Neither value field ever reflects the credited amount -- matches the legacy response's own
        // inability to convey it (see UseInventoryItemService.ResolveGpTicketAsync's own remarks).
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

        // Full-stack consumption, not decrement-by-one: a single use of a 5-stack still grants exactly one
        // 500 credit, and destroys the whole slot -- not just one unit -- unlike GuildScroll_StackedQuantity's
        // decrement-by-one sibling behavior.
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
        Assert.Equal(1, response.Result);
        // Hardening: unlike the legacy call site (which discards the credit call's return value and always
        // consumes the item / reports success), a failed credit here leaves the item and the audit trail
        // untouched.
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
        // The bottle-acquire mirror is posted (fire-and-forget) only after the awaited inventory mirror
        // resolves, so it needs one more tick of its own to be observable on state.BottleSlots.
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

        // Always relayed cross-shard on success, regardless of whether this shard's own zone-37 instance
        // (there is none here) happened to hold a matching entry -- see ProxyShopExpirationRelayHost's own
        // remarks for why this hardens past the legacy single-process-only limitation.
        var relayed = Assert.Single(relay.Enqueued);
        Assert.Equal(ShardId, relayed.SourceShardId);
        Assert.Equal(10, relayed.CharacterId);
        Assert.Equal(expected, relayed.NewExpirationDate);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(10, logged.ActorCharacterId);
        Assert.Equal(ProxyShopOneDayItemId, logged.ItemId);
        Assert.Equal(1, logged.Quantity);
        // The two auxiliary data values carried alongside the slot (Serial/ExpireDate).
        Assert.Equal("Serial=7;ExpireDate=42", logged.Payload);

        // world.Items 567/592/8422/8423's own stack-safe status is unresolved (see
        // CashItemStackConsumption's own remarks) -- currently defaults to whole-stack consumption, so a
        // single-quantity slot is cleared entirely, not merely decremented.
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
        Assert.Equal(1, response.Result);
        // Computed (valid) new expiration is still echoed even though nothing was persisted -- matches the
        // legacy client's inability to distinguish "unreachable" from "reported failure".
        Assert.Equal(expected, response.Value);
        Assert.Equal(0, response.Value2);
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
        // The maximum representable calendar date -- projecting one more day forward overflows.
        offlineShops.SeedShop(new OfflineShopRowDto(10, null, 1, 99991231, 0, 0, 0, 0, 0, ""));
        var relay = new FakeProxyShopExpirationRelayQueue();
        var service = CreateService(characters, guilds, cash, eventLog, offlineShops, relay);

        var response = await service.ResolveAsync(zone, state, 10, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(1, response.Result);
        Assert.Equal(GameDate.Invalid, response.Value);
        Assert.Equal(0, response.Value2);
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
