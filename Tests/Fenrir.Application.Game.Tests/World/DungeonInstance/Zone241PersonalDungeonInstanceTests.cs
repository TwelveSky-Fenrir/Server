using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World.DungeonInstance;

public class Zone241PersonalDungeonInstanceTests
{
    private const short Zone241MapId = 241;
    private const int BossMonsterId = 900;

    private static GameServerOptions Zone241Options()
    {
        return new GameServerOptions { Zone241DungeonMapIds = new HashSet<short> { Zone241MapId } };
    }

    private static PlayerEnterData EnterData(IPacketSession session, short mapId, float posX, float posZ,
        int roundsRemaining = 0, int rebirthCount = 0)
    {
        return new PlayerEnterData(session, "Hero", 0, 0, 2, 3, 1, mapId, posX, 0, posZ, 0f, 100, 100, 50, 50, 1,
            RebirthCount: rebirthCount, DungeonInstanceRoundsRemaining: roundsRemaining);
    }

    private static WorldDataCache BossWorldData(int life = 1)
    {
        var monster = WorldDataTestRows.Monster(BossMonsterId) with { Life = life };
        var rows = WorldDataTestRows.MinimalRows() with
        {
            Monsters = [monster],
            MonsterDropPotions = [new MonsterDropPotionRowDto(BossMonsterId, 0, 1_000_000, 8001)]
        };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    [Fact]
    public void IsZone241TypeZone_ReflectsConfiguredMapIds()
    {
        var zone241 = ZoneTestKit.CreateZone(Zone241MapId, Zone241Options());
        var zoneOrdinary = ZoneTestKit.CreateZone(50, Zone241Options());

        Assert.True(zone241.IsZone241TypeZone);
        Assert.False(zoneOrdinary.IsZone241TypeZone);
    }

    [Fact]
    public void TryEnterZone241PersonalInstance_OutsideZone241Type_ReturnsNotZone241Type()
    {
        var zone = ZoneTestKit.CreateZone(50, Zone241Options());
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1, EnterData(session, 50, 100, 100, 5)));
        zone.Tick(SimulationClock.LegacyTick);

        var outcome = zone.TryEnterZone241PersonalInstance(1);

        Assert.Equal(DungeonInstanceEntryOutcome.NotZone241Type, outcome);
    }

    [Fact]
    public void TryEnterZone241PersonalInstance_QuotaExhausted_RefusesAndTouchesNoState()
    {
        var zone = ZoneTestKit.CreateZone(Zone241MapId, Zone241Options(), worldData: BossWorldData());
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1, EnterData(session, Zone241MapId, 100, 100)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetPlayer(1, out var state));
        Assert.Equal(DungeonInstanceLifecycle.Idle, state!.DungeonInstanceLifecycleState);

        var outcome = zone.TryEnterZone241PersonalInstance(1);

        Assert.Equal(DungeonInstanceEntryOutcome.QuotaExhausted, outcome);
        Assert.Equal(DungeonInstanceLifecycle.Idle, state.DungeonInstanceLifecycleState);
        Assert.Null(state.DungeonInstanceId);
    }

    [Fact]
    public void TryEnterZone241PersonalInstance_NoBossCatalogWired_FailsSummon_RevertsToIdle_QuotaUnchanged()
    {
        var zone = ZoneTestKit.CreateZone(Zone241MapId, Zone241Options(), worldData: BossWorldData());
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1, EnterData(session, Zone241MapId, 100, 100, 3)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetPlayer(1, out var state));
        Assert.Equal(DungeonInstanceLifecycle.Idle, state!.DungeonInstanceLifecycleState);
        Assert.Null(state.DungeonInstanceId);
        Assert.Equal(3, state.DungeonInstanceRoundsRemaining);

        var outcome = zone.TryEnterZone241PersonalInstance(1);

        Assert.Equal(DungeonInstanceEntryOutcome.SummonFailed, outcome);
        Assert.Equal(3, state.DungeonInstanceRoundsRemaining);
    }

    [Fact]
    public void TryEnterZone241PersonalInstance_Success_ArmsInstance_SpawnsBoss_DecrementsQuota()
    {
        var zone = ZoneTestKit.CreateZone(Zone241MapId, Zone241Options(), worldData: BossWorldData());
        zone.PersonalDungeonBossCatalog = new FakeBossCatalog(BossMonsterId);

        var (session, _) = ZoneTestKit.CreateSession(1);
        const int characterId = 7;
        zone.Post(ZoneCommand.Enter(characterId,
            EnterData(session, Zone241MapId, 100, 100, 1)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetPlayer(characterId, out var state));
        Assert.Equal(DungeonInstanceLifecycle.BattleInProgress, state!.DungeonInstanceLifecycleState);
        Assert.Equal(characterId, state.DungeonInstanceId);
        Assert.Equal(0, state.DungeonInstanceRoundsRemaining);

        Assert.True(zone.TryGetMonster(characterId, out var boss));
        Assert.Equal(characterId, boss!.InstanceId);
        Assert.Equal(BossMonsterId, boss.Template.MonsterId);
    }

    [Fact]
    public void TryEnterZone241PersonalInstance_ForciblyDiscardsWhateverOccupiedTheReusedSlot()
    {
        var zone = ZoneTestKit.CreateZone(Zone241MapId, Zone241Options(), worldData: BossWorldData());
        zone.PersonalDungeonBossCatalog = new FakeBossCatalog(BossMonsterId);

        const int characterId = 42;
        var collidingMonster = MonsterEntity.Create(characterId, 1u, WorldDataTestRows.Monster(500) with { Life = 999 },
            characterId, 0, 0, 0, 50);
        zone.SpawnMonster(collidingMonster);
        Assert.True(zone.TryGetMonster(characterId, out _));

        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(characterId,
            EnterData(session, Zone241MapId, 100, 100, 1)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.False(zone.TryDequeueDeadMonster(out _));
        Assert.True(zone.TryGetMonster(characterId, out var occupant));
        Assert.Equal(BossMonsterId, occupant!.Template.MonsterId);
        Assert.Equal(characterId, occupant.InstanceId);
    }

    [Fact]
    public void MonsterBroadcast_HidesTaggedBoss_FromOutOfInstanceBystander_ButNotFromOwner()
    {
        var zone = ZoneTestKit.CreateZone(Zone241MapId, Zone241Options(), worldData: BossWorldData());
        zone.PersonalDungeonBossCatalog = new FakeBossCatalog(BossMonsterId);

        const int ownerId = 1;
        const int bystanderId = 2;
        var (ownerSession, ownerPipe) = ZoneTestKit.CreateSession(1);
        var (bystanderSession, bystanderPipe) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(ownerId, EnterData(ownerSession, Zone241MapId, 100, 100, 1)));
        zone.Post(ZoneCommand.Enter(bystanderId, EnterData(bystanderSession, Zone241MapId, 105, 105)));
        zone.Tick(SimulationClock.LegacyTick);

        ZoneTestKit.DrainOutbound(ownerPipe);
        ZoneTestKit.DrainOutbound(bystanderPipe);

        zone.Tick(SimulationClock.MonsterRebroadcastInterval + SimulationClock.LegacyTick);

        var bystanderBytes = ZoneTestKit.DrainOutbound(bystanderPipe);
        var ownerBytes = ZoneTestKit.DrainOutbound(ownerPipe);

        var avatarFrameSize = FrameWriter.FrameSizeOf<AvatarActionResponse>();
        var monsterFrameSize = FrameWriter.FrameSizeOf<MonsterReplicationResponse>();

        Assert.Equal(avatarFrameSize, bystanderBytes.Length);
        Assert.Equal(avatarFrameSize + monsterFrameSize, ownerBytes.Length);
    }

    [Fact]
    public void TryClaimGroundItem_TaggedItem_MismatchedInstance_IsRefused_AsNotFound()
    {
        var zone = ZoneTestKit.CreateZone(Zone241MapId, Zone241Options());
        zone.SpawnGroundItem(8001, 1, 50, 0, 50, "Boss", "", GroundItemEntity.MonsterKillDropSort, 9);

        var outcome = zone.TryClaimGroundItem(1, 1u, "Someone", null, 50, 0, 50, out var item,
            123);

        Assert.Equal(GroundItemClaimOutcome.NotFound, outcome);
        Assert.Null(item);
        Assert.Equal(1, zone.GroundItemCount);
    }

    [Fact]
    public void TryClaimGroundItem_TaggedItem_MatchingInstance_Succeeds()
    {
        var zone = ZoneTestKit.CreateZone(Zone241MapId, Zone241Options());
        zone.SpawnGroundItem(8001, 1, 50, 0, 50, "Boss", "", GroundItemEntity.MonsterKillDropSort, 9);

        var outcome = zone.TryClaimGroundItem(1, 1u, "Boss", null, 50, 0, 50, out var item,
            9);

        Assert.Equal(GroundItemClaimOutcome.Success, outcome);
        Assert.NotNull(item);
    }

    [Fact]
    public void MonsterAiSystem_TaggedBoss_NeverAggroesOutOfInstanceAvatar_ButCanAggroTheOwner()
    {
        var ai = new MonsterAiSystem(new ScriptedRandomSource(0));
        var zone = ZoneTestKit.CreateZone(Zone241MapId, Zone241Options(),
            simulationSystems: [ai]);

        const int ownerId = 1;
        const int bystanderId = 2;
        var (ownerSession, _) = ZoneTestKit.CreateSession(1);
        var (bystanderSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(ownerId, EnterData(ownerSession, Zone241MapId, 100, 100)));
        zone.Post(ZoneCommand.Enter(bystanderId, EnterData(bystanderSession, Zone241MapId, 105, 105)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetPlayer(ownerId, out var ownerState));
        ownerState!.DungeonInstanceId = ownerId;

        var template = WorldDataTestRows.Monster(BossMonsterId) with
        {
            Life = 999, AttackType = 1, RadiusInfo2 = 50
        };
        var boss = MonsterEntity.Create(ownerId, 1u, template, ownerId, 100, 0, 100, 200f, ownerId);
        boss.AiState = MonsterAiState.Decision;
        zone.SpawnMonster(boss);

        zone.Tick(SimulationClock
            .LegacyTick);
        zone.Tick(SimulationClock
            .LegacyTick);

        Assert.Equal(ownerId, boss.TargetCharacterId);
        Assert.NotEqual(bystanderId, boss.TargetCharacterId);
    }

    [Fact]
    public void BattleInProgress_Broadcasts241Status_Every20Ticks_ToOwnerOnly()
    {
        var zone = ZoneTestKit.CreateZone(Zone241MapId, Zone241Options(), worldData: BossWorldData(999_999));
        zone.PersonalDungeonBossCatalog = new FakeBossCatalog(BossMonsterId);

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        const int characterId = 10;
        zone.Post(ZoneCommand.Enter(characterId, EnterData(session, Zone241MapId, 100, 100, 1)));
        zone.Tick(SimulationClock
            .LegacyTick);

        ZoneTestKit.DrainOutbound(pipe);

        var monsterFrameSize = FrameWriter.FrameSizeOf<MonsterReplicationResponse>();
        var statusFrameSize = FrameWriter.FrameSizeOf<ZoneWar241StatusResponse>();

        for (var i = 0;
             i < 18;
             i++)
            zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(monsterFrameSize,
            ZoneTestKit.DrainOutbound(pipe).Length);

        zone.Tick(SimulationClock
            .LegacyTick);

        Assert.Equal(statusFrameSize, ZoneTestKit.DrainOutbound(pipe).Length);
    }

    [Fact]
    public void Success_OnBossDeath_TransitionsState_AndDoesNotReclaimDroppedLoot_MatchingDeadCleanupGuard()
    {
        var scheduler = new MonsterSpawnScheduler(BossWorldData(), static () => new MaxValueRandom());
        var zone = ZoneTestKit.CreateZone(Zone241MapId, Zone241Options(), worldData: BossWorldData(),
            simulationSystems: [scheduler]);
        zone.PersonalDungeonBossCatalog = new FakeBossCatalog(BossMonsterId);

        var (session, _) = ZoneTestKit.CreateSession(1);
        const int characterId = 5;
        zone.Post(ZoneCommand.Enter(characterId, EnterData(session, Zone241MapId, 100, 100, 1)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetPlayer(characterId, out var state));
        Assert.Equal(DungeonInstanceLifecycle.BattleInProgress, state!.DungeonInstanceLifecycleState);
        Assert.True(zone.TryGetMonster(characterId, out _));

        var found = zone.TryDamageMonster(characterId, 1, characterId, out var wasKillingBlow, out _);
        Assert.True(found);
        Assert.True(wasKillingBlow);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(DungeonInstanceLifecycle.Success, state.DungeonInstanceLifecycleState);
        Assert.Equal(1, zone.GroundItemCount);
    }

    [Fact]
    public void HandleLeave_DuringSummoning_ClearsTaggedMonsterAndGroundItem()
    {
        var zone = ZoneTestKit.CreateZone(Zone241MapId, Zone241Options(), worldData: BossWorldData());
        const int characterId = 11;
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(characterId, EnterData(session, Zone241MapId, 100, 100)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetPlayer(characterId, out var state));
        Assert.Equal(DungeonInstanceLifecycle.Idle, state!.DungeonInstanceLifecycleState);

        state.DungeonInstanceLifecycleState = DungeonInstanceLifecycle.Summoning;
        state.DungeonInstanceId = characterId;

        var taggedMonster = MonsterEntity.Create(characterId, 1u,
            WorldDataTestRows.Monster(BossMonsterId) with { Life = 999 }, characterId, 100, 0, 100, 200f,
            characterId);
        zone.SpawnMonster(taggedMonster);
        zone.SpawnGroundItem(8001, 1, 100, 0, 100, "Hero", "", GroundItemEntity.MonsterKillDropSort, characterId);

        Assert.True(zone.TryGetMonster(characterId, out _));
        Assert.Equal(1, zone.GroundItemCount);

        zone.Post(ZoneCommand.Leave(characterId));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.False(zone.TryGetPlayer(characterId, out _));
        Assert.False(zone.TryGetMonster(characterId, out _));
        Assert.Equal(0, zone.GroundItemCount);
    }

    [Fact]
    public void HandleLeave_DuringSummoning_DoesNotLeakOwnership_WhenSameCharacterReconnectsAndSummonsAgain()
    {
        var zone = ZoneTestKit.CreateZone(Zone241MapId, Zone241Options(), worldData: BossWorldData());
        const int characterId = 13;
        var (firstSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(characterId, EnterData(firstSession, Zone241MapId, 100, 100)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetPlayer(characterId, out var firstState));
        firstState!.DungeonInstanceLifecycleState = DungeonInstanceLifecycle.Summoning;
        firstState.DungeonInstanceId = characterId;

        var staleMonster = MonsterEntity.Create(characterId, 1u,
            WorldDataTestRows.Monster(BossMonsterId) with { Life = 999 }, characterId, 100, 0, 100, 200f,
            characterId);
        zone.SpawnMonster(staleMonster);
        zone.SpawnGroundItem(8001, 1, 100, 0, 100, "Hero", "", GroundItemEntity.MonsterKillDropSort, characterId);

        zone.Post(ZoneCommand.Leave(characterId));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(0, zone.GroundItemCount);

        var (secondSession, _) = ZoneTestKit.CreateSession(2);
        zone.PersonalDungeonBossCatalog = new FakeBossCatalog(BossMonsterId);
        zone.Post(ZoneCommand.Enter(characterId, EnterData(secondSession, Zone241MapId, 100, 100, 1)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetPlayer(characterId, out var secondState));
        Assert.Equal(DungeonInstanceLifecycle.BattleInProgress, secondState!.DungeonInstanceLifecycleState);
        Assert.Equal(characterId, secondState.DungeonInstanceId);

        Assert.True(zone.TryGetMonster(characterId, out var occupant));
        Assert.Equal(BossMonsterId, occupant!.Template.MonsterId);
        Assert.Equal(characterId, occupant.InstanceId);
        Assert.Equal(0, zone.GroundItemCount);
    }

    [Fact]
    public void HandleLeave_OutsideSummoning_DoesNotClearLiveBattleInProgressBoss()
    {
        var zone = ZoneTestKit.CreateZone(Zone241MapId, Zone241Options(), worldData: BossWorldData(999_999));
        zone.PersonalDungeonBossCatalog = new FakeBossCatalog(BossMonsterId);

        var (session, _) = ZoneTestKit.CreateSession(1);
        const int characterId = 17;
        zone.Post(ZoneCommand.Enter(characterId, EnterData(session, Zone241MapId, 100, 100, 1)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetPlayer(characterId, out var state));
        Assert.Equal(DungeonInstanceLifecycle.BattleInProgress, state!.DungeonInstanceLifecycleState);
        Assert.True(zone.TryGetMonster(characterId, out _));

        zone.Post(ZoneCommand.Leave(characterId));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.False(zone.TryGetPlayer(characterId, out _));
        Assert.True(zone.TryGetMonster(characterId, out var survivingBoss));
        Assert.Equal(BossMonsterId, survivingBoss!.Template.MonsterId);
    }

    private sealed class FakeBossCatalog(int monsterId) : IPersonalDungeonBossCatalog
    {
        public bool TryGetBossMonsterId(int rebirthTier, out int resolvedMonsterId)
        {
            resolvedMonsterId = monsterId;
            return true;
        }
    }

        private sealed class MaxValueRandom : Random
    {
        public override int Next(int minValue, int maxValue)
        {
            return Math.Max(minValue, maxValue - 1);
        }
    }
}
