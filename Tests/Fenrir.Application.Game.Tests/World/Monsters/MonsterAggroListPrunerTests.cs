using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterAggroListPrunerTests
{
    private static Zone CreateZoneWithMonster(short meleeRadius, short leashRadius, short pursuerCapacity,
        out MonsterEntity monster, short heightTolerance = 1)
    {
        var zone = ZoneTestKit.CreateZone(1);
        var template = WorldDataTestRows.Monster(600) with
        {
            RadiusInfo1 = meleeRadius,
            RadiusInfo2 = leashRadius,
            FollowInfo1 = pursuerCapacity,
            FollowInfo2 = pursuerCapacity,
            Size2 = heightTolerance
        };
        monster = MonsterEntity.Create(1, 1, template, 1, 0, 0, 0);
        zone.SpawnMonster(monster);
        return zone;
    }

    private static PlayerRuntimeState EnterCharacter(Zone zone, int characterId, string name, float posX = 0f,
        float posZ = 0f, float posY = 0f)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone.Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, 1, name, posX, posY, posZ)));
        zone.Tick(SimulationClock.LegacyTick);
        Assert.True(zone.TryGetPlayer(characterId, out var player));
        return player!;
    }

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
        EnterCharacter(zone, 10, "A", 5, 0);
        RecordDamage(zone, monster, 10, 5);

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster]);

        Assert.Empty(result.Survivors);
        Assert.False(result.HasValidAttackers);
    }

    [Fact]
    public void Prune_NonPositiveLeashRadius_WipesListUnconditionally_EvenWithAValidAttacker()
    {
        var zone = CreateZoneWithMonster(100, 0, 5, out var monster);
        EnterCharacter(zone, 10, "A", 5, 0);
        RecordDamage(zone, monster, 10, 5);

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster]);

        Assert.Empty(result.Survivors);
        Assert.False(result.HasValidAttackers);
    }

    [Fact]
    public void Prune_NearRangeValidAttacker_Survives_WithRefreshedDistanceAndUnchangedDamage()
    {
        var zone = CreateZoneWithMonster(100, 200, 5, out var monster);
        EnterCharacter(zone, 10, "A", 10, 0);
        RecordDamage(zone, monster, 10, 7);

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster]);

        Assert.True(result.HasValidAttackers);
        var survivor = Assert.Single(result.Survivors);
        Assert.Equal(10, survivor.CharacterId);
        Assert.Equal(7, survivor.CumulativeDamage);
        Assert.Equal(100f, survivor.DistanceSquared, 3);
    }

    [Fact]
    public void Prune_AttackerBeyondLeashRadius_IsDropped()
    {
        var zone = CreateZoneWithMonster(10, 20, 5, out var monster);
        EnterCharacter(zone, 10, "A", 1000, 0);
        RecordDamage(zone, monster, 10, 5);

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster]);

        Assert.Empty(result.Survivors);
        Assert.False(result.HasValidAttackers);
    }

    [Fact]
    public void Prune_DisconnectedAttacker_IsDropped()
    {
        var zone = CreateZoneWithMonster(100, 200, 5, out var monster);
        EnterCharacter(zone, 10, "A", 5, 0);
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
        var zone = CreateZoneWithMonster(100, 200, 5, out var monster);
        EnterCharacter(zone, 10, "A", 5, 0);
        RecordDamage(zone, monster, 10, 5);

        zone.Post(ZoneCommand.Leave(10));
        zone.Tick(SimulationClock.LegacyTick);
        EnterCharacter(zone, 10, "A", 5, 0);

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster]);

        Assert.Empty(result.Survivors);
        Assert.False(result.HasValidAttackers);
    }

    [Fact]
    public void Prune_MovingZoneAttacker_IsDropped()
    {
        var zone = CreateZoneWithMonster(100, 200, 5, out var monster);
        var player = EnterCharacter(zone, 10, "A", 5, 0);
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
        var player = EnterCharacter(zone, 10, "A", 5, 0);
        RecordDamage(zone, monster, 10, 5);
        player.IsDead = true;

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster]);

        Assert.Empty(result.Survivors);
        Assert.False(result.HasValidAttackers);
    }

    [Fact]
    public void Prune_MidRangeAttacker_DroppedWhenOtherPursuersAlreadyMeetCap()
    {
        var zone = CreateZoneWithMonster(10, 200, 1, out var monster);
        EnterCharacter(zone, 10, "A", 50, 0);
        RecordDamage(zone, monster, 10, 5);

        var otherPursuer = MonsterEntity.Create(2, 2, WorldDataTestRows.Monster(601), 1, 0, 0, 0);
        otherPursuer.AiState = MonsterAiState.Chase;
        otherPursuer.AssignTarget(10, 10u, 50, 0, 0);

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster, otherPursuer]);

        Assert.Empty(result.Survivors);
        Assert.False(result.HasValidAttackers);
    }

    [Fact]
    public void Prune_MidRangeAttacker_SurvivesWhenOtherPursuersAreUnderCap()
    {
        var zone = CreateZoneWithMonster(10, 200, 2, out var monster);
        EnterCharacter(zone, 10, "A", 50, 0);
        RecordDamage(zone, monster, 10, 5);

        var otherPursuer = MonsterEntity.Create(2, 2, WorldDataTestRows.Monster(601), 1, 0, 0, 0);
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
        var zone = CreateZoneWithMonster(10, 200, 1, out var monster);
        EnterCharacter(zone, 10, "A", 50, 0);
        RecordDamage(zone, monster, 10, 5);

        var idleMonster = MonsterEntity.Create(2, 2, WorldDataTestRows.Monster(601), 1, 0, 0, 0);
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
        EnterCharacter(zone, 10, "A", 5, 0);
        var dropped = EnterCharacter(zone, 11, "B", 5, 0);
        EnterCharacter(zone, 12, "C", 5, 0);

        RecordDamage(zone, monster, 10, 1);
        RecordDamage(zone, monster, 11, 2);
        RecordDamage(zone, monster, 12, 3);
        dropped.IsDead = true;

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster]);

        Assert.Equal(2, result.Survivors.Count);
        Assert.Equal(10, result.Survivors[0].CharacterId);
        Assert.Equal(12, result.Survivors[1].CharacterId);
    }

    [Fact]
    public void Prune_MeleeRangeAttacker_DroppedWhenVerticalSeparationExceedsTemplateTolerance()
    {
        var zone = CreateZoneWithMonster(100, 200, 5, out var monster, heightTolerance: 5);
        EnterCharacter(zone, 10, "A", 5, 0, posY: 100);
        RecordDamage(zone, monster, 10, 5);

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster]);

        Assert.Empty(result.Survivors);
        Assert.False(result.HasValidAttackers);
    }

    [Fact]
    public void Prune_MeleeRangeAttacker_SurvivesWhenVerticalSeparationWithinTemplateTolerance()
    {
        var zone = CreateZoneWithMonster(100, 200, 5, out var monster, heightTolerance: 5);
        EnterCharacter(zone, 10, "A", 5, 0, posY: 3);
        RecordDamage(zone, monster, 10, 5);

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster]);

        Assert.True(result.HasValidAttackers);
        Assert.Single(result.Survivors);
    }

    [Fact]
    public void Prune_MeleeRangeAttacker_VerticalSeparationExactlyAtTolerance_StillSurvives()
    {
        var zone = CreateZoneWithMonster(100, 200, 5, out var monster, heightTolerance: 5);
        EnterCharacter(zone, 10, "A", 5, 0, posY: 5);
        RecordDamage(zone, monster, 10, 5);

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster]);

        Assert.True(result.HasValidAttackers);
        Assert.Single(result.Survivors);
    }

    [Fact]
    public void Prune_MidRangeAttacker_VerticalSeparationIsIgnoredByThePursuerCapacityGate()
    {
        var zone = CreateZoneWithMonster(10, 200, 1, out var monster, heightTolerance: 1);
        EnterCharacter(zone, 10, "A", 50, 0, posY: 999);
        RecordDamage(zone, monster, 10, 5);

        var result = MonsterAggroListPruner.Prune(zone, monster, [monster]);

        Assert.True(result.HasValidAttackers);
        Assert.Single(result.Survivors);
    }

    [Fact]
    public void Prune_ReusedResultBuffer_IsClearedBetweenCalls_NotAppendedTo()
    {
        var zone = CreateZoneWithMonster(100, 200, 5, out var monster);
        EnterCharacter(zone, 10, "A", 5, 0);
        RecordDamage(zone, monster, 10, 5);

        var buffer = new List<MonsterAggroListPruner.Survivor>();
        var first = MonsterAggroListPruner.Prune(zone, monster, [monster], buffer);
        Assert.Same(buffer, first.Survivors);
        Assert.Single(first.Survivors);

        var zone2 = CreateZoneWithMonster(0, 200, 5, out var monster2);
        var second = MonsterAggroListPruner.Prune(zone2, monster2, [monster2], buffer);

        Assert.Same(buffer, second.Survivors);
        Assert.Empty(second.Survivors);
    }
}
