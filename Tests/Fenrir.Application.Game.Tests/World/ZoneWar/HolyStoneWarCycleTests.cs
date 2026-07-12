using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class HolyStoneWarCycleTests
{
    private const short StoneMapId = 38;
    private static readonly HolyStoneWarSite Site = new(StoneMapId, 100f, 100f, 10f, 200f);

    private static ZoneRegistry CreateRegistry(params short[] maps)
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options),
            new MovementRules(Options.Create(options)), new DirtyTracker<int>(), NullLogger<Zone>.Instance,
            ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize(maps);
        return registry;
    }

    private static WorldStateService CreateWorldState()
    {
        var service = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return service;
    }

    private static HolyStoneWarCycle CreateCycle(WorldStateService worldState, ZoneRegistry registry,
        FakeRewardGateway? gateway = null, bool testMode = false)
    {
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        return new HolyStoneWarCycle(worldState, broadcaster, registry, gateway ?? new FakeRewardGateway(),
            NullLogger<HolyStoneWarCycle>.Instance, Site, new ScriptedRandomSource(0), testMode);
    }

    private static void AdvanceThroughOpeningCountdown(HolyStoneWarCycle cycle)
    {
        for (var i = 0; i < HolyStoneWarCycle.OpeningCountdownMinutes + 1; i++)
            cycle.Tick(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void StartsInCooldown_WithFullCooldownArmed()
    {
        var cycle = CreateCycle(CreateWorldState(), CreateRegistry(StoneMapId));

        Assert.Equal(HolyStoneWarPhase.Cooldown, cycle.Phase);
        Assert.Equal(HolyStoneWarCycle.NormalCooldown, cycle.CooldownRemaining);
    }

    [Fact]
    public void TestMode_StartsWithOneHourCooldown_InsteadOfThree()
    {
        var cycle = CreateCycle(CreateWorldState(), CreateRegistry(StoneMapId), testMode: true);

        Assert.Equal(HolyStoneWarCycle.TestModeCooldown, cycle.CooldownRemaining);
    }

    [Fact]
    public void Cooldown_WhileTribeSymbolBattleOpen_NeverProgresses()
    {
        var worldState = CreateWorldState();
        worldState.StartTribeSymbolBattle();
        var cycle = CreateCycle(worldState, CreateRegistry(StoneMapId));

        cycle.Tick(HolyStoneWarCycle.NormalCooldown + TimeSpan.FromHours(1));

        Assert.Equal(HolyStoneWarPhase.Cooldown, cycle.Phase);
        Assert.Equal(HolyStoneWarCycle.NormalCooldown, cycle.CooldownRemaining);
    }

    [Fact]
    public void Cooldown_ElapsesWhileBattleClosed_TransitionsToOpeningCountdown()
    {
        var cycle = CreateCycle(CreateWorldState(), CreateRegistry(StoneMapId));

        cycle.Tick(HolyStoneWarCycle.NormalCooldown);

        Assert.Equal(HolyStoneWarPhase.OpeningCountdown, cycle.Phase);
    }

    [Fact]
    public void OpeningCountdown_ElevenMinutesLater_TransitionsToContest()
    {
        var cycle = CreateCycle(CreateWorldState(), CreateRegistry(StoneMapId));
        cycle.Tick(HolyStoneWarCycle.NormalCooldown);

        AdvanceThroughOpeningCountdown(cycle);

        Assert.Equal(HolyStoneWarPhase.Contest, cycle.Phase);
    }

    [Fact]
    public void Contest_FindsEligibleChallenger_WithinCaptureRadius_TransitionsToChallengePending()
    {
        var worldState = CreateWorldState();
        var registry = CreateRegistry(StoneMapId);
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry[StoneMapId].Post(ZoneCommand.Enter(10,
            ZoneTestKit.EnterData(session, StoneMapId, tribe: 1, posX: Site.StoneX, posZ: Site.StoneZ)));
        registry[StoneMapId].Tick(TimeSpan.FromMilliseconds(50));

        var cycle = CreateCycle(worldState, registry);
        cycle.Tick(HolyStoneWarCycle.NormalCooldown);
        AdvanceThroughOpeningCountdown(cycle);
        Assert.Equal(HolyStoneWarPhase.Contest, cycle.Phase);

        cycle.Tick(TimeSpan.Zero);

        Assert.Equal(HolyStoneWarPhase.ChallengePending, cycle.Phase);
        Assert.Equal(10, cycle.PendingCandidateCharacterId);
    }

    [Fact]
    public void Contest_SkipsCharacterAlreadyMatchingHolderTribe()
    {
        var worldState = CreateWorldState();
        worldState.SetZone038Winner(1);
        var registry = CreateRegistry(StoneMapId);
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry[StoneMapId].Post(ZoneCommand.Enter(10,
            ZoneTestKit.EnterData(session, StoneMapId, tribe: 1, posX: Site.StoneX, posZ: Site.StoneZ)));
        registry[StoneMapId].Tick(TimeSpan.FromMilliseconds(50));

        var cycle = CreateCycle(worldState, registry);
        cycle.Tick(HolyStoneWarCycle.NormalCooldown);
        AdvanceThroughOpeningCountdown(cycle);

        cycle.Tick(TimeSpan.Zero);

        Assert.Equal(HolyStoneWarPhase.Contest, cycle.Phase);
        Assert.Null(cycle.PendingCandidateCharacterId);
    }

    [Fact]
    public void Contest_SkipsCharacterOutsideCaptureRadius()
    {
        var worldState = CreateWorldState();
        var registry = CreateRegistry(StoneMapId);
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry[StoneMapId].Post(ZoneCommand.Enter(10,
            ZoneTestKit.EnterData(session, StoneMapId, tribe: 1, posX: Site.StoneX + 1000, posZ: Site.StoneZ)));
        registry[StoneMapId].Tick(TimeSpan.FromMilliseconds(50));

        var cycle = CreateCycle(worldState, registry);
        cycle.Tick(HolyStoneWarCycle.NormalCooldown);
        AdvanceThroughOpeningCountdown(cycle);

        cycle.Tick(TimeSpan.Zero);

        Assert.Equal(HolyStoneWarPhase.Contest, cycle.Phase);
        Assert.Null(cycle.PendingCandidateCharacterId);
    }

    [Fact]
    public void ChallengePending_SurvivingFiveMinutes_ResolvesCapture_AndBroadcastsSort38()
    {
        var worldState = CreateWorldState();
        var registry = CreateRegistry(StoneMapId);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[StoneMapId].Post(ZoneCommand.Enter(10,
            ZoneTestKit.EnterData(session, StoneMapId, tribe: 1, posX: Site.StoneX, posZ: Site.StoneZ)));
        registry[StoneMapId].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var gateway = new FakeRewardGateway();
        var cycle = CreateCycle(worldState, registry, gateway);
        cycle.Tick(HolyStoneWarCycle.NormalCooldown);
        AdvanceThroughOpeningCountdown(cycle);
        cycle.Tick(TimeSpan.Zero);
        Assert.Equal(HolyStoneWarPhase.ChallengePending, cycle.Phase);

        for (var i = 0; i < HolyStoneWarCycle.ChallengeCountdownMinutes; i++)
            cycle.Tick(TimeSpan.FromMinutes(1));

        Assert.Equal(HolyStoneWarPhase.Cooldown, cycle.Phase);
        Assert.Equal((byte?)1, worldState.World.Zone038WinTribe);
        Assert.Equal(HolyStoneWarCycle.NormalCooldown, cycle.CooldownRemaining);
        Assert.Contains(10, gateway.CaptureRewardedCharacterIds);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(38, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1)));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1)[4..]));
    }

    [Fact]
    public void ChallengePending_CandidateLeavesTheZone_CancelsChallenge_ReturnsToContest()
    {
        var worldState = CreateWorldState();
        var registry = CreateRegistry(StoneMapId);
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry[StoneMapId].Post(ZoneCommand.Enter(10,
            ZoneTestKit.EnterData(session, StoneMapId, tribe: 1, posX: Site.StoneX, posZ: Site.StoneZ)));
        registry[StoneMapId].Tick(TimeSpan.FromMilliseconds(50));

        var cycle = CreateCycle(worldState, registry);
        cycle.Tick(HolyStoneWarCycle.NormalCooldown);
        AdvanceThroughOpeningCountdown(cycle);
        cycle.Tick(TimeSpan.Zero);
        Assert.Equal(HolyStoneWarPhase.ChallengePending, cycle.Phase);

        registry[StoneMapId].Post(ZoneCommand.Leave(10));
        registry[StoneMapId].Tick(TimeSpan.FromMilliseconds(50));

        cycle.Tick(TimeSpan.FromSeconds(1));

        Assert.Equal(HolyStoneWarPhase.Contest, cycle.Phase);
        Assert.Null(cycle.PendingCandidateCharacterId);
    }

    [Fact]
    public void ChallengePending_CandidateMovesOutOfRadius_CancelsChallenge()
    {
        var worldState = CreateWorldState();
        var registry = CreateRegistry(StoneMapId);
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry[StoneMapId].Post(ZoneCommand.Enter(10,
            ZoneTestKit.EnterData(session, StoneMapId, tribe: 1, posX: Site.StoneX, posZ: Site.StoneZ)));
        registry[StoneMapId].Tick(TimeSpan.FromMilliseconds(50));

        var cycle = CreateCycle(worldState, registry);
        cycle.Tick(HolyStoneWarCycle.NormalCooldown);
        AdvanceThroughOpeningCountdown(cycle);
        cycle.Tick(TimeSpan.Zero);
        Assert.Equal(HolyStoneWarPhase.ChallengePending, cycle.Phase);

        Assert.True(registry[StoneMapId].TryGetPlayer(10, out var player));
        player!.PosX = Site.StoneX + 1000;

        cycle.Tick(TimeSpan.FromSeconds(1));

        Assert.Equal(HolyStoneWarPhase.Contest, cycle.Phase);
    }

    [Fact]
    public void ChallengePending_CandidateDies_CancelsChallenge()
    {
        var worldState = CreateWorldState();
        var registry = CreateRegistry(StoneMapId);
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry[StoneMapId].Post(ZoneCommand.Enter(10,
            ZoneTestKit.EnterData(session, StoneMapId, tribe: 1, posX: Site.StoneX, posZ: Site.StoneZ)));
        registry[StoneMapId].Tick(TimeSpan.FromMilliseconds(50));

        var cycle = CreateCycle(worldState, registry);
        cycle.Tick(HolyStoneWarCycle.NormalCooldown);
        AdvanceThroughOpeningCountdown(cycle);
        cycle.Tick(TimeSpan.Zero);
        Assert.Equal(HolyStoneWarPhase.ChallengePending, cycle.Phase);

        registry[StoneMapId].ApplyDeath(10);

        cycle.Tick(TimeSpan.FromSeconds(1));

        Assert.Equal(HolyStoneWarPhase.Contest, cycle.Phase);
    }

    [Fact]
    public void
        ResolveCapture_GrantsParticipationReward_ToRebornTribemateWithinRadius_ButNotToCapturerOrNonRebornOrOutOfRadius()
    {
        var worldState = CreateWorldState();
        var registry = CreateRegistry(StoneMapId);

        var (capturerSession, _) = ZoneTestKit.CreateSession(1);
        registry[StoneMapId].Post(ZoneCommand.Enter(10,
            ZoneTestKit.EnterData(capturerSession, StoneMapId, tribe: 1, posX: Site.StoneX, posZ: Site.StoneZ)));

        var (rebornSession, _) = ZoneTestKit.CreateSession(2);
        registry[StoneMapId].Post(ZoneCommand.Enter(11,
            ZoneTestKit.EnterData(rebornSession, StoneMapId, tribe: 1, posX: Site.StoneX + 50, posZ: Site.StoneZ)));

        var (neverRebornSession, _) = ZoneTestKit.CreateSession(3);
        registry[StoneMapId].Post(ZoneCommand.Enter(12,
            ZoneTestKit.EnterData(neverRebornSession, StoneMapId, tribe: 1, posX: Site.StoneX + 50,
                posZ: Site.StoneZ)));

        var (farAwaySession, _) = ZoneTestKit.CreateSession(4);
        registry[StoneMapId].Post(ZoneCommand.Enter(13,
            ZoneTestKit.EnterData(farAwaySession, StoneMapId, tribe: 1, posX: Site.StoneX + 1000, posZ: Site.StoneZ)));

        registry[StoneMapId].Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(registry[StoneMapId].TryGetPlayer(11, out var reborn));
        reborn!.RebirthCount = 1;
        Assert.True(registry[StoneMapId].TryGetPlayer(13, out var farAway));
        farAway!.RebirthCount = 1;

        var gateway = new FakeRewardGateway();
        var cycle = CreateCycle(worldState, registry, gateway);
        cycle.Tick(HolyStoneWarCycle.NormalCooldown);
        AdvanceThroughOpeningCountdown(cycle);
        cycle.Tick(TimeSpan.Zero);
        Assert.Equal(10, cycle.PendingCandidateCharacterId);

        for (var i = 0; i < HolyStoneWarCycle.ChallengeCountdownMinutes; i++)
            cycle.Tick(TimeSpan.FromMinutes(1));

        Assert.DoesNotContain(10, gateway.ParticipationRewardedCharacterIds);
        Assert.Contains(11, gateway.ParticipationRewardedCharacterIds);
        Assert.DoesNotContain(12, gateway.ParticipationRewardedCharacterIds);
        Assert.DoesNotContain(13, gateway.ParticipationRewardedCharacterIds);
    }

    [Fact]
    public void ResolveCapture_AdvancesWaterfallQuestProgress_OnlyForCharactersInsideTheCaptureZone()
    {
        var worldState = CreateWorldState();
        var registry = CreateRegistry(StoneMapId, 99);
        var questProgress = new QuestProgress(3, 1, 8, StoneMapId, 0);
        var elsewhereQuestProgress = new QuestProgress(3, 1, 8, 99, 0);

        var (capturerSession, _) = ZoneTestKit.CreateSession(1);
        registry[StoneMapId].Post(ZoneCommand.Enter(10,
            ZoneTestKit.EnterData(capturerSession, StoneMapId, tribe: 1, posX: Site.StoneX, posZ: Site.StoneZ) with
            {
                QuestProgress = questProgress
            }));

        var (elsewhereSession, _) = ZoneTestKit.CreateSession(2);
        registry[99].Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(elsewhereSession, 99, tribe: 1) with
        {
            QuestProgress = elsewhereQuestProgress
        }));

        registry[StoneMapId].Tick(TimeSpan.FromMilliseconds(50));
        registry[99].Tick(TimeSpan.FromMilliseconds(50));

        var cycle = CreateCycle(worldState, registry);
        cycle.Tick(HolyStoneWarCycle.NormalCooldown);
        AdvanceThroughOpeningCountdown(cycle);
        cycle.Tick(TimeSpan.Zero);

        for (var i = 0; i < HolyStoneWarCycle.ChallengeCountdownMinutes; i++)
            cycle.Tick(TimeSpan.FromMinutes(1));

        registry[StoneMapId].Tick(TimeSpan.FromMilliseconds(50));
        registry[99].Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(registry[StoneMapId].TryGetPlayer(10, out var capturer));
        Assert.True(registry[99].TryGetPlayer(20, out var elsewhere));
        Assert.Equal(1, capturer!.QuestKillCounter);
        Assert.Equal(0, elsewhere!.QuestKillCounter);
    }

    private sealed class FakeRewardGateway : IHolyStoneCaptureRewardGateway
    {
        public List<int> CaptureRewardedCharacterIds { get; } = [];
        public List<int> ParticipationRewardedCharacterIds { get; } = [];

        public void GrantCaptureReward(PlayerRuntimeState capturer)
        {
            CaptureRewardedCharacterIds.Add(capturer.CharacterId);
        }

        public void GrantParticipationReward(PlayerRuntimeState tribemate)
        {
            ParticipationRewardedCharacterIds.Add(tribemate.CharacterId);
        }
    }
}
