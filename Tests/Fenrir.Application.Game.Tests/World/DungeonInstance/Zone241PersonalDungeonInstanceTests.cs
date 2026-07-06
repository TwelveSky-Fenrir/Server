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
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World.DungeonInstance;

/// <summary>
///     Zone-241 "LOD" personal-boss-chain dungeon: per-avatar content partitioning, covering
///     <see cref="Zone.TryEnterZone241PersonalInstance" />, <see cref="Zone.AdvanceZone241PersonalDungeonInstances" />,
///     and the broadcast/pickup/aggro instance-equality filters. See <c>Zone.DungeonInstance.cs</c>'s own
///     remarks for exactly which states/transitions are (and are not) modeled.
/// </summary>
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

    private sealed class FakeBossCatalog(int monsterId) : IPersonalDungeonBossCatalog
    {
        public bool TryGetBossMonsterId(int rebirthTier, out int resolvedMonsterId)
        {
            resolvedMonsterId = monsterId;
            return true;
        }
    }

    /// <summary>Always draws the maximum value so the guaranteed drop rolls succeed deterministically.</summary>
    private sealed class MaxValueRandom : Random
    {
        public override int Next(int minValue, int maxValue)
        {
            return Math.Max(minValue, maxValue - 1);
        }
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
        // roundsRemaining defaults to 0 -- the automatic HandleEnter-triggered attempt already consumed
        // nothing since quota was already exhausted before this explicit retry.
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
        // Default PersonalDungeonBossCatalog (NullPersonalDungeonBossCatalog) -- every tier resolution fails.
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1, EnterData(session, Zone241MapId, 100, 100, roundsRemaining: 3)));
        zone.Tick(SimulationClock.LegacyTick); // HandleEnter's automatic trigger already ran once and failed

        Assert.True(zone.TryGetPlayer(1, out var state));
        Assert.Equal(DungeonInstanceLifecycle.Idle, state!.DungeonInstanceLifecycleState);
        Assert.Null(state.DungeonInstanceId);
        Assert.Equal(3, state.DungeonInstanceRoundsRemaining); // never consumed on a failed summon

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
            EnterData(session, Zone241MapId, 100, 100, roundsRemaining: 1)));
        zone.Tick(SimulationClock.LegacyTick); // HandleEnter's automatic trigger arms + summons

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
        // An unrelated, still-alive monster happens to occupy the exact slot the personal boss will reuse.
        var collidingMonster = MonsterEntity.Create(characterId, 1u, WorldDataTestRows.Monster(500) with { Life = 999 },
            characterId, 0, 0, 0, 50);
        zone.SpawnMonster(collidingMonster);
        Assert.True(zone.TryGetMonster(characterId, out _));

        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(characterId,
            EnterData(session, Zone241MapId, 100, 100, roundsRemaining: 1)));
        zone.Tick(SimulationClock.LegacyTick);

        // The colliding monster was silently discarded (not the normal despawn path): no DeadMonsterEvent, no
        // loot -- just gone, replaced by the personal boss under the exact same ServerIndex.
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

        zone.Post(ZoneCommand.Enter(ownerId, EnterData(ownerSession, Zone241MapId, 100, 100, roundsRemaining: 1)));
        zone.Post(ZoneCommand.Enter(bystanderId, EnterData(bystanderSession, Zone241MapId, 105, 105)));
        zone.Tick(SimulationClock.LegacyTick); // enters both + arms/summons the owner's personal boss

        ZoneTestKit.DrainOutbound(ownerPipe); // discard everything sent so far (self-spawn/neighbor announces)
        ZoneTestKit.DrainOutbound(bystanderPipe);

        // Force a fresh keep-alive broadcast for both the boss (5 s cadence) and, unavoidably, the ordinary
        // avatar keep-alive (3.5 s cadence, not instance-filtered) on the same tick -- so this discriminates by
        // exact frame size instead of "any bytes at all": both players legitimately exchange one AvatarActionResponse
        // frame each via mutual AOI visibility, but only the owner's own frame budget also includes the boss's
        // MonsterReplicationResponse frame.
        zone.Tick(SimulationClock.MonsterRebroadcastInterval + SimulationClock.LegacyTick);

        var bystanderBytes = ZoneTestKit.DrainOutbound(bystanderPipe);
        var ownerBytes = ZoneTestKit.DrainOutbound(ownerPipe);

        var avatarFrameSize = FrameWriter.FrameSizeOf<AvatarActionResponse>();
        var monsterFrameSize = FrameWriter.FrameSizeOf<MonsterReplicationResponse>();

        // The bystander receives exactly the owner's ordinary avatar keep-alive -- never the boss's frame.
        Assert.Equal(avatarFrameSize, bystanderBytes.Length);
        // The owner receives both: the bystander's ordinary avatar keep-alive AND its own personal boss's frame.
        Assert.Equal(avatarFrameSize + monsterFrameSize, ownerBytes.Length);
    }

    [Fact]
    public void TryClaimGroundItem_TaggedItem_MismatchedInstance_IsRefused_AsNotFound()
    {
        var zone = ZoneTestKit.CreateZone(Zone241MapId, Zone241Options());
        zone.SpawnGroundItem(8001, 1, 50, 0, 50, "Boss", "", GroundItemEntity.MonsterKillDropSort, instanceId: 9);

        var outcome = zone.TryClaimGroundItem(1, 1u, "Someone", null, 50, 0, 50, out var item,
            claimantInstanceId: 123);

        Assert.Equal(GroundItemClaimOutcome.NotFound, outcome);
        Assert.Null(item);
        Assert.Equal(1, zone.GroundItemCount); // never removed on a mismatched-instance claim
    }

    [Fact]
    public void TryClaimGroundItem_TaggedItem_MatchingInstance_Succeeds()
    {
        var zone = ZoneTestKit.CreateZone(Zone241MapId, Zone241Options());
        zone.SpawnGroundItem(8001, 1, 50, 0, 50, "Boss", "", GroundItemEntity.MonsterKillDropSort, instanceId: 9);

        // "Boss" matches the recorded Master (rule 4: the recorded owner can always reclaim their own drop) --
        // the instance-equality gate is the mechanic under test here, not the ownership window.
        var outcome = zone.TryClaimGroundItem(1, 1u, "Boss", null, 50, 0, 50, out var item,
            claimantInstanceId: 9);

        Assert.Equal(GroundItemClaimOutcome.Success, outcome);
        Assert.NotNull(item);
    }

    [Fact]
    public void MonsterAiSystem_TaggedBoss_NeverAggroesOutOfInstanceAvatar_ButCanAggroTheOwner()
    {
        var ai = new MonsterAiSystem();
        var zone = ZoneTestKit.CreateZone(Zone241MapId, Zone241Options(),
            simulationSystems: [ai]);

        const int ownerId = 1;
        const int bystanderId = 2;
        var (ownerSession, _) = ZoneTestKit.CreateSession(1);
        var (bystanderSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(ownerId, EnterData(ownerSession, Zone241MapId, 100, 100)));
        zone.Post(ZoneCommand.Enter(bystanderId, EnterData(bystanderSession, Zone241MapId, 105, 105)));
        zone.Tick(SimulationClock.LegacyTick); // registers both players (no quota, so the automatic Zone-241
                                                // entry attempt fails harmlessly for both -- this test tags the
                                                // owner's own instance id directly below instead)

        Assert.True(zone.TryGetPlayer(ownerId, out var ownerState));
        // Simulates an already-armed personal instance for the owner, matching what a real, quota-granted
        // TryEnterZone241PersonalInstance call would have set -- done directly here so this test can isolate
        // the AI aggro filter from the quota/summon machinery covered by the other tests in this file.
        ownerState!.DungeonInstanceId = ownerId;

        var template = WorldDataTestRows.Monster(BossMonsterId) with
        {
            Life = 999, AttackType = 1, RadiusInfo2 = 50
        };
        var boss = MonsterEntity.Create(ownerId, 1u, template, ownerId, 100, 0, 100, 200f, ownerId);
        boss.AiState = MonsterAiState.Decision;
        zone.SpawnMonster(boss);

        zone.Tick(SimulationClock.LegacyTick); // AI decision runs -- only the tagged owner is an eligible target

        Assert.Equal(ownerId, boss.TargetCharacterId);
        Assert.NotEqual(bystanderId, boss.TargetCharacterId);
    }

    [Fact]
    public void BattleInProgress_Broadcasts241Status_Every20Ticks_ToOwnerOnly()
    {
        var zone = ZoneTestKit.CreateZone(Zone241MapId, Zone241Options(), worldData: BossWorldData(life: 999_999));
        zone.PersonalDungeonBossCatalog = new FakeBossCatalog(BossMonsterId);

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        const int characterId = 3;
        zone.Post(ZoneCommand.Enter(characterId, EnterData(session, Zone241MapId, 100, 100, roundsRemaining: 1)));
        zone.Tick(SimulationClock.LegacyTick); // tick call #1: enter + arm + summon (creation broadcast) -> BattleInProgress, dungeon tick counter at 1

        ZoneTestKit.DrainOutbound(pipe); // discard the creation broadcast

        // The boss is summoned exactly at the owner's own position, so the owner is always its own boss's AOI
        // neighbor too -- the boss's ordinary 5 s (10-tick) keep-alive fires once on its own schedule during
        // this 18-call loop (at call #11), independently of the 20-tick 241-status cadence under test here
        // (they never land on the same tick call: the keep-alive's own schedule is 11, 21, 31... while the
        // status cadence's is 20, 40, 60... which never coincide). Discriminating by exact frame size, not
        // "any bytes at all", keeps this test correct despite that unavoidable, unrelated cadence sharing the
        // same pipe.
        var monsterFrameSize = FrameWriter.FrameSizeOf<MonsterReplicationResponse>();
        var statusFrameSize = FrameWriter.FrameSizeOf<ZoneWar241StatusResponse>();

        for (var i = 0; i < 18; i++) // tick calls #2..#19 -- dungeon tick counter reaches 19, no 241-status cadence hit yet
            zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(monsterFrameSize, ZoneTestKit.DrainOutbound(pipe).Length); // only the boss's own routine keep-alive

        zone.Tick(SimulationClock.LegacyTick); // tick call #20 -- dungeon tick counter reaches 20, 241-status cadence fires

        Assert.Equal(statusFrameSize, ZoneTestKit.DrainOutbound(pipe).Length); // exactly the 241-status broadcast
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
        zone.Post(ZoneCommand.Enter(characterId, EnterData(session, Zone241MapId, 100, 100, roundsRemaining: 1)));
        zone.Tick(SimulationClock.LegacyTick); // enter + arm + summon

        Assert.True(zone.TryGetPlayer(characterId, out var state));
        Assert.Equal(DungeonInstanceLifecycle.BattleInProgress, state!.DungeonInstanceLifecycleState);
        Assert.True(zone.TryGetMonster(characterId, out _));

        var found = zone.TryDamageMonster(characterId, 1, characterId, out var wasKillingBlow, out _);
        Assert.True(found);
        Assert.True(wasKillingBlow);

        zone.Tick(SimulationClock.LegacyTick); // drains the death (loot rolls + drops), then advances to Success

        Assert.Equal(DungeonInstanceLifecycle.Success, state.DungeonInstanceLifecycleState);
        // The cleanup guard only ever fires while lifecycle == Summoning; by the time it runs here the state
        // has already moved to Success, so the boss's own dropped loot is faithfully NOT reclaimed.
        Assert.Equal(1, zone.GroundItemCount);
    }
}
