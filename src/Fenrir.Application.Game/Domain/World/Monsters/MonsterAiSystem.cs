using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Application.Game.Domain.World.Pathfinding;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.World.Monsters;

public sealed partial class MonsterAiSystem(
    IRandomSource? random = null,
    WorldStateService? worldState = null,
    Lazy<ZoneCenterBroadcastIngestor>? siegeIngestor = null)
    : ISimulationSystem
{
    private const float TickSeconds = SimulationClock.LegacyTickMilliseconds / 1000f;

    private const float ArrivalEpsilon = 1f;

    private const float PathReplanGoalMoveThreshold = 40f;

    private const float WanderMinRadius = 50f;

    private const int WanderRadiusRollSpan = 51;

    private const int WanderDirectionRollSpan = 201;

    private const int WanderDirectionRollHalfSpan = 100;

    private const float WanderMinDisplacement = 50f;

    private const float WanderPathStepIntervalSeconds = 0.033f;

    private const float HomeReturnRaycastStepSeconds = 0.033f;

    private const float LegacyFrameUnitsPerSecond = 30f;

    private readonly IRandomSource _random = random ?? SystemRandomSource.Instance;

    private readonly Lazy<ZoneCenterBroadcastIngestor>? _siegeIngestor = siegeIngestor;

    private readonly WorldStateService? _worldState = worldState;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var dt = TickSeconds * legacyTicksElapsed;

        var monsters = zone.MonstersSnapshot;

        foreach (var monster in monsters)
            Update(zone, monster, dt, legacyTicksElapsed, monsters);
    }

    private void Update(Zone zone, MonsterEntity monster, float dt, int legacyTicksElapsed,
        IEnumerable<MonsterEntity> allMonsters)
    {
        switch (monster.AiState)
        {
            case MonsterAiState.Spawning:
                monster.StateFrameAccumulator += dt * LegacyFrameUnitsPerSecond;
                if (monster.StateFrameAccumulator >= Math.Max(1, (int)monster.Template.FrameInfo1))
                {
                    monster.StateFrameAccumulator = 0f;
                    monster.AiState = MonsterAiState.Decision;
                    monster.StateTicks = 0;
                }

                break;

            case MonsterAiState.Decision:
                RunDecision(zone, monster, legacyTicksElapsed, allMonsters);
                break;

            case MonsterAiState.Patrol:
                RunPatrol(zone, monster, dt, legacyTicksElapsed, allMonsters);
                break;

            case MonsterAiState.Chase:
                RunChase(zone, monster, dt, legacyTicksElapsed);
                break;

            case MonsterAiState.AttackWindup:
                RunAttackWindup(zone, monster, dt);
                break;

            case MonsterAiState.RangedAttackWindup:
                if (monster.StateTicks++ == 0)
                    monster.StateFrameAccumulator = 0f;

                monster.StateFrameAccumulator += dt * LegacyFrameUnitsPerSecond;
                if (monster.StateFrameAccumulator >= Math.Max(1, (int)monster.Template.FrameInfo4))
                {
                    monster.StateFrameAccumulator = 0f;
                    monster.AiState = MonsterAiState.Decision;
                    monster.StateTicks = 0;
                }

                break;

            case MonsterAiState.Flinch:
                if (monster.StateTicks++ == 0)
                    monster.StateFrameAccumulator = 0f;

                monster.StateFrameAccumulator += dt * LegacyFrameUnitsPerSecond;
                if (monster.StateFrameAccumulator >= Math.Max(1, (int)monster.Template.FrameInfo2))
                {
                    monster.StateFrameAccumulator = 0f;
                    monster.AiState = MonsterAiState.Decision;
                    monster.StateTicks = 0;
                }

                break;

            case MonsterAiState.ReturnToSpawn:
                monster.StateFrameAccumulator += dt * LegacyFrameUnitsPerSecond;
                if (monster.StateFrameAccumulator >= Math.Max(1, (int)monster.Template.FrameInfo6))
                {
                    monster.StateFrameAccumulator = 0f;

                    monster.PosX = monster.HomeX;
                    monster.PosY = monster.HomeY;
                    monster.PosZ = monster.HomeZ;
                    monster.ReleaseTarget();
                    monster.AiState = MonsterAiState.Spawning;
                    monster.StateTicks = 0;

                    zone.BroadcastMonsterActionChange(monster);
                }

                break;

            case MonsterAiState.Dead:
                monster.StateFrameAccumulator += dt * LegacyFrameUnitsPerSecond;
                if (!MonsterDeathSequence.IsCorpseCountdownComplete(monster))
                    break;

                zone.InvalidateDeadMonster(monster);
                return;
        }

        zone.SyncMonsterCell(monster);
    }

    private static void RunAttackWindup(Zone zone, MonsterEntity monster, float dt)
    {
        if (monster.StateTicks++ == 0)
        {
            monster.StateFrameAccumulator = 0f;
            if (monster.TargetCharacterId is { } attackTargetId)
                zone.ResolveMonsterAttack(monster, attackTargetId);
        }

        monster.StateFrameAccumulator += dt * LegacyFrameUnitsPerSecond;
        if (monster.StateFrameAccumulator < Math.Max(1, (int)monster.Template.FrameInfo3))
            return;

        if (monster.AttackPacketConfirmationArmed)
        {
            if (monster.StateTicks < SimulationClock.OneSecondGateLegacyTicks)
                return;

            monster.AttackPacketConfirmationArmed = false;
        }

        monster.StateFrameAccumulator = 0f;
        monster.AiState = MonsterAiState.Decision;
        monster.StateTicks = 0;
    }

    private void RunPatrol(Zone zone, MonsterEntity monster, float dt, int legacyTicksElapsed,
        IEnumerable<MonsterEntity> allMonsters)
    {
        if (monster.SpecialSort == MonsterSpecialSort.Standard &&
            HasActiveOrFreshlyAcquiredAttacker(zone, monster, legacyTicksElapsed, allMonsters))
        {
            monster.AiState = MonsterAiState.Decision;
            monster.StateTicks = 0;

            zone.BroadcastMonsterActionChange(monster);
            return;
        }

        var blocked = MoveToward(zone, monster, monster.WanderTargetX, monster.WanderTargetZ,
            monster.Template.WalkSpeed, dt);
        if (blocked)
        {
            monster.AiState = MonsterAiState.Decision;
            monster.StateTicks = 0;

            zone.BroadcastMonsterPathBlocked(monster);
            return;
        }

        if (DistanceSquared(monster.PosX, monster.PosZ, monster.WanderTargetX, monster.WanderTargetZ) <=
            ArrivalEpsilon * ArrivalEpsilon)
        {
            monster.AiState = MonsterAiState.Decision;
            monster.StateTicks = 0;
        }
    }

    private bool HasActiveOrFreshlyAcquiredAttacker(Zone zone, MonsterEntity monster, int legacyTicksElapsed,
        IEnumerable<MonsterEntity> allMonsters)
    {
        return monster.HasTrackedAttackers() ||
               TryAcquireTarget(zone, monster, legacyTicksElapsed, allMonsters, false);
    }

    private void RunDecision(Zone zone, MonsterEntity monster, int legacyTicksElapsed,
        IEnumerable<MonsterEntity> allMonsters)
    {
        if (IsZone175TypeBoss(monster.Template.SpecialType))
        {
            RunZone175BossDecision(zone, monster, legacyTicksElapsed);
            return;
        }

        switch (monster.SpecialSort)
        {
            case MonsterSpecialSort.Standard:
                RunStandardDecision(zone, monster, legacyTicksElapsed, allMonsters);
                break;

            case MonsterSpecialSort.CarThrower:
                RunThrowerDecision(zone, monster);
                break;

            case MonsterSpecialSort.TribeGuard:
                RunGuardDecision(zone, monster, legacyTicksElapsed);
                break;

            case MonsterSpecialSort.TribeSymbolStone:
                RunTribeSymbolStoneDecision(monster, legacyTicksElapsed);
                break;

            case MonsterSpecialSort.AllianceStone:
                RunAllianceStoneDecision(monster, legacyTicksElapsed);
                break;

            case MonsterSpecialSort.Tower:
                break;

            case MonsterSpecialSort.Inert:
            default:
                break;
        }
    }

    private void RunStandardDecision(Zone zone, MonsterEntity monster, int legacyTicksElapsed,
        IEnumerable<MonsterEntity> allMonsters)
    {
        if (!monster.HasTrackedAttackers())
        {
            if (!TryAcquireTarget(zone, monster, legacyTicksElapsed, allMonsters, false))
            {
                RunIdleWanderOrReturnHome(zone, monster, legacyTicksElapsed);
                return;
            }

            monster.IdleWanderElapsedTicks = 0;
        }

        RunPrunedAttackerEngagement(zone, monster, allMonsters);
    }

    private void RunIdleWanderOrReturnHome(Zone zone, MonsterEntity monster, int legacyTicksElapsed)
    {
        monster.IdleReturnElapsedTicks += legacyTicksElapsed;
        if (monster.IdleReturnElapsedTicks > SimulationClock.MonsterIdleReturnHomeLegacyTicks)
        {
            monster.IdleReturnElapsedTicks = 0;

            ResolveHomeReturnPath(zone, monster, out var resolvedX, out var resolvedY, out var resolvedZ);

            if (DistanceSquared(resolvedX, resolvedY, resolvedZ, monster.HomeX, monster.HomeY, monster.HomeZ) >
                ArrivalEpsilon * ArrivalEpsilon)
            {
                monster.IdleWanderElapsedTicks = 0;

                monster.HomeReturnTargetX = monster.HomeX;
                monster.HomeReturnTargetY = monster.HomeY;
                monster.HomeReturnTargetZ = monster.HomeZ;
                monster.TargetLocationX = monster.HomeX;
                monster.TargetLocationY = monster.HomeY;
                monster.TargetLocationZ = monster.HomeZ;
                monster.Heading = WireHeading.Between(monster.PosX, monster.PosZ, monster.HomeX, monster.HomeZ);
                monster.AiState = MonsterAiState.ReturnToSpawn;
                monster.StateTicks = 0;
                monster.StateFrameAccumulator = 0f;

                zone.BroadcastMonsterActionChange(monster);
                return;
            }
        }

        monster.IdleWanderElapsedTicks += legacyTicksElapsed;
        if (monster.IdleWanderElapsedTicks < SimulationClock.MonsterIdleWanderLegacyTicks)
            return;

        monster.IdleWanderElapsedTicks = 0;

        if (!TryComputeWanderDestination(zone, monster, out var destX, out var destZ))
            return;

        monster.WanderTargetX = destX;
        monster.WanderTargetZ = destZ;
        monster.TargetLocationX = destX;
        monster.TargetLocationY = monster.PosY;
        monster.TargetLocationZ = destZ;
        monster.Heading = WireHeading.Between(monster.PosX, monster.PosZ, destX, destZ);
        monster.AiState = MonsterAiState.Patrol;
        monster.StateTicks = 0;

        zone.BroadcastMonsterActionChange(monster);
    }

    private static void ResolveHomeReturnPath(Zone zone, MonsterEntity monster, out float resolvedX,
        out float resolvedY, out float resolvedZ)
    {
        var destX = monster.HomeX;
        var destY = monster.HomeY;
        var destZ = monster.HomeZ;

        if (zone.Geometry is not { } geometry)
        {
            resolvedX = destX;
            resolvedY = destY;
            resolvedZ = destZ;
            return;
        }

        var speed = (float)monster.Template.WalkSpeed;
        if (speed <= 0f)
        {
            resolvedX = monster.PosX;
            resolvedY = monster.PosY;
            resolvedZ = monster.PosZ;
            return;
        }

        var stepLength = speed * HomeReturnRaycastStepSeconds;
        var currentX = monster.PosX;
        var currentZ = monster.PosZ;

        while (true)
        {
            var dx = destX - currentX;
            var dz = destZ - currentZ;
            var remaining = MathF.Sqrt(dx * dx + dz * dz);

            var reachedDestination = remaining <= stepLength;
            var candidateX = reachedDestination ? destX : currentX + dx / remaining * stepLength;
            var candidateZ = reachedDestination ? destZ : currentZ + dz / remaining * stepLength;

            if (!geometry.IsWalkable(candidateX, candidateZ))
                break;

            currentX = candidateX;
            currentZ = candidateZ;

            if (reachedDestination)
                break;
        }

        if (geometry.TryGetGroundHeight(currentX, currentZ, out var groundY))
        {
            resolvedX = currentX;
            resolvedY = groundY;
            resolvedZ = currentZ;
        }
        else
        {
            resolvedX = monster.PosX;
            resolvedY = monster.PosY;
            resolvedZ = monster.PosZ;
        }
    }

    private void RunPrunedAttackerEngagement(Zone zone, MonsterEntity monster, IEnumerable<MonsterEntity> allMonsters)
    {
        var pruneResult = MonsterAggroListPruner.Prune(zone, monster, allMonsters);
        monster.ReplaceAttackDamage(pruneResult.Survivors);

        if (!pruneResult.HasValidAttackers)
            return;

        var survivors = pruneResult.Survivors;

        var meleeRadius = monster.Template.RadiusInfo1;
        var meleeRadiusSq = (float)meleeRadius * meleeRadius;

        MonsterAggroListPruner.Survivor? chosen = null;
        foreach (var survivor in survivors)
        {
            if (survivor.DistanceSquared > meleeRadiusSq)
                continue;

            if (_random.NextInt32(2) == 0)
            {
                chosen = survivor;
                break;
            }
        }

        chosen ??= survivors[_random.NextInt32(survivors.Count)];

        var pick = chosen.Value;

        if (!zone.TryGetPlayer(pick.CharacterId, out var target) || target is null ||
            !ReferenceEquals(target, pick.SessionToken))
            return;

        if (pick.DistanceSquared <= meleeRadiusSq)
        {
            monster.AssignTarget(pick.CharacterId, target.UniqueNumber, target.PosX, target.PosY, target.PosZ);
            CommitMeleeEngagement(zone, monster, target);
            return;
        }

        var chaseSpeed = monster.Template.RunSpeed;
        if (chaseSpeed <= 0 || meleeRadius <= 0)
            return;

        ComputeArcApproachPoint(monster, target.PosX, target.PosZ, meleeRadius, out var approachX,
            out var approachZ);

        if (CanReachPoint(zone, monster, approachX, approachZ))
        {
            monster.AssignTarget(pick.CharacterId, target.UniqueNumber, approachX, monster.PosY, approachZ);
            monster.AiState = MonsterAiState.Chase;
            monster.StateTicks = 0;
            zone.BroadcastMonsterActionChange(monster);
        }
        else
        {
            AbandonChaseAndReturnHome(zone, monster);
        }
    }

    private static void AbandonChaseAndReturnHome(Zone zone, MonsterEntity monster)
    {
        monster.HomeReturnTargetX = monster.HomeX;
        monster.HomeReturnTargetY = monster.HomeY;
        monster.HomeReturnTargetZ = monster.HomeZ;
        monster.AiState = MonsterAiState.ReturnToSpawn;
        monster.StateTicks = 0;
        monster.StateFrameAccumulator = 0f;
        zone.BroadcastMonsterActionChange(monster);
    }

    private void ComputeArcApproachPoint(MonsterEntity monster, float targetX, float targetZ, int meleeRadius,
        out float approachX, out float approachZ)
    {
        var dx = targetX - monster.PosX;
        var dz = targetZ - monster.PosZ;
        var distance = MathF.Sqrt(dx * dx + dz * dz);

        if (distance <= 0.0001f)
        {
            approachX = targetX;
            approachZ = targetZ;
            return;
        }

        var lateral = _random.NextInt32(meleeRadius);
        var sign = _random.NextInt32(2) == 0 ? -1f : 1f;
        var ratio = Math.Clamp(lateral / distance, -1f, 1f);
        var theta = sign * MathF.Asin(ratio);

        var cos = MathF.Cos(theta);
        var sin = MathF.Sin(theta);

        approachX = monster.PosX + (dx * cos - dz * sin);
        approachZ = monster.PosZ + (dx * sin + dz * cos);
    }

    private static bool CanReachPoint(Zone zone, MonsterEntity monster, float destX, float destZ)
    {
        if (zone.Geometry is not { } geometry)
            return true;

        if (zone.Pathfinder is { } pathfinder)
            return pathfinder.TryFindPath(new Vector3(monster.PosX, monster.PosY, monster.PosZ),
                new Vector3(destX, monster.PosY, destZ), []);

        return geometry.IsWalkable(destX, destZ);
    }

    private bool TryComputeWanderDestination(Zone zone, MonsterEntity monster, out float destX, out float destZ)
    {
        var dirX = (float)(_random.NextInt32(WanderDirectionRollSpan) - WanderDirectionRollHalfSpan);
        var dirZ = (float)(_random.NextInt32(WanderDirectionRollSpan) - WanderDirectionRollHalfSpan);
        var length = MathF.Sqrt(dirX * dirX + dirZ * dirZ);
        if (length > 0f)
        {
            dirX /= length;
            dirZ /= length;
        }

        var radius = WanderMinRadius + _random.NextInt32(WanderRadiusRollSpan);
        var candidateX = monster.PosX + dirX * radius;
        var candidateZ = monster.PosZ + dirZ * radius;

        destX = candidateX;
        destZ = candidateZ;

        if (zone.Geometry is { } geometry)
            SweepToFurthestWalkablePoint(geometry, monster.PosX, monster.PosZ, candidateX, candidateZ,
                monster.Template.WalkSpeed, out destX, out destZ);

        return DistanceSquared(monster.PosX, monster.PosZ, destX, destZ) >=
               WanderMinDisplacement * WanderMinDisplacement;
    }

    private static void SweepToFurthestWalkablePoint(ZoneGeometry geometry, float startX, float startZ,
        float destX, float destZ, float walkSpeed, out float resultX, out float resultZ)
    {
        resultX = startX;
        resultZ = startZ;

        if (walkSpeed <= 0f)
            return;

        var stepLength = walkSpeed * WanderPathStepIntervalSeconds;

        while (true)
        {
            var dx = destX - resultX;
            var dz = destZ - resultZ;
            var remaining = MathF.Sqrt(dx * dx + dz * dz);

            if (remaining <= stepLength)
            {
                if (geometry.IsWalkable(destX, destZ))
                {
                    resultX = destX;
                    resultZ = destZ;
                }

                return;
            }

            var nextX = resultX + stepLength * dx / remaining;
            var nextZ = resultZ + stepLength * dz / remaining;

            if (!geometry.IsWalkable(nextX, nextZ))
                return;

            resultX = nextX;
            resultZ = nextZ;
        }
    }

    private bool TryAcquireTarget(Zone zone, MonsterEntity monster, int legacyTicksElapsed,
        IEnumerable<MonsterEntity> allMonsters, bool transitionToChaseOnAcquire = true)
    {
        if (monster.Template.AttackType is not (1 or 3 or 6))
            return false;

        monster.DetectionThrottleTicks += legacyTicksElapsed;
        if (monster.DetectionThrottleTicks < SimulationClock.MonsterDetectionThrottleLegacyTicks)
            return false;

        monster.DetectionThrottleTicks = 0;

        var detectionRadius = monster.Template.RadiusInfo2;
        if (detectionRadius <= 0)
            return false;

        var detectionRadiusSq = (float)detectionRadius * detectionRadius;
        var monsterCellY = MathF.Floor(monster.PosY / zone.AoiCellSize);

        foreach (var characterId in zone.NeighborsOfPosition(monster.PosX, monster.PosZ))
        {
            if (!zone.TryGetPlayer(characterId, out var player) || !IsCandidateValid(player))
                continue;

            if (player.ActionSort is 0 or 33)
                continue;

            if (monster.InstanceId is { } requiredInstanceId && player.DungeonInstanceId != requiredInstanceId)
                continue;

            if (!WithinAoiCellHeightBand(zone, monsterCellY, player))
                continue;

            if (DistanceSquared(monster.PosX, monster.PosZ, player.PosX, player.PosZ) > detectionRadiusSq)
                continue;

            if (CountOtherPursuers(allMonsters, monster, characterId) > monster.PursuerCapacity - 1)
                continue;

            if (_random.NextInt32(2) != 0)
                continue;

            monster.AssignTarget(characterId, player.UniqueNumber, player.PosX, player.PosY, player.PosZ);

            monster.RegisterAcquisition(characterId, player);

            if (transitionToChaseOnAcquire)
            {
                monster.AiState = MonsterAiState.Chase;
                monster.StateTicks = 0;

                zone.BroadcastMonsterActionChange(monster);
            }

            return true;
        }

        return false;
    }

    private static int CountOtherPursuers(IEnumerable<MonsterEntity> allMonsters, MonsterEntity monster,
        int candidateCharacterId)
    {
        var count = 0;
        foreach (var other in allMonsters)
        {
            if (other.ServerIndex == monster.ServerIndex)
                continue;

            if (other.AiState is not (MonsterAiState.Chase or MonsterAiState.AttackWindup))
                continue;

            if (other.TargetCharacterId == candidateCharacterId)
                count++;
        }

        return count;
    }

    private static bool IsCandidateValid([NotNullWhen(true)] PlayerRuntimeState? player)
    {
        return player is not null && player.Session is IZoneSession { State: ZoneSessionState.InWorld } &&
               !player.IsMovingZone && !IsHiding(player) && !player.IsDead;
    }

    private static bool IsHiding(PlayerRuntimeState player)
    {
        return player.VisibleState == 0;
    }

    private bool TryShortRangeRetarget(Zone zone, MonsterEntity monster, int legacyTicksElapsed,
        [NotNullWhen(true)] out PlayerRuntimeState? retargeted)
    {
        retargeted = null;

        if (monster.Template.AttackType is not (1 or 3 or 6))
            return false;

        monster.DetectionThrottleTicks += legacyTicksElapsed;
        if (monster.DetectionThrottleTicks < SimulationClock.MonsterDetectionThrottleLegacyTicks)
            return false;

        monster.DetectionThrottleTicks = 0;

        var shortRadius = monster.Template.RadiusInfo1;
        if (shortRadius <= 0)
            return false;

        var shortRadiusSq = (float)shortRadius * shortRadius;
        var heightHalfExtent = (float)monster.Template.Size2;
        var monsterCellY = MathF.Floor(monster.PosY / zone.AoiCellSize);

        foreach (var characterId in zone.NeighborsOfPosition(monster.PosX, monster.PosZ))
        {
            if (!zone.TryGetPlayer(characterId, out var candidate) || !IsCandidateValid(candidate))
                continue;

            if (candidate.ActionSort is 0 or 33)
                continue;

            if (monster.InstanceId is { } requiredInstanceId && candidate.DungeonInstanceId != requiredInstanceId)
                continue;

            if (!WithinAoiCellHeightBand(zone, monsterCellY, candidate))
                continue;

            var candidateDistSq = DistanceSquared(monster.PosX, monster.PosZ, candidate.PosX, candidate.PosZ);
            if (candidateDistSq > shortRadiusSq)
                continue;

            if (MathF.Abs(monster.PosY - candidate.PosY) > heightHalfExtent)
                continue;

            if (_random.NextInt32(2) != 0)
                continue;

            monster.AssignTarget(characterId, candidate.UniqueNumber, candidate.PosX, candidate.PosY,
                candidate.PosZ);
            monster.RegisterAcquisition(characterId, candidate);
            retargeted = candidate;
            return true;
        }

        return false;
    }

    private static bool WithinAoiCellHeightBand(Zone zone, float monsterCellY, PlayerRuntimeState player)
    {
        return MathF.Abs(MathF.Floor(player.PosY / zone.AoiCellSize) - monsterCellY) <= 1f;
    }

    private void RunChase(Zone zone, MonsterEntity monster, float dt, int legacyTicksElapsed)
    {
        if (monster.TargetCharacterId is not { } targetId || !zone.TryGetPlayer(targetId, out var target) ||
            !IsCandidateValid(target))
        {
            ReleaseAndReturnToDecision(zone, monster);
            return;
        }

        var detectionRadiusSq = (float)monster.Template.RadiusInfo2 * monster.Template.RadiusInfo2;
        if (DistanceSquared(monster.PosX, monster.PosZ, target.PosX, target.PosZ) > detectionRadiusSq)
        {
            ReleaseAndReturnToDecision(zone, monster);
            return;
        }

        if (TryShortRangeRetarget(zone, monster, legacyTicksElapsed, out var retargeted))
        {
            CommitMeleeEngagement(zone, monster, retargeted);
            return;
        }

        monster.TargetLocationX = target.PosX;
        monster.TargetLocationY = target.PosY;
        monster.TargetLocationZ = target.PosZ;

        if (MoveToward(zone, monster, target.PosX, target.PosZ, monster.Template.RunSpeed, dt,
                monster.Template.RadiusInfo1))
        {
            ReleaseAndReturnToDecision(zone, monster);
            return;
        }

        if (IsZone175TypeBoss(monster.Template.SpecialType))
            return;

        var attackRadiusSq = (float)monster.Template.RadiusInfo1 * monster.Template.RadiusInfo1;
        if (DistanceSquared(monster.PosX, monster.PosZ, target.PosX, target.PosZ) <= attackRadiusSq)
            CommitMeleeEngagementOrGiveUpToHeightMismatch(zone, monster, target);
    }

    private static void ReleaseAndReturnToDecision(Zone zone, MonsterEntity monster)
    {
        monster.ReleaseTarget();
        monster.AiState = MonsterAiState.Decision;
        monster.StateTicks = 0;

        zone.BroadcastMonsterActionChange(monster);
    }

    private static void CommitMeleeEngagement(Zone zone, MonsterEntity monster, PlayerRuntimeState target)
    {
        monster.Heading = WireHeading.Between(monster.PosX, monster.PosZ, target.PosX, target.PosZ);
        monster.AiState = MonsterAiState.AttackWindup;
        monster.StateTicks = 0;

        if (monster.Template.AttackType is 1 or 2)
            monster.AttackPacketConfirmationArmed = true;

        zone.BroadcastMonsterActionChange(monster);
    }

    private static void CommitMeleeEngagementOrGiveUpToHeightMismatch(Zone zone, MonsterEntity monster,
        PlayerRuntimeState target)
    {
        var verticalSeparation = MathF.Abs(target.PosY - monster.PosY);
        var heightTolerance = (float)monster.Template.Size2;

        if (verticalSeparation <= heightTolerance)
        {
            CommitMeleeEngagement(zone, monster, target);
            return;
        }

        AbandonChaseAndReturnHome(zone, monster);
    }

    private static bool IsZone175TypeBoss(byte specialType)
    {
        return specialType is >= 40 and <= 44;
    }

    private static bool MoveToward(Zone zone, MonsterEntity monster, float targetX, float targetZ, float speed,
        float dt, float? tetherRadius = null)
    {
        if (zone.Geometry is not { } geometry)
            return StepToward(monster, targetX, targetZ, speed, dt, null, false) == MonsterStepOutcome.Blocked;

        if (zone.Pathfinder is { } pathfinder)
            return MoveAlongPath(pathfinder, geometry, monster, targetX, targetZ, speed, dt, tetherRadius);

        return StepToward(monster, targetX, targetZ, speed, dt, geometry, true) == MonsterStepOutcome.Blocked;
    }

    private static bool MoveAlongPath(MonsterPathfinder pathfinder, ZoneGeometry geometry, MonsterEntity monster,
        float targetX, float targetZ, float speed, float dt, float? tetherRadius)
    {
        var exhausted = monster.WaypointCursor >= monster.PathWaypoints.Count;
        var needReplan = exhausted
                         || PathGoalMoved(monster, targetX, targetZ)
                         || NextStepBlocked(monster, geometry, speed, dt);

        if (needReplan)
        {
            var from = new Vector3(monster.PosX, monster.PosY, monster.PosZ);
            var to = new Vector3(targetX, monster.PosY, targetZ);
            var found = tetherRadius is { } radius
                ? pathfinder.TryFindPursuitPath(from, to, new Vector2(targetX, targetZ), radius,
                    monster.PathWaypoints)
                : pathfinder.TryFindPathClamped(from, to, monster.PathWaypoints);
            if (found)
            {
                monster.WaypointCursor = 0;
                monster.PathGoalX = targetX;
                monster.PathGoalZ = targetZ;
            }
            else
            {
                monster.ClearPath();
                return StepToward(monster, targetX, targetZ, speed, dt, geometry, true) ==
                       MonsterStepOutcome.Blocked;
            }
        }

        FollowWaypoints(monster, geometry, speed, dt);
        return false;
    }

    private static void FollowWaypoints(MonsterEntity monster, ZoneGeometry geometry, float speed, float dt)
    {
        var remainingStep = speed * dt;
        if (remainingStep <= 0f)
            return;

        while (monster.WaypointCursor < monster.PathWaypoints.Count)
        {
            var waypoint = monster.PathWaypoints[monster.WaypointCursor];
            var dx = waypoint.X - monster.PosX;
            var dz = waypoint.Y - monster.PosZ;
            var distance = MathF.Sqrt(dx * dx + dz * dz);

            if (distance <= ArrivalEpsilon)
            {
                monster.WaypointCursor++;
                continue;
            }

            monster.Heading = WireHeading.FromDelta(dx, dz);

            if (remainingStep >= distance)
            {
                CommitPosition(monster, geometry, waypoint.X, waypoint.Y);
                remainingStep -= distance;
                monster.WaypointCursor++;
                continue;
            }

            var newX = monster.PosX + dx / distance * remainingStep;
            var newZ = monster.PosZ + dz / distance * remainingStep;
            CommitPosition(monster, geometry, newX, newZ);
            return;
        }
    }

    private static MonsterStepOutcome StepToward(MonsterEntity monster, float destX, float destZ, float speed,
        float dt, ZoneGeometry? geometry, bool refuseBlockedStep)
    {
        var dx = destX - monster.PosX;
        var dz = destZ - monster.PosZ;
        var distance = MathF.Sqrt(dx * dx + dz * dz);
        if (distance <= 0.0001f)
            return MonsterStepOutcome.Arrived;

        var step = speed * dt;
        if (step <= 0f)
            return MonsterStepOutcome.Moved;

        float newX, newZ;
        bool arrived;
        if (step >= distance)
        {
            newX = destX;
            newZ = destZ;
            arrived = true;
        }
        else
        {
            newX = monster.PosX + dx / distance * step;
            newZ = monster.PosZ + dz / distance * step;
            arrived = false;
        }

        if (geometry is { } g)
        {
            if (refuseBlockedStep && !g.IsWalkable(newX, newZ))
                return MonsterStepOutcome.Blocked;

            if (g.TryGetGroundHeight(newX, newZ, out var groundY))
                monster.PosY = groundY;
        }

        monster.PosX = newX;
        monster.PosZ = newZ;
        monster.Heading = WireHeading.FromDelta(dx, dz);
        return arrived ? MonsterStepOutcome.Arrived : MonsterStepOutcome.Moved;
    }

    private static void CommitPosition(MonsterEntity monster, ZoneGeometry geometry, float x, float z)
    {
        monster.PosX = x;
        monster.PosZ = z;
        if (geometry.TryGetGroundHeight(x, z, out var groundY))
            monster.PosY = groundY;
    }

    private static bool PathGoalMoved(MonsterEntity monster, float targetX, float targetZ)
    {
        var dx = targetX - monster.PathGoalX;
        var dz = targetZ - monster.PathGoalZ;
        return dx * dx + dz * dz > PathReplanGoalMoveThreshold * PathReplanGoalMoveThreshold;
    }

    private static bool NextStepBlocked(MonsterEntity monster, ZoneGeometry geometry, float speed, float dt)
    {
        if (monster.WaypointCursor >= monster.PathWaypoints.Count)
            return false;

        var waypoint = monster.PathWaypoints[monster.WaypointCursor];
        var dx = waypoint.X - monster.PosX;
        var dz = waypoint.Y - monster.PosZ;
        var distance = MathF.Sqrt(dx * dx + dz * dz);
        if (distance <= ArrivalEpsilon)
            return false;

        var step = MathF.Min(speed * dt, distance);
        if (step <= 0f)
            return false;

        var newX = monster.PosX + dx / distance * step;
        var newZ = monster.PosZ + dz / distance * step;
        return !geometry.IsWalkable(newX, newZ);
    }

    private static float DistanceSquared(float x1, float z1, float x2, float z2)
    {
        var dx = x1 - x2;
        var dz = z1 - z2;
        return dx * dx + dz * dz;
    }

    private static float DistanceSquared(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        var dz = z1 - z2;
        return dx * dx + dy * dy + dz * dz;
    }

    private enum MonsterStepOutcome
    {
        Moved,
        Arrived,
        Blocked
    }
}
