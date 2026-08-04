using System.Diagnostics.CodeAnalysis;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Application.Game.Domain.World.Runtime;
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

    private const float WanderMinRadius = 50f;

    private const int WanderRadiusRollSpan = 51;

    private const int WanderDirectionRollSpan = 201;

    private const int WanderDirectionRollHalfSpan = 100;

    private const float WanderMinDisplacement = 50f;

    private const float WanderPathStepIntervalSeconds = 0.033f;

    private const float HomeReturnRaycastStepSeconds = 0.033f;

    private const float LegacyFrameUnitsPerSecond = 30f;

    private const float AttackPacketConfirmationTimeoutSeconds = 1f;

    private readonly IRandomSource _random = random ?? SystemRandomSource.Instance;

    private readonly Lazy<ZoneCenterBroadcastIngestor>? _siegeIngestor = siegeIngestor;

    private readonly WorldStateService? _worldState = worldState;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        if (legacyTicksElapsed <= 0)
            return;

        const int elapsedLegacyTicks = 1;
        var dt = TickSeconds;

        foreach (var monster in zone.MonstersSnapshot)
            Update(zone, monster, dt, elapsedLegacyTicks);
    }

    private void Update(Zone zone, MonsterEntity monster, float dt, int legacyTicksElapsed)
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
                RunDecision(zone, monster, legacyTicksElapsed);
                break;

            case MonsterAiState.Patrol:
                RunPatrol(zone, monster, dt, legacyTicksElapsed);
                break;

            case MonsterAiState.Chase:
                RunChase(zone, monster, dt, legacyTicksElapsed);
                break;

            case MonsterAiState.AttackWindup:
                RunAttackWindup(monster, dt, monster.Template.FrameInfo3);
                break;

            case MonsterAiState.RangedAttackWindup:
                RunAttackWindup(monster, dt, monster.Template.FrameInfo4);
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

    private static void RunAttackWindup(MonsterEntity monster, float dt, short frameInfo)
    {
        if (monster.StateTicks++ == 0)
            monster.StateFrameAccumulator = 0f;

        monster.StateFrameAccumulator += dt * LegacyFrameUnitsPerSecond;
        monster.AdvanceAttackPacketConfirmation(dt);
        if (monster.StateFrameAccumulator < Math.Max(1, (int)frameInfo))
            return;

        if (monster.AttackPacketConfirmationArmed &&
            monster.AttackPacketConfirmationElapsedSeconds < AttackPacketConfirmationTimeoutSeconds)
            return;

        monster.ClearAttackPacketConfirmation();
        monster.StateFrameAccumulator = 0f;
        monster.AiState = MonsterAiState.Decision;
        monster.StateTicks = 0;
    }

    private void RunPatrol(Zone zone, MonsterEntity monster, float dt, int legacyTicksElapsed)
    {
        if (monster.SpecialSort == MonsterSpecialSort.Standard &&
            HasActiveOrFreshlyAcquiredAttacker(zone, monster, legacyTicksElapsed))
        {
            monster.AiState = MonsterAiState.Decision;
            monster.StateTicks = 0;

            zone.BroadcastMonsterActionChange(monster);
            return;
        }

        var blocked = MoveToward(zone, monster, monster.WanderTargetX, monster.WanderTargetZ,
            monster.Template.WalkSpeed, dt, out _);
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

    private bool HasActiveOrFreshlyAcquiredAttacker(Zone zone, MonsterEntity monster, int legacyTicksElapsed)
    {
        return monster.HasTrackedAttackers() ||
               TryAcquireTarget(zone, monster, legacyTicksElapsed, false);
    }

    private void RunDecision(Zone zone, MonsterEntity monster, int legacyTicksElapsed)
    {
        if (IsZone175TypeBoss(monster.Template.SpecialType))
        {
            RunZone175BossDecision(zone, monster, legacyTicksElapsed);
            return;
        }

        switch (monster.SpecialSort)
        {
            case MonsterSpecialSort.Standard:
                RunStandardDecision(zone, monster, legacyTicksElapsed);
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

    private void RunStandardDecision(Zone zone, MonsterEntity monster, int legacyTicksElapsed)
    {
        if (!monster.HasTrackedAttackers())
        {
            if (!TryAcquireTarget(zone, monster, legacyTicksElapsed, false))
            {
                RunIdleWanderOrReturnHome(zone, monster, legacyTicksElapsed);
                return;
            }

            monster.IdleWanderElapsedTicks = 0;
        }

        RunPrunedAttackerEngagement(zone, monster);
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

        var geometry = zone.Geometry;

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
        var currentY = monster.PosY;
        var currentZ = monster.PosZ;

        while (true)
        {
            var dx = destX - currentX;
            var dz = destZ - currentZ;
            var remaining = MathF.Sqrt(dx * dx + dz * dz);

            var reachedDestination = remaining <= stepLength;
            var candidateX = reachedDestination ? destX : currentX + dx / remaining * stepLength;
            var candidateZ = reachedDestination ? destZ : currentZ + dz / remaining * stepLength;

            if (!geometry.IsWalkable(candidateX, candidateZ) ||
                !geometry.TryGetGroundHeight(candidateX, candidateZ, out currentY))
                break;

            currentX = candidateX;
            currentZ = candidateZ;

            if (reachedDestination)
                break;
        }

        resolvedX = currentX;
        resolvedY = currentY;
        resolvedZ = currentZ;
    }

    private void RunPrunedAttackerEngagement(Zone zone, MonsterEntity monster)
    {
        var pruneResult = MonsterAggroListPruner.Prune(zone, monster);
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
            target.Incarnation != pick.Incarnation)
            return;

        if (pick.DistanceSquared <= meleeRadiusSq)
        {
            monster.AssignTarget(pick.CharacterId, target.Incarnation, target.UniqueNumber, target.PosX,
                target.PosY, target.PosZ);
            CommitMeleeEngagement(zone, monster, target);
            return;
        }

        var chaseSpeed = monster.Template.RunSpeed;
        if (chaseSpeed <= 0 || meleeRadius <= 0)
            return;

        ComputeArcApproachPoint(monster, target.PosX, target.PosZ, meleeRadius, out var approachX,
            out var approachZ);

        if (!TrySampleTerrainSegment(zone.Geometry, monster.PosX, monster.PosZ, approachX, approachZ,
                chaseSpeed, out _))
        {
            AbandonChaseAndReturnHome(zone, monster);
            return;
        }

        monster.AssignTarget(pick.CharacterId, target.Incarnation, target.UniqueNumber, approachX, monster.PosY,
            approachZ);
        monster.AiState = MonsterAiState.Chase;
        monster.StateTicks = 0;
        zone.BroadcastMonsterActionChange(monster);
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

        SweepToFurthestWalkablePoint(zone.Geometry, monster.PosX, monster.PosZ, candidateX, candidateZ,
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
                if (geometry.IsWalkable(destX, destZ) && geometry.TryGetGroundHeight(destX, destZ, out _))
                {
                    resultX = destX;
                    resultZ = destZ;
                }

                return;
            }

            var nextX = resultX + stepLength * dx / remaining;
            var nextZ = resultZ + stepLength * dz / remaining;

            if (!geometry.IsWalkable(nextX, nextZ) || !geometry.TryGetGroundHeight(nextX, nextZ, out _))
                return;

            resultX = nextX;
            resultZ = nextZ;
        }
    }

    private bool TryAcquireTarget(Zone zone, MonsterEntity monster, int legacyTicksElapsed,
        bool transitionToChaseOnAcquire = true)
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

        foreach (var characterId in StableNeighborsOfPosition(zone, monster.PosX, monster.PosZ))
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

            if (zone.CountOtherMonsterPursuers(monster, characterId, player.Incarnation) > monster.PursuerCapacity -
                1)
                continue;

            if (_random.NextInt32(2) != 0)
                continue;

            monster.AssignTarget(characterId, player.Incarnation, player.UniqueNumber, player.PosX, player.PosY,
                player.PosZ);

            monster.RegisterAcquisition(characterId, player.Incarnation);

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

        foreach (var characterId in StableNeighborsOfPosition(zone, monster.PosX, monster.PosZ))
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

            monster.AssignTarget(characterId, candidate.Incarnation, candidate.UniqueNumber, candidate.PosX,
                candidate.PosY, candidate.PosZ);
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
            !IsCandidateValid(target) || target.Incarnation != monster.TargetIncarnation ||
            target.UniqueNumber != monster.TargetUniqueNumber)
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

        if (MoveToward(zone, monster, monster.TargetLocationX, monster.TargetLocationZ, monster.Template.RunSpeed,
                dt, out var arrived))
        {
            ReleaseAndReturnToDecision(zone, monster);
            return;
        }

        if (IsZone175TypeBoss(monster.Template.SpecialType))
        {
            if (arrived)
                ReturnToDecisionOnArrival(monster);

            return;
        }

        var attackRadiusSq = (float)monster.Template.RadiusInfo1 * monster.Template.RadiusInfo1;
        if (DistanceSquared(monster.PosX, monster.PosZ, target.PosX, target.PosZ) <= attackRadiusSq)
        {
            CommitMeleeEngagementOrGiveUpToHeightMismatch(zone, monster, target);
            return;
        }

        if (arrived)
            ReturnToDecisionOnArrival(monster);
    }

    private static void ReturnToDecisionOnArrival(MonsterEntity monster)
    {
        monster.AiState = MonsterAiState.Decision;
        monster.StateTicks = 0;
        monster.StateFrameAccumulator = 0f;
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
        monster.ArmAttackPacketConfirmation();
        monster.AiState = MonsterAiState.AttackWindup;
        monster.StateTicks = 0;

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
        float dt, out bool arrived)
    {
        var outcome = StepToward(monster, targetX, targetZ, speed, dt, zone.Geometry);
        arrived = outcome == MonsterStepOutcome.Arrived;
        return outcome == MonsterStepOutcome.Blocked;
    }

    private static MonsterStepOutcome StepToward(MonsterEntity monster, float destX, float destZ, float speed,
        float dt, ZoneGeometry geometry)
    {
        if (!float.IsFinite(monster.PosX) || !float.IsFinite(monster.PosY) || !float.IsFinite(monster.PosZ) ||
            !float.IsFinite(destX) || !float.IsFinite(destZ) || !float.IsFinite(speed) || !float.IsFinite(dt) ||
            speed <= 0f || dt <= 0f)
            return MonsterStepOutcome.Blocked;

        var dx = destX - monster.PosX;
        var dz = destZ - monster.PosZ;
        var distance = MathF.Sqrt(dx * dx + dz * dz);
        if (!float.IsFinite(distance))
            return MonsterStepOutcome.Blocked;

        if (distance <= 0.0001f)
        {
            return geometry.IsWalkable(monster.PosX, monster.PosZ) &&
                   geometry.TryGetGroundHeight(monster.PosX, monster.PosZ, out _)
                ? MonsterStepOutcome.Arrived
                : MonsterStepOutcome.Blocked;
        }

        var step = speed * dt;
        if (!float.IsFinite(step) || step <= 0f)
            return MonsterStepOutcome.Blocked;

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

        if (!TrySampleTerrainSegment(geometry, monster.PosX, monster.PosZ, newX, newZ, speed, out var groundY))
            return MonsterStepOutcome.Blocked;

        monster.PosX = newX;
        monster.PosY = groundY;
        monster.PosZ = newZ;
        monster.Heading = WireHeading.FromDelta(dx, dz);
        return arrived ? MonsterStepOutcome.Arrived : MonsterStepOutcome.Moved;
    }

    private static List<int> StableNeighborsOfPosition(Zone zone, float x, float z, int scale = 1)
    {
        var neighbors = zone.NeighborsOfPosition(x, z, scale);
        neighbors.Sort();
        return neighbors;
    }

    private static bool TrySampleTerrainSegment(ZoneGeometry geometry, float startX, float startZ, float destX,
        float destZ, float speed, out float groundY)
    {
        groundY = 0f;

        if (!float.IsFinite(startX) || !float.IsFinite(startZ) || !float.IsFinite(destX) ||
            !float.IsFinite(destZ) || !float.IsFinite(speed) || speed <= 0f)
            return false;

        var stepLength = speed * WanderPathStepIntervalSeconds;
        if (!float.IsFinite(stepLength) || stepLength <= 0f)
            return false;

        var currentX = startX;
        var currentZ = startZ;
        while (true)
        {
            var dx = destX - currentX;
            var dz = destZ - currentZ;
            var remaining = MathF.Sqrt(dx * dx + dz * dz);
            if (!float.IsFinite(remaining))
                return false;

            var reachedDestination = remaining <= stepLength;
            var candidateX = reachedDestination ? destX : currentX + dx / remaining * stepLength;
            var candidateZ = reachedDestination ? destZ : currentZ + dz / remaining * stepLength;

            if (!geometry.IsWalkable(candidateX, candidateZ) ||
                !geometry.TryGetGroundHeight(candidateX, candidateZ, out groundY))
                return false;

            if (reachedDestination)
                return true;

            currentX = candidateX;
            currentZ = candidateZ;
        }
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
