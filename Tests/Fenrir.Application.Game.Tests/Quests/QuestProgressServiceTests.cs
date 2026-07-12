using Fenrir.Application.Game.Abstractions.Quests;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.Quests;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Quests;

public class QuestProgressServiceTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const byte Tribe = 1;
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
        Assert.Equal(1, logged.Quantity);
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Null(logged.DeltaMoney);
        Assert.Null(logged.Payload);
    }

    [Fact]
    public async Task Complete_MoneyOnlyReward_DepositTargetDeclared_LogsAnAuditRow_WithDeltaMoney_AndZeroItemId()
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
        Assert.Equal(0, logged.ItemId);
        Assert.Equal(0, logged.Quantity);
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Equal("ExperienceReward=0;KillOtherTribeCountReward=0;TeacherPointReward=0", logged.Payload);
        Assert.Equal(0, state.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0)!.Value.ItemId);
    }

    [Fact]
    public async Task Complete_ExperienceKillOtherTribeCountAndTeacherPointReward_DepositTargetDeclared_LogsAuditRow_WithZeroItemIdAndPayload()
    {
        var rewards = new[]
        {
            new QuestRewardRowDto(QuestId, 0, 4, null, 200),
            new QuestRewardRowDto(QuestId, 1, 3, null, 50),
            new QuestRewardRowDto(QuestId, 2, 5, null, 5)
        };
        var (_, zone, state, _, eventLog, service) = SetUp(KillQuestAtStep3(), rewards, 1);
        var packet = new QuestProgressRequest { Sort = 2, Page1 = 0, Index1 = 0, XPost = 0, YPost = 0 };

        var result = await RunToCompletionAsync(
            service.CompleteAsync(packet, state, zone, CharacterId, AccountId, CancellationToken.None), zone);

        Assert.True(result.Success);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(EventLogCategory.ItemCreate, logged.Category);
        Assert.Null(logged.DeltaMoney);
        Assert.Equal(0, logged.ItemId);
        Assert.Equal(0, logged.Quantity);
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Equal("ExperienceReward=200;KillOtherTribeCountReward=50;TeacherPointReward=5", logged.Payload);
        Assert.Equal(50, state.MissionKillOtherTribe);
        Assert.Equal(0, state.ContributionPoints);
    }

    [Fact]
    public async Task Complete_NoDepositTargetDeclared_MoneyOnlyReward_LogsAnAuditRow_WithNoItemId()
    {
        var moneyReward = new QuestRewardRowDto(QuestId, 0, 2, null, 1000);
        var (_, zone, state, _, eventLog, service) = SetUp(KillQuestAtStep3(), [moneyReward], 1);
        var packet = new QuestProgressRequest { Sort = 2, Page1 = -1, Index1 = -1, XPost = 0, YPost = 0 };

        var result = await RunToCompletionAsync(
            service.CompleteAsync(packet, state, zone, CharacterId, AccountId, CancellationToken.None), zone);

        Assert.True(result.Success);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(1000, logged.DeltaMoney);
        Assert.Null(logged.ItemId);
        Assert.Null(logged.Quantity);
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
        Assert.Equal("ExperienceReward=0;KillOtherTribeCountReward=0;TeacherPointReward=0", logged.Payload);
    }

    [Fact]
    public async Task Complete_RewardItemConfigured_ButNoDepositSlotOffered_SkipsTheDeposit_AndLogsNothing()
    {
        var reward = new QuestRewardRowDto(QuestId, 0, 6, RewardItemId, null);
        var (_, zone, state, _, eventLog, service) = SetUp(KillQuestAtStep3(), [reward], 1);
        var packet = new QuestProgressRequest { Sort = 2, Page1 = -1, Index1 = -1, XPost = 0, YPost = 0 };

        var result = await RunToCompletionAsync(
            service.CompleteAsync(packet, state, zone, CharacterId, AccountId, CancellationToken.None), zone);

        Assert.True(result.Success);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task Complete_RewardItem_ToExpiredSecondInventoryPage_Fails_NoStateChange()
    {
        var reward = new QuestRewardRowDto(QuestId, 0, 6, RewardItemId, null);
        var (_, zone, state, characters, eventLog, service) = SetUp(KillQuestAtStep3(), [reward], 1);
        var packet = new QuestProgressRequest
        {
            Sort = 2, Page1 = ContainerMatrix.InventoryPage1, Index1 = 0, XPost = 0, YPost = 0
        };

        var result = await service.CompleteAsync(packet, state, zone, CharacterId, AccountId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(characters.QuestTransitions);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task Complete_RewardItem_ToUnexpiredSecondInventoryPage_Succeeds()
    {
        var reward = new QuestRewardRowDto(QuestId, 0, 6, RewardItemId, null);
        var (_, zone, state, _, eventLog, service) = SetUp(KillQuestAtStep3(), [reward], 1);
        state.InventoryDate = 99991231;
        var packet = new QuestProgressRequest
        {
            Sort = 2, Page1 = ContainerMatrix.InventoryPage1, Index1 = 0, XPost = 0, YPost = 0
        };

        var result = await RunToCompletionAsync(
            service.CompleteAsync(packet, state, zone, CharacterId, AccountId, CancellationToken.None), zone);

        Assert.True(result.Success);
        Assert.Single(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task Complete_EndConditionNotMet_Fails_AndLogsNothing()
    {
        var reward = new QuestRewardRowDto(QuestId, 0, 6, RewardItemId, null);
        var (_, zone, state, characters, eventLog, service) = SetUp(KillQuestAtStep3(), [reward], 0);
        var packet = new QuestProgressRequest { Sort = 2, Page1 = 0, Index1 = 0, XPost = 0, YPost = 0 };

        var result = await service.CompleteAsync(packet, state, zone, CharacterId, AccountId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(characters.QuestTransitions);
        Assert.Empty(eventLog.LoggedEvents);
    }
}
