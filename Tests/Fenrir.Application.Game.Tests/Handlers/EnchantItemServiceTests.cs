using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.ItemModification;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class EnchantItemServiceTests
{
    private const int TargetItemId = 2000;

        private const int GuaranteedSuccessMaterialId = 633;

        private const int PaidMaterialId = 1019;

    private static async Task<EnchantItemResult> RunToCompletionAsync(ValueTask<EnchantItemResult> pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("EnchantItemService task never completed.");
        }

        return await task;
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, Zone Zone, PlayerRuntimeState State,
        FakeCharacterRepository Repository, FakeEventLogQueue EventLog) SetUp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, 10);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(10, out var state));
        return (session, pipe, zone, state!, new FakeCharacterRepository(), new FakeEventLogQueue());
    }

    private static void SeedInventory(Zone zone, ItemStack target, ItemStack material)
    {
        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0,
                ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, target).SetItem(1, material)));
        zone.PostInventoryCommand(new InventoryZoneCommand(10, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    private static EnchantItemService CreateService(FakeCharacterRepository characters, FakeEventLogQueue eventLog)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [TargetItemId] = new(WorldDataTestRows.Item(TargetItemId) with { Sort = 7, CheckImprove = 2 }, []),
            [GuaranteedSuccessMaterialId] = new(WorldDataTestRows.Item(GuaranteedSuccessMaterialId), []),
            [PaidMaterialId] = new(WorldDataTestRows.Item(PaidMaterialId), [])
        }.ToFrozenDictionary();

        return new EnchantItemService(characters, ZoneTestKit.EmptyWorldData(itemsById), eventLog,
            NullLogger<EnchantItemService>.Instance);
    }

    [Fact]
    public async Task GuaranteedSuccessMaterial_AppliedAndLogsAttempt()
    {
        var (session, _, zone, state, repo, eventLog) = SetUp();
        state.LastEnchantAttemptUtc = DateTime.UtcNow - SimulationClock.LegacyTick - TimeSpan.FromMilliseconds(1);
        SeedInventory(zone, new ItemStack(TargetItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 777),
            new ItemStack(GuaranteedSuccessMaterialId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var service = CreateService(repo, eventLog);

        var result = await RunToCompletionAsync(
            service.EnchantAsync(new EnchantItemRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1, Luck = 0 },
                zone, state,
                10, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(EnchantItemOutcome.Applied, result.Outcome);
        Assert.Equal(0, result.ResultCode);
        Assert.Equal(1, result.NewEnchant);
        Assert.NotNull(repo.LastAdjustMoneyAndReplaceContainer);
        Assert.Equal(0, repo.LastAdjustMoneyAndReplaceContainer!.Value.DeltaMoney);

        var logged = Assert.Single(eventLog.Enqueued);
        Assert.Equal(24, logged.EventCode);
        Assert.Equal((byte)EventLogCategory.Enchant, logged.Category);
        Assert.Equal(10, logged.ActorCharacterId);
        Assert.Equal(0L, logged.DeltaMoney);
        Assert.Equal(TargetItemId, logged.ItemId);
        Assert.Equal(1, logged.Quantity);
        Assert.Equal((byte?)0, logged.Outcome);
    }

    [Fact]
    public async Task UnknownMaterial_RejectedBeforeAnyMutation_NotLogged()
    {
        var (_, _, zone, state, repo, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(TargetItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            new ItemStack(999_999, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var service = CreateService(repo, eventLog);

        var result = await service.EnchantAsync(
            new EnchantItemRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1, Luck = 0 }, zone, state, 10,
            CancellationToken.None);

        Assert.Equal(EnchantItemOutcome.Rejected, result.Outcome);
        Assert.Null(repo.LastAdjustMoneyAndReplaceContainer);
        Assert.Empty(eventLog.Enqueued);
    }

        [Fact]
    public async Task WingTarget_DeductsContributionPoints_NotMoney()
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [TargetItemId] = new(WorldDataTestRows.Item(TargetItemId) with { Sort = 6, CheckImprove = 2 }, []),
            [PaidMaterialId] = new(WorldDataTestRows.Item(PaidMaterialId), [])
        }.ToFrozenDictionary();

        var (_, _, zone, state, repo, eventLog) = SetUp();
        state.ContributionPoints = 50_000;
        state.LastEnchantAttemptUtc = DateTime.UtcNow - SimulationClock.LegacyTick - TimeSpan.FromMilliseconds(1);
        SeedInventory(zone, new ItemStack(TargetItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            new ItemStack(PaidMaterialId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var service = new EnchantItemService(repo, ZoneTestKit.EmptyWorldData(itemsById), eventLog,
            NullLogger<EnchantItemService>.Instance);

        var result = await RunToCompletionAsync(
            service.EnchantAsync(new EnchantItemRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1, Luck = 0 },
                zone, state, 10, CancellationToken.None), zone);

        Assert.Equal(EnchantItemOutcome.Applied, result.Outcome);
        Assert.Equal(0, result.ResultCode);
        Assert.Equal(10_000, result.Cost);
        Assert.NotNull(repo.LastAdjustMoneyAndReplaceContainer);
        Assert.Equal(0, repo.LastAdjustMoneyAndReplaceContainer!.Value.DeltaMoney);
        Assert.Equal(40_000, state.ContributionPoints);

        var logged = Assert.Single(eventLog.Enqueued);
        Assert.Null(logged.DeltaMoney);
    }

    [Fact]
    public async Task WingTarget_InsufficientContributionPoints_RejectedBeforeAnyMutation()
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [TargetItemId] = new(WorldDataTestRows.Item(TargetItemId) with { Sort = 6, CheckImprove = 2 }, []),
            [PaidMaterialId] = new(WorldDataTestRows.Item(PaidMaterialId), [])
        }.ToFrozenDictionary();

        var (_, _, zone, state, repo, eventLog) = SetUp();
        state.ContributionPoints = 1;
        state.LastEnchantAttemptUtc = DateTime.UtcNow - SimulationClock.LegacyTick - TimeSpan.FromMilliseconds(1);
        SeedInventory(zone, new ItemStack(TargetItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            new ItemStack(PaidMaterialId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var service = new EnchantItemService(repo, ZoneTestKit.EmptyWorldData(itemsById), eventLog,
            NullLogger<EnchantItemService>.Instance);

        var result = await service.EnchantAsync(
            new EnchantItemRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1, Luck = 0 }, zone, state, 10,
            CancellationToken.None);

        Assert.Equal(EnchantItemOutcome.Rejected, result.Outcome);
        Assert.Equal(1, state.ContributionPoints);
        Assert.Null(repo.LastAdjustMoneyAndReplaceContainer);
        Assert.Empty(eventLog.Enqueued);
    }

    [Fact]
    public async Task InsufficientFunds_TreatedAsRejected_NotLogged()
    {
        var (_, _, zone, state, repo, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(TargetItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            new ItemStack(PaidMaterialId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        repo.ThrowOnAdjustMoney = true;
        var service = CreateService(repo, eventLog);

        var result = await service.EnchantAsync(
            new EnchantItemRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1, Luck = 0 }, zone, state, 10,
            CancellationToken.None);

        Assert.Equal(EnchantItemOutcome.Rejected, result.Outcome);
        Assert.Empty(eventLog.Enqueued);
    }

        [Fact]
    public async Task ImproveItemValueCharge_Present_ConsumedAndMirroredOnSuccess()
    {
        var (_, _, zone, state, repo, eventLog) = SetUp();
        state.ImproveItemValue = 3;
        state.LastEnchantAttemptUtc = DateTime.UtcNow - SimulationClock.LegacyTick - TimeSpan.FromMilliseconds(1);
        SeedInventory(zone, new ItemStack(TargetItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 777),
            new ItemStack(GuaranteedSuccessMaterialId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var service = CreateService(repo, eventLog);

        var result = await RunToCompletionAsync(
            service.EnchantAsync(new EnchantItemRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1, Luck = 0 },
                zone, state, 10, CancellationToken.None), zone);

        Assert.Equal(EnchantItemOutcome.Applied, result.Outcome);
        Assert.Equal(2, state.ImproveItemValue);
    }

        [Fact]
    public async Task ImproveItemValueCharge_Absent_NotTouched()
    {
        var (_, _, zone, state, repo, eventLog) = SetUp();
        state.LastEnchantAttemptUtc = DateTime.UtcNow - SimulationClock.LegacyTick - TimeSpan.FromMilliseconds(1);
        SeedInventory(zone, new ItemStack(TargetItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 777),
            new ItemStack(GuaranteedSuccessMaterialId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var service = CreateService(repo, eventLog);

        var result = await RunToCompletionAsync(
            service.EnchantAsync(new EnchantItemRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1, Luck = 0 },
                zone, state, 10, CancellationToken.None), zone);

        Assert.Equal(EnchantItemOutcome.Applied, result.Outcome);
        Assert.Equal(0, state.ImproveItemValue);
    }

        [Fact]
    public async Task NoChangeMaterialFailedRoll_NonWingTarget_ReportsResultCodeEight_EnchantUnchanged()
    {
        const int noChangeMaterialId = 8101;
        const byte currentImprove = 32;

        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [TargetItemId] = new(WorldDataTestRows.Item(TargetItemId) with { Sort = 7, CheckImprove = 2 }, []),
            [noChangeMaterialId] = new(WorldDataTestRows.Item(noChangeMaterialId), [])
        }.ToFrozenDictionary();

        for (var attempt = 0; attempt < 40; attempt++)
        {
            var (session, _, zone, state, repo, eventLog) = SetUp();
            state.LastEnchantAttemptUtc = DateTime.UtcNow - SimulationClock.LegacyTick - TimeSpan.FromMilliseconds(1);
            SeedInventory(zone, new ItemStack(TargetItemId, 1, currentImprove, 0, 0, 0, 0, 0, 0, 0, 777),
                new ItemStack(noChangeMaterialId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
            var service = new EnchantItemService(repo, ZoneTestKit.EmptyWorldData(itemsById), eventLog,
                NullLogger<EnchantItemService>.Instance);

            var result = await RunToCompletionAsync(
                service.EnchantAsync(
                    new EnchantItemRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1, Luck = 0 }, zone, state,
                    10, CancellationToken.None), zone);

            Assert.Null(session.DisconnectReason);
            Assert.Equal(EnchantItemOutcome.Applied, result.Outcome);

            if (result.NewEnchant != currentImprove)
                continue;

            Assert.Equal(8, result.ResultCode);
            Assert.Equal(currentImprove, state.Inventory.GetSlot(0, 0)!.Value.Enchant);
            return;
        }

        Assert.Fail(
            "Failed (NoChange) roll never landed across 40 attempts (5% success chance per attempt, each independent).");
    }
}
