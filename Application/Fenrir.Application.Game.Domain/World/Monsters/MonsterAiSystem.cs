using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Application.Game.Domain.World.Pathfinding;

namespace Fenrir.Application.Game.Domain.World.Monsters;

public sealed partial class MonsterAiSystem(IRandomSource? random = null) : ISimulationSystem
{
    private const float TickSeconds = SimulationClock.LegacyTickMilliseconds / 1000f;

    private const float ArrivalEpsilon = 1f;

    private const float PathReplanGoalMoveThreshold = 40f;

    private const float WanderMinRadius = 50f;

    private const int WanderRadiusRollSpan = 51;

    private const int WanderDirectionRollSpan = 201;

    private const int WanderDirectionRollHalfSpan = 100;

    private const float WanderMinDisplacement = 50f;

    private readonly IRandomSource _random = random ?? SystemRandomSource.Instance;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var dt = TickSeconds * legacyTicksElapsed;

        zone.ResetPathBudget();

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
                monster.StateTicks++;
                if (monster.StateTicks >= Math.Max(1, (int)monster.Template.FrameInfo1))
                {
                    monster.AiState = MonsterAiState.Decision;
                    monster.StateTicks = 0;
                }

                break;

            case MonsterAiState.Decision:
                RunDecision(zone, monster, legacyTicksElapsed, allMonsters);
                break;

            case MonsterAiState.Patrol:
                MoveToward(zone, monster, monster.WanderTargetX, monster.WanderTargetZ, monster.Template.WalkSpeed,
                    dt);
                if (DistanceSquared(monster.PosX, monster.PosZ, monster.WanderTargetX, monster.WanderTargetZ) <=
                    ArrivalEpsilon * ArrivalEpsilon)
                {
                    monster.AiState = MonsterAiState.Decision;
                    monster.StateTicks = 0;
                }
                else
                {
                    TryAcquireTarget(zone, monster, legacyTicksElapsed,
                        allMonsters);
                }

                break;

            case MonsterAiState.Chase:
                RunChase(zone, monster, dt, legacyTicksElapsed);
                break;

            case MonsterAiState.AttackWindup:
                monster.StateTicks++;
                if (monster.StateTicks == 1 && monster.TargetCharacterId is { } attackTargetId)
                    zone.ResolveMonsterAttack(monster, attackTargetId);

                if (monster.StateTicks >= Math.Max(1, (int)monster.Template.FrameInfo3))
                {
                    monster.AiState = MonsterAiState.Decision;
                    monster.StateTicks = 0;
                }

                break;

            case MonsterAiState.RangedAttackWindup:
                monster.StateTicks++;
                if (monster.StateTicks >= Math.Max(1, (int)monster.Template.FrameInfo4))
                {
                    monster.AiState = MonsterAiState.Decision;
                    monster.StateTicks = 0;
                }

                break;

            case MonsterAiState.Flinch:
                monster.StateTicks++;
                if (monster.StateTicks >= Math.Max(1, (int)monster.Template.FrameInfo2))
                {
                    monster.AiState = MonsterAiState.Decision;
                    monster.StateTicks = 0;
                }

                break;

            case MonsterAiState.ReturnToSpawn:
                monster.StateTicks++;
                if (monster.StateTicks >= Math.Max(1, (int)monster.Template.FrameInfo6))
                {
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
                break;
        }

        zone.SyncMonsterCell(monster);
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
            case MonsterSpecialSort.AllianceStone:
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
            if (!TryAcquireTarget(zone, monster, legacyTicksElapsed, allMonsters, false))
            {
                RunIdleWanderOrReturnHome(zone, monster, legacyTicksElapsed);
                return;
            }

        RunPrunedAttackerEngagement(zone, monster, allMonsters);
    }

    private void RunIdleWanderOrReturnHome(Zone zone, MonsterEntity monster, int legacyTicksElapsed)
    {
        monster.IdleReturnElapsedTicks += legacyTicksElapsed;
        if (monster.IdleReturnElapsedTicks > SimulationClock.MonsterIdleReturnHomeLegacyTicks)
        {
            monster.IdleReturnElapsedTicks = 0;

            if (DistanceSquared(monster.PosX, monster.PosZ, monster.HomeX, monster.HomeZ) >
                ArrivalEpsilon * ArrivalEpsilon)
            {
                monster.AiState = MonsterAiState.ReturnToSpawn;
                monster.StateTicks = 0;
                monster.IdleWanderElapsedTicks = 0;

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
        monster.AiState = MonsterAiState.Patrol;
        monster.StateTicks = 0;

        zone.BroadcastMonsterActionChange(monster);
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
            monster.AiState = MonsterAiState.AttackWindup;
            monster.StateTicks = 0;
            zone.BroadcastMonsterActionChange(monster);
            return;
        }

        var chaseSpeed = monster.Template.RunSpeed;
        if (chaseSpeed <= 0)
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
            monster.AiState = MonsterAiState.ReturnToSpawn;
            monster.StateTicks = 0;
            zone.BroadcastMonsterActionChange(monster);
        }
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

        var lateral = _random.NextInt32(meleeRadius + 1);
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
        destX = monster.PosX + dirX * radius;
        destZ = monster.PosZ + dirZ * radius;

        if (zone.Geometry is { } geometry && !geometry.IsWalkable(destX, destZ))
        {
            destX = monster.PosX;
            destZ = monster.PosZ;
        }

        return DistanceSquared(monster.PosX, monster.PosZ, destX, destZ) >=
               WanderMinDisplacement * WanderMinDisplacement;
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

        foreach (var characterId in zone.NeighborsOfPosition(monster.PosX, monster.PosZ))
        {
            if (!zone.TryGetPlayer(characterId, out var player) || !IsCandidateValid(player))
                continue;

            if (monster.InstanceId is { } requiredInstanceId && player.DungeonInstanceId != requiredInstanceId)
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

            if (other.AiState is not (MonsterAiState.Chase or MonsterAiState.AttackWindup
                or MonsterAiState.RangedAttackWindup))
                continue;

            if (other.TargetCharacterId == candidateCharacterId)
                count++;
        }

        return count;
    }

    private static bool IsCandidateValid([NotNullWhen(true)] PlayerRuntimeState? player)
    {
        return player is not null && !player.IsMovingZone && !IsHiding(player) && !player.IsDead;
    }

    private static bool IsHiding(PlayerRuntimeState player)
    {
        _ = player;
        return false;
    }

    private PlayerRuntimeState TryShortRangeRetarget(Zone zone, MonsterEntity monster, PlayerRuntimeState current,
        int legacyTicksElapsed)
    {
        if (monster.Template.AttackType is not (1 or 3 or 6))
            return current;

        monster.ShortRangeRetargetThrottleTicks += legacyTicksElapsed;
        if (monster.ShortRangeRetargetThrottleTicks < SimulationClock.MonsterDetectionThrottleLegacyTicks)
            return current;

        monster.ShortRangeRetargetThrottleTicks = 0;

        var shortRadius = monster.Template.RadiusInfo1;
        if (shortRadius <= 0)
            return current;

        var shortRadiusSq = (float)shortRadius * shortRadius;
        var heightHalfExtent = (float)monster.Template.Size2;

        foreach (var characterId in zone.NeighborsOfPosition(monster.PosX, monster.PosZ))
        {
            if (!zone.TryGetPlayer(characterId, out var candidate) || !IsCandidateValid(candidate))
                continue;

            if (monster.InstanceId is { } requiredInstanceId && candidate.DungeonInstanceId != requiredInstanceId)
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
            return candidate;
        }

        return current;
    }

    private void RunChase(Zone zone, MonsterEntity monster, float dt, int legacyTicksElapsed)
    {
        if (monster.TargetCharacterId is not { } targetId || !zone.TryGetPlayer(targetId, out var target) ||
            !IsCandidateValid(target))
        {
            monster.ReleaseTarget();
            monster.AiState = MonsterAiState.Decision;
            monster.StateTicks = 0;

            zone.BroadcastMonsterActionChange(monster);
            return;
        }

        target = TryShortRangeRetarget(zone, monster, target, legacyTicksElapsed);

        var distanceToTargetSq = DistanceSquared(monster.PosX, monster.PosZ, target.PosX, target.PosZ);

        var detectionRadiusSq = (float)monster.Template.RadiusInfo2 * monster.Template.RadiusInfo2;
        if (distanceToTargetSq > detectionRadiusSq)
        {
            monster.ReleaseTarget();
            monster.AiState = MonsterAiState.Decision;
            monster.StateTicks = 0;

            zone.BroadcastMonsterActionChange(monster);
            return;
        }

        monster.TargetLocationX = target.PosX;
        monster.TargetLocationY = target.PosY;
        monster.TargetLocationZ = target.PosZ;

        var attackRadiusSq = (float)monster.Template.RadiusInfo1 * monster.Template.RadiusInfo1;
        if (distanceToTargetSq <= attackRadiusSq)
        {
            monster.AiState = MonsterAiState.AttackWindup;
            monster.StateTicks = 0;

            zone.BroadcastMonsterActionChange(monster);
            return;
        }

        if (IsZone175TypeBoss(monster.Template.SpecialType) && distanceToTargetSq <= detectionRadiusSq)
        {
            monster.AiState = MonsterAiState.RangedAttackWindup;
            monster.StateTicks = 0;

            zone.BroadcastMonsterActionChange(monster);
            return;
        }

        MoveToward(zone, monster, target.PosX, target.PosZ, monster.Template.RunSpeed, dt,
            monster.Template.RadiusInfo1);
    }

    private static bool IsZone175TypeBoss(byte specialType)
    {
        return specialType is >= 40 and <= 44;
    }

    private static void MoveToward(Zone zone, MonsterEntity monster, float targetX, float targetZ, float speed,
        float dt, float? tetherRadius = null)
    {
        if (zone.Geometry is not { } geometry)
        {
            StepToward(monster, targetX, targetZ, speed, dt, null, false);
            return;
        }

        if (zone.Pathfinder is { } pathfinder)
        {
            MoveAlongPath(pathfinder, geometry, monster, targetX, targetZ, speed, dt, tetherRadius);
            return;
        }

        StepToward(monster, targetX, targetZ, speed, dt, geometry, true);
    }

    private static void MoveAlongPath(MonsterPathfinder pathfinder, ZoneGeometry geometry, MonsterEntity monster,
        float targetX, float targetZ, float speed, float dt, float? tetherRadius)
    {
        var exhausted = monster.WaypointCursor >= monster.PathWaypoints.Count;
        var needReplan = exhausted
                         || PathGoalMoved(monster, targetX, targetZ)
                         || NextStepBlocked(monster, geometry, speed, dt);

        if (needReplan)
        {
            if (pathfinder.TryConsumeBudget())
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
                    StepToward(monster, targetX, targetZ, speed, dt, geometry, true);
                    return;
                }
            }
            else if (exhausted)
            {
                StepToward(monster, targetX, targetZ, speed, dt, geometry, true);
                return;
            }
        }

        FollowWaypoints(monster, geometry, speed, dt);
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

            monster.Heading = MathF.Atan2(dx, dz);

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

    private static bool StepToward(MonsterEntity monster, float destX, float destZ, float speed, float dt,
        ZoneGeometry? geometry, bool refuseBlockedStep)
    {
        var dx = destX - monster.PosX;
        var dz = destZ - monster.PosZ;
        var distance = MathF.Sqrt(dx * dx + dz * dz);
        if (distance <= 0.0001f)
            return true;

        var step = speed * dt;
        if (step <= 0f)
            return false;

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
                return false;

            if (g.TryGetGroundHeight(newX, newZ, out var groundY))
                monster.PosY = groundY;
        }

        monster.PosX = newX;
        monster.PosZ = newZ;
        monster.Heading = MathF.Atan2(dx, dz);
        return arrived;
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
}
