using Fenrir.Application.Game.Abstractions.Quests;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.Quests;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Quests;

/// <summary>
///     Drives the real <see cref="QuestProgressService" /> (opcode 36, CZ_PROCESS_QUEST_SEND, tSort 2
///     "mission completed") over a real <see cref="Zone" />; ticks the zone while the service's own
///     <c>PostQuestCommandAndWaitAsync</c> await is pending, same pattern as
///     <c>UseInventoryItemServiceTests</c>. Covers only the game.EventLog (Category=ItemCreate) wiring added
///     for the quest-completion reward grant -- item deposit and/or nonzero money/experience/
///     contribution-point/teacher-point reward -- every other CompleteAsync/QuestStateMachine rule is
///     covered by <c>QuestStateMachineTests</c> and is not duplicated here.
/// </summary>
public class QuestProgressServiceTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const byte Tribe = 1; // matches ZoneTestKit.EnterData's own default tribe
    private const byte Category = Tribe + 1;
    private const int RewardItemId = 9500;
    private const int QuestId = 1;

    private static async Task<QuestActionResult> RunToCompletionAsync(ValueTask<QuestActionResult> pending,
        Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("QuestProgressService task never completed.");
        }

        return await task;
    }

    /// <summary>A qSort-1 (kill monster) quest at Step 3 -- Solution2 (1) is the required kill count.</summary>
    private static QuestRowDto KillQuestAtStep3()
    {
        return WorldDataTestRows.Quest(QuestId) with
        {
            Category = Category, Step = 3, Sort = 1, Solution1 = 5001, Solution2 = 1
        };
    }

    private static (ZoneClientSession Session, Zone Zone, PlayerRuntimeState State, FakeCharacterRepository
        Characters, FakeEventLogRepository EventLog, QuestProgressService Service) SetUp(
            QuestRowDto quest, QuestRewardRowDto[] rewards, int killCounter)
    {
        var rows = WorldDataTestRows.MinimalRows() with
        {
            Items = [WorldDataTestRows.Item(1), WorldDataTestRows.Item(RewardItemId) with { Sort = 1 }],
            Quests = [quest],
            QuestRewards = rewards,
            QuestSpeeches = []
        };
        var (cache, _) = WorldDataCacheBuilder.Build(rows);
        var catalog = new QuestCatalog(cache);

        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, CharacterId);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, 1, tribe: Tribe)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(CharacterId, out var state));

        state!.QuestStepPermanent = quest.Step;
        state.QuestActiveFlag = 1;
        state.QuestSort = quest.Sort;
        state.QuestTargetPhase = quest.Solution1 ?? 0;
        state.QuestKillCounter = killCounter;

        var characters = new FakeCharacterRepository();
        var eventLog = new FakeEventLogRepository();
        var service = new QuestProgressService(characters, eventLog, cache, catalog,
            NullLogger<QuestProgressService>.Instance);

        return (session, zone, state, characters, eventLog, service);
    }

    [Fact]
    public async Task Complete_WithItemReward_DepositsTheItem_AndLogsAnItemCreateAuditRow()
    {
        var reward = new QuestRewardRowDto(QuestId, 0, 6, RewardItemId, null);
        var (_, zone, state, characters, eventLog, service) = SetUp(KillQuestAtStep3(), [reward], 1);
        var packet = new QuestProgressRequest { Sort = 2, Page1 = 0, Index1 = 0, XPost = 0, YPost = 0 };

        var result = await RunToCompletionAsync(
            service.CompleteAsync(packet, state, zone, CharacterId, AccountId, CancellationToken.None), zone);

        Assert.True(result.Success);
        Assert.Single(characters.QuestTransitions);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(EventLogCategory.ItemCreate, logged.Category);
        Assert.Equal(AccountId, logged.ActorAccountId);
        Assert.Equal(CharacterId, logged.ActorCharacterId);
        Assert.Null(logged.TargetAccountId);
        Assert.Null(logged.TargetCharacterId);
        Assert.Equal(RewardItemId, logged.ItemId);
        Assert.Equal(1, logged.Quantity); // Sort=1 (non-equipment) -> quantity 1, see QuestStateMachine.Complete
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Null(logged.DeltaMoney);
        Assert.Null(logged.Payload); // no nonzero money/XP/CP/teacher-point reward configured alongside the item
    }

    [Fact]
    public async Task Complete_MoneyOnlyReward_NoItemGranted_LogsAnAuditRow_WithDeltaMoney_ButNoItemId()
    {
        var moneyReward = new QuestRewardRowDto(QuestId, 0, 2, null, 1000);
        var (_, zone, state, _, eventLog, service) = SetUp(KillQuestAtStep3(), [moneyReward], 1);
        var packet = new QuestProgressRequest { Sort = 2, Page1 = 0, Index1 = 0, XPost = 0, YPost = 0 };

        var result = await RunToCompletionAsync(
            service.CompleteAsync(packet, state, zone, CharacterId, AccountId, CancellationToken.None), zone);

        Assert.True(result.Success);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(EventLogCategory.ItemCreate, logged.Category);
        Assert.Equal(AccountId, logged.ActorAccountId);
        Assert.Equal(CharacterId, logged.ActorCharacterId);
        Assert.Equal(1000, logged.DeltaMoney);
        Assert.Null(logged.ItemId);
        Assert.Null(logged.Quantity);
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Equal("ExperienceReward=0;ContributionPointsReward=0;TeacherPointReward=0", logged.Payload);
    }

    [Fact]
    public async Task Complete_ExperienceContributionAndTeacherPointReward_NoItemOrMoney_LogsAuditRow_WithPayload()
    {
        var rewards = new[]
        {
            new QuestRewardRowDto(QuestId, 0, 4, null, 200), // RewardType 4 = experience
            new QuestRewardRowDto(QuestId, 1, 3, null, 50), // RewardType 3 = contribution points
            new QuestRewardRowDto(QuestId, 2, 5, null, 5) // RewardType 5 = teacher points
        };
        var (_, zone, state, _, eventLog, service) = SetUp(KillQuestAtStep3(), rewards, 1);
        var packet = new QuestProgressRequest { Sort = 2, Page1 = 0, Index1 = 0, XPost = 0, YPost = 0 };

        var result = await RunToCompletionAsync(
            service.CompleteAsync(packet, state, zone, CharacterId, AccountId, CancellationToken.None), zone);

        Assert.True(result.Success);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(EventLogCategory.ItemCreate, logged.Category);
        Assert.Null(logged.DeltaMoney);
        Assert.Null(logged.ItemId);
        Assert.Null(logged.Quantity);
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Equal("ExperienceReward=200;ContributionPointsReward=50;TeacherPointReward=5", logged.Payload);
    }

    [Fact]
    public async Task Complete_ItemAndMoneyReward_LogsOneAuditRow_WithBothItemAndDeltaMoney()
    {
        var rewards = new[]
        {
            new QuestRewardRowDto(QuestId, 0, 6, RewardItemId, null),
            new QuestRewardRowDto(QuestId, 1, 2, null, 500)
        };
        var (_, zone, state, _, eventLog, service) = SetUp(KillQuestAtStep3(), rewards, 1);
        var packet = new QuestProgressRequest { Sort = 2, Page1 = 0, Index1 = 0, XPost = 0, YPost = 0 };

        var result = await RunToCompletionAsync(
            service.CompleteAsync(packet, state, zone, CharacterId, AccountId, CancellationToken.None), zone);

        Assert.True(result.Success);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(RewardItemId, logged.ItemId);
        Assert.Equal(1, logged.Quantity);
        Assert.Equal(500, logged.DeltaMoney);
        Assert.Equal("ExperienceReward=0;ContributionPointsReward=0;TeacherPointReward=0", logged.Payload);
    }

    [Fact]
    public async Task Complete_RewardItemConfigured_ButNoDepositSlotOffered_SkipsTheDeposit_AndLogsNothing()
    {
        // Page1 == -1 signals "no deposit slot" -- CompleteAsync skips the item grant entirely even though a
        // reward item is configured, so no item is ever created and nothing is logged.
        var reward = new QuestRewardRowDto(QuestId, 0, 6, RewardItemId, null);
        var (_, zone, state, _, eventLog, service) = SetUp(KillQuestAtStep3(), [reward], 1);
        var packet = new QuestProgressRequest { Sort = 2, Page1 = -1, Index1 = -1, XPost = 0, YPost = 0 };

        var result = await RunToCompletionAsync(
            service.CompleteAsync(packet, state, zone, CharacterId, AccountId, CancellationToken.None), zone);

        Assert.True(result.Success);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task Complete_EndConditionNotMet_Fails_AndLogsNothing()
    {
        var reward = new QuestRewardRowDto(QuestId, 0, 6, RewardItemId, null);
        // killCounter (0) below Solution2 (1) -> end condition not met
        var (_, zone, state, characters, eventLog, service) = SetUp(KillQuestAtStep3(), [reward], 0);
        var packet = new QuestProgressRequest { Sort = 2, Page1 = 0, Index1 = 0, XPost = 0, YPost = 0 };

        var result = await service.CompleteAsync(packet, state, zone, CharacterId, AccountId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(characters.QuestTransitions);
        Assert.Empty(eventLog.LoggedEvents);
    }
}
