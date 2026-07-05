using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Guilds;
using Fenrir.Application.Game.Handlers;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.ZoneLifecycle.Services;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Guilds;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

/// <summary>
///     Drives the real <see cref="UseInventoryItemHandler" /> (opcode 23) over a real <see cref="Zone" />; ticks
///     the zone while the handler's own <c>PostInventoryCommandAndWaitAsync</c> await is pending, same pattern as
///     <c>SkyUpgradeItemHandlerTests</c>. Covers the Guild Scroll and Faction Transfer Scroll families added on
///     top of the pre-existing Bottle-only dispatch, plus one Bottle regression case guarding the dispatch
///     refactor itself.
/// </summary>
public class UseInventoryItemHandlerTests
{
    private const byte BottleSort = 26;
    private const byte SpecialUseSort = 3;
    private const int GuildScroll30MinItemId = 558;
    private const int GuildScroll60MinItemId = 1211;
    private const int TribeTransferScrollItemId = 8153;
    private const int BottleItemId = 501;
    private const int UnhandledItemId = 999001;

    private static async Task RunToCompletionAsync(ValueTask pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("UseInventoryItemHandler task never completed.");
        }

        await task;
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, Zone Zone, PlayerRuntimeState State,
        FakeCharacterRepository Characters, FakeGuildRepository Guilds) SetUp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, 10);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(10, out var state));
        return (session, pipe, zone, state!, new FakeCharacterRepository(), new FakeGuildRepository());
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

    private static UseInventoryItemHandler CreateHandler(FakeCharacterRepository characters,
        FakeGuildRepository guilds)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [GuildScroll30MinItemId] =
                new(WorldDataTestRows.Item(GuildScroll30MinItemId) with { Sort = SpecialUseSort }, []),
            [GuildScroll60MinItemId] =
                new(WorldDataTestRows.Item(GuildScroll60MinItemId) with { Sort = SpecialUseSort }, []),
            [TribeTransferScrollItemId] =
                new(WorldDataTestRows.Item(TribeTransferScrollItemId) with { Sort = SpecialUseSort }, []),
            [BottleItemId] = new(WorldDataTestRows.Item(BottleItemId) with { Sort = BottleSort }, []),
            [UnhandledItemId] = new(WorldDataTestRows.Item(UnhandledItemId) with { Sort = SpecialUseSort }, [])
        }.ToFrozenDictionary();

        return new UseInventoryItemHandler(new UseInventoryItemService(characters, guilds,
            ZoneTestKit.EmptyWorldData(itemsById), NullLogger<UseInventoryItemService>.Instance));
    }

    [Fact]
    public async Task GuildScroll_WhileInGuild_RechargesBuffTimeByTheItemsFixedAmount_AndConsumesTheScroll()
    {
        var (session, pipe, zone, _, characters, guilds) = SetUp();
        SeedGuildMembership(zone, 77);
        guilds.Seed(new GuildSummaryDto(77, "TestGuild", 1, 10, 0, 2, 1, 5, 1000L, 0, DateTime.UtcNow, 1));
        SeedInventory(zone, new ItemStack(GuildScroll60MinItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var handler = CreateHandler(characters, guilds);

        await RunToCompletionAsync(
            handler.HandleAsync(new UseInventoryItemRequest { Page = 0, Index = 0, Value = 0 }, session,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);

        // BuffType/BuffState/BuffTimeForDiff carried through unchanged; only BuffTime (5 -> 5+60) moves.
        Assert.Equal((77, 2, 1, 65, 1000L), guilds.LastSetBuff);

        Assert.NotNull(characters.LastReplacedContainer);
        Assert.DoesNotContain(characters.LastReplacedContainer!.Value.Items, i => i.Slot == 0);

        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task GuildScroll_StackedQuantity_DecrementsByOne_AndKeepsTheSlotOccupied()
    {
        var (session, _, zone, _, characters, guilds) = SetUp();
        SeedGuildMembership(zone, 77);
        guilds.Seed(new GuildSummaryDto(77, "TestGuild", 1, 10, 0, 0, 0, 0, 0L, 0, DateTime.UtcNow, 1));
        SeedInventory(zone, new ItemStack(GuildScroll30MinItemId, 3, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var handler = CreateHandler(characters, guilds);

        await RunToCompletionAsync(
            handler.HandleAsync(new UseInventoryItemRequest { Page = 0, Index = 0, Value = 0 }, session,
                CancellationToken.None), zone);

        Assert.Equal(30, guilds.LastSetBuff!.Value.BuffTime);

        var persisted = Assert.Single(characters.LastReplacedContainer!.Value.Items, i => i.Slot == 0);
        Assert.Equal(2, persisted.Quantity);

        Assert.True(zone.TryGetPlayer(10, out var state));
        Assert.Equal(2, state!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0)!.Value.Quantity);
    }

    [Fact]
    public async Task GuildScroll_WhileNotInAGuild_RepliesResultOne_AndLeavesTheItemUntouched()
    {
        var (session, pipe, zone, _, characters, guilds) = SetUp();
        SeedInventory(zone, new ItemStack(GuildScroll30MinItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var handler = CreateHandler(characters, guilds);

        await handler.HandleAsync(new UseInventoryItemRequest { Page = 0, Index = 0, Value = 0 }, session,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Null(characters.LastReplacedContainer);
        Assert.Null(guilds.LastSetBuff);
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task GuildScroll_GuildBuffWriteFails_RepliesResultOne_AndLeavesTheItemUntouched()
    {
        var (session, _, zone, _, characters, guilds) = SetUp();
        SeedGuildMembership(zone, 77);
        guilds.Seed(new GuildSummaryDto(77, "TestGuild", 1, 10, 0, 0, 0, 0, 0L, 0, DateTime.UtcNow, 1));
        guilds.ThrowOnSetBuff = true;
        SeedInventory(zone, new ItemStack(GuildScroll30MinItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var handler = CreateHandler(characters, guilds);

        await handler.HandleAsync(new UseInventoryItemRequest { Page = 0, Index = 0, Value = 0 }, session,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Null(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task TribeTransferScroll_Use_GrantsOnePermit_AndConsumesTheScroll()
    {
        var (session, pipe, zone, _, characters, guilds) = SetUp();
        SeedInventory(zone, new ItemStack(TribeTransferScrollItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var handler = CreateHandler(characters, guilds);

        await RunToCompletionAsync(
            handler.HandleAsync(new UseInventoryItemRequest { Page = 0, Index = 0, Value = 0 }, session,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal((10, 1), characters.LastGrantTribeTransferPermit);
        Assert.Equal(1, characters.TribeTransferPermitCount);

        Assert.NotNull(characters.LastReplacedContainer);
        Assert.DoesNotContain(characters.LastReplacedContainer!.Value.Items, i => i.Slot == 0);

        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task UnhandledItemFamily_RepliesResultOne_AndLeavesTheItemUntouched()
    {
        var (session, pipe, zone, _, characters, guilds) = SetUp();
        SeedInventory(zone, new ItemStack(UnhandledItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var handler = CreateHandler(characters, guilds);

        await handler.HandleAsync(new UseInventoryItemRequest { Page = 0, Index = 0, Value = 0 }, session,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Null(characters.LastReplacedContainer);
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task Bottle_StillAcquiresIntoTheFirstEmptySlot_AfterTheDispatchRefactor()
    {
        var (session, pipe, zone, _, characters, guilds) = SetUp();
        SeedInventory(zone, new ItemStack(BottleItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var handler = CreateHandler(characters, guilds);

        await RunToCompletionAsync(
            handler.HandleAsync(new UseInventoryItemRequest { Page = 0, Index = 0, Value = 0 }, session,
                CancellationToken.None), zone);
        // The bottle-acquire mirror is posted (fire-and-forget) only after the awaited inventory mirror
        // resolves, so it needs one more tick of its own to be observable on state.BottleSlots.
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.NotNull(characters.LastReplacedContainer);
        Assert.DoesNotContain(characters.LastReplacedContainer!.Value.Items, i => i.Slot == 0);

        Assert.True(zone.TryGetPlayer(10, out var state));
        Assert.Equal((BottleItemId, 30), state!.BottleSlots[0]);

        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe));
    }
}
