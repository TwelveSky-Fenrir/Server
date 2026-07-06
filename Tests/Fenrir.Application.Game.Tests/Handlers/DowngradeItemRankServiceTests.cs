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
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

/// <summary>
///     Drives the real <see cref="DowngradeItemRankService" /> (opcode 28) over a real <see cref="Zone" />;
///     ticks the zone while the service's own <c>PostInventoryCommandAndWaitAsync</c> await is pending, same
///     pattern as <c>UpgradeCapeServiceTests</c>. Primarily exercises the game.EventLog write-behind wiring --
///     roll-math fidelity is covered deterministically by <c>RankChangeResolverTests</c>.
/// </summary>
public class DowngradeItemRankServiceTests
{
    private const int TargetItemId = 1;
    private const int CandidateItemId = 2;
    private const int MaterialItemId = 1025;

    private static async Task<DowngradeItemRankResult> RunToCompletionAsync(
        ValueTask<DowngradeItemRankResult> pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("DowngradeItemRankService task never completed.");
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

    /// <summary>Level 55 Rare, Sort 9 -- matches RankChangeResolverTests' own downgrade fixtures (RareDownTiers[0]).</summary>
    private static ItemRowDto TargetRow()
    {
        return WorldDataTestRows.Item(TargetItemId) with
        {
            Type = RankChangeResolver.RareItemType, Sort = 9, Level = 55, CheckLowItem = 2, EquipInfo1 = 1
        };
    }

    /// <summary>Level 45 Rare, Sort 9 -- the only catalog entry that can satisfy FindReplacement for TargetRow.</summary>
    private static ItemRowDto CandidateRow()
    {
        return WorldDataTestRows.Item(CandidateItemId) with
        {
            Type = RankChangeResolver.RareItemType, Sort = 9, Level = 45, CheckLowItem = 2, EquipInfo1 = 1
        };
    }

    private static DowngradeItemRankService CreateService(FakeCharacterRepository characters,
        FakeEventLogQueue eventLog, bool includeCandidate)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [TargetItemId] = new(TargetRow(), []),
            [MaterialItemId] = new(WorldDataTestRows.Item(MaterialItemId), [])
        };
        if (includeCandidate)
            itemsById[CandidateItemId] = new ItemDefinition(CandidateRow(), []);

        return new DowngradeItemRankService(characters, ZoneTestKit.EmptyWorldData(itemsById.ToFrozenDictionary()),
            eventLog, NullLogger<DowngradeItemRankService>.Instance);
    }

    [Fact]
    public async Task ValidPreconditions_AlwaysDeductsMoneyAndLogsAttempt_RegardlessOfRandomOutcome()
    {
        // DowngradeItemRankService rolls via SystemRandomSource.Instance (no DI seam) -- success/failure branch
        // fidelity is covered deterministically by RankChangeResolverTests. This asserts only what's true on
        // BOTH outcomes: money is always deducted, material is always consumed, and exactly one EventLog row
        // is queued either way.
        var (session, _, zone, state, repo, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(TargetItemId, 1, 7, 3, 0, 0, 0, 0, 0, 0, 777),
            new ItemStack(MaterialItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var service = CreateService(repo, eventLog, includeCandidate: true);

        var result = await RunToCompletionAsync(
            service.DowngradeAsync(new DowngradeItemRankRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1, Luck = 0 },
                zone, state, 10, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(DowngradeItemRankOutcome.Applied, result.Outcome);
        Assert.NotNull(repo.LastAdjustMoneyAndReplaceContainer);
        Assert.Equal(-100_000, repo.LastAdjustMoneyAndReplaceContainer!.Value.DeltaMoney);

        var logged = Assert.Single(eventLog.Enqueued);
        Assert.Equal(28, logged.EventCode);
        Assert.Equal((byte)EventLogCategory.Enchant, logged.Category);
        Assert.Equal(10, logged.ActorCharacterId);
        Assert.Equal(-100_000, logged.DeltaMoney);
        Assert.Equal(TargetItemId, logged.ItemId);
        Assert.True(logged.Outcome is 0 or 1);
    }

    [Fact]
    public async Task TargetAtMinimumLevel_Rejected_NotLogged()
    {
        var (_, _, zone, state, repo, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(TargetItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1),
            new ItemStack(MaterialItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        var itemsById = new Dictionary<int, ItemDefinition>
        {
            // Level 45 is the Rare floor -- RankChangeResolver rejects downgrading at or below it.
            [TargetItemId] = new(WorldDataTestRows.Item(TargetItemId) with
            {
                Type = RankChangeResolver.RareItemType, Sort = 9, Level = 45, CheckLowItem = 2
            }, []),
            [MaterialItemId] = new(WorldDataTestRows.Item(MaterialItemId), [])
        }.ToFrozenDictionary();
        var service = new DowngradeItemRankService(repo, ZoneTestKit.EmptyWorldData(itemsById), eventLog,
            NullLogger<DowngradeItemRankService>.Instance);

        var result = await service.DowngradeAsync(
            new DowngradeItemRankRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1, Luck = 0 }, zone, state, 10,
            CancellationToken.None);

        Assert.Equal(DowngradeItemRankOutcome.Rejected, result.Outcome);
        Assert.Null(repo.LastAdjustMoneyAndReplaceContainer);
        Assert.Empty(eventLog.Enqueued);
    }

    [Fact]
    public async Task NoCandidateInCatalog_ReportsCostButNeverMutatesOrLogs()
    {
        var (_, _, zone, state, repo, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(TargetItemId, 1, 7, 3, 0, 0, 0, 0, 0, 0, 1),
            new ItemStack(MaterialItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var service = CreateService(repo, eventLog, includeCandidate: false);

        var result = await service.DowngradeAsync(
            new DowngradeItemRankRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1, Luck = 0 }, zone, state, 10,
            CancellationToken.None);

        Assert.Equal(DowngradeItemRankOutcome.NoCandidate, result.Outcome);
        Assert.Equal(100_000, result.Cost);
        Assert.Null(repo.LastAdjustMoneyAndReplaceContainer);
        Assert.Empty(eventLog.Enqueued);
    }

    [Fact]
    public async Task InsufficientFunds_TreatedAsRejected_NotLogged()
    {
        var (_, _, zone, state, repo, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(TargetItemId, 1, 7, 3, 0, 0, 0, 0, 0, 0, 1),
            new ItemStack(MaterialItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        repo.ThrowOnAdjustMoney = true;
        var service = CreateService(repo, eventLog, includeCandidate: true);

        var result = await service.DowngradeAsync(
            new DowngradeItemRankRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1, Luck = 0 }, zone, state, 10,
            CancellationToken.None);

        Assert.Equal(DowngradeItemRankOutcome.Rejected, result.Outcome);
        Assert.Empty(eventLog.Enqueued);
    }
}
