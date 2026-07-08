using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.ItemModification;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

/// <summary>
///     Drives the real <see cref="CraftItemService" /> (op29, CZ_MAKE_ITEM_SEND) over a real <see cref="Zone" />;
///     ticks the zone while the service's own <c>PostInventoryCommandAndWaitAsync</c> await is pending, same
///     pattern as <c>UseInventoryItemServiceTests</c>. Covers the game.EventLog (Category=ItemCreate) wiring
///     added on top of the pre-existing Jade-upgrade/Advanced-elixir craft resolution -- see
///     <c>CraftResolverTests</c> for the pure-domain outcome coverage this does not duplicate.
/// </summary>
public class CraftItemServiceTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;

    private static async Task<T> RunToCompletionAsync<T>(ValueTask<T> pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("CraftItemService task never completed.");
        }

        return await task;
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, Zone Zone, PlayerRuntimeState State,
        FakeCharacterRepository Characters, FakeEventLogRepository EventLog) SetUp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, CharacterId);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        return (session, pipe, zone, state!, new FakeCharacterRepository(), new FakeEventLogRepository());
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

    private static CraftItemService CreateService(FakeCharacterRepository characters, FakeEventLogRepository eventLog)
    {
        return new CraftItemService(characters, eventLog, ZoneTestKit.EmptyWorldData(),
            NullLogger<CraftItemService>.Instance);
    }

    private static ItemStack Jade(int quantity, int serial)
    {
        return new ItemStack(CraftRecipeCatalog.PurpleJadeItemId, quantity, 0, 0, 0, 0, 0, 0, 0, 0, serial);
    }

    private static CraftItemRequest JadeUpgradePacket()
    {
        return new CraftItemRequest
        {
            Sort = CraftRecipeCatalog.JadeUpgradeSort, Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1, Page3 = -1,
            Index3 = -1, Page4 = -1, Index4 = -1
        };
    }

    private static CraftItemRequest AdvancedElixirPacket()
    {
        return new CraftItemRequest
        {
            Sort = CraftRecipeCatalog.AdvancedElixirSort, Page1 = 0, Index1 = 0, Page2 = -1, Index2 = -1,
            Page3 = -1, Index3 = -1, Page4 = -1, Index4 = -1
        };
    }

    [Fact]
    public async Task JadeUpgrade_Success_LogsAnItemCreateAuditRow_ForTheMintedRedJade()
    {
        var (session, _, zone, state, characters, eventLog) = SetUp();
        SeedInventory(zone, (0, Jade(1, 777)), (1, Jade(1, 778)));
        var service = CreateService(characters, eventLog);

        var result = await RunToCompletionAsync(
            service.ResolveJadeUpgradeAsync(JadeUpgradePacket(), zone, state, CharacterId, AccountId,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(JadeUpgradeOutcome.Applied, result.Outcome);
        Assert.Equal(CraftRecipeCatalog.RedJadeItemId, result.ResultItemId);
        Assert.NotNull(characters.LastReplacedContainer);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(EventLogCategory.ItemCreate, logged.Category);
        Assert.Equal(AccountId, logged.ActorAccountId);
        Assert.Equal(CharacterId, logged.ActorCharacterId);
        Assert.Null(logged.TargetAccountId);
        Assert.Null(logged.TargetCharacterId);
        Assert.Equal(CraftRecipeCatalog.RedJadeItemId, logged.ItemId);
        Assert.Equal((byte)1, logged.Outcome);
    }

    [Fact]
    public async Task JadeUpgrade_WrongMaterial_IsRejected_AndLogsNothing()
    {
        var (session, _, zone, state, characters, eventLog) = SetUp();
        SeedInventory(zone, (0, Jade(1, 777)), (1, new ItemStack(999, 1, 0, 0, 0, 0, 0, 0, 0, 0, 779)));
        var service = CreateService(characters, eventLog);

        var result = await service.ResolveJadeUpgradeAsync(JadeUpgradePacket(), zone, state, CharacterId, AccountId,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(JadeUpgradeOutcome.Rejected, result.Outcome);
        Assert.Null(characters.LastReplacedContainer);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task AdvancedElixir_WrongMaterial_IsRejected_AndLogsNothing()
    {
        var (session, _, zone, state, characters, eventLog) = SetUp();
        SeedInventory(zone, (0, new ItemStack(1019, 10, 0, 0, 0, 0, 0, 0, 0, 0, 900)));
        var service = CreateService(characters, eventLog);

        var result = await service.ResolveAdvancedElixirAsync(AdvancedElixirPacket(), zone, state, CharacterId,
            AccountId, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(AdvancedElixirOutcome.Rejected, result.Outcome);
        Assert.Null(characters.LastReplacedContainer);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task AdvancedElixir_SuccessOutcome_LogsItemCreate_FailureOutcome_LogsNothing()
    {
        // CraftItemService rolls the elixir via SystemRandomSource.Instance -- no DI seam, the same posture as
        // SkyUpgradeItemService/EnchantItemService (see SkyUpgradeItemServiceTests' own remarks), so this
        // repeats many independent trials rather than forcing a specific roll. At the fixed 20% success rate
        // (CraftRecipeCatalog.AdvancedElixirSuccessRatePercent), the odds that 60 independent trials produce
        // zero successes (0.8^60) or zero failures (0.2^60) are both negligible, so asserting both branches
        // were observed is not flaky in practice.
        var sawSuccess = false;
        var sawFailure = false;

        for (var i = 0; i < 60; i++)
        {
            var (_, _, zone, state, characters, eventLog) = SetUp();
            SeedInventory(zone,
                (0, new ItemStack(506, CraftRecipeCatalog.AdvancedElixirRequiredQuantity, 0, 0, 0, 0, 0, 0, 0, 0,
                    900 + i)));
            var service = CreateService(characters, eventLog);

            var result = await RunToCompletionAsync(
                service.ResolveAdvancedElixirAsync(AdvancedElixirPacket(), zone, state, CharacterId, AccountId,
                    CancellationToken.None), zone);

            if (result.Outcome == AdvancedElixirOutcome.Success)
            {
                sawSuccess = true;
                var logged = Assert.Single(eventLog.LoggedEvents);
                Assert.Equal(EventLogCategory.ItemCreate, logged.Category);
                Assert.Equal(AccountId, logged.ActorAccountId);
                Assert.Equal(CharacterId, logged.ActorCharacterId);
                Assert.Equal(result.NewItemStack!.Value.ItemId, logged.ItemId);
                Assert.Equal(result.NewItemStack.Value.Quantity, logged.Quantity);
                Assert.Equal((byte)1, logged.Outcome);
            }
            else
            {
                sawFailure = true;
                Assert.Equal(AdvancedElixirOutcome.Failed, result.Outcome);
                Assert.Empty(eventLog.LoggedEvents);
            }
        }

        Assert.True(sawSuccess, "Expected at least one success roll across 60 independent trials.");
        Assert.True(sawFailure, "Expected at least one failure roll across 60 independent trials.");
    }
}
