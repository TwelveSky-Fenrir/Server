using Fenrir.Application.Game.Abstractions.Quests;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.Quests;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Quests;

public class QuestProgressProximityGateTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const byte Tribe = 1;
    private const byte Category = Tribe + 1;
    private const int QuestId = 1;

    private const int AnchorNpc = 777;

    private static async Task<QuestActionResult> RunToCompletionAsync(ValueTask<QuestActionResult> pending, Zone zone)
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

    private static (Zone Zone, PlayerRuntimeState State, FakeCharacterRepository Characters,
        FakeEventLogRepository EventLog, QuestProgressService Service) SetUp(params QuestRowDto[] quests)
    {
        var rows = WorldDataTestRows.MinimalRows() with
        {
            Quests = quests,
            QuestRewards = [],
            QuestSpeeches = [],
            ZoneNpcSpawns = [new ZoneNpcSpawnRowDto(1, 0, AnchorNpc, 0f, 0f, 0f, 0f)]
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

        var characters = new FakeCharacterRepository();
        var eventLog = new FakeEventLogRepository();
        var service = new QuestProgressService(characters, eventLog, cache, catalog,
            NullLogger<QuestProgressService>.Instance);

        return (zone, state!, characters, eventLog, service);
    }

    private static void PlacePlayer(PlayerRuntimeState state, float x, float y, float z)
    {
        state.PosX = x;
        state.PosY = y;
        state.PosZ = z;
    }

    private static void MarkKillQuestEndConditionMet(PlayerRuntimeState state, QuestRowDto quest)
    {
        state.QuestStepPermanent = quest.Step;
        state.QuestActiveFlag = 1;
        state.QuestSort = quest.Sort;
        state.QuestTargetPhase = quest.Solution1 ?? 0;
        state.QuestKillCounter = 1;
    }

    private static void MarkIdleAtStep2(PlayerRuntimeState state)
    {
        state.QuestStepPermanent = 2;
        state.QuestActiveFlag = 0;
        state.QuestSort = 0;
        state.QuestTargetPhase = 0;
        state.QuestKillCounter = 0;
    }

    private static QuestRowDto KillQuestAtStep3(int endNpcNumber)
    {
        return WorldDataTestRows.Quest(QuestId) with
        {
            Category = Category, Step = 3, Sort = 1, Level = 1, Solution1 = 5001, Solution2 = 1,
            StartNPCNumber = AnchorNpc, EndNPCNumber = endNpcNumber
        };
    }


    [Fact]
    public async Task Complete_NearEndNpc_Succeeds()
    {
        var quest = KillQuestAtStep3(AnchorNpc);
        var (zone, state, characters, _, service) = SetUp(quest);
        MarkKillQuestEndConditionMet(state, quest);
        PlacePlayer(state, 0, 0, 30);
        var packet = new QuestProgressRequest { Sort = 2, Page1 = -1, Index1 = -1, XPost = 0, YPost = 0 };

        var result = await RunToCompletionAsync(
            service.CompleteAsync(packet, state, zone, CharacterId, AccountId, CancellationToken.None), zone);

        Assert.True(result.Success);
        Assert.Single(characters.QuestTransitions);
    }

    [Fact]
    public async Task Complete_FarFromEndNpc_Rejected_NoStateMutation()
    {
        var quest = KillQuestAtStep3(AnchorNpc);
        var (zone, state, characters, eventLog, service) = SetUp(quest);
        MarkKillQuestEndConditionMet(state, quest);
        PlacePlayer(state, 0, 0, 300);

        var packet = new QuestProgressRequest { Sort = 2, Page1 = -1, Index1 = -1, XPost = 0, YPost = 0 };

        var result = await service.CompleteAsync(packet, state, zone, CharacterId, AccountId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(characters.QuestTransitions);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task Complete_EndNpcNotPlacedOnMap_FailsOpen_Succeeds()
    {
        var quest = KillQuestAtStep3(888);
        var (zone, state, characters, _, service) = SetUp(quest);
        MarkKillQuestEndConditionMet(state, quest);
        PlacePlayer(state, 0, 0, 300);

        var packet = new QuestProgressRequest { Sort = 2, Page1 = -1, Index1 = -1, XPost = 0, YPost = 0 };

        var result = await RunToCompletionAsync(
            service.CompleteAsync(packet, state, zone, CharacterId, AccountId, CancellationToken.None), zone);

        Assert.True(result.Success);
        Assert.Single(characters.QuestTransitions);
    }

    [Fact]
    public async Task Complete_NoEndNpcConfigured_FailsOpen_Succeeds()
    {
        var quest = KillQuestAtStep3(0);
        var (zone, state, characters, _, service) = SetUp(quest);
        MarkKillQuestEndConditionMet(state, quest);
        PlacePlayer(state, 0, 0, 300);

        var packet = new QuestProgressRequest { Sort = 2, Page1 = -1, Index1 = -1, XPost = 0, YPost = 0 };

        var result = await RunToCompletionAsync(
            service.CompleteAsync(packet, state, zone, CharacterId, AccountId, CancellationToken.None), zone);

        Assert.True(result.Success);
        Assert.Single(characters.QuestTransitions);
    }


    [Fact]
    public async Task Accept_NearStartNpc_Succeeds()
    {
        var nextStep = KillQuestAtStep3(AnchorNpc);
        var (zone, state, characters, _, service) = SetUp(nextStep);
        MarkIdleAtStep2(state);
        PlacePlayer(state, 30, 0, 0);
        var packet = new QuestProgressRequest { Sort = 1, Page1 = 0, Index1 = 0, XPost = 0, YPost = 0 };

        var result = await RunToCompletionAsync(
            service.AcceptAsync(packet, state, zone, CharacterId, CancellationToken.None), zone);

        Assert.True(result.Success);
        Assert.Single(characters.QuestTransitions);
    }

    [Fact]
    public async Task Accept_FarFromStartNpc_Rejected_NoStateMutation()
    {
        var nextStep = KillQuestAtStep3(AnchorNpc);
        var (zone, state, characters, _, service) = SetUp(nextStep);
        MarkIdleAtStep2(state);
        PlacePlayer(state, 300, 0, 0);

        var packet = new QuestProgressRequest { Sort = 1, Page1 = 0, Index1 = 0, XPost = 0, YPost = 0 };

        var result = await service.AcceptAsync(packet, state, zone, CharacterId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(characters.QuestTransitions);
    }
}
