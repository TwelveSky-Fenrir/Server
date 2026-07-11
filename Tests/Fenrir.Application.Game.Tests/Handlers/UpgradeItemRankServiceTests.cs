using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Forge;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.ItemModification;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class UpgradeItemRankServiceTests
{
    private const int TargetItemId = 1;
    private const int CandidateItemId = 2;
    private const int MaterialItemId = 1025;

    private static async Task<UpgradeItemRankResult> RunToCompletionAsync(ValueTask<UpgradeItemRankResult> pending,
        Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("UpgradeItemRankService task never completed.");
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

        private static ItemRowDto TargetRow()
    {
        return WorldDataTestRows.Item(TargetItemId) with
        {
            Type = RankChangeResolver.RareItemType, Sort = 9, Level = 45, CheckHighItem = 2, EquipInfo1 = 1
        };
    }

        private static ItemRowDto CandidateRow()
    {
        return WorldDataTestRows.Item(CandidateItemId) with
        {
            Type = RankChangeResolver.RareItemType, Sort = 9, Level = 55, CheckHighItem = 2, EquipInfo1 = 1
        };
    }

    private static UpgradeItemRankService CreateService(FakeCharacterRepository characters,
        FakeEventLogQueue eventLog, bool includeCandidate)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [TargetItemId] = new(TargetRow(), []),
            [MaterialItemId] = new(WorldDataTestRows.Item(MaterialItemId), [])
        };
        if (includeCandidate)
            itemsById[CandidateItemId] = new ItemDefinition(CandidateRow(), []);

        return new UpgradeItemRankService(characters, ZoneTestKit.EmptyWorldData(itemsById.ToFrozenDictionary()),
            eventLog, new WarlordPityLockState(), NullLogger<UpgradeItemRankService>.Instance);
    }

    [Fact]
    public async Task ValidPreconditions_AlwaysDeductsMoneyAndLogsAttempt_RegardlessOfRandomOutcome()
    {
        var (session, _, zone, state, repo, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(TargetItemId, 1, 6, 2, 0, 0, 0, 0, 0, 0, 777),
            new ItemStack(MaterialItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var service = CreateService(repo, eventLog, true);

        var result = await RunToCompletionAsync(
            service.UpgradeAsync(new UpgradeItemRankRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1, Luck = 0 },
                zone,
                state, 10, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(UpgradeItemRankOutcome.Applied, result.Outcome);
        Assert.NotNull(repo.LastAdjustMoneyAndReplaceContainer);
        Assert.Equal(-100_000, repo.LastAdjustMoneyAndReplaceContainer!.Value.DeltaMoney);

        var logged = Assert.Single(eventLog.Enqueued);
        Assert.Equal(27, logged.EventCode);
        Assert.Equal((byte)EventLogCategory.Enchant, logged.Category);
        Assert.Equal(10, logged.ActorCharacterId);
        Assert.Equal(-100_000, logged.DeltaMoney);
        Assert.Equal(TargetItemId, logged.ItemId);
        Assert.True(logged.Outcome is 0 or 1);
    }

    [Fact]
    public async Task InvalidTargetType_Rejected_NotLogged()
    {
        var (_, _, zone, state, repo, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(TargetItemId, 1, 6, 2, 0, 0, 0, 0, 0, 0, 1),
            new ItemStack(MaterialItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [TargetItemId] = new(WorldDataTestRows.Item(TargetItemId) with { Sort = 9, CheckHighItem = 2 }, []),
            [MaterialItemId] = new(WorldDataTestRows.Item(MaterialItemId), [])
        }.ToFrozenDictionary();
        var service = new UpgradeItemRankService(repo, ZoneTestKit.EmptyWorldData(itemsById), eventLog,
            new WarlordPityLockState(), NullLogger<UpgradeItemRankService>.Instance);

        var result = await service.UpgradeAsync(
            new UpgradeItemRankRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1, Luck = 0 }, zone, state, 10,
            CancellationToken.None);

        Assert.Equal(UpgradeItemRankOutcome.Rejected, result.Outcome);
        Assert.Null(repo.LastAdjustMoneyAndReplaceContainer);
        Assert.Empty(eventLog.Enqueued);
    }

    [Fact]
    public async Task NoCandidateInCatalog_ReportsCostButNeverMutatesOrLogs()
    {
        var (_, _, zone, state, repo, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(TargetItemId, 1, 6, 2, 0, 0, 0, 0, 0, 0, 1),
            new ItemStack(MaterialItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var service = CreateService(repo, eventLog, false);

        var result = await service.UpgradeAsync(
            new UpgradeItemRankRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1, Luck = 0 }, zone, state, 10,
            CancellationToken.None);

        Assert.Equal(UpgradeItemRankOutcome.NoCandidate, result.Outcome);
        Assert.Equal(100_000, result.Cost);
        Assert.Null(repo.LastAdjustMoneyAndReplaceContainer);
        Assert.Empty(eventLog.Enqueued);
    }

    [Fact]
    public async Task InsufficientFunds_TreatedAsRejected_NotLogged()
    {
        var (_, _, zone, state, repo, eventLog) = SetUp();
        SeedInventory(zone, new ItemStack(TargetItemId, 1, 6, 2, 0, 0, 0, 0, 0, 0, 1),
            new ItemStack(MaterialItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        repo.ThrowOnAdjustMoney = true;
        var service = CreateService(repo, eventLog, true);

        var result = await service.UpgradeAsync(
            new UpgradeItemRankRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1, Luck = 0 }, zone, state, 10,
            CancellationToken.None);

        Assert.Equal(UpgradeItemRankOutcome.Rejected, result.Outcome);
        Assert.Empty(eventLog.Enqueued);
    }

    [Fact]
    public async Task WarlordRerollEligible_EliteSwap_LogsWarlordSwapNotice()
    {
        const byte eliteItemType = RankChangeResolver.EliteItemType;
        const byte amuletSort = 7;

        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [TargetItemId] = new(
                WorldDataTestRows.Item(TargetItemId) with
                {
                    Type = eliteItemType, Sort = amuletSort, Level = 145, MartialLevel = 11, CheckHighItem = 2,
                    EquipInfo1 = 1
                }, []),
            [CandidateItemId] = new(
                WorldDataTestRows.Item(CandidateItemId) with
                {
                    Type = eliteItemType, Sort = amuletSort, Level = 145, MartialLevel = 12, CheckHighItem = 2,
                    EquipInfo1 = 1
                }, []),
            [MaterialItemId] = new(WorldDataTestRows.Item(MaterialItemId), [])
        }.ToFrozenDictionary();

        for (var attempt = 0; attempt < 40; attempt++)
        {
            var (_, _, zone, state, repo, eventLog) = SetUp();
            SeedInventory(zone, new ItemStack(TargetItemId, 1, 6, 2, 0, 0, 0, 0, 0, 0, 777),
                new ItemStack(MaterialItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
            state.Stats = new EffectiveStats(0, 0, 0, 0, 0, 0, 0, 0, 300_000, 0, 0);

            var logger = new CapturingLogger<UpgradeItemRankService>();
            var service = new UpgradeItemRankService(repo, ZoneTestKit.EmptyWorldData(itemsById), eventLog,
                new WarlordPityLockState(), logger);

            var result = await RunToCompletionAsync(
                service.UpgradeAsync(
                    new UpgradeItemRankRequest { Page1 = 0, Index1 = 0, Page2 = 0, Index2 = 1, Luck = 0 }, zone,
                    state, 10, CancellationToken.None), zone);

            Assert.Equal(UpgradeItemRankOutcome.Applied, result.Outcome);
            Assert.True(result.Succeeded);

            var noticeEntries = logger.Entries.Where(e => e.Message.Contains("Warlord-swap notice")).ToList();
            if (noticeEntries.Count == 0)
                continue;

            Assert.Equal(LogLevel.Information, noticeEntries[0].Level);
            Assert.Contains("tribe 1", noticeEntries[0].Message);
            Assert.Contains("Hero", noticeEntries[0].Message);
            Assert.Contains(result.Value[0].ToString(), noticeEntries[0].Message);
            return;
        }

        Assert.Fail("Warlord-swap notice never logged across 40 attempts (25% miss chance per attempt).");
    }
}
