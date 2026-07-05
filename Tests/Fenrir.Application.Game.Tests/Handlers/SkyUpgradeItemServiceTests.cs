using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.ItemModification;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

/// <summary>
///     Drives the real <see cref="SkyUpgradeItemService" /> (opcode 93) over a real <see cref="Zone" />; ticks the
///     zone while the service's own <c>PostInventoryCommandAndWaitAsync</c> await is pending, same pattern as
///     <c>FishingCatchHandlerTests</c>.
/// </summary>
public class SkyUpgradeItemServiceTests
{
    private const int WarlordItemId = 87000;

    private static async Task<SkyUpgradeItemResult> RunToCompletionAsync(ValueTask<SkyUpgradeItemResult> pending,
        Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("SkyUpgradeItemService task never completed.");
        }

        return await task;
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, Zone Zone, PlayerRuntimeState State,
        FakeCharacterRepository Repository) SetUp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, 10);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(10, out var state));
        return (session, pipe, zone, state!, new FakeCharacterRepository());
    }

    private static void SeedInventory(Zone zone, ItemStack target, ItemStack material)
    {
        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0,
                ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, target).SetItem(1, material)));
        zone.PostInventoryCommand(new InventoryZoneCommand(10, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    private static SkyUpgradeItemService CreateService(FakeCharacterRepository characters)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [WarlordItemId] = new(WorldDataTestRows.Item(WarlordItemId) with { Sort = 7, CheckSetItem = 2 }, [])
        }.ToFrozenDictionary();

        return new SkyUpgradeItemService(characters, ZoneTestKit.EmptyWorldData(itemsById),
            NullLogger<SkyUpgradeItemService>.Instance);
    }

    [Fact]
    public async Task ValidPreconditions_AlwaysDeductsMoneyAndConsumesMaterial_RegardlessOfRandomOutcome()
    {
        // SkyUpgradeItemService rolls via SystemRandomSource.Instance (no DI seam, matching EnchantItemHandler's
        // own precedent) -- success/failure branch fidelity is covered deterministically by
        // SkyUpgradeResolverTests. This asserts only what's true on BOTH outcomes: money is always deducted and
        // the material is always consumed (S04_MyWork02.cpp:12862's own unconditional placement of both before
        // the roll).
        var (session, _, zone, state, repo) = SetUp();
        SeedInventory(zone, new ItemStack(WarlordItemId, 1, 30, 0, 0, 0, 0, 0, 0, 0, 1),
            new ItemStack(501, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var service = CreateService(repo);

        var result = await RunToCompletionAsync(
            service.UpgradeAsync(new SkyUpgradeItemRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1 }, zone,
                state, 10, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(SkyUpgradeItemOutcome.Applied, result.Outcome);
        Assert.NotNull(repo.LastAdjustMoneyAndReplaceContainer);
        Assert.Equal(-50_000_000, repo.LastAdjustMoneyAndReplaceContainer!.Value.DeltaMoney);

        var items = repo.LastAdjustMoneyAndReplaceContainer.Value.Items;
        Assert.DoesNotContain(items, i => i.Slot == 1);
    }

    [Fact]
    public async Task RejectedPrecondition_Rejected()
    {
        var (_, _, zone, state, repo) = SetUp();
        // Enchant below the required +30 threshold.
        SeedInventory(zone, new ItemStack(WarlordItemId, 1, 10, 0, 0, 0, 0, 0, 0, 0, 1),
            new ItemStack(501, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var service = CreateService(repo);

        var result = await service.UpgradeAsync(
            new SkyUpgradeItemRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1 }, zone, state, 10,
            CancellationToken.None);

        Assert.Equal(SkyUpgradeItemOutcome.Rejected, result.Outcome);
        Assert.Null(repo.LastAdjustMoneyAndReplaceContainer);
    }

    [Fact]
    public async Task InsufficientFunds_TreatedAsRejected()
    {
        var (_, _, zone, state, repo) = SetUp();
        SeedInventory(zone, new ItemStack(WarlordItemId, 1, 30, 0, 0, 0, 0, 0, 0, 0, 1),
            new ItemStack(501, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        repo.ThrowOnAdjustMoney = true;
        var service = CreateService(repo);

        var result = await service.UpgradeAsync(
            new SkyUpgradeItemRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1 }, zone, state, 10,
            CancellationToken.None);

        Assert.Equal(SkyUpgradeItemOutcome.Rejected, result.Outcome);
    }
}
