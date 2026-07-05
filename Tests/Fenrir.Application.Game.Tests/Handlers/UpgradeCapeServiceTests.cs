using System.Collections.Immutable;
using Fenrir.Application.Game.Handlers.ItemModification.Services;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

/// <summary>
///     Drives the real <see cref="UpgradeCapeService" /> (opcode 127) over a real <see cref="Zone" />; ticks the
///     zone while the service's own <c>PostInventoryCommandAndWaitAsync</c> await is pending, same pattern as
///     <c>FishingCatchHandlerTests</c>.
/// </summary>
public class UpgradeCapeServiceTests
{
    private static async Task<UpgradeCapeResult> RunToCompletionAsync(ValueTask<UpgradeCapeResult> pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("UpgradeCapeService task never completed.");
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

    [Fact]
    public async Task ValidPreconditions_AlwaysDeductsMoneyAndConsumesMaterial_RegardlessOfRandomOutcome()
    {
        // UpgradeCapeService rolls via SystemRandomSource.Instance (no DI seam, matching EnchantItemHandler's
        // own precedent) -- success/failure branch fidelity (including the all-zero Value on failure) is
        // covered deterministically by CapeUpgradeResolverTests.
        var (session, _, zone, state, repo) = SetUp();
        SeedInventory(zone, new ItemStack(1401, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            new ItemStack(984, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var service = new UpgradeCapeService(repo, NullLogger<UpgradeCapeService>.Instance);

        var result = await RunToCompletionAsync(
            service.UpgradeAsync(new UpgradeCapeRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1 }, zone, state,
                10, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(UpgradeCapeOutcome.Applied, result.Outcome);
        Assert.NotNull(repo.LastAdjustMoneyAndReplaceContainer);
        Assert.Equal(-20_000_000, repo.LastAdjustMoneyAndReplaceContainer!.Value.DeltaMoney);

        var items = repo.LastAdjustMoneyAndReplaceContainer.Value.Items;
        Assert.DoesNotContain(items, i => i.Slot == 1);
    }

    [Fact]
    public async Task InvalidTargetItem_Rejected()
    {
        var (_, _, zone, state, repo) = SetUp();
        SeedInventory(zone, new ItemStack(9999, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            new ItemStack(984, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var service = new UpgradeCapeService(repo, NullLogger<UpgradeCapeService>.Instance);

        var result = await service.UpgradeAsync(
            new UpgradeCapeRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1 }, zone, state, 10,
            CancellationToken.None);

        Assert.Equal(UpgradeCapeOutcome.Rejected, result.Outcome);
        Assert.Null(repo.LastAdjustMoneyAndReplaceContainer);
    }

    [Fact]
    public async Task InvalidMaterial_Rejected()
    {
        var (_, _, zone, state, repo) = SetUp();
        SeedInventory(zone, new ItemStack(1401, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            new ItemStack(12345, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var service = new UpgradeCapeService(repo, NullLogger<UpgradeCapeService>.Instance);

        var result = await service.UpgradeAsync(
            new UpgradeCapeRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1 }, zone, state, 10,
            CancellationToken.None);

        Assert.Equal(UpgradeCapeOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public async Task InsufficientFunds_TreatedAsRejected()
    {
        var (_, _, zone, state, repo) = SetUp();
        SeedInventory(zone, new ItemStack(1401, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            new ItemStack(984, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        repo.ThrowOnAdjustMoney = true;
        var service = new UpgradeCapeService(repo, NullLogger<UpgradeCapeService>.Instance);

        var result = await service.UpgradeAsync(
            new UpgradeCapeRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1 }, zone, state, 10,
            CancellationToken.None);

        Assert.Equal(UpgradeCapeOutcome.Rejected, result.Outcome);
    }
}
