using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     Covers <see cref="MonsterAggroListPruner" /> -- the standalone <c>AdjustValidAttackTarget</c> pruning
///     algorithm (behavior contract <c>A3-aggro-pruning</c>) over the SAME shared 50-slot attacker table
///     <see cref="Zone.TryDamageMonster" /> writes into (see <see cref="MonsterAttackDamageTableTests" />' own
///     remarks on why these tests can only observe that table through <see cref="Zone" />'s public surface --
///     no <c>InternalsVisibleTo</c> grant exists from the Domain assembly to this test project).
/// </summary>
public class MonsterAggroListPrunerTests
{
    /// <summary>
    ///     A manually-spawned monster (no spawn scheduler / world-data catalog needed) parked at the origin, with
    ///     a deterministic (non-random) <see cref="MonsterEntity.PursuerCapacity" /> -- <c>FollowInfo1 == FollowInfo2</c>
    ///     collapses <see cref="MonsterEntity.Create" />'s roll to that exact value, same convention as
    ///     <see cref="MonsterAttackDamageTableTests" />.
    /// </summary>
    private static Zone CreateZoneWithMonster(short meleeRadius, short leashRadius, short pursuerCapacity,
        out MonsterEntity monster)
    {
        var zone = ZoneTestKit.CreateZone(1);
        var template = WorldDataTestRows.Monster(600) with
        {
            RadiusInfo1 = meleeRadius,
            RadiusInfo2 = leashRadius,
            FollowInfo1 = pursuerCapacity,
            FollowInfo2 = pursuerCapacity
        };
        monster = MonsterEntity.Create(1, 1, template, 1, 0, 0, 0, 50);
        zone.SpawnMonster(monster);
        return zone;
    }

    /// <summary>Enters a character at the given position, draining the Enter command so it's live in the zone's player map.</summary>
    private static PlayerRuntimeState EnterCharacter(Zone zone, int characterId, string name, float posX = 0f,
        float posZ = 0f)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone.Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, 1, name, posX: posX, posZ: posZ)));
        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetPlayer(characterId, out var player));
        return player!;
    }

    /// <summary>Records one attacker-table entry via the same public path <see cref="MonsterAttackDamageTableTests" /> uses.</summary>
    private static void RecordDamage(Zone zone, MonsterEntity monster, int attackerCharacterId, int damage)
    {
        Assert.True(zone.TryDamageMonster(monster.ServerIndex, damage, attackerCharacterId, out var died, out _));
        Assert.False(died);
    }

    [Fact]
    public void Prune_EmptyAggroList_ReturnsNoSurvivorsAndNoException()
    {
        var zone = CreateZoneWithMonster(100, 200, 5, out var monster);

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster]);

        Assert.Empty(result.Survivors);
        Assert.False(result.HasValidAttackers);
    }

    [Fact]
    public void Prune_NonPositiveMeleeRadius_WipesListUnconditionally_EvenWithAValidAttacker()
    {
        var zone = CreateZoneWithMonster(0, 200, 5, out var monster);
        EnterCharacter(zone, 10, "A", posX: 5, posZ: 0);
        RecordDamage(zone, monster, 10, 5);

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster]);

        Assert.Empty(result.Survivors);
        Assert.False(result.HasValidAttackers);
    }

    [Fact]
    public void Prune_NonPositiveLeashRadius_WipesListUnconditionally_EvenWithAValidAttacker()
    {
        var zone = CreateZoneWithMonster(100, 0, 5, out var monster);
        EnterCharacter(zone, 10, "A", posX: 5, posZ: 0);
        RecordDamage(zone, monster, 10, 5);

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster]);

        Assert.Empty(result.Survivors);
        Assert.False(result.HasValidAttackers);
    }

    [Fact]
    public void Prune_NearRangeValidAttacker_Survives_WithRefreshedDistanceAndUnchangedDamage()
    {
        // Melee 100, leash 200 -- an attacker at (10,0) from a monster at the origin is well inside the melee
        // (near-range) band: distance = 10, squared = 100.
        var zone = CreateZoneWithMonster(100, 200, 5, out var monster);
        EnterCharacter(zone, 10, "A", posX: 10, posZ: 0);
        RecordDamage(zone, monster, 10, 7);

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster]);

        Assert.True(result.HasValidAttackers);
        var survivor = Assert.Single(result.Survivors);
        Assert.Equal(10, survivor.CharacterId);
        Assert.Equal(7, survivor.CumulativeDamage); // carried forward completely unchanged
        Assert.Equal(100f, survivor.DistanceSquared, 3);
    }

    [Fact]
    public void Prune_AttackerBeyondLeashRadius_IsDropped()
    {
        var zone = CreateZoneWithMonster(10, 20, 5, out var monster);
        EnterCharacter(zone, 10, "A", posX: 1000, posZ: 0);
        RecordDamage(zone, monster, 10, 5);

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster]);

        Assert.Empty(result.Survivors);
        Assert.False(result.HasValidAttackers);
    }

    [Fact]
    public void Prune_DisconnectedAttacker_IsDropped()
    {
        var zone = CreateZoneWithMonster(100, 200, 5, out var monster);
        EnterCharacter(zone, 10, "A", posX: 5, posZ: 0);
        RecordDamage(zone, monster, 10, 5);

        zone.Post(ZoneCommand.Leave(10));
        zone.Tick(SimulationClock.LegacyTick);
        Assert.False(zone.TryGetPlayer(10, out _));

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster]);

        Assert.Empty(result.Survivors);
        Assert.False(result.HasValidAttackers);
    }

    [Fact]
    public void Prune_ReconnectedAttackerWithNewSession_IsDroppedAsStaleReference()
    {
        // The identity+session-slot exclusion (S07_MyGame05.cpp:358-365) covers both "slot no longer occupied"
        // AND "slot reused by a different live session" -- Fenrir models both as one reference re-check
        // (MonsterAttackDamageEntry's own remarks). A reconnect with the SAME character id gets a brand-new
        // PlayerRuntimeState, so the OLD tracked entry's SessionToken must no longer match.
        var zone = CreateZoneWithMonster(100, 200, 5, out var monster);
        EnterCharacter(zone, 10, "A", posX: 5, posZ: 0);
        RecordDamage(zone, monster, 10, 5);

        zone.Post(ZoneCommand.Leave(10));
        zone.Tick(SimulationClock.LegacyTick);
        EnterCharacter(zone, 10, "A", posX: 5, posZ: 0); // same id, new PlayerRuntimeState instance

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster]);

        Assert.Empty(result.Survivors);
        Assert.False(result.HasValidAttackers);
    }

    [Fact]
    public void Prune_MovingZoneAttacker_IsDropped()
    {
        var zone = CreateZoneWithMonster(100, 200, 5, out var monster);
        var player = EnterCharacter(zone, 10, "A", posX: 5, posZ: 0);
        RecordDamage(zone, monster, 10, 5);
        player.IsMovingZone = true;

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster]);

        Assert.Empty(result.Survivors);
        Assert.False(result.HasValidAttackers);
    }

    [Fact]
    public void Prune_DeadAttacker_IsDropped()
    {
        var zone = CreateZoneWithMonster(100, 200, 5, out var monster);
        var player = EnterCharacter(zone, 10, "A", posX: 5, posZ: 0);
        RecordDamage(zone, monster, 10, 5);
        player.IsDead = true;

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster]);

        Assert.Empty(result.Survivors);
        Assert.False(result.HasValidAttackers);
    }

    [Fact]
    public void Prune_MidRangeAttacker_DroppedWhenOtherPursuersAlreadyMeetCap()
    {
        // Melee 10, leash 200 -- an attacker at (50,0) is inside leash but OUTSIDE melee: the mid-range
        // crowd-control band. PursuerCapacity == 1: a single other monster already chasing this exact
        // character already meets the cap, so this candidate entry must be dropped entirely.
        var zone = CreateZoneWithMonster(10, 200, 1, out var monster);
        EnterCharacter(zone, 10, "A", posX: 50, posZ: 0);
        RecordDamage(zone, monster, 10, 5);

        var otherPursuer = MonsterEntity.Create(2, 2, WorldDataTestRows.Monster(601), 1, 0, 0, 0, 50);
        otherPursuer.AiState = MonsterAiState.Chase;
        otherPursuer.AssignTarget(10, 10u, 50, 0, 0);

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster, otherPursuer]);

        Assert.Empty(result.Survivors);
        Assert.False(result.HasValidAttackers);
    }

    [Fact]
    public void Prune_MidRangeAttacker_SurvivesWhenOtherPursuersAreUnderCap()
    {
        // Same shape as the cap-exceeded test above, but PursuerCapacity == 2 -- a single other pursuer is
        // strictly under the cap, so the candidate must survive.
        var zone = CreateZoneWithMonster(10, 200, 2, out var monster);
        EnterCharacter(zone, 10, "A", posX: 50, posZ: 0);
        RecordDamage(zone, monster, 10, 5);

        var otherPursuer = MonsterEntity.Create(2, 2, WorldDataTestRows.Monster(601), 1, 0, 0, 0, 50);
        otherPursuer.AiState = MonsterAiState.Chase;
        otherPursuer.AssignTarget(10, 10u, 50, 0, 0);

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster, otherPursuer]);

        Assert.True(result.HasValidAttackers);
        var survivor = Assert.Single(result.Survivors);
        Assert.Equal(10, survivor.CharacterId);
    }

    [Fact]
    public void Prune_MidRangeAttacker_OtherMonsterNotActuallyPursuingDoesNotCountTowardCap()
    {
        // A second monster that is merely idle (not Chase/AttackWindup/RangedAttackWindup) must not count
        // toward the crowd-control cap even if its own TargetCharacterId happens to match.
        var zone = CreateZoneWithMonster(10, 200, 1, out var monster);
        EnterCharacter(zone, 10, "A", posX: 50, posZ: 0);
        RecordDamage(zone, monster, 10, 5);

        var idleMonster = MonsterEntity.Create(2, 2, WorldDataTestRows.Monster(601), 1, 0, 0, 0, 50);
        idleMonster.AiState = MonsterAiState.Decision;
        idleMonster.AssignTarget(10, 10u, 50, 0, 0);

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster, idleMonster]);

        Assert.True(result.HasValidAttackers);
        Assert.Single(result.Survivors);
    }

    [Fact]
    public void Prune_PreservesRelativeOrderAndCompactsDroppedEntries()
    {
        var zone = CreateZoneWithMonster(100, 200, 5, out var monster);
        EnterCharacter(zone, 10, "A", posX: 5, posZ: 0);
        var dropped = EnterCharacter(zone, 11, "B", posX: 5, posZ: 0);
        EnterCharacter(zone, 12, "C", posX: 5, posZ: 0);

        RecordDamage(zone, monster, 10, 1);
        RecordDamage(zone, monster, 11, 2);
        RecordDamage(zone, monster, 12, 3);
        dropped.IsDead = true; // B drops out; A and C must survive, in their original relative order

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster]);

        Assert.Equal(2, result.Survivors.Count);
        Assert.Equal(10, result.Survivors[0].CharacterId);
        Assert.Equal(12, result.Survivors[1].CharacterId);
    }

    [Fact]
    public void Prune_ReusedResultBuffer_IsClearedBetweenCalls_NotAppendedTo()
    {
        var zone = CreateZoneWithMonster(100, 200, 5, out var monster);
        EnterCharacter(zone, 10, "A", posX: 5, posZ: 0);
        RecordDamage(zone, monster, 10, 5);

        var buffer = new List<MonsterAggroListPruner.Survivor>();
        var first = MonsterAggroListPruner.Prune(zone, monster, [monster], buffer);
        Assert.Same(buffer, first.Survivors);
        Assert.Single(first.Survivors);

        // Second pass over a now-empty (bypassed) list must clear the reused buffer rather than append.
        var zone2 = CreateZoneWithMonster(0, 200, 5, out var monster2);
        var second = MonsterAggroListPruner.Prune(zone2, monster2, [monster2], buffer);

        Assert.Same(buffer, second.Survivors);
        Assert.Empty(second.Survivors);
    }
}
