using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Forge;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.ItemModification;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

/// <summary>
///     Drives the real <see cref="DestroyItemService" /> (opcode 89) over a real <see cref="Zone" />; ticks the
///     zone while the service's own <c>PostInventoryCommandAndWaitAsync</c> await is pending, same pattern as
///     <c>SkyUpgradeItemServiceTests</c>. Covers the game.EventLog (Category=ItemDestroy) success-path audit
///     row wired in alongside the pre-existing exception-only LogWarning/LogError telemetry -- the enchant-tier
///     -&gt; money/stone mapping itself is covered deterministically by <c>DestroyResolverTests</c>, not
///     re-verified here.
/// </summary>
public class DestroyItemServiceTests
{
    private const int RareItemId = 87000;
    private const int StoneItemId = 1021;
    private const int AccountId = 42;

    private static async Task<DestroyItemResult> RunToCompletionAsync(ValueTask<DestroyItemResult> pending,
        Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("DestroyItemService task never completed.");
        }

        return await task;
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, Zone Zone, PlayerRuntimeState State,
        FakeCharacterRepository Characters, FakeEventLogRepository EventLog) SetUp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, 10);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(10, out var state));
        return (session, pipe, zone, state!, new FakeCharacterRepository(), new FakeEventLogRepository());
    }

    private static void SeedInventory(Zone zone, ItemStack item)
    {
        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0,
                ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, item)));
        zone.PostInventoryCommand(new InventoryZoneCommand(10, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    private static DestroyItemService CreateService(FakeCharacterRepository characters,
        FakeEventLogRepository eventLog)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [RareItemId] = new(WorldDataTestRows.Item(RareItemId) with
            {
                Type = DestroyResolver.RareItemType, Sort = 7, CheckImprove = 2
            }, []),
            [StoneItemId] = new(WorldDataTestRows.Item(StoneItemId), [])
        }.ToFrozenDictionary();

        return new DestroyItemService(characters, ZoneTestKit.EmptyWorldData(itemsById), eventLog,
            NullLogger<DestroyItemService>.Instance);
    }

    [Fact]
    public async Task SuccessfulDestroy_WritesAnItemDestroyEventLogRow_WithTheDestroyedItemAndMoneyGranted()
    {
        var (session, _, zone, state, characters, eventLog) = SetUp();
        // Enchant 10 -> tier 1: 1,000,000 money, stone 1021 (see DestroyResolverTests.EnchantTier_MapsToExpectedMoneyAndStone).
        SeedInventory(zone, new ItemStack(RareItemId, 1, 10, 0, 0, 0, 0, 0, 0, 0, 777));
        var service = CreateService(characters, eventLog);

        var result = await RunToCompletionAsync(
            service.DestroyAsync(new DestroyItemRequest { Page1 = ContainerMatrix.InventoryPage0, Index1 = 0 },
                zone, state, 10, AccountId, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(DestroyItemOutcome.Applied, result.Outcome);
        Assert.Equal(1_000_000, result.Money);
        Assert.Equal(StoneItemId, result.StoneItemId);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(EventLogCategory.ItemDestroy, logged.Category);
        Assert.Equal(AccountId, logged.ActorAccountId);
        Assert.Equal(10, logged.ActorCharacterId);
        Assert.Null(logged.TargetAccountId);
        Assert.Null(logged.TargetCharacterId);
        Assert.Equal(1_000_000L, logged.DeltaMoney);
        Assert.Equal(RareItemId, logged.ItemId);
        Assert.Equal(1, logged.Quantity);
        Assert.Equal((byte?)1, logged.Outcome);
        Assert.NotNull(logged.Payload);
        Assert.Contains($"StoneItemId={StoneItemId}", logged.Payload);
    }

    [Fact]
    public async Task RejectedDestroy_EnchantBelowThreshold_WritesNoEventLogRow()
    {
        var (session, _, zone, state, characters, eventLog) = SetUp();
        // Enchant 0 is always rejected by DestroyResolver (see DestroyResolverTests.EnchantZero_IsRejected).
        SeedInventory(zone, new ItemStack(RareItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 777));
        var service = CreateService(characters, eventLog);

        var result = await service.DestroyAsync(
            new DestroyItemRequest { Page1 = ContainerMatrix.InventoryPage0, Index1 = 0 }, zone, state, 10,
            AccountId, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(DestroyItemOutcome.Rejected, result.Outcome);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task MoneyCapOverflow_TreatedAsRejected_WritesNoEventLogRow()
    {
        var (session, _, zone, state, characters, eventLog) = SetUp();
        characters.ThrowOnAdjustMoney = true;
        SeedInventory(zone, new ItemStack(RareItemId, 1, 10, 0, 0, 0, 0, 0, 0, 0, 777));
        var service = CreateService(characters, eventLog);

        var result = await service.DestroyAsync(
            new DestroyItemRequest { Page1 = ContainerMatrix.InventoryPage0, Index1 = 0 }, zone, state, 10,
            AccountId, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(DestroyItemOutcome.Rejected, result.Outcome);
        Assert.Empty(eventLog.LoggedEvents);
    }
}
